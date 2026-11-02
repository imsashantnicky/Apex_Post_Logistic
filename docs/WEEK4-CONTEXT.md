# Week 4 context: offline courier

## Goal of the week

Week 4 gives the ApexPost courier an offline-first workflow. A courier records pickups and deliveries on a device
without network access, stores them locally, and uploads them when a connection is available.

| Part | Focus |
| --- | --- |
| A | Courier domain model and offline storage with Couchbase Lite, driven by a console harness |
| B | Upload and synchronisation behaviour against the offline store |
| C | Courier API that receives uploaded shipments |
| D | Mobile app that uses the offline store and a real HTTP uploader against the API |

The Week 3 SQL Server and EF Core code under `src/ApexPost.*` and `tests/` is not changed by Week 4.

## Project layout

| Project | Type | References | Purpose |
| --- | --- | --- | --- |
| `src/Courier/ApexPost.Courier.Domain` | Class library | none, no NuGet packages | Courier entities, value objects, rules and ports such as `IShipmentUploader` |
| `src/Courier/ApexPost.Courier.Offline` | Class library | Domain, `Couchbase.Lite` 3.4.2 | Couchbase Lite storage behind the Domain ports |
| `src/Courier/ApexPost.Courier.Harness` | Console app | Domain, Offline | Runs the offline workflow end to end from the command line |

All three projects inherit `net10.0`, C# 14, nullable reference types, implicit usings and
`TreatWarningsAsErrors` from the root `Directory.Build.props`.

## Rules

- `ApexPost.Courier.Domain` never references Couchbase Lite or any other storage library. Only
  `ApexPost.Courier.Offline` knows about Couchbase.
- `IShipmentUploader` is a Domain port. In Part A its only implementation is a fake that records uploads in memory.
  Part D replaces the fake with a real HTTP uploader that calls the courier API; Domain code does not change.
- The local Couchbase database folder (`CouchbaseData/`, `*.cblite2/`) is git-ignored and never committed.

## Git workflow

- `week4-partA` is the integration branch for Part A. It was created from `chore/setup`, the repository's default
  branch that holds all Week 3 work (`main` stopped at Week 2).
- Every step runs on its own branch `week4-partA-NN-name` (for example `week4-partA-00-setup`). Git cannot hold
  both `week4-partA` and `week4-partA/NN-name`, so step branches use a hyphen instead of a slash.
- Each step ends with a passing `dotnet build`, a Conventional Commit and a merge into `week4-partA`
  (pull request when `gh` is available, otherwise `git merge --no-ff`), after which the step branch is deleted.
- When Part A is complete, `week4-partA` is merged into the default branch in one final pull request.
- `git diff chore/setup --stat` must never list a Week 3 file.

## Progress log

| Date | Part | Progress |
| --- | --- | --- |
| 2026-11-02 | A | Step 0 – setup done: integration branch `week4-partA`, the three courier projects in the solution, Couchbase ignore rules and this document. |
| 2026-11-02 | A | Step 1 – domain done: `ShipmentScan` (new scans start `Pending`), `ScanType`, `SyncStatus`, `ScanIds`, `ScanTypeCount`, and the ports `IScanRepository`, `IScanQueries`, `IShipmentUploader` with `UploadResult`. No Couchbase types in Domain. |
