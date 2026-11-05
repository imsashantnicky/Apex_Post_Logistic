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

## Decision: additive tracking module (Part B)

Week 3 has no shipment a courier can track: `ShippingLineItem` is a counter basket line with an `AP` + 10 hex
reference, no status and no recipient phone. The API needs `APX-########` numbers, a status lifecycle and scans.
The brief forbids changing the Week 3 data layer except by adding service methods, so tracking is a separate module
that only adds files:

| Layer | Folder | Contents |
| --- | --- | --- |
| Domain | `src/ApexPost.Domain/Tracking` | `TrackedShipment` (aggregate), `Recipient`, `TrackingScan`, `ShipmentStatus`, `ScanType`, `ShipmentStatusRules`, `TrackingFormats` |
| Application | `src/ApexPost.Application/Tracking` | `IShipmentTrackingService` + `ShipmentTrackingService` (all business rules), ports `ITrackedShipmentRepository`, `ITrackingQueries`, `ITrackingUnitOfWork`, views, `InvalidStatusTransitionException`, `ShipmentLockedException` |
| Infrastructure | `src/ApexPost.Infrastructure/Tracking` | `TrackingDbContext`, configurations, repository, queries, unit of work, design-time factory, `Migrations/`, `AddShipmentTracking(services, configuration)` |
| Tests | `tests/ApexPost.Tracking.UnitTests` | Rules, domain, service (in-memory fakes), model and SQL Server tests |

- `TrackingDbContext` uses the same `ConnectionStrings:ApexPostDb` user-secret but owns only the tables
  `TrackedShipments` and `TrackingScans`, its own migrations and the history table `__TrackingMigrationsHistory`.
  `ApexPostDbContext`, its migrations and `__EFMigrationsHistory` are not touched; a test checks the two models
  share no table.
- Unique indexes: `IX_TrackedShipments_TrackingNumber` and `IX_TrackingScans_ScanId`. The scan id index makes a
  repeated upload a duplicate even when two uploads race.
- Lifecycle: Created → PickedUp → InTransit → OutForDelivery → Delivered | Failed. Moves go forward only and may skip
  a stage, because couriers have no OutForDelivery scan and a Delivered scan must still apply after InTransit.
  Delivered and Failed are final. A scan that is not a forward move is stored as history without changing the status.
- The Week 3 Domain references no other assembly, so it cannot use `ScanRules` from Courier.Domain.
  `TrackingFormats` repeats the patterns, and `TrackingFormatsTests` fails if the two copies drift apart.
- Errors for Part B to map: `NotFoundException` 404; `DuplicateKeyException`, `InvalidStatusTransitionException`,
  `ShipmentLockedException` (delete after Created, edit after a final status) and `ConcurrencyConflictException` 409;
  `ValidationFailedException` (page size over 50, empty upload) and `DomainException` (bad input) 400.

## Git workflow

