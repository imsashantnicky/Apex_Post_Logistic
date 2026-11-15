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

Some projects hold tests of both levels today (for example Couchbase tests inside `ApexPost.Courier.UnitTests`).
The `Category` trait, not the project name, decides the level; `TESTING-STRATEGY.md` section 5 lists each class.

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
