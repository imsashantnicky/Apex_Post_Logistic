# Week 3 submission checklist

**Read this first.** The official Week 3 brief and its assessment rubric were not available for this check: the files are
not in the repository or on the test machine. This list is therefore built from the project rules and the Phase 5
requirements, not from the rubric, and no score is claimed. Compare it with the rubric before submitting.

| Status | Meaning |
| --- | --- |
| Done | Done, and the evidence was produced by running it |
| Deviation | Works, but differs from the written rule on purpose; the reason is recorded |
| Open | Could not be verified here |

Results at the end of the work (2026-11-02, Windows 11, .NET SDK 10.0.402, SQL Server 2025):

| Check | Result | Evidence |
| --- | --- | --- |
| `dotnet restore` | Succeeded | [build-and-test.txt](build-and-test.txt) |
| `dotnet build -c Release` | 0 warnings, 0 errors | [build-and-test.txt](build-and-test.txt) |
| `dotnet test -c Release` | 972 passed, 0 failed, 0 skipped (542 unit, 430 integration) | [build-and-test.txt](build-and-test.txt) |
| The same without the 8 measurement tests | 964 passed | [fresh-clone.txt](fresh-clone.txt) |
| New clone, following the README | Passed, including the batch file, migrations, build, tests and a first start | [fresh-clone.txt](fresh-clone.txt) |
| CI on GitHub: Ubuntu, SQL Server 2022 | All steps succeeded, tests in 69 seconds | [Run 41399500671](https://github.com/imsashantnicky/Apex_Post_Logistic/actions/runs/41399500671). Its log needs a GitHub sign-in, so the test counts of that run were not read |

## Requirements of Phase 5

| # | Requirement | Status | Evidence |
| --- | --- | --- | --- |
| 1 | Unit tests for stock, identity verification, weight limits, voids and refunds | Done | `tests/ApexPost.UnitTests/Rules`, with `TransactionLifecycleRuleTests` for what spans several steps |
| 1 | Service tests with fake repositories and a spy Unit of Work: one commit on success, none on failure | Done | `UnitOfWorkBoundaryTests`, `UnitOfWorkSpy`. Three deliberate code changes (a second commit, a dropped commit, a commit in a repository) each made tests fail |
| 2 | SQL Server tests on a disposable database, never the development one | Done | `DisposableDatabaseTests`: the host is on `ApexPostTests_<guid>`, and the database is gone afterwards. 0 left behind in [fresh-clone.txt](fresh-clone.txt) |
| 2 | EF mappings | Done | `EfMappingTests`: model against migrations, all 17 tables, column types, a mixed basket read back, stored values, constraints, delete behaviour |
| 2 | Query translation | Done | `QueryTranslationTests` compares each query service with hand-written SQL; `GeneratedSqlTests` |
| 2 | Atomic rollback | Done | `AtomicSaleTests` |
| 2 | Stock concurrency conflicts | Done | `StockConcurrencyTests` (two servers on one database), [rowversion-conflict.txt](rowversion-conflict.txt) |
| 2 | Stored procedure results | Done | `StoredProcedureTests`, [stored-procedures.txt](stored-procedures.txt) |
| 3 | MVC tests: sale persistence | Done | `SaleToReportTests.A_sale_leaves_every_row_it_should` |
| 3 | MVC tests: Branch Manager authorization for voids | Done | `SaleToReportTests`, `ReversalTests`, `BusinessRulesOnSqlTests` |
| 3 | MVC tests: report totals | Done | `The_daily_sales_report_grows_by_exactly_the_sale` checks the exact change against arithmetic done by the test |
| 3 | MVC tests: Problem Details | Done | `SaleToReportTests` (400, 403, 404), `ErrorMappingTests` (400, 404, 409), [http-problem-details.txt](http-problem-details.txt) |
| 4 | Week 2 business-rule tests unchanged | Done | [week2-tests-unchanged.txt](week2-tests-unchanged.txt): 0 of 33 files changed, and their 395 tests pass |
| 4 | Those rules proved with SQL persistence enabled | Deviation | The Week 2 unit tests run on fakes by design, so they do not touch SQL. The same rules and boundaries run through the real application on SQL Server instead: `BusinessRulesOnSqlTests` and 3 other suites, 52 tests. Some Week 2 integration test files were adapted when their factory moved to SQL Server; the list is in the same file |
| 5 | Nullable enabled, zero warnings | Done | `Directory.Build.props`, [build-and-test.txt](build-and-test.txt) |
| 5 | All database access asynchronous, with `CancellationToken` where supported | Done | `PortContractTests` reads the compiled code. A cancelled token is tested in `QueryTranslationTests`. `ApexFactory` migrates synchronously, because the host override it runs in is synchronous; that is test code |
| 6 | At least seven new ADRs | Done | ADR-010 to ADR-017 in [DESIGN.md](../../DESIGN.md): eight |
| 7 | README: prerequisites, database, secrets, migrations, build, test, run, demo accounts, screenshots, troubleshooting | Done | [README.md](../../README.md) |
| 8 | DESIGN: architecture and dependency diagrams, ER diagram, normalization anomalies, ADRs, SOLID | Done | Sections 2, 6, 7 and 14 of [DESIGN.md](../../DESIGN.md). The 6 diagrams were opened on GitHub: all rendered, no error text, and the ER diagram was inspected by eye |
| 9 | PERFORMANCE: plans, measurements, index trade-offs, generated SQL | Done | [PERFORMANCE.md](../../PERFORMANCE.md). All 105 cells of its main table were compared with the raw files, and 3 workloads were measured again ([perf-recheck.txt](perf-recheck.txt)) |
| 10 | Idempotent migration script; SQL scripts, migrations and seed data committed | Done | `sql/migrations/idempotent-migrations.sql`, run twice on a blank database ([idempotent-migration-check.txt](idempotent-migration-check.txt)); `MigrationScriptTests` |
| 11 | Build and test evidence | Done | [build-and-test.txt](build-and-test.txt), images 31 and 32 |
| 11 | Major MVC workflows | Done | 30 steps: [mvc-workflows.txt](mvc-workflows.txt), [docs/screenshots/](../screenshots/) |
| 11 | Validation and error handling | Done | Images 05, 06, 07, 11, 12, 15, 28; [http-problem-details.txt](http-problem-details.txt) |
| 11 | Database schema and data | Done | [database-schema.txt](database-schema.txt), [database-data.txt](database-data.txt), images 33 and 34 (`sqlcmd` output, not SSMS) |
| 11 | Concurrency behaviour | Done | [rowversion-conflict.txt](rowversion-conflict.txt), image 28 for the 409 in the browser, `docs/perf/blocking` |
| 11 | Stored procedures | Done | [stored-procedures.txt](stored-procedures.txt), image 35 |
| 11 | Performance comparisons | Done | [perf-before-after.txt](perf-before-after.txt), image 37, `docs/perf/plans` |
| 12 | `.gitignore`, secrets, connection strings, machine names, personal data, database files, in the tree and the history | Done | [security-scan.txt](security-scan.txt). Nothing was exposed, so no history rewrite or rotation was needed. `.gitignore` now also covers database and key files |
| 13 | Fresh clone by the README | Done | [fresh-clone.txt](fresh-clone.txt). It found real problems, listed below |
| 13 | Meaningful commits and feature branches | Done | `test/week3-verification`, `chore/week3-submission` |

## Rules of the project, where the code differs

| Rule | Status | Reason |
| --- | --- | --- |
| Domain references nothing; Application defines ports; only Infrastructure uses EF Core | Done | `LayeringTests`, `ControllerLayeringTests` |
| Only EF Core packages added | Deviation | `System.Security.Cryptography.Pkcs` is pinned as well: without it the build fails on NU1902 and NU1903, because `SqlClient` brings a vulnerable 9.0.11. Tried and confirmed. Recorded in DESIGN section 2 |
| 17 tables, types, constraints, delete behaviour | Done | `EfMappingTests`, [database-schema.txt](database-schema.txt) |
| Repositories include `IMoneyOrderRepository` | Deviation | It does not exist. A money order is a line of the aggregate and is stored with it, so the port would have no caller, and the project rules forbid an interface without a use. See ADR-013 |
| `ShipmentRecord` and `MoneyOrderRecord`, keyed 1:1 by `TransactionLineId` | Deviation | The line kinds are mapped table-per-concrete-type, so there is no shared parent table and no record for them. See ADR-014 |
| `Address` owned by `Branch` | Deviation | The domain gives a branch no address; `Address` is owned by `Shipment`. See ADR-014 |
| Role and ProductCategory as domain entities | Deviation | They are enums in the domain, stored as lookup tables through records. See ADR-014 |
| Audit store commits separately and never throws | Done | ADR-016, `AuditPersistenceTests` |
| No invented numbers | Done | Every figure links to saved output |

## What the verification found and fixed

| Problem | Fix |
| --- | --- |
| `Counters.BranchCode` allowed NULL, so a counter could exist without a branch | Required, in migration `RequireCounterBranch` |
| A JSON caller of a form POST with invalid input got a redirect, not a 400 | `ValidationFilter` now honours the `Accept` header, like `DomainExceptionFilter` |
| `setup-secrets.bat` wrote a placeholder over any real connection string | It asks now, and defaults to the local database |
| An empty answer to `setup-secrets.bat` ended in a misleading project error | It stops with a clear message |
| `CommandCountTests` depended on the order of the tests, which only a new clone showed | The test clears the catalogue cache first |
| The idempotent script fails under plain `sqlcmd` (Msg 1934) | `-I` is documented in the README |
| The README claimed that no screenshots exist, and CI never ran on the branch in use | Both corrected |
| The first CI run on that branch stalled in the tests for over 20 minutes. The likely cause is a missing repository secret, so that SQL Server never started; the log could not be read to confirm it | The container now gets a password made per run, with a health check and a 20-minute limit. The next run passed |

## Open items

| Item | Why it is open |
| --- | --- |
| The official rubric | Not available. Items it adds, or weighs differently, are not covered here |
| Performance on SQL Server 2022 | The tests pass there in CI, but the measurements come from SQL Server 2025 and were not repeated |
| SSMS screenshots and two SSMS sessions | SSMS is not installed; `sqlcmd` was used |
| Signing in as `apexpost_app_login` | The server accepts Windows logins only. The role was tested by impersonation |
| Load, disk performance | No test ran many users at once, and all runs were served from memory |
