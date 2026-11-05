# Week 4 context: offline courier, REST API and mobile app

## Goal of the week

Week 4 gives the ApexPost courier an offline-first workflow. A courier records pickups and deliveries on a device
without network access, stores them locally, and uploads them to a REST API when a connection is available.

| Part | Focus |
| --- | --- |
| A | Couchbase Lite offline store for courier scans, driven by a console harness |
| B | REST API `ApexPost.Api`: shipment endpoints and an idempotent scan upload |
| C | OpenAPI document, Swagger UI, API versioning (v1 and v2) and a generated NSwag client |
| D | Security (JWT, roles, API keys, HTTPS) and a MAUI app with Polly that replaces `FakeShipmentUploader` with the real API |

The Week 3 SQL Server and EF Core code under `src/ApexPost.*`, `tests/` and `sql/` is not changed by Week 4.

## Project layout

| Project | Type | References | Purpose |
| --- | --- | --- | --- |
| `src/Courier/ApexPost.Courier.Domain` | Class library | none, no NuGet packages | `ShipmentScan`, `ScanRules`, enums and ports such as `IShipmentUploader` |
| `src/Courier/ApexPost.Courier.Offline` | Class library | Domain, `Couchbase.Lite` 3.4.2 | Couchbase Lite storage behind the Domain ports |
| `src/Courier/ApexPost.Courier.Harness` | Console app | Domain, Offline | Runs the offline workflow end to end from the command line |

Parts B to D add their projects next to these. All projects inherit `net10.0`, C# 14, nullable reference types,
implicit usings and `TreatWarningsAsErrors` from the root `Directory.Build.props`.

## Rules

- `ApexPost.Courier.Domain` never references Couchbase Lite or any other storage library. Only
  `ApexPost.Courier.Offline` knows about Couchbase.
- Tracking numbers match `^APX-\d{8}$` and courier ids match `^CR-\d{3}$`. Both patterns live in
  `ScanRules` (Courier.Domain); the API (Part B) and the app (Part D) reuse them instead of repeating them.
- `IShipmentUploader` is a Domain port. In Part A its only implementation is `FakeShipmentUploader`, which records
  uploads in memory. Part D replaces it with an HTTP uploader that calls the API through Polly; Domain code does
  not change.
- The local Couchbase database folder (`CouchbaseData/`, `*.cblite2/`) is git-ignored and never committed.
- Fictional data only. Secrets and connection strings live in `dotnet user-secrets`, never in committed files.

## Git workflow

- `main` is the base branch. It contains all Week 3 work (merged from `chore/setup` in PR #20).
- Each part has an integration branch `week4-partX` created from `main`.
- Every step runs on its own branch `week4-partX-NN-name` created from the integration branch. Git cannot hold both
  `week4-partA` and `week4-partA/NN-name`, so step branches use a hyphen, never a slash.
- Each step ends with a passing Release build and test run, a Conventional Commit, a push of the step branch and a
  pull request into the integration branch. The pull requests are merged on GitHub by hand.
- When a part is complete, its integration branch is merged into `main` in one pull request.
- `git diff origin/main --stat` must never list a Week 3 file.

## Progress log

| Date | Part | Progress |
| --- | --- | --- |
| 2026-11-02 | A | Step 0 – setup done: integration branch `week4-partA`, the three courier projects in the solution, Couchbase ignore rules and this document. |
| 2026-11-02 | A | Step 1 – domain done: `ShipmentScan` (new scans start `Pending`), `ScanType`, `SyncStatus`, `ScanIds`, `ScanTypeCount`, and the ports `IScanRepository`, `IScanQueries`, `IShipmentUploader` with `UploadResult`. No Couchbase types in Domain. |
| 2026-11-05 | A | Week 3 moved into `main`; the Part A scaffold was removed from the Week 3 branch and restored on `week4-partA` from `main`. |
| 2026-11-05 | A | Step 01 – restore done: scaffold and domain back, `ScanRules` checks tracking numbers (`APX-` + 8 digits) and courier ids (`CR-` + 3 digits), standing rules in `CLAUDE.md` (now committed), CI runs on `week4-*` branches, 17 tests in the new `ApexPost.Courier.UnitTests`. |
| 2026-11-05 | A | Step 02 – database done: `CourierDatabase` opens `apexpost_courier` (default folder `CouchbaseData`), creates the value indexes `idx_scan_trackingNumber` and `idx_scan_syncStatus` idempotently, keeps listener tokens and removes them before closing; use after dispose throws `ObjectDisposedException`. 6 new tests (23 courier tests). |
| 2026-11-05 | A | Step 03 – repository done: `ScanDocumentMapper` writes the exact JSON fields (+ `type: shipmentScan`, enums as names, `scannedAtUtc` in `"O"` UTC) and restores through `ShipmentScan.Restore`; `CouchbaseScanRepository` does key-value Create/GetById/Update/Delete, a parameterised `ListAll` ordered by `scannedAtUtc`, and `UpdateSyncStatusAsync` for the outbox. An edited scan goes back to `Pending`. `IScanQueries` is stubbed until step 04. 25 new tests (48 courier tests). |
| 2026-11-05 | A | Step 04 – queries done: `IScanQueries` implemented with parameterised SQL++ (`$trackingNumber`, `$syncStatus`, `$courierId`) in private constants; queries and result sets are disposed. `ExplainQueries()` shows `SEARCH _ USING INDEX idx_scan_trackingNumber` and `idx_scan_syncStatus`; the two indexed queries have no `type` filter. An injection-style input is matched as a plain value. 9 new tests (57 courier tests). |
| 2026-11-05 | A | Step 05 – outbox done: `FakeShipmentUploader` (100–300 ms delay, `APX-99999999` always fails, optional seeded `failureRate`) and `OutboxService.ProcessPendingAsync(includeFailed, ct)`: oldest first, accepted → `Synced`, rejected or exception → `Failed`, one bad scan never stops the rest, `SemaphoreSlim` single-flight (a busy run returns `OutboxResult.Empty`), cancellation leaves unattempted scans `Pending`. New port method `IScanQueries.GetFailedAsync`. 15 new tests (72 courier tests). |
| 2026-11-05 | A | Step 06 – listener done: `ScanChangeMonitor.Start()` adds a collection change listener and hands the token to `CourierDatabase.RegisterListenerToken`. Each change writes the timestamp, `added`/`updated`/`deleted` and the document id, then `[LIVE] Pending scans: N`, to the console and `logs/listener.log` (git-ignored) under a lock; exceptions in the callback are caught and logged. `PendingCountChanged` event for the Part D screen. 6 new tests (78 courier tests). |
