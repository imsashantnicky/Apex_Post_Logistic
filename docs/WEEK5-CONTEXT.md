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
