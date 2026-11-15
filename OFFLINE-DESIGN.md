# Offline design – courier scans (Week 4 Part A)

A courier records pickups and deliveries where there is often no network: in a van, a basement or a rural road.
The device keeps every scan in a local Couchbase Lite database (`apexpost_courier`) and uploads it later through an
outbox. This note explains the three main decisions.

## 1. Save locally first, then sync through an outbox

Recording a scan never waits for the network. `CouchbaseScanRepository` saves the scan with `syncStatus = Pending`,
and only `OutboxService` changes the status afterwards.

```text
 Courier taps "scan"
        |
        v
 +--------------------+   save (no network needed)   +-----------------------------+
 | ShipmentScan       | ---------------------------> | Couchbase Lite document     |
 | id = scan::{Guid}  |                              | syncStatus = "Pending"      |
 +--------------------+                              +-----------------------------+
                                                                   |
                                     OutboxService.ProcessPendingAsync (oldest first)
                                                                   |
                                                                   v
                                                     IShipmentUploader.UploadAsync
                                                     (Part A: FakeShipmentUploader,
                                                      Part D: HttpShipmentUploader)
                                                       |                       |
                                             accepted / duplicate        rejected / error
                                                       |                       |
                                                       v                       v
                                              syncStatus = "Synced"   syncStatus = "Failed"
                                                                      (retried with includeFailed)
```

Why this order:

- **The scan is never lost.** It is on disk before any upload is tried, so a crash, a dead battery or a timeout
  cannot drop it.
- **The courier is never blocked.** Saving is a local write; the upload runs later, in the background.
- **One bad scan does not stop the rest.** Each upload is a separate attempt; a failure marks only that scan `Failed`.
- **No double uploads from one device.** The outbox is single-flight (`SemaphoreSlim`): a second run that starts
  while one is busy returns at once. A cancelled run leaves the scans it did not try as `Pending`.

The uploader is a port in `ApexPost.Courier.Domain` (`IShipmentUploader`). Part A uses `FakeShipmentUploader`
(100–300 ms delay, `APX-99999999` always fails). **Part D swaps it for `HttpShipmentUploader`** that calls the API
through Polly; the outbox, the repository and the domain do not change.

### Part D: batches through the real API

The MAUI app registers `HttpShipmentUploader` (`src/Mobile/ApexPost.Courier.ApiClient/Sync`) instead of the fake;
the console harness and the Part A tests keep `FakeShipmentUploader`. `IShipmentUploader` is unchanged. Batches use
a separate port, `IBatchShipmentUploader : IShipmentUploader` (`UploadBatchAsync(trackingNumber, scans)`), and
`OutboxService` uses it whenever the uploader implements it:

- Pending scans are grouped per tracking number (the API takes the scans of one shipment per call), groups in the
  order of their oldest scan, scans oldest first, and split into batches of **20** (`OutboxService.BatchSize`; the
  API accepts 100, a smaller request is kinder to a weak mobile link).
- Each batch is one `POST /api/v2/shipments/{trackingNumber}/scans` with the bearer token, the retries and the
  circuit breaker of [RESILIENCE.md](RESILIENCE.md).

| Answer of the batch | `BatchUploadOutcome` | New `syncStatus` | The run |
| --- | --- | --- | --- |
| 200: `accepted` and `duplicatesSkipped` | `Completed` | `Synced` | goes on |
| 200: listed in `rejected` | `Completed` | `Failed` (reason logged) | goes on |
| 400, 403, 404, other 4xx | `Refused` | `Failed` for the whole batch | goes on |
| network error, timeout, 5xx after retries, 429, unreadable answer, **open circuit** | `Retryable` | unchanged (`Pending` stays `Pending`) | stops; the rest wait for the next run |
| 401 | `Unauthorized` | unchanged | stops; the app clears the token and opens Sign in |

Runs start at start-up, after sign-in, when `Connectivity.ConnectivityChanged` reports Internet access, after a new
scan and from the **Sync now** button (`CourierSync`). The outbox stays single-flight, so overlapping triggers start
one run. The token lives only in MAUI `SecureStorage`, never in Preferences, files or logs. The pending count on the
main page comes from the Part A `ScanChangeMonitor.PendingCountChanged` event, switched to the UI thread with the
page's dispatcher.

## 2. Conflicts: the server decides, scans are append-only

Two devices, or a device and a back-office user, can change the same shipment while one of them is offline.

| Option | How it works | Problem for ApexPost |
| --- | --- | --- |
| Last-write-wins | The newest timestamp overwrites the stored record | Device clocks drift; a late upload of an old "Pickup" could overwrite a "Delivered" status, so data is silently lost |
| **Server decides** (chosen) | The device only sends facts; the API applies its own rules and answers accepted, duplicate or rejected | The device must accept the server's answer and mark the scan `Failed` when it is rejected |

Decision: **the server decides, and scans are append-only events.**

- A scan is a fact ("CR-101 picked up APX-10000001 at 09:14"), not a copy of the shipment. The device never edits a
  shipment, so there is nothing to overwrite.
- Every scan has a unique id `scan::{Guid}`, created on the device. Part B uses it as the **idempotency key**: an
  upload of the same id again is counted as `duplicatesSkipped` and is not stored twice. A retry after a lost
  response is therefore safe.
- The API maps the scan to the shipment status and allows only forward moves
  (Created → PickedUp → InTransit → OutForDelivery → Delivered | Failed). A scan that would move a shipment backwards
  is rejected with a reason, and the device marks it `Failed`.
- The response `{ accepted, duplicatesSkipped, rejected: [{ scanId, reason }] }` tells the device exactly which scans
  to mark `Synced` (accepted and duplicates) and which `Failed` (rejected).

## 3. One process owns the embedded database

Couchbase Lite is an embedded database: a library that writes files inside the app's folder (`CouchbaseData/`,
`apexpost_courier.cblite2/`). It is not a server, so several processes must not open the same database.

- **File locks.** The database files are locked by the process that opened them. A second process either fails to
  open them or competes for the same files.
- **No coordinating server.** There is no process in the middle that orders the writes of two clients, as SQL Server
  does. Each process keeps its own in-memory state and caches.
- **Risk of corruption.** Two writers on the same files, outside one process, can leave the database inconsistent.
- **Listeners work per process.** A change listener only sees the changes made through its own `Database` object.
  A scan written by another process would not raise the `[LIVE] Pending scans` event, so the screen and the outbox
  would act on old data.

Rule: **one owner process.** The harness (and in Part D the MAUI app) opens one `CourierDatabase`, shares that one
instance with the repository, the outbox and the listener, and disposes it on exit: listener tokens are removed first,
then the database is closed. Anything else that needs the scans goes through that process, or later through the API.

## Document model

Each scan is one JSON document in the default collection, with the scan id as the document id:

```json
{
  "type": "shipmentScan",
  "trackingNumber": "APX-10000001",
  "scanType": "Pickup",
  "courierId": "CR-101",
  "location": "Maple Depot",
  "scannedAtUtc": "2026-11-05T09:14:00.0000000Z",
  "notes": null,
  "syncStatus": "Pending"
}
```

The extra `type` field is not part of the shared scan contract. It marks the kind of document, so other document
types (for example a cached shipment or a settings document) can later live in the same collection, and queries over
all documents can filter on `type = 'shipmentScan'`. Enums are stored as their names, and `scannedAtUtc` is ISO-8601
UTC (`"O"` format), so ordering by the string is chronological. The value indexes `idx_scan_trackingNumber` and
`idx_scan_syncStatus` serve the find-by-tracking-number and outbox queries; all SQL++ values are passed as parameters.
