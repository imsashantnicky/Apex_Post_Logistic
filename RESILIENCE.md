# Resilience – ApexPost courier app (Week 4 Part D)

The courier app talks to `ApexPost.Api` over mobile networks that drop, stall and come back. This document explains
how the typed client `ApexApiClient` reacts to that: which failures it retries, when it stops trying, how the app
keeps working offline, and why each number was chosen.

The code is in `src/Mobile/ApexPost.Courier.ApiClient/Resilience/`. It is built with
`Microsoft.Extensions.Http.Resilience` (Polly v8) and registered in `MauiProgram`:

```csharp
builder.Services
    .AddHttpClient<ApexApiClient>(http => http.BaseAddress = baseAddress)
    .AddHttpMessageHandler<AuthHeaderHandler>()
    .AddApexResilience();   // ApexResilience.cs → AddResilienceHandler("apex-api", ...)
```

## 1. The pipeline

Strategies run from the outside in. Each one wraps everything below it.

```text
ApexApiClient.SendAsync
  └─ AuthHeaderHandler                  adds "Authorization: Bearer <token>" once per call
      └─ resilience handler "apex-api"
          1. Total timeout      30 s    limit for the whole call, retries and waits included
          2. Retry              3 ×     exponential backoff with jitter, transient failures of safe requests only
          3. Circuit breaker            50 % failures of ≥ 4 attempts in 30 s → open for 15 s
          4. Attempt timeout     8 s    limit for one HTTP attempt
              └─ primary handler (SocketsHttpHandler / Android handler) → network
```

Why this order:

- **Attempt timeout innermost.** A stalled attempt is cut after 8 s and becomes a `TimeoutRejectedException`, which
  the breaker counts and the retry can repeat. Outside the retry it would end the whole call instead.
- **Breaker inside the retry.** Every attempt is counted, not only the final outcome of a call. When the circuit opens
  in the middle of a call, the remaining retries are refused at once (`BrokenCircuitException` is not retried), so a
  dead server costs no more radio time.
- **Total timeout outermost.** It bounds what the courier waits for, whatever the retry and the breaker do.

## 2. Chosen values and why

| Setting | Value | Why |
| --- | --- | --- |
| Total timeout | 30 s | The longest a courier should look at a spinner. Worst case without it: 4 attempts × 8 s + about 7 s of waits = 39 s, so the total timeout really does cut the slowest calls. |
| Attempt timeout | 8 s | The API answers a normal request in well under a second; a scan batch upload is at most 100 small scans. 8 s leaves room for a slow 3G link and a cold start, but stops a stalled connection long before the total limit. |
| Retries | 3 (4 attempts in total) | Mobile errors are usually short (a cell handover, a tunnel). Three retries over about 7 s ride through most of them; more retries mostly waste battery while the outbox keeps the scans anyway. |
| Backoff | exponential, base 1 s, jitter, max 8 s | About 1 s, 2 s, 4 s. Jitter spreads the retries of many couriers so they do not hit a recovering server at the same moment. A `Retry-After` header from the server is honoured, but capped at 8 s. |
| Failure ratio | 0.5 | Half of the attempts failing is not bad luck any more; the server or the network is in trouble. |
| Minimum throughput | 4 | A single failed request never opens the circuit. Four attempts is exactly one fully retried call, so one call that fails four times in a row opens it. |
| Sampling duration | 30 s | Long enough to hold one retried call (about 7 s of waits plus the attempts), short enough that an old failure is forgotten quickly. |
| Break duration | 15 s | Gives a restarting server or a dropped link time to recover. After 15 s the next call is let through as a trial (half-open): a success closes the circuit, a failure opens it again for another 15 s. |

The values live in `ResilienceSettings` (defaults = the table above). `RetryRuleTests.The_default_settings_are_the_documented_values`
keeps this table and the code in step. Tests pass shorter times so the suite runs in seconds.

## 3. What is retried, and what never is

A retry happens only when **both** conditions hold.

**1. The outcome is transient** (`TransientFailure.Is`):

| Outcome | Retried | Counted by the breaker |
| --- | --- | --- |
| `HttpRequestException` (no connection, DNS, reset) | yes | yes |
| Attempt timeout (`TimeoutRejectedException`) | yes | yes |
| 408 Request Timeout | yes | yes |
| 5xx (500, 502, 503, 504, …) | yes | yes |
| any other 4xx (400, 401, 403, 404, 409, 415, 429, …) | **never** | no |
| 2xx / 3xx | no | no (success) |
| cancellation by the caller | no, it is thrown to the caller | no |

A 4xx answer is the server's final word about the request. Sending the same invalid body, expired token or stale
version again gives the same answer, so a retry would only add load. 429 is left out on purpose: the courier app
does not get rate limited today, and when it does, waiting for the outbox's next run is the right reaction.

**2. The request is safe to repeat** (`IdempotentRequest.IsRetrySafe`):

| Request | Retried | Why |
| --- | --- | --- |
| `GET`, `HEAD`, `OPTIONS` | yes | read only |
| `PUT`, `DELETE` | yes | idempotent by HTTP semantics: the same request twice leaves the same state |
| `POST .../scans` (scan upload) | yes, marked with the request option `ApexPost.RetrySafe` | see section 4 |
| `PATCH .../status` | **no** | a lost answer could hide a status move that did happen; the second try would then answer 409 |
| any other `POST` | **no** | could create a second resource |

The circuit breaker counts every method, because it measures server health, not whether one request may be repeated.

