# TDD log – scan age rule (Week 5)

This log records one rule built test-first, in small RED / GREEN / REFACTOR commits. Each commit was pushed on its
own, so the history on `main` shows the order.

## 1. Requirement

> The server rejects an uploaded scan whose `scannedAtUtc` is more than 72 hours before the upload time, with the
> reason "Scan is older than 72 hours.". A scan exactly 72 hours old is accepted. The limit is configurable
> (`TrackingOptions.MaxScanAgeHours`, default 72).

Why: a courier device can stay offline for a long time. A scan that arrives days late would move a shipment's status
long after the parcel has moved on, so the server refuses it with a reason and the device marks it `Failed`.

### Proof that the rule did not exist

Before the first test, the code was searched for the rule:

```text
grep -rniE "72|older than|MaxScanAge|ScanAge|TrackingOptions" --include=*.cs src
```

The only hits were GUID examples containing "72" in scan ids (`ContractRules.cs`, `ScanItem.cs`,
`ScanResponse.cs`, `ScanUploadRequest.cs`). There was no age check in `ShipmentTrackingService`, no
`TrackingOptions` class and no `MaxScanAgeHours` setting. An upload accepted a scan of any age.

## 2. First failing test (RED)

File: `tests/ApexPost.Tracking.UnitTests/Services/ScanAgeRuleTests.cs`. The upload time is fixed with a
`FakeTimeProvider` (2026-11-10 12:00 UTC); the scan was made at 2026-11-07 11:59 UTC, which is 72 hours and
1 minute earlier.

```csharp
[Fact]
public async Task UploadScansAsync_ScanOlderThan72Hours_RejectsScanWithReason()
{
    // Arrange
    _store.Store(TestData.Shipment());
    var scan = ScanAt(new DateTime(2026, 11, 7, 11, 59, 0, DateTimeKind.Utc));

    // Act
    var result = await _service.UploadScansAsync([scan], Ct);

    // Assert
    var rejected = Assert.Single(result.Rejected);
    Assert.Equal(scan.ScanId, rejected.ScanId);
    Assert.Equal("Scan is older than 72 hours.", rejected.Reason);
    Assert.Equal(0, result.Accepted);
    Assert.Empty(_store.StoredScans);
}
```

The test compiles and fails on an assertion (not a compile error), because the service accepted the old scan.
Real output of
`dotnet test tests/ApexPost.Tracking.UnitTests -c Release --filter "FullyQualifiedName~ScanAgeRuleTests"`:

```text
[xUnit.net 00:00:01.53]     ApexPost.Tracking.UnitTests.Services.ScanAgeRuleTests.UploadScansAsync_ScanOlderThan72Hours_RejectsScanWithReason [FAIL]
  Failed ApexPost.Tracking.UnitTests.Services.ScanAgeRuleTests.UploadScansAsync_ScanOlderThan72Hours_RejectsScanWithReason [44 ms]
  Error Message:
   Assert.Single() Failure: The collection was empty
  Stack Trace:
     at ApexPost.Tracking.UnitTests.Services.ScanAgeRuleTests.UploadScansAsync_ScanOlderThan72Hours_RejectsScanWithReason() in e:\week-3-assignment\Apex_Post_Logistic\tests\ApexPost.Tracking.UnitTests\Services\ScanAgeRuleTests.cs:line 39
--- End of stack trace from previous location ---

Failed!  - Failed:     1, Passed:     0, Skipped:     0, Total:     1, Duration: 83 ms - ApexPost.Tracking.UnitTests.dll (net10.0)
```

## 3. Minimal implementation (GREEN)

The smallest change that makes the test pass: one check in `ShipmentTrackingService.TryRecordAsync`, placed before
the shipment is loaded, so an old scan costs no database read. The number 72 and the text are hard-coded on purpose;
moving them out is the refactoring's job, not GREEN's.

```csharp
if (Now - scan.ScannedAtUtc > TimeSpan.FromHours(72))
{
    return "Scan is older than 72 hours.";
}
```

`Now` already comes from the injected `TimeProvider`, so the test controls the upload time without any new seam.
The rejected scan follows the existing path: it is listed in `rejected` with its reason, the rest of the batch is
still handled, and nothing is saved when no scan is accepted.

