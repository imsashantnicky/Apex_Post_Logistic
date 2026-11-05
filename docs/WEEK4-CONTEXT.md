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