- `main` is the base branch. It contains all Week 3 work (merged from `chore/setup` in PR #20).
- Part A was merged into `chore/setup` (PR #35), which is also the default branch on GitHub, not into `main`.
  `week4-partB` was therefore created from `chore/setup` (agreed with the user) so it contains Part A.
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
| 2026-11-05 | A | Step 07 – harness done: `ApexPost.Courier.Harness` opens `CourierDatabase`, wires `CouchbaseScanRepository`, `OutboxService(FakeShipmentUploader)` and `ScanChangeMonitor.Start()`. Header shows the database name, path and pending count; menu 1 Add Scan, 2 Find by Tracking Number, 3 List Pending, 4 Simulate Sync, 5 Delete Scan, 6 Exit, plus 7 Scans per Type for Courier, 8 Update Scan Notes, 9 Get Scan by ID, 10 Seed Fictional Demo Data (CR-101/CR-102, APX-10000001…06, APX-99999999), 11 Show Query Plans. Aligned tables, validated input, errors return to the menu; Ctrl+C and Exit dispose cleanly ("Listener token removed", "Database closed"). 13 new tests (91 courier tests). |
| 2026-11-05 | A | Step 08 – tests done: new project `tests/ApexPost.Courier.Offline.Tests` (xUnit v3, in the `.slnx`), every test class opens its own temp database folder. 38 tests: `scan::{Guid}` ids, new scans Pending, wrong APX/CR formats rejected, key-value CRUD round trip and reopen, tracking-number ordering, pending query, per-type counts, injection-style input → 0 rows, both indexes via `GetIndexes` and `Explain`, outbox Synced/Failed, no listener callback after dispose, Courier.Domain references no Couchbase assembly. |
| 2026-11-05 | A | Step 09 – docs done: `OFFLINE-DESIGN.md` (save locally first with an ASCII outbox diagram, server decides with append-only scans and idempotent `scan::{Guid}` ids, one owner process for the embedded database, the extra `type` field, Part D swaps the uploader), screenshot list in `docs/screenshots/week4-partA/README.md`, and a "Week 4 Part A" section in `README.md` on running the harness and tests. |
| 2026-11-05 | B | Step 01 – tracking model done: additive tracking module (see the decision above). `TrackedShipment` with `Recipient`, `ShipmentStatusRules.CanMove` (forward only, Delivered/Failed final), `TrackingScan` (`scan::{Guid}`); `ShipmentTrackingService` with List (page size 1–50, status, city), Get, Create, Replace, ChangeStatus, Delete (only Created), UploadScans (duplicates skipped, oldest first, one retry after a parallel commit) and GetScans; `TrackingDbContext` + migration `AddShipmentTracking` with `__TrackingMigrationsHistory`; `AddShipmentTracking` registration. New project `tests/ApexPost.Tracking.UnitTests`: 113 tests, including SQL Server round trip, unique index and version conflict tests. |
| 2026-11-05 | B | Step 02 – API host done: new project `src/ApexPost.Api` (minimal hosting, no `Startup.cs`, same `UserSecretsId` as the web project, no connection string in `appsettings*.json`). DI: `AddSqlPersistence`, `AddInfrastructureServices`, `AddShipmentTracking`, then `AddApexApi` (controllers, `AddProblemDetails` with `traceId`, `ApiExceptionHandler`, plus `TaxCalculator` and `NoSignedInStaff` so the Week 3 registrations resolve with `ValidateOnBuild`). `ApiExceptionHandler` maps 404/409/400/403/500, stack trace in Development only. Development start migrates `TrackingDbContext` and seeds `APX-20000001`…`10` through the service (idempotent). New project `tests/ApexPost.Api.Tests`: 23 tests with `WebApplicationFactory` on a throw-away SQL Server database. |
| 2026-11-05 | B | Step 03 – DTOs done: `src/ApexPost.Api/Contracts/V1` holds `CreateShipmentRequest`, `UpdateShipmentRequest`, `UpdateStatusRequest`, `ScanUploadRequest` + `ScanItem`, `ShipmentResponse`, `ScanResponse`, `PagedResponse<T>`, `ScanUploadResult` + `RejectedScanResponse`, and `ShipmentMapping` (requests → `ShipmentCommand`/`ScanUpload`, views → responses; never an EF entity). Patterns and messages live in `Contracts/ContractRules` (tracking number and courier id reuse `TrackingFormats`). Weight is `[Range(0.1, 30)]` to match `TrackedShipment.MaximumWeightKg` (agreed with the user instead of 70). Custom rules: `[NotInFuture]` (`TimeProvider` from the request services, 2-minute clock skew) on `scannedAtUtc`, and `ScanItem : IValidatableObject` requiring `recipientSignatureName` for Delivered. `[ApiController]` gives the 400. 95 new tests (118 API tests). |

## Contract addition: recipientSignatureName (Part B step 03)

The v1 `ScanItem` has one field the courier document does not have: `recipientSignatureName`, required when
`scanType` is Delivered (agreed with the user). The API checks it and does not store it: `ScanUpload`, the
`TrackingScans` table and the Part A Couchbase document are unchanged. Part D must add the field to Delivered scans
before the device uploads to the real API, or those scans are answered with 400.