Result after GREEN: the new test passes, and the full suite
(`dotnet test Apex_Post_Logistic.slnx -c Release --filter "Category!=Measurement"`) has 1,917 passed, 0 failed,
0 skipped. No existing test uploaded a scan older than 72 hours, so none had to change.

## 4. Boundary tests (TEST, not RED)

Two more tests pin the edge of the rule at the same upload time (2026-11-10 12:00 UTC):

| Test | Scan time | Age | Expected |
| --- | --- | --- | --- |
| `UploadScansAsync_ScanExactly72HoursOld_AcceptsScan` | 2026-11-07 12:00:00 | exactly 72 h | accepted |
| `UploadScansAsync_Scan72HoursAndOneSecondOld_RejectsScanWithReason` | 2026-11-07 11:59:59 | 72 h + 1 s | rejected, same reason |

Honest note: **both passed the first time they ran**. The GREEN check already used a strict `>`, so exactly
72 hours was accepted and one second more was refused. They were therefore committed as `TEST: boundary cases`,
not as a RED commit, and no second GREEN commit was needed. They still earn their place: if someone changes `>` to
`>=`, or turns the limit into whole days, one of them fails.

Real output:

```text
  Passed ApexPost.Tracking.UnitTests.Services.ScanAgeRuleTests.UploadScansAsync_ScanOlderThan72Hours_RejectsScanWithReason [47 ms]
  Passed ApexPost.Tracking.UnitTests.Services.ScanAgeRuleTests.UploadScansAsync_Scan72HoursAndOneSecondOld_RejectsScanWithReason [1 ms]
  Passed ApexPost.Tracking.UnitTests.Services.ScanAgeRuleTests.UploadScansAsync_ScanExactly72HoursOld_AcceptsScan [5 ms]
Total tests: 3
```

## 5. Refactoring 1 – the limit moves into `TrackingOptions`

GREEN left a literal `72` and a fixed text inside the service. The refactor removes them without changing behaviour:

| File | Change |
| --- | --- |
| `src/ApexPost.Application/Tracking/TrackingOptions.cs` (new) | "Tracking" section, `MaxScanAgeHours` (default `DefaultMaxScanAgeHours = 72`) |
| `src/ApexPost.Application/Tracking/ScanAgePolicy.cs` (new) | Takes `IOptions<TrackingOptions>` by constructor injection; `RejectionFor(scannedAtUtc, uploadedAtUtc)` returns `"Scan is older than {hours} hours."` or `null`. Same strict `>` as GREEN |
| `src/ApexPost.Application/Tracking/ShipmentTrackingService.cs` | Constructor takes `ScanAgePolicy`; `TryRecordAsync` asks the policy instead of comparing with `72` |
| `src/ApexPost.Infrastructure/Tracking/TrackingDependencyInjection.cs` | `AddShipmentTracking` registers `TrackingOptions` and `ScanAgePolicy` (singleton: it holds only the limit) |
| `src/ApexPost.Api/Composition/ApiRegistration.cs` | Binds the "Tracking" section and validates `MaxScanAgeHours >= 1` with `ValidateOnStart`, so a bad value stops the host instead of rejecting every scan |

```csharp
public sealed class ScanAgePolicy(IOptions<TrackingOptions> options)
{
    private readonly int _maxAgeHours = options.Value.MaxScanAgeHours;

    public string? RejectionFor(DateTime scannedAtUtc, DateTime uploadedAtUtc) =>
        uploadedAtUtc - scannedAtUtc > TimeSpan.FromHours(_maxAgeHours)
            ? $"Scan is older than {_maxAgeHours} hours."
            : null;
}
```

No behaviour change: the three `ScanAgeRuleTests` were not edited except for the constructor call, and still pass.
The new constructor parameter meant that six places that build the service by hand now pass
`TestData.ScanAge()` (or `new ScanAgePolicy(Options.Create(new TrackingOptions()))` in the SQL tests): the three
existing service classes, the Moq class and the two SQL classes. Only the construction changed, no assertion; each is
recorded in the review table of `TESTING-STRATEGY.md` (5.2 / 5.3).

