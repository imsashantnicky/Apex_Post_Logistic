# Week 5 context: test strategy and automated quality

## Goal of the week

Week 5 does not add features. It makes the existing solution (Week 3 counter platform, Week 4 tracking API,
courier offline store and typed API client) safer to change. The week delivers:

- a written test strategy (`TESTING-STRATEGY.md`) that is written before any new test and stays true to the code;
- unit tests that follow AAA, `Method_Scenario_ExpectedResult` naming and `Category` traits;
- isolation with Moq where a service has dependencies, and a real SQL Server test database instead of mocked EF Core;
- one rule built test-first (`TDD-LOG.md`, `RED:` / `GREEN:` / `REFACTOR:` commits);
- business-readable specifications with Reqnroll (`tests/ApexPost.Specs`);
- Copilot-assisted tests reviewed by a human (`COPILOT-LOG.md`, at least two mutation proofs);
- a reflection and the evidence (`docs/testing/test-run.txt`, screenshots under `docs/screenshots/week5/`).

The standing rules are in `CLAUDE.md`, section "WEEK 5" (W5.0 to W5.12). Production code changes only for
testability or for the TDD rule, and every change is recorded.

## Test project structure

| Level | Projects |
| --- | --- |
| Unit | `tests/ApexPost.UnitTests`, `tests/ApexPost.Tracking.UnitTests`, `tests/Courier/ApexPost.Courier.UnitTests`, `tests/Mobile/ApexPost.Courier.ApiClient.Tests`, `tests/Mobile/ApexPost.Courier.Presentation.Tests` |
| Integration | `tests/ApexPost.IntegrationTests`, `tests/ApexPost.Api.Tests`, `tests/ApexPost.Courier.Offline.Tests` (real embedded Couchbase), plus SQL tests moved out of unit projects |
| BDD | `tests/ApexPost.Specs` (new, Reqnroll) |

Since step 02 every test class carries a `Category` trait, and the trait, not the project name, decides the level
(`TESTING-STRATEGY.md` section 5 lists each class). The SQL tracking tests now live in `ApexPost.IntegrationTests/Tracking`
and every Couchbase class except `CourierHarnessTests` lives in `ApexPost.Courier.Offline.Tests`. Shared test packages
are in `tests/Directory.Build.props`; the one SQL Server lookup for all SQL fixtures is `tests/Common/SqlTestServer.cs`.

## Branches

- Integration branch: `week5-testing`, created from `main` (51ed2f3).
- Step branches: `week5-testing-NN-name`, each merged by the user into `week5-testing` through a PR.
- Final PR `week5-testing` → `main` only when the user asks for it ("final PR week5").

## Commands

```bash
dotnet build Apex_Post_Logistic.slnx -c Release
dotnet test Apex_Post_Logistic.slnx -c Release --filter "Category!=Measurement"
dotnet test Apex_Post_Logistic.slnx -c Release --filter "Category=Unit"
```

## Progress log