## 4. Why the scan upload is safe to retry

The upload is a `POST`, which HTTP does not call idempotent. It is retried anyway because the API makes it idempotent
(Part B, [API-NOTES.md – Idempotent scan upload](API-NOTES.md#idempotent-scan-upload)):

1. **Every scan carries its own id**, the device document id `scan::{Guid}`. The id is created once when the courier
   scans and never changes. A retry sends exactly the same bytes: same scan ids, same times
   (`The_marked_scan_upload_is_retried_with_the_same_scan_ids_and_token` checks this).
2. **The server de-duplicates by that id.** The unique index `IX_TrackingScans_ScanId` allows one stored row per id.
   Ids that are already stored are answered as `duplicatesSkipped` and not applied again, so the status does not move
   twice.
3. **Parallel copies are handled too.** If the first attempt is still being committed when the retry arrives, the
   database refuses the second commit and the service runs once more, which turns the scans into duplicates.
4. **The device's answer is the same either way.** Accepted and duplicate scans both become `Synced`, so a retry after
   a lost answer gives the same end state as one successful attempt.

So the worst a repeated upload can do is to return `accepted: 0, duplicatesSkipped: n`. The client marks only this
request with `IdempotentRequest.Mark(request)` in `ApexApiClient.UploadScansAsync`; nothing else is opted in.

## 5. Offline while the circuit is open

- `CircuitStateTracker` (singleton) is updated from the breaker's `OnOpened`, `OnHalfOpened` and `OnClosed` callbacks
  and exposed to the UI as `ICircuitStateProvider` (`State`, `IsServerUnavailable`, `StateChanged`).
- `MainPage` subscribes while it is visible and shows the red banner
  **"Server unavailable – working offline. Scans are still saved on this device."** The event is raised on the HTTP
  thread, so the page switches to the UI thread with `Dispatcher.Dispatch`.
- While the circuit is open, a call fails at once without touching the network. `ApexApiClient` turns the
  `BrokenCircuitException` into an `ApiFailure` of kind `ServerUnavailable` with the same text, never an exception.
- **Scans keep saving locally.** Saving a scan writes only to Couchbase Lite (`CouchbaseScanRepository`) and never
  goes through `HttpClient`, so the circuit cannot block it. The scan stays `Pending` in the outbox until an upload
  succeeds (Part A, [OFFLINE-DESIGN.md](OFFLINE-DESIGN.md)).
- The total timeout and the open circuit surface as typed failures (`Timeout`, `ServerUnavailable`); only a
  cancellation by the caller is thrown.

## 6. Logging

Every retry and every state change is logged under the category `ApexPost.Courier.ApiClient.Resilience`
(`ResilienceLog.cs`, source-generated `[LoggerMessage]`):

| Event id | Level | Message |
| --- | --- | --- |
| 4101 | Warning | `Retry {Attempt} of {MaxAttempts} for {Method} {Path} in {DelayMs} ms after {Reason}` |
| 4102 | Error | `Circuit opened for {BreakSeconds} s after {Reason}; the app works offline` |
| 4103 | Information | `Circuit half-open: the next call tests the server` |
| 4104 | Information | `Circuit closed: the server answers again` |

`Reason` is only the status code (`HTTP 503`) or the exception type (`TimeoutRejectedException`). `Path` is the URL
path without the query string. No line holds a token, a header, a body or a scan field;
`No_log_line_contains_the_token_or_the_request_body` checks every captured line of every category.

Example of the format (the tests use a 1 ms base delay and a 0.5 s break):

```text
warn: ApexPost.Courier.ApiClient.Resilience[4101]
      Retry 1 of 3 for GET /api/v2/shipments/APX-20000005 in 1 ms after HTTP 503
fail: ApexPost.Courier.ApiClient.Resilience[4102]
      Circuit opened for 0.5 s after HTTP 503; the app works offline
```

## 7. Tests

`tests/Mobile/ApexPost.Courier.ApiClient.Tests/Resilience/` uses the same registration as `MauiProgram` with a
scripted fake primary handler (`Support/ScriptedHttpHandler.cs`), so no network is used:

| Test class | What it proves |
| --- | --- |
| `RetryTests` | 503 retried until success; a lasting 503 makes 4 attempts; 408/500/502/504 and `HttpRequestException` retried; 400/401/403/404/409/429 sent once; `PATCH` not retried; the marked upload is retried with identical scan ids and token; a cancellation by the caller is thrown and not retried |
| `CircuitBreakerTests` | starts closed; repeated 503s open it and raise `StateChanged`; while open no request is sent and the failure says "working offline"; 404s never open it; after the break a good trial closes it (Open → HalfOpen → Closed) |
| `TimeoutTests` | a hanging attempt is cut by the attempt timeout and retried; the total timeout ends the call as a `Timeout` failure |
| `ResilienceLoggingTests` | retry lines with attempt, method, path, delay and reason; open, half-open and close lines; no token, body or scan field in any log line |
| `RetryRuleTests` | the method and status rules of section 3 and the default values of section 2 |

## 8. Limits

- The breaker state lives in memory. When the app restarts, the circuit starts closed again; the first calls then
  find out quickly whether the server is back.
- One circuit covers the whole API. A failing endpoint can open it for the healthy ones; with one small API this is
  simpler and good enough.
- A `PATCH` that timed out is not retried, so the courier sees a failure even if the server applied the change.
  Refreshing the shipment shows the real status.