Result: `dotnet build Apex_Post_Logistic.slnx -c Release` 0 warnings, 0 errors; full suite 1,919 passed, 0 failed,
0 skipped (1,917 + the 2 boundary tests).

## 6. Refactoring 2 – cleanup, API-level proof and an `.http` example

| File | Change |
| --- | --- |
| `src/ApexPost.Application/Tracking/ScanAgePolicy.cs` | The limit (`TimeSpan`) and the reason text are built once in the constructor (invariant culture) instead of on every scan. Same strict `>` |
| `src/ApexPost.Application/Tracking/IShipmentTrackingService.cs` | The XML comment of `UploadScansAsync` now lists the scan age rejection next to the other per-scan reasons |
| `tests/.../Services/ScanAgeRuleTests.cs` | The three facts share `UploadAsync`, `AssertAccepted` and `AssertRejected` helpers; their inputs and assertions are unchanged. New theory `UploadScansAsync_ConfiguredLimit_AcceptsAtLimitAndRejectsOneSecondOlder` (24 h and 96 h: at the limit accepted, one second older rejected with "Scan is older than 24 hours." / "… 96 hours.") |
| `tests/ApexPost.Api.Tests/Hosting/TrackingOptionsRegistrationTests.cs` (new) | `AddApexApi` binds the "Tracking" section: default 72, configured 48; 0 and −1 throw `OptionsValidationException` with "Tracking:MaxScanAgeHours must be 1 or more." |
| `tests/ApexPost.Api.Tests/Endpoints/ScanAgeEndpointTests.cs` (new) | Through the real host (fixed clock, throw-away SQL Server database): a 73 h old scan and a fresh one → 200, `accepted: 1`, `rejected: [{ scanId, reason: "Scan is older than 72 hours." }]`, only the fresh scan in the history. With `Tracking:MaxScanAgeHours = 24` on the host, a 25 h old scan → "Scan is older than 24 hours." |
| `src/ApexPost.Api/ApexPost.Api.http` | New v1 and v2 examples: a scan from 4 days / 73 hours ago answered with 200 and the rejection reason. The scans that the file expects to be accepted used fixed dates (2026-11-01); the new rule would reject them a few days later, so they now use `{{$datetime iso8601 -N m}}` (same order inside each batch) |
| `API-NOTES.md` | The per-item rejection list of the idempotent upload names the scan age rule |

Honest note: the new tests in this commit were written after the production code they check, so they are not TDD.
The configured-limit theory and the registration tests passed on the first run; they guard the refactored
configuration path. The TDD part of this log is sections 2 to 4.

Result: 0 warnings, 0 errors; full suite 1,929 passed, 0 failed, 0 skipped (1,919 + 4 unit cases in
`ScanAgeRuleTests`, 4 in `TrackingOptionsRegistrationTests`, 2 in `ScanAgeEndpointTests`).

## 7. Production code changed for testability, and why

| Change | Why |
| --- | --- |
| None was needed for the first test | `ShipmentTrackingService` already took a `TimeProvider`, so a `FakeTimeProvider` could fix the upload time; the scan time comes from the request. This seam was added in Week 4, which made the RED test possible without touching production code |
| `ScanAgePolicy` injected by constructor into `ShipmentTrackingService` | The limit can be set per test (`TestData.ScanAge(24)`) and per deployment, without a static setting or a hidden `DateTime.UtcNow`. The policy is a concrete class without an interface: there is one implementation, and tests build it with `Options.Create(...)` |
| `IOptions<TrackingOptions>` instead of a constant | The brief asks for a configurable limit; options also let the host validate the value at start-up (`ValidateOnStart`) |
| `TrackingOptions.DefaultMaxScanAgeHours` public constant | Tests and the default share one number instead of repeating `72` |

Existing behaviour that did not change: every upload that does not contain a scan older than the limit gives the
same answer as before; all earlier tests pass with their assertions unchanged (only six constructor calls gained the
policy argument, recorded in `TESTING-STRATEGY.md` 5.2 / 5.3).