| Date | Step | Progress |
| --- | --- | --- |
| 2026-11-15 | 01 | Strategy: `week5-testing` created from `main`; `CLAUDE.md` with the Week 5 section committed; CI runs on `week5-*` branches; `TESTING-STRATEGY.md` written from a count of the real tests (1,134 test methods in 8 projects, 190 classes) before any new test. |
| 2026-11-15 | 02 | Restructure: shared test packages (Moq 4.21.0, TimeProvider.Testing 11.0.0, coverlet.collector 10.1.0); one SQL Server lookup (`SqlTestServer`: env var → user-secrets → LocalDB → skip, fails in CI); `TrackingSqlTests` moved to IntegrationTests; Courier duplicates merged by level (5 classes removed, unique cases kept); `Category` trait on all 185 classes; both test hosts on a fixed `FakeTimeProvider`. Full suite before 1,769 passed / 0 failed / 0 skipped, after 1,748 / 0 / 0 (−34 duplicate cases, +13 kept unique cases). No production code changed. |
| 2026-11-15 | 03 | Unit tests: full 6×6 `ShipmentStatusRules.CanMove` matrix (36 `MemberData` rows) + `StatusFor`; tracking-number and courier-id theories (`CR-099` / `CR-1000`); `decimal` weight `TheoryData` (0 / 0.0999 / 0.1 / 30 / 30.001 / -1 / 4 decimals) on domain and DTO; new `TrackingLimitsTests` (page 0/1, page size 0/1/50/51, upload 0/1/100/101); exact type + message for `DomainException`, `ValidationFailedException`, `InvalidStatusTransitionException` and courier `ArgumentException`. README "Running the tests". Unit cases 991 → 1,103; full suite 1,860 passed / 0 failed / 0 skipped. No production code changed. |
| 2026-11-15 | 04 | Moq: `ShipmentTrackingServiceMoqTests` (all four tracking ports mocked + `FakeTimeProvider`: duplicate create / invalid transition / locked delete never save, NotFound, scans staged by id (`It.Is`) with one save, stored and in-batch duplicates skipped, forbidden upload never touches the repository (`VerifyNoOtherCalls`), token pass-through, `DiscardChanges` after `ThrowsAsync(DuplicateKeyException)`); `OutboxServiceMoqTests` (Synced / Failed per scan, one throwing upload does not stop the rest (`Times.Exactly(3)`), nothing pending → uploader never called, oldest first, token rules). Testability change: new port `IScanSyncStatusUpdater`, taken by `OutboxService` instead of the concrete Couchbase repository (no behaviour change). `TESTING-STRATEGY.md` 4.3 "Moq vs fakes" + "what each mock does NOT prove", 6.1 production changes. Full suite 1,886 passed / 0 failed / 0 skipped. |
| 2026-11-15 | 05 | HTTP handler: one test per case for `ApexApiClient` (`ApexApiClientHttpHandlerTests`) and `HttpShipmentUploader` (`HttpShipmentUploaderHttpHandlerTests`) through auth + resilience: 200 v2 JSON with string enums, 404, 500 retried 4 times then failure, every attempt timing out, caller cancel (thrown, 1 attempt), 409 ProblemDetails and 400 ValidationProblemDetails → friendly text, plus Moq `Protected()` `SendAsync` with `Verify` (POST, path, Bearer token, JSON body). No network: base address `https://apexpost.test` and `NetworkGuardHandler` in front of every fake (`NetworkGuardHandlerTests`). No waiting: zero retry delay, timeouts on a `FakeTimeProvider` (`TimeoutTests` rewritten that way after its 300 ms wall-clock version flaked once). No production code changed. Full suite 1,907 passed / 0 failed / 0 skipped. |
| 2026-11-15 | 06 | Integration: `TrackingRepositorySqlTests` on a throw-away SQL Server database (two contexts as two requests): duplicate tracking number past the check → `DuplicateKeyException` (`SqlException` 2601), same scan id twice → one row (service retry and repository race), status + city (upper case) + page 2 filtered and paged in SQL (command text captured by `SqlCommandCapture`), stale `Version` → `ConcurrencyConflictException`; `CounterTransactionRowVersionTests` proves the real `rowversion` conflict on CounterTransactions. `ShipmentLifecycleEndToEndTests` on `TestAuthApiFactory` (`TestAuthHandler` in `ConfigureTestServices`, Courier CR-101): POST v2 → 201 + Location → GET 200 → scans 2 accepted → same batch 2 duplicates → GET scans → InTransit; 401 anonymous, 403 Courier on an Admin action. `TESTING-STRATEGY.md` 3.6 counts, 6.2 "Why mocking EF Core gives false confidence" (3 examples from this code), 6.3 test scheme vs real JWT. No production code changed. Full suite 1,916 passed / 0 failed / 0 skipped. |
| 2026-11-15 | 07 | TDD (W5.5): a search proved no scan-age rule existed. `RED` (72 h 1 min old scan must be rejected; failed on `Assert.Single`, real output in `TDD-LOG.md`) → `GREEN` (one check in `TryRecordAsync`) → `TEST` (exactly 72 h accepted and 72 h + 1 s rejected both passed at once, so no second RED/GREEN; stated honestly) → `REFACTOR` (`TrackingOptions.MaxScanAgeHours` default 72, `ScanAgePolicy` with `IOptions<TrackingOptions>` by constructor injection, registered in `AddShipmentTracking`, bound + validated at start-up in the API) → `REFACTOR` (policy tidy, configured-limit theory, `TrackingOptionsRegistrationTests`, `ScanAgeEndpointTests` through the real host: 200 + "Scan is older than 72 hours.", host setting 24 h honoured; `.http` age examples and relative times for accepted scans; `API-NOTES.md`). Six existing constructor calls gained the policy argument (review table). Full suite 1,929 passed / 0 failed / 0 skipped. PR to be merged with "Create a merge commit". |
