# Testing strategy – ApexPost Logistics (Week 5)

This document was written **before any new Week 5 test**. Every number in it comes from the repository at commit
`51ed2f3` (`main` after Week 4) or from a real test run on that commit. It is updated in every Week 5 step so that it
stays true to the code; the step pull requests on GitHub say what changed when. Diagrams of this strategy:
[docs/testing/TEST-STRATEGY-DIAGRAM.md](docs/testing/TEST-STRATEGY-DIAGRAM.md).

Baseline run (2026-11-15, `dotnet build Apex_Post_Logistic.slnx -c Release` then
`dotnet test Apex_Post_Logistic.slnx -c Release --no-build --filter "Category!=Measurement"`):
build 0 warnings, 0 errors; **1,769 test cases passed, 0 failed, 0 skipped**, run against a local SQL Server.

After step 02 (restructure, same commands, same machine): build 0 warnings, 0 errors; **1,748 test cases passed,
0 failed, 0 skipped**. The 21 fewer cases are the removed Courier duplicates (34 cases) minus the unique cases kept
from them (13 cases); the 4 SQL tracking tests moved project but still run. Section 3.2 has the details.

## 1. Scope

**In scope** – code with business rules or contracts that other parts depend on:

| Area | Code |
| --- | --- |
| Domain and application services | `src/ApexPost.Domain` (Week 3 counter rules, `Tracking/`), `src/ApexPost.Application` (services, `Tracking/ShipmentTrackingService`) |
| Validation rules | `src/ApexPost.Api/Contracts` (request DTOs, `ContractRules`, `NotInFutureAttribute`), `ApexPost.Application/Validation` |
| Repositories and queries (integration level) | `src/ApexPost.Infrastructure` (EF Core, SQL Server, stored procedures, `Tracking/`) |
| Web API controllers and endpoints | `src/ApexPost.Api` (v1, v2, partner route, security, exception handler) |
| Typed API client | `src/Mobile/ApexPost.Courier.ApiClient` (JSON, ProblemDetails, auth handler, Polly resilience, `HttpShipmentUploader`) |
| Couchbase Lite outbox and repository | `src/Courier/ApexPost.Courier.Offline` (`CouchbaseScanRepository`, `CouchbaseScanQueries`, `OutboxService`) |

**Out of scope** – MAUI UI, pages, navigation and any browser or front-end testing (`src/Mobile/ApexPost.Courier`).
`tests/Mobile/ApexPost.Courier.Presentation.Tests` is **kept** because its tests check plain C# view-model logic
without a UI, but it is **not extended** in Week 5. The Week 3 MVC pages are covered only by the existing
`WebApplicationFactory` tests in `ApexPost.IntegrationTests`; no new page tests are added.

## 2. Risk map

Likelihood and cost are rated Low / Medium / High. "Cost" is the damage if the defect reaches production.

| # | Risk | Code location | Likelihood | Cost | Test level | Test classes (existing → planned) |
| --- | --- | --- | --- | --- | --- | --- |
| R1 | A shipment moves backwards, stays, or leaves Delivered/Failed | `Domain/Tracking/ShipmentStatusRules.cs` (`CanMove`, `StatusFor`), `ShipmentTrackingService.ChangeStatusAsync` / `TryRecordAsync` | Medium | High (wrong tracking shown to customers, delivered parcel re-opened) | Unit (full 6×6 matrix); integration only for 409 wiring | `ShipmentStatusRulesTests` (full 6×6 `MemberData` matrix since step 03), `ShipmentTrackingServiceTests` (exact `InvalidStatusTransitionException` message), `ShipmentEndpointTests.Patch_status_backward_returns_409_invalid_transition` |
| R2 | Two shipments get the same tracking number | `ShipmentTrackingService.CreateAsync` (check), unique index `IX_TrackedShipments_TrackingNumber` (`Infrastructure/Tracking/Configurations/TrackedShipmentConfiguration.cs`), `SqlException` 2601/2627 → `DuplicateKeyException` | Medium | High (scans attached to the wrong parcel) | Unit (service check with Moq), integration (real unique index), BDD (business outcome) | `ShipmentTrackingServiceTests`, `ShipmentTrackingServiceMoqTests` (duplicate → `SaveChangesAsync` never), `TrackingModelTests.Tracking_number_and_scan_id_have_unique_indexes`, `ShipmentEndpointTests.Create_with_a_tracking_number_in_use_returns_409`, `TrackingRepositorySqlTests` (two requests pass the check, the unique index refuses the second: `DuplicateKeyException` over `SqlException` 2601, since step 06) → `ShipmentCreation.feature` (duplicate tracking number refused, step 08) |
| R3 | A re-sent scan is stored twice (offline retries make re-sends normal) | `ShipmentTrackingService.UploadAsync` / `UploadOnceAsync` (`DistinctBy`, `FindStoredScanIdsAsync`, retry on `DuplicateKeyException`), unique scan id index | High | High (history duplicated, status moved twice) | Unit (in-batch duplicates, stored ids), integration (parallel uploads on SQL Server), BDD | `ShipmentScanUploadTests`, `ShipmentScanEndpointTests.Parallel_uploads_of_the_same_batch_store_each_scan_once`, `HttpUploaderEndToEndTests`, `TrackingRepositorySqlTests` (same scan id in two requests → one row, since step 06), `ShipmentLifecycleEndToEndTests` (second upload counted as duplicates) → `ScanSync.feature` (lost answer, re-sent scan stored once, step 08) |
| R4 | A courier uploads scans as another courier, or a role does what it must not | `Application/Tracking/ShipmentTrackingService.CheckBatch`, `Api/Security/ClaimsCurrentCourier.cs`, `Composition/ApiSecurityRegistration.cs` (policies) | Medium | High (forged delivery proof) | Unit (ownership rule with Moq `ICurrentCourier`), integration (real JWT pipeline, 401/403) | `ScanOwnershipTests`, `ShipmentTrackingServiceMoqTests` (Moq `ICurrentCourier`, repository never touched, since step 04), Api `AuthorizationTests`, `AuthenticationTests` (real JWT), `ShipmentLifecycleEndToEndTests` (`TestAuthHandler` scheme: Courier CR-101 flow, 401, 403, since step 06; section 6.3) |
| R5 | The client retries a 4xx or a non-idempotent request (duplicate PATCH, hammering on 400) | `ApiClient/Resilience/ApexResilience.cs`, `TransientFailure.cs`, `IdempotentRequest.cs` | Medium | Medium (duplicate status change, battery/network waste, locked account) | Unit (fake `HttpMessageHandler`, Moq `Protected()`) | `RetryRuleTests`, `RetryTests`, `CircuitBreakerTests`, `TimeoutTests` (fake clock since step 05), `ApexApiClientHttpHandlerTests` and `HttpShipmentUploaderHttpHandlerTests` (one test per HTTP case, Moq `Protected()` `SendAsync` + `Verify`, since step 05) |
| R6 | The outbox marks a scan `Synced` that the server did not store, or loses a scan on a network error | `Courier.Offline/Outbox/OutboxService.cs`, `OutboxService.Batches.cs` (`ApplyAsync`) | Medium | High (proof of delivery silently lost) | Integration (real Couchbase in a temp folder); unit with Moq `IShipmentUploader` | `OutboxServiceTests`, `BatchOutboxTests`, `OutboxServiceMoqTests` (Moq uploader and status updater, since step 04) → `ScanSync.feature` (offline scans stay Pending, then Synced; rejected scan Failed, step 08) |
| R7 | A validation limit is off by one (weight 0.1–30 kg, upload 1–100 scans, page size 1–50, scan time 2 min ahead) | `Api/Contracts/ContractRules.cs`, `TrackingLimits.cs`, `TrackedShipment.MaximumWeightKg`, `NotInFutureAttribute.cs` | High | Medium | Unit (at, below and above each limit) | `ShipmentRequestTests`, `ScanItemTests`, `ScanUploadRequestTests`, `TrackedShipmentTests`, `TrackingLimitsTests` (boundary theories since step 03) |
| R8 | An unexpected error leaks a stack trace or internal message to callers | `Api/Errors/ApiExceptionHandler.cs` (`StackTraceKey` only in Development), Web `ProblemExceptionHandler` | Low | High (information disclosure) | Integration (Production host) | Api `ProductionErrorTests`, `ExceptionMappingTests`, Web `ExceptionHandlingTests`, `ProblemExceptionHandlerTests` |
| R9 | The deprecated v1 contract drifts and breaks existing partners | `Api/Contracts/V1/*`, `Controllers/V1/*`, `contracts/v1.json` | Medium | High (partner integrations break) | Integration (JSON snapshot through the real host) | `V1ContractSnapshotTests`, `ContractTrackingTests`, `VersionHeaderTests` |
| R10 | Week 3 money or stock is wrong (rounding, tax, negative stock, lost update) | `Domain/Common/Money.cs`, `Domain/Inventory/StockItem.cs`, `Application/Services/CheckoutService`, rowversion on `BranchStock` | Low (mature, heavily tested) | High (financial loss) | Unit (rules), integration (concurrency on SQL Server) | `MoneyTests`, `StockRuleTests`, `TaxRuleTests`, `CheckoutServiceTests`, `StockConcurrencyTests`, `BusinessRulesOnSqlTests` – kept; `CounterTransactionRowVersionTests` added in step 06 (stale `rowversion` on CounterTransactions → `ConcurrencyConflictException`) |
| R11 | A test passes or fails depending on the wall clock | Tests using `DateTime.UtcNow` (section 5) | Medium | Medium (flaky CI, hidden bugs around midnight/expiry) | All | Fixed in step 02: both test hosts run on a fixed `FakeTimeProvider`, test data uses fixed times (section 5.1, finding 3) |
| R12 | A scan uploaded days late moves a shipment long after the parcel moved on | `Application/Tracking/ScanAgePolicy.cs`, `TrackingOptions.MaxScanAgeHours`, `ShipmentTrackingService.TryRecordAsync` | Medium (devices stay offline) | Medium (wrong status shown, confusing history) | Unit (TDD, at / above the 72 h limit); integration for the API answer | `ScanAgeRuleTests` (TDD, step 07, section 8), `TrackingOptionsRegistrationTests`, `ScanAgeEndpointTests` (API answer), `ScanSync.feature` (71 h / 72 h / 73 h outline as the courier sees it, step 08) |

## 3. Test pyramid

### 3.1 Baseline (counted, commit 51ed2f3, before step 02)

"Methods" counts `[Fact]` and `[Theory]` attributes. "Cases" counts executed test cases (a theory row is one case),
taken from a TRX run of the baseline. The level comes from what a class really touches, not from the project name:
**unit** = no database, no file system, no host; **integration** = real SQL Server, real embedded Couchbase, temp
files, or an in-process `WebApplicationFactory` host.

| Project | Unit methods | Unit cases | Integration methods | Integration cases | Measurement methods |
| --- | --- | --- | --- | --- | --- |
| `tests/ApexPost.UnitTests` | 328 | 489 | 37 | 45 | 5 |
| `tests/ApexPost.Tracking.UnitTests` | 67 | 130 | 4 | 4 | 0 |
| `tests/Courier/ApexPost.Courier.UnitTests` | 21 | 38 | 52 | 53 | 0 |
| `tests/Mobile/ApexPost.Courier.ApiClient.Tests` | 74 | 159 | 0 | 0 | 0 |
| `tests/Mobile/ApexPost.Courier.Presentation.Tests` | 42 | 53 | 0 | 0 | 0 |
| `tests/ApexPost.IntegrationTests` | 7 | 13 | 280 | 427 | 0 |
| `tests/ApexPost.Api.Tests` | 45 | 102 | 138 | 208 | 0 |
| `tests/ApexPost.Courier.Offline.Tests` | 9 | 18 | 25 | 30 | 0 |
| **Total** | **593** | **1,002** | **536** | **767** | **5** |
| **Share** | 52.3 % | 56.6 % | 47.3 % | 43.4 % | 0.4 % of methods |

Totals: 8 test projects, 190 test classes, 1,134 test methods (923 `[Fact]`, 211 `[Theory]`), 1,769 executed cases
(the 5 measurement methods are excluded by the CI filter). BDD: 0 today. Only the 4 measurement classes carry a
`Category` trait. No test project references Moq, SQLite or Reqnroll.

Where the project name and the level disagree: 52 methods in `ApexPost.Courier.UnitTests` open a real Couchbase
database, 4 in `ApexPost.Tracking.UnitTests` open SQL Server, and 37 in `ApexPost.UnitTests` read and write temp
files (Week 2 JSON store). The other way round, 61 methods in the integration projects need no host or database
(validation attributes, mappers, reflection guards).

### 3.2 After step 02 (restructure)

Same counting rules. Methods are counted from the source; cases come from real runs with
`--filter "Category=Unit"` and `--filter "Category=Integration"` (every class now carries the trait, so the filter and
the level are the same thing).

| Project | Unit methods | Unit cases | Integration methods | Integration cases | Measurement methods |
| --- | --- | --- | --- | --- | --- |
| `tests/ApexPost.UnitTests` | 328 | 489 | 37 | 45 | 5 |
| `tests/ApexPost.Tracking.UnitTests` | 67 | 130 | 0 | 0 | 0 |
| `tests/Courier/ApexPost.Courier.UnitTests` | 27 | 45 | 9 | 9 | 0 |
| `tests/Mobile/ApexPost.Courier.ApiClient.Tests` | 74 | 159 | 0 | 0 | 0 |
| `tests/Mobile/ApexPost.Courier.Presentation.Tests` | 42 | 53 | 0 | 0 | 0 |
| `tests/ApexPost.IntegrationTests` | 7 | 13 | 284 | 431 | 0 |
| `tests/ApexPost.Api.Tests` | 45 | 102 | 138 | 208 | 0 |
| `tests/ApexPost.Courier.Offline.Tests` | 0 | 0 | 57 | 64 | 0 |
| **Total** | **590** | **991** | **525** | **757** | **5** |
| **Share** | 52.7 % | 56.7 % | 46.9 % | 43.3 % | 0.4 % of methods |

Totals: 8 test projects, 185 test classes (all with a `Category` trait), 1,120 test methods (911 `[Fact]`,
209 `[Theory]`), 1,748 executed cases. What changed and why:

| Change | Methods | Cases |
| --- | --- | --- |
| `TrackingSqlTests` moved from Tracking.UnitTests to IntegrationTests (same level, now in the right project) | 0 | 0 |
| 5 duplicate classes removed from Courier.Offline.Tests (section 5.2) | −20 | −34 |
| Unique cases of those classes kept in the merged classes (3 scan-id/Pending facts and 1 injection row in `ShipmentScanTests`, reopen in `CouchbaseScanRepositoryTests`, courier-id injection, 5 more injection rows and the 2-row Explain theory in `CouchbaseScanQueriesTests`) | +6 | +13 |
| **Net** | **−14** | **−21** |

The level of a project and of its classes now agree, with two documented exceptions: `CourierHarnessTests`
(Integration, stays in Courier.UnitTests because the harness is `internal`) and the Week 2 JSON-file classes in
`ApexPost.UnitTests` (Integration, kept in place as decided in step 01). The shares barely moved because this step
removed duplicates on both levels; the unit share rises in step 03 (parameterized boundary theories).

### 3.3 After step 03 (unit tests)

Same counting rules; cases from real runs on 2026-11-15. Only unit classes changed, so the integration columns are
the same as in 3.2.

| Project | Unit methods | Unit cases | Integration methods | Integration cases | Measurement methods |
| --- | --- | --- | --- | --- | --- |
| `tests/ApexPost.UnitTests` | 328 | 489 | 37 | 45 | 5 |
| `tests/ApexPost.Tracking.UnitTests` | 78 | 217 | 0 | 0 | 0 |
| `tests/Courier/ApexPost.Courier.UnitTests` | 31 | 59 | 9 | 9 | 0 |
| `tests/Mobile/ApexPost.Courier.ApiClient.Tests` | 74 | 159 | 0 | 0 | 0 |
| `tests/Mobile/ApexPost.Courier.Presentation.Tests` | 42 | 53 | 0 | 0 | 0 |
| `tests/ApexPost.IntegrationTests` | 7 | 13 | 284 | 431 | 0 |
| `tests/ApexPost.Api.Tests` | 45 | 113 | 138 | 208 | 0 |
| `tests/ApexPost.Courier.Offline.Tests` | 0 | 0 | 57 | 64 | 0 |
| **Total** | **605** | **1,103** | **525** | **757** | **5** |
| **Share** | 53.3 % | 59.3 % | 46.3 % | 40.7 % | 0.4 % of methods |

Totals: 186 test classes, 1,135 test methods (914 `[Fact]`, 221 `[Theory]`), 1,860 executed cases, 0 failed,
0 skipped. What changed (+15 methods, +112 cases):

| Change | Methods | Cases |
| --- | --- | --- |
| `ShipmentStatusRulesTests` replaced: full 6×6 matrix, completeness guard, undefined statuses, `IsFinal`, `StatusFor` with message | +1 | +24 |
| `TrackedShipmentTests`: wider tracking-number theory, `decimal` weight `TheoryData` on `Create` and `Replace`, exact messages | +3 | +26 |
| `TrackingFormatsTests`: missing format rows, courier `CR-099` / `CR-1000` | 0 | +15 |
| `TrackingLimitsTests` (new): page 0/1, page size 0/1/50/51, upload 0/1/100/101 with field and message | +7 | +21 |
| `ShipmentTrackingServiceTests`: Failed → Delivered row for `InvalidStatusTransitionException` | 0 | +1 |
| `ShipmentScanTests` (courier): courier-id boundaries, blank/null tracking number, undefined scan type, wrong id on `Restore` | +4 | +14 |
| `ShipmentRequestTests`, `ScanItemTests`: more tracking-number, weight and courier-id rows | 0 | +11 |
| **Net** | **+15** | **+112** |

### 3.4 After step 04 (Moq)

Real run on 2026-11-15 (`dotnet test Apex_Post_Logistic.slnx -c Release --filter "Category!=Measurement"`). Only
two new unit classes were added; every other row is the same as in 3.3.

| Project | Unit methods | Unit cases | Integration methods | Integration cases | Measurement methods |
| --- | --- | --- | --- | --- | --- |
| `tests/ApexPost.Tracking.UnitTests` | 93 | 236 | 0 | 0 | 0 |
| `tests/Courier/ApexPost.Courier.UnitTests` | 38 | 66 | 9 | 9 | 0 |
| other six projects | unchanged | unchanged | unchanged | unchanged | unchanged |
| **Total** | **627** | **1,129** | **525** | **757** | **5** |
| **Share** | 54.2 % | 59.9 % | 45.4 % | 40.1 % | 0.4 % of methods |

Totals: 188 test classes, 1,157 test methods (934 `[Fact]`, 223 `[Theory]`), 1,886 executed cases, 0 failed,
0 skipped. Added: `ShipmentTrackingServiceMoqTests` (+15 methods, +19 cases) and `OutboxServiceMoqTests`
(+7 methods, +7 cases).

### 3.5 After step 05 (HTTP handler)

Real run on 2026-11-15 (`dotnet test Apex_Post_Logistic.slnx -c Release --filter "Category!=Measurement"`). Three
new unit classes in one project; every other row is the same as in 3.4.

| Project | Unit methods | Unit cases | Integration methods | Integration cases | Measurement methods |
| --- | --- | --- | --- | --- | --- |
| `tests/Mobile/ApexPost.Courier.ApiClient.Tests` | 93 | 180 | 0 | 0 | 0 |
| other seven projects | unchanged | unchanged | unchanged | unchanged | unchanged |
| **Total** | **646** | **1,150** | **525** | **757** | **5** |
| **Share** | 54.9 % | 60.3 % | 44.6 % | 39.7 % | 0.4 % of methods |

Totals: 191 test classes, 1,176 test methods (952 `[Fact]`, 224 `[Theory]`), 1,907 executed cases, 0 failed,
0 skipped. Added: `ApexApiClientHttpHandlerTests` (+8), `HttpShipmentUploaderHttpHandlerTests` (+8),
`NetworkGuardHandlerTests` (+3 methods, +5 cases). `TimeoutTests` was rewritten on a fake clock with the same two
methods.

### 3.6 After step 06 (integration)

Real run on 2026-11-15 (`dotnet test Apex_Post_Logistic.slnx -c Release --filter "Category!=Measurement"`, SQL
Server tests on real throw-away databases, nothing skipped). Three new integration classes; every other row is the
same as in 3.5.

| Project | Unit methods | Unit cases | Integration methods | Integration cases | Measurement methods |
| --- | --- | --- | --- | --- | --- |
| `tests/ApexPost.IntegrationTests` | 7 | 13 | 290 | 437 | 0 |
| `tests/ApexPost.Api.Tests` | 45 | 113 | 141 | 211 | 0 |
| other six projects | unchanged | unchanged | unchanged | unchanged | unchanged |
| **Total** | **646** | **1,150** | **534** | **766** | **5** |
| **Share** | 54.5 % | 60.0 % | 45.1 % | 40.0 % | 0.4 % of methods |

Totals: 194 test classes, 1,185 test methods (961 `[Fact]`, 224 `[Theory]`), 1,916 executed cases, 0 failed,
0 skipped. Added: `TrackingRepositorySqlTests` (+5), `CounterTransactionRowVersionTests` (+1) and
`ShipmentLifecycleEndToEndTests` (+3).

### 3.7 After step 07 (TDD)

Real run on 2026-11-15 (`dotnet test Apex_Post_Logistic.slnx -c Release --filter "Category!=Measurement"`, SQL
Server tests on real throw-away databases, nothing skipped). Three new classes; every other row is the same as in 3.6.

| Project | Unit methods | Unit cases | Integration methods | Integration cases | Measurement methods |
| --- | --- | --- | --- | --- | --- |
| `tests/ApexPost.Tracking.UnitTests` | 97 | 243 | 0 | 0 | 0 |
| `tests/ApexPost.Api.Tests` | 48 | 117 | 143 | 213 | 0 |
| other six projects | unchanged | unchanged | unchanged | unchanged | unchanged |
| **Total** | **653** | **1,161** | **536** | **768** | **5** |
| **Share** | 54.7 % | 60.2 % | 44.9 % | 39.8 % | 0.4 % of methods |

Totals: 197 test classes, 1,194 test methods (968 `[Fact]`, 226 `[Theory]`), 1,929 executed cases, 0 failed,
0 skipped. Added: `ScanAgeRuleTests` (+4 methods, 7 cases), `TrackingOptionsRegistrationTests` (+3 methods, 4 cases)
and `ScanAgeEndpointTests` (+2).

### 3.8 After step 08 (BDD)

Real run on 2026-11-15 (`dotnet build Apex_Post_Logistic.slnx -c Release`: 0 warnings, 0 errors; then
`dotnet test Apex_Post_Logistic.slnx -c Release --no-build --filter "Category!=Measurement"`, SQL Server tests on
real throw-away databases, nothing skipped). One new project; every unit and integration row is the same as in 3.7.

| Level | Methods (scenarios) | Executed cases | Share of cases |
| --- | --- | --- | --- |
| Unit | 653 | 1,161 | 59.8 % |
| Integration | 536 | 768 | 39.6 % |
| Spec (`tests/ApexPost.Specs`) | 7 scenarios (2 of them outlines) | 12 | 0.6 % |
| **Total** | **1,196 + 5 measurement methods** | **1,941** | 100 % |

Per project (cases): Courier.Offline.Tests 64, Tracking.UnitTests 243, UnitTests 534, Courier.UnitTests 75,
Courier.ApiClient.Tests 180, Courier.Presentation.Tests 53, Api.Tests 330, Specs 12, IntegrationTests 450;
**1,941 passed, 0 failed, 0 skipped**. The Spec share is below the ~5 % target on purpose: each scenario shows a
business outcome whose rules are already proved below (section 9), so more scenarios would only repeat them.

### 3.9 Target

| Level | Target share (executed cases) | What belongs there |
| --- | --- | --- |
| Unit | ~70 % | Every business rule and limit: status transitions, scan → status mapping, ownership, de-duplication logic, weight/page/upload/scan-age limits, DTO validation attributes, mappers, retry classification, JSON and ProblemDetails parsing, Week 3 money/tax/stock rules |
| Integration | ~25 % | Wiring that a unit test cannot prove: EF Core mapping and SQL translation, unique indexes, rowversion, stored procedures, Couchbase indexes and listeners, middleware/filter order, authentication and authorization pipeline, status codes, Location headers, ProblemDetails shape, versioned routes and contract snapshots |
| BDD | ~5 % | A few business scenarios end to end at one stated level (section 9) |

**Rule: prove each rule once, at the lowest level that can prove it. Higher levels check wiring only.**
Example: the weight limit (0.1–30 kg) is proved with boundary theories on the DTO and the domain; the endpoint tests
check only that *an* invalid body becomes a 400 ValidationProblemDetails; the BDD outline shows the business meaning.

How we move towards the target, without inventing numbers:

- remove integration tests that repeat a rule already proved below (Courier duplicates, rule repeats through the
  host, see section 5);
- add parameterized unit tests for the rules listed in section 7 (each theory row is one executed case);
- add Moq-based service tests instead of new host tests.

The Week 3 web pages are tested end to end on purpose (they are server-rendered and have no other tests), so the
integration share will stay above 25 % unless those tests are rewritten, which is out of scope. The real proportion
at the end of the week is reported in section 12 and `docs/testing/test-run.txt`; if it misses the target we say so.

## 4. Framework choice

### 4.1 xUnit v3 versus NUnit

| Criterion | xUnit v3 | NUnit 4 |
| --- | --- | --- |
| Isolation | New class instance per test; no shared state unless asked for (`IClassFixture`, `ICollectionFixture`) | One instance per fixture by default; state leaks between tests unless `[FixtureLifeCycle(InstancePerTestCase)]` |
| Setup / teardown | Constructor + `IDisposable` / `IAsyncLifetime` per test; fixtures for expensive shared resources (SQL database, factory) | `[SetUp]`/`[TearDown]`, `[OneTimeSetUp]`; flexible but more attributes and hidden order |
| Parameterized tests | `[Theory]` + `[InlineData]`, `[MemberData]`, typed `TheoryData<…>` | `[TestCase]`, `[TestCaseSource]`, `[Values]`/`[Range]` combinatorics |
| Async and cancellation | `TestContext.Current.CancellationToken`, `Assert.Skip` for honest dynamic skips | Async supported; `Assert.Ignore` |
| Reqnroll support | `Reqnroll.xunit.v3` package | `Reqnroll.NUnit` |
| Consistency with this repo | All 8 test projects, 1,134 test methods, already use `xunit.v3.mtp-off` 4.0.2 | Would mean two frameworks, two runners and two styles |
| Team familiarity | Used since Week 2 | Not used in this course so far |

**Decision: xUnit v3 for the whole solution, including the new BDD project.** NUnit's extra features
(combinatorial values, one-time setup) do not solve a problem we have, and mixing frameworks would cost more
than it gives.

### 4.2 Moq versus hand-written fakes

| Use **Moq** when | Use a **hand-written fake** when |
| --- | --- |
| The test is about the *interaction*: was `SaveChangesAsync` called once, never, after `Add`? (`Verify`, `Times.*`) | The test needs *state* that behaves like storage across several calls (add, then find, then list) |
| A dependency must fail in a specific way (`ThrowsAsync(new DuplicateKeyException(…))`, a timeout) | Many tests share the same behaviour and a fake keeps them short (`InMemoryTracking`, `InMemoryRepository`) |
| Only one or two members of a port matter for the test | BDD scenarios, which must read like business steps, not like mock setup |
| HTTP: one `Moq.Protected()` `SendAsync` setup to show and verify the exact request | HTTP: scripted sequences of answers (`ScriptedHttpHandler`, `FakeHttpMessageHandler`) for retry and circuit tests |

Interfaces mocked with Moq (step 04): `ITrackedShipmentRepository` (the brief's `IShipmentRepository`),
`ITrackingQueries`, `ITrackingUnitOfWork`, `ICurrentCourier`, Courier `IScanRepository` / `IScanQueries`,
`IShipmentUploader`, and `HttpMessageHandler` (protected `SendAsync`). The existing fakes stay; section 4.3 says what
each mock does **not** prove.

### 4.3 Moq vs fakes (step 04)

**Brief names → real interfaces.** The brief uses generic names; this repository splits them by responsibility:

| Brief | Real interface(s) | Project | Mocked in |
| --- | --- | --- | --- |
| `IShipmentRepository` | `ITrackedShipmentRepository` (load + stage), `ITrackingUnitOfWork` (commit), `ITrackingQueries` (read views) | `src/ApexPost.Application/Tracking` | `Services/ShipmentTrackingServiceMoqTests` |
| (caller identity) | `ICurrentCourier` (courier id claim) | `src/ApexPost.Application/Tracking` | `Services/ShipmentTrackingServiceMoqTests` |
| `IScanRepository` | `IScanRepository` (CRUD), `IScanQueries` (pending / failed lists), `IScanSyncStatusUpdater` (status write, new in step 04) | `src/Courier/ApexPost.Courier.Domain/Ports` | `Offline/OutboxServiceMoqTests` (queries + status updater; the outbox never calls `IScanRepository`) |
| `IShipmentUploader` | `IShipmentUploader` (single scan); `IBatchShipmentUploader` extends it | `src/Courier/ApexPost.Courier.Domain/Ports` | `Offline/OutboxServiceMoqTests` (a `Mock<IShipmentUploader>` is not a batch uploader, so the single-scan path runs) |

**Which one, where.**

| Class | Double | Why |
| --- | --- | --- |
| `ShipmentTrackingServiceMoqTests` (new) | Moq on all four ports + `FakeTimeProvider` | The questions are interactions: is `SaveChangesAsync` called `Times.Once` / `Times.Never`, which scan ids reach `AddScan` (`It.Is`), is the caller's token passed on, is `DiscardChanges` called after a `DuplicateKeyException` (`ThrowsAsync`, `SetupSequence`), and is the repository not touched at all on a forbidden upload (`VerifyNoOtherCalls`) |
| `ShipmentTrackingServiceTests`, `ShipmentScanUploadTests`, `ScanOwnershipTests` (kept) | `InMemoryTracking` fake | Multi-step outcomes (create → move → upload → read history) need state that behaves like storage; Moq setups for that would copy the implementation |
| `OutboxServiceMoqTests` (new) | Moq on `IScanQueries`, `IScanSyncStatusUpdater`, `IShipmentUploader` | Which status each scan gets, that one throwing upload does not stop the others (`Times.Exactly(3)`), that nothing pending means the uploader is never called, the order of sending (`Callback`) and the token rules |
| `OutboxServiceTests`, `BatchOutboxTests` (kept, integration) | Real Couchbase + fake/scripted uploaders | The status is really written to the document and found again by the next query |
| BDD `ScanSync.feature` (step 06) | In-memory repositories | Scenarios must read as business steps, not as mock setup |

**What each mock does NOT prove.**

| Mocked interface | What the mock does NOT prove | Where that is proved instead |
| --- | --- | --- |
| `ITrackedShipmentRepository` | That `FindAsync` really finds a stored shipment, that `FindStoredScanIdsAsync` translates to valid SQL, that the unique scan-id / tracking-number indexes exist, that `AddScan` maps every column | `IntegrationTests/Tracking/TrackingSqlTests` (real SQL Server), `TrackingModelTests` |
| `ITrackingUnitOfWork` | That `SaveChangesAsync` commits, that a duplicate key or a rowversion conflict really arrives as `DuplicateKeyException` / `ConcurrencyConflictException`, that `DiscardChanges` really clears the change tracker | `TrackingSqlTests`, Api `ShipmentScanEndpointTests.Parallel_uploads_of_the_same_batch_store_each_scan_once` |
| `ITrackingQueries` | That the projection fills every field of `ShipmentView`, paging and ordering in SQL, the latest-scan subquery | `TrackingSqlTests`, Api endpoint tests |
| `ICurrentCourier` | That the API reads the `courierId` claim from a real token, that a non-courier role is refused by the authorization policy before the service runs | Api `AuthorizationTests`, `AuthenticationTests`, the `TestAuthHandler` end-to-end test (planned for a later step) |
| `IScanQueries` | That the SQL++ query returns only Pending (or Failed) scans, oldest first, using the index | `Courier.Offline.Tests/Queries/CouchbaseScanQueriesTests` |
| `IScanSyncStatusUpdater` | That the status is written to the Couchbase document, that an unknown id returns `false`, that the write survives a reopen | `Courier.Offline.Tests/Outbox/OutboxServiceTests`, `Repository/CouchbaseScanRepositoryTests` |
| `IShipmentUploader` | The HTTP request, JSON, status-code mapping, retries, timeouts and the real server answer (accepted / duplicate / rejected) | `Mobile/ApexPost.Courier.ApiClient.Tests` (HTTP uploader), `HttpUploaderEndToEndTests`, `BatchOutboxTests` |

A mock only answers what the test told it to answer, so a mocked test can pass while the real implementation is
wrong. That is why every row above has a real-storage or real-HTTP test as well, and why `DbContext` / `DbSet` are
never mocked (section 6).

### 4.4 HTTP handler tests for the typed client (step 05)

`ApexApiClient` and `HttpShipmentUploader` each have one test per HTTP case. Both run through the real registration
of `MauiProgram` (`AddHttpClient` + `AuthHeaderHandler` + `AddApexResilience`) with a fake primary handler
(`Support/ResilientClient.cs`), so every case also shows what the retry and the breaker do with it.

| Case | `ApexApiClientHttpHandlerTests` | `HttpShipmentUploaderHttpHandlerTests` |
| --- | --- | --- |
| 200 + v2 JSON, string enums | `GetShipmentAsync_Http200WithV2Json_…`: `"InTransit"` → `ShipmentStatus.InTransit`, nested recipient, 1 attempt | `UploadBatchAsync_Http200WithV2Json_…`: `Completed` with the rejected reason; request `scanType` sent as `"Pickup"` |
| 404 | NotFound, server detail, `StatusCode` 404, 1 attempt (4xx never retried) | `Refused` with the server detail, 1 attempt |
| 500 | Retried per policy: 1 + `MaxRetryAttempts` (= 4) attempts, then `Server` failure with the fixed text; the leaked exception text is not shown | 4 attempts with the same body (scan ids make the repeat safe), then `Retryable` with the fixed text |
| Attempt timeout | Every attempt hangs; the test advances a `FakeTimeProvider` by `AttemptTimeout` after each attempt reaches the handler; 4 attempts, then `Timeout` failure | 4 attempts, then `Retryable` with `FailureMessages.Timeout` |
| Caller cancellation | Cancel while the attempt hangs → `OperationCanceledException` thrown, 1 attempt (no retry) | Same: the exception reaches the outbox, 1 attempt |
| ProblemDetails | 409 problem → `Conflict`, the server detail as the message, trace id kept | `Refused` with the server detail |
| ValidationProblemDetails | 400 → `Validation`, "Please check: …" from the field errors, `FieldErrors` filled | `Refused` with the same "Please check: …" text |
| Moq `Protected()` | `UploadScansAsync_MoqHandler_…`: `Setup<Task<HttpResponseMessage>>("SendAsync", …)` + `Verify(Times.Once(), ItExpr.Is<HttpRequestMessage>(…))` on method POST, host, path, `Bearer moq-token`, and the `JsonContent` value (scan id); the serialized JSON is also parsed | `UploadAsync_MoqHandler_…`: same `Verify` on the full URL, `Bearer` token, `application/json` and the scan id and courier id in the body; `Times.Once()` also proves a 200 is not retried |

**No real network.** Every fake handler sits behind `Support/NetworkGuardHandler.cs` (`ClientFactory.Create`,
`ResilientClient`, `HttpClientFactoryWiringTests`, and the Moq tests). The base address is `https://apexpost.test/`;
`.test` is a reserved top-level domain that never resolves. The guard fails the test (`Assert.Fail`) when a request
names any other host or when the handler chain below it ends in a real `HttpClientHandler` / `SocketsHttpHandler`.
`NetworkGuardHandlerTests` proves both checks. The guard throws an xUnit `FailException`, which neither the client
nor Polly catches, so a leak cannot be turned into a "Network" failure and pass silently.

**No waiting.** `ResilientClient.NoDelaySettings` keeps the production retry count and breaker values but sets the
retry delay to zero and long timeouts (8 s per attempt, 5 min total) that only fire when a test advances a
`FakeTimeProvider`. `ResilientClient` registers that clock in DI, and the resilience pipeline takes its
`TimeProvider` from there. `ScriptedHttpHandler.Entered` (a semaphore released per attempt) tells the test when to
advance. The 16 case tests and the two timeout tests take no wall-clock time for delays or timeouts.

**What the Moq handler does NOT prove.** It returns whatever the setup says, so it does not prove that the real API
sends that answer, uses those status codes or field names, or accepts the request. That is proved by the API tests
(`ApexPost.Api.Tests`, `V2ContractTests`) and the end-to-end `HttpUploaderEndToEndTests`, which send the real client
to the in-process API. It also says nothing about TLS, DNS or proxies, which need a real network that tests never use (section 11).

## 5. Existing tests review

One row per existing test class (190 classes, 1,134 methods). Level as defined in 3.1. Decision is one of
**kept / modified / removed / replaced / moved**; "planned in step NN" means the change happens in a later step and
this table is updated then.

Two changes apply to *every* class and are not repeated in each row: since step 02 every class carries
`[Trait("Category", "Unit" | "Integration" | "Measurement")]` with the level of this table (before, only the 4
`Measurement` classes had one), and changed test methods are renamed to `Method_Scenario_ExpectedResult` style
(section 7). A trait alone does not count as "modified".

### 5.1 Findings evaluated

1. **SQL Server tests inside a unit project.** `ApexPost.Tracking.UnitTests/Infrastructure/TrackingSqlTests` opens a
   real SQL Server database (`TrackingDatabase` fixture) – it is an integration test in a unit project, and it fails
   with `InvalidOperationException` instead of skipping when no server is configured. → **moved** to
   `tests/ApexPost.IntegrationTests/Tracking/` with the shared SQL helper (done in step 02).
   `TrackingModelTests` only reads EF model metadata with `Server=unused` and never connects → **kept** as unit.
   The same is true for `ApexPost.UnitTests` `GeneratedSqlTests`, `MigrationScriptTests` and `DevelopmentSeederTests`
   (connection strings that are never opened or point to an unreachable port on purpose) → kept as unit.
2. **Courier project overlap.** Both `tests/Courier/ApexPost.Courier.UnitTests` (Part A) and
   `tests/ApexPost.Courier.Offline.Tests` (Part D) test the offline store, and most "unit" classes in the first one
   open a real Couchbase database. Duplicate pairs, compared method by method:

   | Courier.UnitTests class | Courier.Offline.Tests class | Overlap |
   | --- | --- | --- |
   | `Scans/ShipmentScanTests` | `Domain/ShipmentScanTests` | wrong tracking number and courier id rejected, lower case stored upper case |
   | `Offline/CouchbaseScanRepositoryTests` | `Repository/KeyValueCrudTests` | create/read/update/delete round trip, duplicate create rejected, unknown or malformed id returns null |
   | `Offline/CouchbaseScanQueriesTests` | `Queries/ScanQueryTests` | by tracking number oldest first, pending oldest first, counts per scan type, injection-style input |
   | `Offline/CourierDatabaseTests` | `Indexes/IndexTests` | both value indexes created, reopening creates no duplicates; `Explain` also in `CouchbaseScanQueriesTests` |
   | `Offline/OutboxServiceTests` | `Outbox/OutboxTests` (and `BatchOutboxTests`) | synced not uploaded again, failed only on retry, second run while busy |
   | `Offline/ScanChangeMonitorTests` / `CourierDatabaseTests.Dispose_with_registered_listener_tokens_closes_cleanly` | `Listener/ListenerDisposeTests` | listener clean-up on dispose (partial overlap) |

   Done in step 02 (planned for step 03 at first, brought forward because the step 02 brief asks for it):
   Courier.UnitTests keeps the pure-logic classes (scan rules, document mapper, fake uploader, table writer, the
   reflection guard moved in from Offline.Tests); the Couchbase classes **moved** to `ApexPost.Courier.Offline.Tests`
   (the integration project for Couchbase); inside each pair the unique cases were merged into the Part A class and
   the duplicate class was **removed**. Two choices differ from the first plan: `IndexTests` is removed, not merged
   into `CourierDatabaseTests`, because its Explain theory is a query concern and went to `CouchbaseScanQueriesTests`;
   and `CourierHarnessTests` stays in Courier.UnitTests (Integration trait) because the harness is `internal` and
   only that project has `InternalsVisibleTo`.
3. **Tests that read the wall clock.** `DateTime.UtcNow` / `DateTimeOffset.UtcNow` appear in Api
   `AutomaticValidationTests`, `NotInFutureAttributeTests`, `AuthorizationTests`, `DevTokenTests`, `V2ContractTests`,
   `HttpUploaderEndToEndTests` and the helpers `Support/ApiClient.cs`, `Support/TestTokens.cs`; and in Week 3
   `AtomicSaleTests`, `OverPostingTests`, `StoredProcedureTests`, `ReportPagesTests`, `SaleToReportTests` and the
   helper `Support/CapturedLogs.cs`. No production code uses `DateTime.UtcNow` (all services take `TimeProvider` or
   `IClock`); `TimeProvider.System` is registered only in composition roots. Done in step 02:
   - `ApexFactory` (Week 3 host) and `ApiFactory` (API host) replace `TimeProvider` with a `FakeTimeProvider` that
     starts at a fixed fictional instant (`FixedNow`, 2026-11-05) and moves 1 ms per read, so rows still get distinct,
     ordered times. Report "today", audit columns, price revisions, scan times, test tokens and the dev-token expiry
     are all computed from it.
   - IdentityModel checks token lifetime against the wall clock and has no public clock to inject, so the API test
     host replaces only `TokenValidationParameters.LifetimeValidator` with the same rule (expiry required, 30 s skew,
     same exception types) on the fake clock. Signature, issuer, audience and role checks stay the real ones.
   - The stored procedure that stamps its own time is compared with `SELECT SYSUTCDATETIME()` before and after the
     call. `OverPostingTests` compares with the host clock instead, because that column is set by the audit
     interceptor from `TimeProvider`, not by a database default.
   - `CapturedLogs` waits with a `CancellationTokenSource` timeout; `TimeProvider.System` in `GeneratedClientTests`,
     `FakeShipmentUploaderTests` and `ShipmentScanTests` was replaced by the host clock or a fixed `FakeTimeProvider`.
   - The one test whose *subject* is the system-clock fallback
     (`NotInFutureAttributeTests.IsValid_WithoutTimeProviderService_FallsBackToTheSystemClock`) reads
     `TimeProvider.System` with a one-hour margin and is the only documented exception.
   No production code had to change for this.
4. **HTTP validation tests that repeat attribute unit tests.** `AutomaticValidationTests` sends invalid bodies through
   the host to a probe controller. Its weight/phone/tracking-number and "scan from the future" cases repeat
   `ShipmentRequestTests` and `ScanItemTests`, which already prove every rule with `Validator`. What only the host can
   prove is that `[ApiController]` turns an invalid body into 400 ValidationProblemDetails and validates nested scans.
   → **modified** (step 03): keep one valid and one invalid body per request type and the nested Delivered-signature
   case; remove the clock-based "future" case (proved in `ScanItemTests`). The per-endpoint 400 tests in
   `ShipmentEndpointTests` / `ShipmentScanEndpointTests` are kept: each checks one endpoint's wiring once.
5. **Presentation view-model tests versus scope.** The 9 classes test plain C# (commands, banners, sync status) with
   fakes, no MAUI types. → **kept, not extended** (UI is out of scope; the logic is cheap to keep green).
6. **Status-transition coverage.** `ShipmentStatusRulesTests` has 4 `InlineData` rows; the full 6×6 matrix (36 pairs)
   is not proved. → **replaced** by a `TheoryData<ShipmentStatus, ShipmentStatus, bool>` matrix test (done in step 03).

### 5.2 Review table

Decisions after step 07: 155 kept, 22 modified, 7 moved, 5 removed, 1 replaced (190 classes of the baseline; a moved
class is listed under its new project and path). Step 03 had 157 / 20 / 7 / 5 / 1, step 02 had 163 / 14 / 7 / 5 / 1 and step 01 166 / 15 / 6 / 2 / 1; the differences are explained in
the rows that say "Was …". "Methods" and "Cases" are the baseline numbers of 3.1, except for merged classes, which show
their numbers after the merge.

| Project | Class | Methods | Cases | Level | Decision | Reason |
| --- | --- | --- | --- | --- | --- | --- |
| UnitTests | `Architecture/LayeringTests.cs` | 8 | 16 | Unit | kept | Reflection layering guard; cheap and catches wrong references early |
| UnitTests | `Architecture/PortContractTests.cs` | 5 | 5 | Unit | kept | Reflection layering guard; cheap and catches wrong references early |
| UnitTests | `Domain/CodeTests.cs` | 5 | 17 | Unit | kept | Week 3 domain invariant proved at the lowest level (R10) |
| UnitTests | `Domain/CounterTransactionTests.cs` | 9 | 14 | Unit | kept | Week 3 domain invariant proved at the lowest level (R10) |
| UnitTests | `Domain/EntityTests.cs` | 14 | 22 | Unit | kept | Week 3 domain invariant proved at the lowest level (R10) |
| UnitTests | `Domain/IdentityVerificationTests.cs` | 6 | 11 | Unit | kept | Week 3 domain invariant proved at the lowest level (R10) |
| UnitTests | `Domain/LineItemTests.cs` | 6 | 7 | Unit | kept | Week 3 line-item invariants (R10) |
| UnitTests | `Domain/MeasureTests.cs` | 10 | 14 | Unit | kept | Week 3 domain invariant proved at the lowest level (R10) |
| UnitTests | `Domain/MoneyTests.cs` | 10 | 15 | Unit | kept | Week 3 domain invariant proved at the lowest level (R10) |
| UnitTests | `Infrastructure/CachedCatalogTests.cs` | 8 | 10 | Unit | kept | Infrastructure logic without I/O (EF model metadata or pure logic); never opens a connection |
| UnitTests | `Infrastructure/DataFolderHealthTests.cs` | 2 | 2 | Integration | kept | Real temp-folder I/O (Week 2 JSON data folder); integration by what it touches, stays in place: moving adds churn and no value |
| UnitTests | `Infrastructure/DemoDataSeederTests.cs` | 7 | 9 | Integration | kept | Seeds JSON files in a temp folder; integration by what it touches, stays in place |
| UnitTests | `Infrastructure/DemoDataSetTests.cs` | 11 | 17 | Unit | kept | Infrastructure logic without I/O (EF model metadata or pure logic); never opens a connection |
| UnitTests | `Infrastructure/DependencyInjectionTests.cs` | 3 | 3 | Integration | kept | Builds the real container on a temp data folder; integration by what it touches |
| UnitTests | `Infrastructure/DevelopmentHistoryTests.cs` | 6 | 7 | Unit | kept | Infrastructure logic without I/O (EF model metadata or pure logic); never opens a connection |
| UnitTests | `Infrastructure/DevelopmentSeederTests.cs` | 1 | 5 | Unit | kept | Infrastructure logic without I/O (EF model metadata or pure logic); never opens a connection |
| UnitTests | `Infrastructure/GeneratedSqlTests.cs` | 3 | 3 | Unit | kept | Infrastructure logic without I/O (EF model metadata or pure logic); never opens a connection |
| UnitTests | `Infrastructure/InMemoryNotificationSinkTests.cs` | 3 | 3 | Unit | kept | Infrastructure logic without I/O (EF model metadata or pure logic); never opens a connection |
| UnitTests | `Infrastructure/JsonCredentialStoreTests.cs` | 3 | 3 | Integration | kept | Real file round trip in a temp folder; integration by what it touches, stays in place |
| UnitTests | `Infrastructure/JsonLinesAuditStoreTests.cs` | 6 | 6 | Integration | kept | Real append-only file in a temp folder; integration by what it touches, stays in place |
| UnitTests | `Infrastructure/JsonRepositoryTests.cs` | 14 | 18 | Integration | kept | Restart, version conflict, parallel writers on real files; integration by what it touches, stays in place |
| UnitTests | `Infrastructure/MigrationScriptTests.cs` | 1 | 1 | Unit | kept | Infrastructure logic without I/O (EF model metadata or pure logic); never opens a connection |
| UnitTests | `Infrastructure/Pbkdf2PasswordHasherTests.cs` | 9 | 16 | Unit | kept | Infrastructure logic without I/O (EF model metadata or pure logic); never opens a connection |
| UnitTests | `Infrastructure/SimulatedCourierServiceTests.cs` | 8 | 11 | Unit | kept | Infrastructure logic without I/O (EF model metadata or pure logic); never opens a connection |
| UnitTests | `Infrastructure/SimulatedIdentityVerificationServiceTests.cs` | 1 | 4 | Unit | kept | Infrastructure logic without I/O (EF model metadata or pure logic); never opens a connection |
| UnitTests | `Measurements/CatalogCacheMeasurements.cs` | 2 | – (not run) | Measurement | kept | Measurement, excluded from CI by the Category filter; its numbers are quoted in DESIGN.md and PERFORMANCE.md |
| UnitTests | `Measurements/CourierConcurrencyMeasurements.cs` | 1 | – (not run) | Measurement | kept | Measurement, excluded from CI by the Category filter; its numbers are quoted in DESIGN.md and PERFORMANCE.md |
| UnitTests | `Measurements/DashboardMeasurements.cs` | 1 | – (not run) | Measurement | kept | Measurement, excluded from CI by the Category filter; its numbers are quoted in DESIGN.md and PERFORMANCE.md |
| UnitTests | `Measurements/GeneratedSqlMeasurements.cs` | 1 | – (not run) | Measurement | kept | Measurement, excluded from CI by the Category filter; its numbers are quoted in DESIGN.md and PERFORMANCE.md |
| UnitTests | `Rules/ClosedBranchRuleTests.cs` | 4 | 4 | Unit | kept | Week 3 business rule with its boundary, proved once at unit level (R10) |
| UnitTests | `Rules/FeeAndCashRoundingRuleTests.cs` | 3 | 12 | Unit | kept | Week 3 business rule with its boundary, proved once at unit level (R10) |
| UnitTests | `Rules/HomeBranchRuleTests.cs` | 5 | 7 | Unit | kept | Week 3 business rule with its boundary, proved once at unit level (R10) |
| UnitTests | `Rules/InactiveStaffRuleTests.cs` | 4 | 5 | Unit | kept | Week 3 business rule with its boundary, proved once at unit level (R10) |
| UnitTests | `Rules/MoneyOrderVerificationRuleTests.cs` | 5 | 7 | Unit | kept | Week 3 business rule with its boundary, proved once at unit level (R10) |
| UnitTests | `Rules/NotificationEventRuleTests.cs` | 5 | 8 | Unit | kept | Week 3 business rule with its boundary, proved once at unit level (R10) |
| UnitTests | `Rules/ShippingPricingRuleTests.cs` | 8 | 16 | Unit | kept | Week 3 business rule with its boundary, proved once at unit level (R10) |
| UnitTests | `Rules/StockRuleTests.cs` | 8 | 8 | Unit | kept | Week 3 business rule with its boundary, proved once at unit level (R10) |
| UnitTests | `Rules/TaxRuleTests.cs` | 6 | 11 | Unit | kept | Week 3 business rule with its boundary, proved once at unit level (R10) |
| UnitTests | `Rules/TransactionLifecycleRuleTests.cs` | 11 | 13 | Unit | kept | Week 3 business rule with its boundary, proved once at unit level (R10) |
| UnitTests | `Rules/VoidRefundRuleTests.cs` | 11 | 14 | Unit | kept | Week 3 business rule with its boundary, proved once at unit level (R10) |
| UnitTests | `Rules/WeightLimitRuleTests.cs` | 4 | 14 | Unit | kept | Week 3 business rule with its boundary, proved once at unit level (R10) |
| UnitTests | `Services/AttributeModelValidatorTests.cs` | 6 | 6 | Unit | kept | Week 3 application service with in-memory fakes; rule-level checks (R10) |
| UnitTests | `Services/AuditRedactorTests.cs` | 4 | 24 | Unit | kept | Week 3 application service with in-memory fakes; rule-level checks (R10) |
| UnitTests | `Services/AuditServiceTests.cs` | 2 | 4 | Integration | kept | Uses the real JSON audit store in a temp folder; integration by what it touches |
| UnitTests | `Services/BranchServiceTests.cs` | 7 | 9 | Unit | kept | Week 3 application service with in-memory fakes; rule-level checks (R10) |
| UnitTests | `Services/CheckoutServiceTests.cs` | 16 | 17 | Unit | kept | Week 3 application service with in-memory fakes; rule-level checks (R10) |
| UnitTests | `Services/FeeAndCashRoundingTests.cs` | 2 | 6 | Unit | kept | Week 3 application service with in-memory fakes; rule-level checks (R10) |
| UnitTests | `Services/MoneyOrderVerificationTests.cs` | 5 | 8 | Unit | kept | Week 3 application service with in-memory fakes; rule-level checks (R10) |
| UnitTests | `Services/ParallelPaymentTests.cs` | 6 | 6 | Unit | kept | Week 3 application service with in-memory fakes; rule-level checks (R10) |
| UnitTests | `Services/PriceRevisionServiceTests.cs` | 5 | 11 | Unit | kept | Week 3 application service with in-memory fakes; rule-level checks (R10) |
| UnitTests | `Services/ReportServiceTests.cs` | 20 | 20 | Unit | kept | Week 3 application service with in-memory fakes; rule-level checks (R10) |
| UnitTests | `Services/ReversalServiceTests.cs` | 7 | 8 | Unit | kept | Week 3 application service with in-memory fakes; rule-level checks (R10) |
| UnitTests | `Services/ShippingPricerTests.cs` | 5 | 7 | Unit | kept | Week 3 application service with in-memory fakes; rule-level checks (R10) |
| UnitTests | `Services/SpecificationAndExtensionTests.cs` | 7 | 7 | Unit | kept | Week 3 application service with in-memory fakes; rule-level checks (R10) |
| UnitTests | `Services/StaffAndLoginServiceTests.cs` | 11 | 13 | Unit | kept | Week 3 application service with in-memory fakes; rule-level checks (R10) |
| UnitTests | `Services/StockAdjustmentRecordTests.cs` | 2 | 2 | Unit | kept | Week 3 application service with in-memory fakes; rule-level checks (R10) |
| UnitTests | `Services/StockServiceTests.cs` | 8 | 9 | Unit | kept | Week 3 application service with in-memory fakes; rule-level checks (R10) |
| UnitTests | `Services/TaxPreviewTests.cs` | 4 | 4 | Unit | kept | Week 3 application service with in-memory fakes; rule-level checks (R10) |
| UnitTests | `Services/UnitOfWorkBoundaryTests.cs` | 15 | 15 | Unit | kept | Week 3 application service with in-memory fakes; rule-level checks (R10) |
| Tracking.UnitTests | `Domain/TrackedShipmentTests.cs` | 9 | 23 | Unit | modified (step 03) | Tracking-number theory widened (lower case, 7/9 digits, missing dash, spaces, empty, null) with the exact `DomainException` message; the two `double` weight theories replaced by `decimal` `TheoryData` (0 / 0.0999 / 0.1 / 30 / 30.001 / -1 / 4 decimals) on `Create` and `Replace`; MoveTo split into forward and backward with the exact message; final-shipment `Replace` checks Delivered and Failed with the message. Changed methods renamed with AAA (R1, R7) |
| Tracking.UnitTests | `Domain/TrackingFormatsTests.cs` | 4 | 12 | Unit | modified (step 03) | The two format theories got the missing rows (lower case, missing dash, inner/trailing space, empty; courier `CR-099`, `CR-000`, `CR-999` valid, `CR-1000` and `CR-99` invalid) and still compare with the device rule; renamed with AAA (R7) |
| Tracking.UnitTests | `Domain/TrackingScanTests.cs` | 4 | 7 | Unit | kept | Tracking domain invariant at unit level (R7) |
| Tracking.UnitTests | `Infrastructure/TrackingModelTests.cs` | 5 | 6 | Unit | kept | Reads EF model metadata with Server=unused and never connects, so it is a unit test |
| IntegrationTests | `Tracking/TrackingSqlTests.cs` (was Tracking.UnitTests `Infrastructure/`) | 4 | 4 | Integration | moved (step 02) | Opens a real SQL Server database, so it left the unit project; its fixture `Tracking/TrackingDatabase.cs` uses the shared `SqlTestServer` (skips instead of throwing), a fixed `FakeTimeProvider` and a Moq courier; renamed with AAA. Step 07: the service factory also passes `new ScanAgePolicy(Options.Create(new TrackingOptions()))` (default 72 h, from the TDD refactor); no assertion changed |
| Tracking.UnitTests | `Rules/ShipmentStatusRulesTests.cs` | 5 | 28 | Unit | replaced (step 03) | 22 hand-picked rows replaced by the full 6x6 table written out by hand (36 `MemberData` rows, 14 allowed), a guard that the table lists each of the 36 pairs once, undefined statuses, `IsFinal` for all 6 statuses, `StatusFor` for all 4 scan types and the exact `DomainException` message for an undefined scan type; all names Method_Scenario_ExpectedResult with AAA (R1) |
| Tracking.UnitTests | `Services/ScanOwnershipTests.cs` | 6 | 8 | Unit | modified (step 07) | Was "kept": only the service factory changed; it passes the `ScanAgePolicy` that the TDD refactor added to the constructor (`TestData.ScanAge()`, default 72 h). No assertion changed. Tracking service rules with the stateful InMemoryTracking fake (R1-R4) |
| Tracking.UnitTests | `Services/ShipmentScanUploadTests.cs` | 5 | 6 | Unit | modified (step 07) | Was "kept": only the constructor call changed; it passes the `ScanAgePolicy` from the TDD refactor (`TestData.ScanAge()`, default 72 h). No assertion changed. Tracking service rules with the stateful InMemoryTracking fake (R1-R4) |
| Tracking.UnitTests | `Services/ShipmentTrackingServiceTests.cs` | 29 | 40 | Unit | modified (step 03) | Two methods changed: the `InvalidStatusTransitionException` theory now asserts the exact message and has a Failed → Delivered row; the undefined-status test asserts the `ValidationFailedException` field `status`, its message and that nothing is saved. Both renamed with AAA. Page and upload limits stay here only as wiring (3 + 2 rows); their boundaries are proved in `TrackingLimitsTests`. Moq tests come in new classes (step 04). Step 07: the constructor call also passes the `ScanAgePolicy` from the TDD refactor (`TestData.ScanAge()`); no assertion changed (R1-R4) |
| Courier.UnitTests | `Harness/CourierHarnessTests.cs` | 9 | 9 | Integration | kept | Runs the harness on real Couchbase and carries the Integration trait. It stays here because the harness is `internal` and only this project has `InternalsVisibleTo`; moving it would need a production change for no gain |
| Courier.UnitTests | `Harness/TableWriterTests.cs` | 2 | 4 | Unit | kept | Pure text formatting of the harness tables |
| Courier.Offline.Tests | `Queries/CouchbaseScanQueriesTests.cs` (was Courier.UnitTests `Offline/`) | 12 | 18 | Integration | moved (step 02) | Real Couchbase; moved to the Couchbase integration project and merged with ScanQueryTests and IndexTests: the single injection case became a 6-row theory (its own input + the 5 of ScanQueryTests), plus the courier-id injection case and the `SEARCH _ USING INDEX` Explain theory; renamed with AAA |
| Courier.Offline.Tests | `Repository/CouchbaseScanRepositoryTests.cs` (was Courier.UnitTests `Offline/`) | 14 | 15 | Integration | moved (step 02) | Real Couchbase; moved and merged with KeyValueCrudTests (its unique reopen case added); renamed with AAA |
| Courier.Offline.Tests | `Lifecycle/CourierDatabaseTests.cs` (was Courier.UnitTests `Offline/`) | 6 | 6 | Integration | moved (step 02) | Real Couchbase; moved unchanged except namespace and trait. It already covers both IndexTests duplicates (indexes created, reopen creates none) |
| Courier.UnitTests | `Offline/FakeShipmentUploaderTests.cs` | 5 | 6 | Unit | modified (step 02) | Part A fake uploader rules without a database; its scan helper used `TimeProvider.System`, now a fixed `FakeTimeProvider` (helper only, test methods unchanged) |
| Courier.Offline.Tests | `Outbox/OutboxServiceTests.cs` (was Courier.UnitTests `Offline/`) | 8 | 8 | Integration | moved (step 02) | Real Couchbase; moved unchanged next to BatchOutboxTests; covers both OutboxTests cases (R6) |
| Courier.Offline.Tests | `Listener/ScanChangeMonitorTests.cs` (was Courier.UnitTests `Offline/`) | 6 | 6 | Integration | moved (step 02) | Real Couchbase listener; moved unchanged next to ListenerDisposeTests |
| Courier.UnitTests | `Offline/ScanDocumentMapperTests.cs` | 8 | 11 | Unit | kept | Exact JSON field names and round trip of the scan document (shared contract) |
| Courier.UnitTests | `Scans/ShipmentScanTests.cs` | 9 | 21 | Unit | modified (step 02, 03) | Merged with the duplicate Offline.Tests class: took over its unique cases (scan:: + Guid id, two ids differ, starts Pending, injection-style tracking number); the clock is a fixed `FakeTimeProvider`; renamed with AAA. Step 03: exact `ArgumentException` message and parameter for a wrong tracking number and courier id, courier rows `CR-000`/`CR-099`/`CR-999` valid and `CR-99`/`CR-1000` invalid, blank (`ArgumentException`) and null (`ArgumentNullException`) tracking number, undefined scan type, and `Restore` with a wrong id |
| Courier.ApiClient.Tests | `Auth/AccessTokenClaimsTests.cs` | 3 | 11 | Unit | kept | Auth header handler and token claims with a fake handler (R4) |
| Courier.ApiClient.Tests | `Auth/AuthHeaderHandlerTests.cs` | 4 | 6 | Unit | kept | Auth header handler and token claims with a fake handler (R4) |
| Courier.ApiClient.Tests | `Auth/HttpClientFactoryWiringTests.cs` | 1 | 1 | Unit | modified (step 05) | The fake primary handler now sits behind `NetworkGuardHandler` and the expected URL is `https://apexpost.test/...` (was `localhost:7310`); renamed `AddHttpClient_TypedClientFromFactory_HasBaseAddressAndAuthHandler` with AAA (R4) |
| Courier.ApiClient.Tests | `Client/ApexApiClientTests.cs` | 10 | 13 | Unit | modified (step 05) | Kept for routes, query strings, field mapping, input checks and exceptions without the pipeline; `ClientFactory` now puts `NetworkGuardHandler` in front of the fake, so the one URL assertion changed to `https://apexpost.test/...` and that method was renamed `GetShipmentAsync_V2Json_ReadsStringEnumsNestedRecipientAndCallsTheFakeHost` with AAA. The HTTP case matrix through the full pipeline is in the new `ApexApiClientHttpHandlerTests` |
| Courier.ApiClient.Tests | `Client/DevelopmentTokenTests.cs` | 3 | 3 | Unit | kept | Typed client results and ProblemDetails parsing with a fake handler |
| Courier.ApiClient.Tests | `Client/ProblemDetailsTests.cs` | 6 | 13 | Unit | kept | Detailed ProblemDetails parsing (fixed texts per status, unreadable bodies, null field messages) without the pipeline. Overlap with `ApexApiClientHttpHandlerTests` (404, 409, 400) is deliberate: this class checks parsing edge cases, the new class checks the case through retry/breaker (no retry on 4xx). Runs behind `NetworkGuardHandler` since step 05 (helper change only) |
| Courier.ApiClient.Tests | `Client/ResponseRobustnessTests.cs` | 4 | 13 | Unit | kept | Typed client results and ProblemDetails parsing with a fake handler |
| Courier.ApiClient.Tests | `Http/ApiBaseAddressTests.cs` | 3 | 7 | Unit | kept | Base address rules and DEBUG-only development hosts |
| Courier.ApiClient.Tests | `Http/DevelopmentHostsTests.cs` | 3 | 9 | Unit | kept | Base address rules and DEBUG-only development hosts |
| Courier.ApiClient.Tests | `Json/ApexJsonTests.cs` | 5 | 14 | Unit | kept | Shared JSON options (string enums, camelCase) |
| Courier.ApiClient.Tests | `Resilience/CircuitBreakerTests.cs` | 5 | 5 | Unit | kept | Polly retry / circuit breaker / timeout with scripted handlers (R5) |
| Courier.ApiClient.Tests | `Resilience/ResilienceLoggingTests.cs` | 3 | 3 | Unit | kept | Polly retry / circuit breaker / timeout with scripted handlers (R5) |
| Courier.ApiClient.Tests | `Resilience/RetryRuleTests.cs` | 6 | 19 | Unit | kept | Polly retry / circuit breaker / timeout with scripted handlers (R5) |
| Courier.ApiClient.Tests | `Resilience/RetryTests.cs` | 8 | 16 | Unit | kept | Retry rules per status, method and marking (R5). Runs behind `NetworkGuardHandler` since step 05 (helper change only). Its caller-cancellation test still uses a 100 ms wall-clock `CancelAfter`; it would only flake if the first attempt needed more than 100 ms to reach the handler (never seen), and the deterministic version is in the two new HTTP case classes |
| Courier.ApiClient.Tests | `Resilience/TimeoutTests.cs` | 2 | 2 | Unit | modified (step 05) | Both tests ran on real 200 ms / 300 ms timers and the total-timeout test expected exactly 2 attempts; it failed once in the first step-05 run on a cold start. Now Polly runs on a `FakeTimeProvider` and the test advances the clock after each attempt reaches the handler (`ScriptedHttpHandler.Entered`), retry delay zero. Same two behaviours, renamed with AAA (R5, R11) |
| Courier.ApiClient.Tests | `Sync/HttpShipmentUploaderTests.cs` | 8 | 24 | Unit | kept | Batch mapping, every field of the request, the failure-kind → outcome table, circuit open, input checks (R3, R6). Runs behind `NetworkGuardHandler` since step 05 (helper change only). The per-HTTP-case tests (timeout, caller cancel, ProblemDetails, Moq `Verify`) are in the new `HttpShipmentUploaderHttpHandlerTests` |
| Courier.Presentation.Tests | `Mvvm/AsyncCommandTests.cs` | 3 | 3 | Unit | kept | Plain C# MVVM helper; kept, not extended (UI out of scope) |
| Courier.Presentation.Tests | `Mvvm/ObservableObjectTests.cs` | 1 | 1 | Unit | kept | Plain C# MVVM helper; kept, not extended (UI out of scope) |
| Courier.Presentation.Tests | `ViewModels/ConnectionBannerViewModelTests.cs` | 5 | 7 | Unit | kept | Plain view-model logic with fakes, no MAUI types; kept, not extended (UI out of scope) |
| Courier.Presentation.Tests | `ViewModels/FriendlyErrorTests.cs` | 2 | 4 | Unit | kept | Plain view-model logic with fakes, no MAUI types; kept, not extended (UI out of scope) |
| Courier.Presentation.Tests | `ViewModels/LoginViewModelTests.cs` | 6 | 9 | Unit | kept | Plain view-model logic with fakes, no MAUI types; kept, not extended (UI out of scope) |
| Courier.Presentation.Tests | `ViewModels/NewScanViewModelTests.cs` | 6 | 10 | Unit | kept | Plain view-model logic with fakes, no MAUI types; kept, not extended (UI out of scope) |
| Courier.Presentation.Tests | `ViewModels/PendingScansViewModelTests.cs` | 5 | 5 | Unit | kept | Plain view-model logic with fakes, no MAUI types; kept, not extended (UI out of scope) |
| Courier.Presentation.Tests | `ViewModels/ShipmentLookupViewModelTests.cs` | 4 | 4 | Unit | kept | Plain view-model logic with fakes, no MAUI types; kept, not extended (UI out of scope) |
| Courier.Presentation.Tests | `ViewModels/SyncStatusViewModelTests.cs` | 10 | 10 | Unit | kept | Plain view-model logic with fakes, no MAUI types; kept, not extended (UI out of scope) |
| IntegrationTests | `Architecture/ControllerLayeringTests.cs` | 4 | 4 | Unit | kept | Reflection guard: controllers use no Infrastructure type |
| IntegrationTests | `Authentication/SignInTests.cs` | 7 | 9 | Integration | kept | Cookie sign-in needs the real host and pipeline |
| IntegrationTests | `Filters/AdminAuditTests.cs` | 5 | 5 | Integration | kept | MVC filter order and audit writing need the real pipeline |
| IntegrationTests | `Filters/AuditCoverageTests.cs` | 7 | 7 | Integration | kept | MVC filter order and audit writing need the real pipeline |
| IntegrationTests | `Filters/AuditPageTests.cs` | 5 | 7 | Integration | kept | MVC filter order and audit writing need the real pipeline |
| IntegrationTests | `Filters/AuditRedactionTests.cs` | 4 | 4 | Integration | kept | MVC filter order and audit writing need the real pipeline |
| IntegrationTests | `Filters/FilterPipelineTests.cs` | 4 | 11 | Integration | kept | MVC filter order and audit writing need the real pipeline |
| IntegrationTests | `Filters/InlineValidationTests.cs` | 6 | 6 | Integration | kept | MVC filter order and audit writing need the real pipeline |
| IntegrationTests | `Filters/ValidationAndDomainFilterTests.cs` | 6 | 6 | Integration | kept | MVC filter order and audit writing need the real pipeline |
| IntegrationTests | `Middleware/ExceptionHandlingTests.cs` | 3 | 3 | Integration | kept | Middleware order, headers and error pages need the real pipeline (R8) |
| IntegrationTests | `Middleware/HealthTests.cs` | 2 | 2 | Integration | kept | Middleware order, headers and error pages need the real pipeline (R8) |
| IntegrationTests | `Middleware/MaintenanceModeTests.cs` | 3 | 3 | Integration | kept | Middleware order, headers and error pages need the real pipeline (R8) |
| IntegrationTests | `Middleware/PipelineHeadersTests.cs` | 6 | 9 | Integration | kept | Middleware order, headers and error pages need the real pipeline (R8) |
| IntegrationTests | `Middleware/PipelineOrderTests.cs` | 6 | 6 | Integration | kept | Middleware order, headers and error pages need the real pipeline (R8) |
| IntegrationTests | `Middleware/ProblemExceptionHandlerTests.cs` | 3 | 9 | Unit | kept | Calls the handler directly without a host (unit level); stack trace only in Development (R8) |
| IntegrationTests | `Middleware/StatusPageTests.cs` | 6 | 8 | Integration | kept | Middleware order, headers and error pages need the real pipeline (R8) |
| IntegrationTests | `Middleware/TemporaryStorageTests.cs` | 1 | 1 | Integration | kept | Middleware order, headers and error pages need the real pipeline (R8) |
| IntegrationTests | `Middleware/TransportSecurityTests.cs` | 2 | 2 | Integration | kept | Middleware order, headers and error pages need the real pipeline (R8) |
| IntegrationTests | `Security/AdminAccessTests.cs` | 14 | 61 | Integration | kept | Auth, IP allowlist, API key and rate limit need the real pipeline |
| IntegrationTests | `Security/ApiKeyNotConfiguredTests.cs` | 1 | 2 | Integration | kept | Auth, IP allowlist, API key and rate limit need the real pipeline |
| IntegrationTests | `Security/AuthorizationTests.cs` | 10 | 18 | Integration | kept | Auth, IP allowlist, API key and rate limit need the real pipeline |
| IntegrationTests | `Security/IntegrationEndpointTests.cs` | 14 | 17 | Integration | kept | Auth, IP allowlist, API key and rate limit need the real pipeline |
| IntegrationTests | `Security/PageMarkupTests.cs` | 5 | 18 | Integration | kept | Auth, IP allowlist, API key and rate limit need the real pipeline |
| IntegrationTests | `Security/RateLimitingTests.cs` | 4 | 4 | Integration | kept | Auth, IP allowlist, API key and rate limit need the real pipeline |
| IntegrationTests | `Security/SensitiveDataTests.cs` | 2 | 2 | Integration | kept | Auth, IP allowlist, API key and rate limit need the real pipeline |
| IntegrationTests | `Sql/AtomicSaleTests.cs` | 6 | 6 | Integration | modified (step 02) | `DateTime.UtcNow` / `DateTimeOffset.UtcNow` were arbitrary values; now the host clock (3 tests renamed with AAA) |
| IntegrationTests | `Sql/AuditPersistenceTests.cs` | 4 | 4 | Integration | kept | Real SQL Server behaviour (constraints, rowversion, procedures, translation) no unit test can prove (R10) |
| IntegrationTests | `Sql/BusinessRulesOnSqlTests.cs` | 9 | 21 | Integration | kept | Week 3 rules enforced by SQL CHECK/UNIQUE constraints (R10) |
| IntegrationTests | `Sql/CommandCountTests.cs` | 5 | 14 | Integration | kept | Real SQL Server behaviour (constraints, rowversion, procedures, translation) no unit test can prove (R10) |
| IntegrationTests | `Sql/DeadlockTests.cs` | 3 | 3 | Integration | kept | Real SQL Server behaviour (constraints, rowversion, procedures, translation) no unit test can prove (R10) |
| IntegrationTests | `Sql/DisposableDatabaseTests.cs` | 2 | 2 | Integration | kept | Real SQL Server behaviour (constraints, rowversion, procedures, translation) no unit test can prove (R10) |
| IntegrationTests | `Sql/EfMappingTests.cs` | 16 | 35 | Integration | kept | Real SQL Server behaviour (constraints, rowversion, procedures, translation) no unit test can prove (R10) |
| IntegrationTests | `Sql/ErrorMappingTests.cs` | 7 | 7 | Integration | kept | Real SQL Server behaviour (constraints, rowversion, procedures, translation) no unit test can prove (R10) |
| IntegrationTests | `Sql/LeastPrivilegeTests.cs` | 6 | 21 | Integration | kept | Real SQL Server behaviour (constraints, rowversion, procedures, translation) no unit test can prove (R10) |
| IntegrationTests | `Sql/OverPostingTests.cs` | 2 | 2 | Integration | modified (step 02) | CreatedAtUtc is set by the audit interceptor from `TimeProvider`, not by a DB default, so it is compared with the host clock (exact window) and not with `SYSUTCDATETIME()` as first planned; renamed with AAA |
| IntegrationTests | `Sql/PriceRevisionTests.cs` | 5 | 6 | Integration | kept | Real SQL Server behaviour (constraints, rowversion, procedures, translation) no unit test can prove (R10) |
| IntegrationTests | `Sql/QueryTranslationTests.cs` | 11 | 11 | Integration | kept | Real SQL Server behaviour (constraints, rowversion, procedures, translation) no unit test can prove (R10) |
| IntegrationTests | `Sql/ReversalTests.cs` | 7 | 9 | Integration | kept | Real SQL Server behaviour (constraints, rowversion, procedures, translation) no unit test can prove (R10) |
| IntegrationTests | `Sql/StockConcurrencyTests.cs` | 5 | 5 | Integration | kept | rowversion lost-update protection on BranchStock (R10) |
| IntegrationTests | `Sql/StoredProcedureTests.cs` | 15 | 15 | Integration | modified (step 02) | Adjustment times come from the host clock; the server-time test compares with `SELECT SYSUTCDATETIME()` before and after the call (renamed with AAA) |
| IntegrationTests | `Sql/WriteOrderTests.cs` | 2 | 2 | Integration | kept | Real SQL Server behaviour (constraints, rowversion, procedures, translation) no unit test can prove (R10) |
| IntegrationTests | `Workflows/BranchAdministrationTests.cs` | 7 | 8 | Integration | kept | End-to-end Week 3 page flow; the only tests of the server-rendered pages |
| IntegrationTests | `Workflows/CounterWorkflowTests.cs` | 14 | 14 | Integration | kept | End-to-end Week 3 page flow; the only tests of the server-rendered pages |
| IntegrationTests | `Workflows/NotificationPageTests.cs` | 3 | 3 | Integration | kept | End-to-end Week 3 page flow; the only tests of the server-rendered pages |
| IntegrationTests | `Workflows/ReportPagesTests.cs` | 9 | 9 | Integration | modified (step 02) | "Today" is `ApexFactory.Today` (the host clock date), so it cannot flip at midnight (helper only) |
| IntegrationTests | `Workflows/SaleToReportTests.cs` | 8 | 8 | Integration | modified (step 02) | Report date from `ApexFactory.Today` (helper only) |
| IntegrationTests | `Workflows/StaffAdministrationTests.cs` | 11 | 11 | Integration | kept | End-to-end Week 3 page flow; the only tests of the server-rendered pages |
| Api.Tests | `Client/ApexPostApiOptionsTests.cs` | 3 | 5 | Unit | kept | Generated client options defaults, no host needed |
| Api.Tests | `Client/GeneratedClientTests.cs` | 7 | 7 | Integration | modified (step 02) | The demo timed its scans with `TimeProvider.System`, which the host on a fixed clock would see as future scans; the 3 demo tests now pass the host clock (renamed with AAA) |
| Api.Tests | `Contracts/AutomaticValidationTests.cs` | 7 | 7 | Integration | modified (step 02) - more planned in step 03 | Step 02: the 3 scan tests take "now" from the host clock (renamed with AAA). Step 03: keep one valid and one invalid body per request type plus the nested Delivered-signature case |
| Api.Tests | `Contracts/NotInFutureAttributeTests.cs` | 7 | 7 | Unit | modified (step 02) | Already used a fixed TimeProvider; the system-clock fallback test now reads `TimeProvider.System` with a 1 h margin and is the one documented clock exception (section 7); renamed with AAA |
| Api.Tests | `Contracts/ScanItemTests.cs` | 11 | 36 | Unit | modified (step 03) | Courier-id theory got `CR-000`, `CR-099`, `CR-999` (valid) and `CR-1000` (invalid); renamed with AAA. Other methods unchanged (R7) |
| Api.Tests | `Contracts/ScanUploadRequestTests.cs` | 1 | 4 | Unit | modified (step 03) | Was "extended in a new class": it already had 0 / 1 / 100 / 101, so it was renamed with AAA instead of being copied; the service-side limit with its exact message is proved in `TrackingLimitsTests` (R7) |
| Api.Tests | `Contracts/ShipmentMappingTests.cs` | 7 | 7 | Unit | kept | Explicit DTO <-> command mapping, no host needed |
| Api.Tests | `Contracts/ShipmentRequestTests.cs` | 12 | 34 | Unit | modified (step 03) | Tracking-number theory widened (missing dash, inner/trailing space, null); the string weight theory replaced by `decimal` `TheoryData` (0 / 0.0999 / 0.1 / 0.1001 / 30 / 30.001 / -1 / 70 / 1.0005) on create and update with the exact message. 1.0005 kg passes the `[Range]` attribute and is refused by the domain (documented in the test). Changed methods renamed with AAA (R7) |
| Api.Tests | `Documentation/ContractTrackingTests.cs` | 2 | 3 | Integration | kept | OpenAPI documents produced by the real host (Part C contract) |
| Api.Tests | `Documentation/DevelopmentDocumentationTests.cs` | 4 | 4 | Integration | kept | OpenAPI documents produced by the real host (Part C contract) |
| Api.Tests | `Documentation/EnumSchemaTests.cs` | 2 | 3 | Integration | kept | OpenAPI documents produced by the real host (Part C contract) |
| Api.Tests | `Documentation/OperationMetadataTests.cs` | 7 | 22 | Integration | kept | OpenAPI documents produced by the real host (Part C contract) |
| Api.Tests | `Documentation/ProductionDocumentationTests.cs` | 1 | 5 | Integration | kept | OpenAPI documents produced by the real host (Part C contract) |
| Api.Tests | `Documentation/SecuritySchemeTests.cs` | 3 | 4 | Integration | kept | OpenAPI documents produced by the real host (Part C contract) |
| Api.Tests | `Documentation/VersionedDocumentTests.cs` | 8 | 10 | Integration | kept | OpenAPI documents produced by the real host (Part C contract) |
| Api.Tests | `Endpoints/ServerErrorEndpointTests.cs` | 1 | 8 | Integration | kept | Status code, Location and ProblemDetails wiring once per endpoint (R2, R3) |
| Api.Tests | `Endpoints/ShipmentEndpointTests.cs` | 23 | 29 | Integration | kept | One test per endpoint and status code (200/201+Location/204/400/404/409/415); per-endpoint 400 is wiring, not a rule repeat (R1, R2) |
| Api.Tests | `Endpoints/ShipmentScanEndpointTests.cs` | 9 | 9 | Integration | kept | Upload counts, duplicates, parallel uploads on SQL Server (R3) |
| Api.Tests | `Errors/ExceptionMappingTests.cs` | 6 | 14 | Integration | kept | Exception handler output through the real host; no stack trace outside Development (R8) |
| Api.Tests | `Errors/ProductionErrorTests.cs` | 3 | 3 | Integration | kept | Production host hides stack trace and message (R8) |
| Api.Tests | `Hosting/DevelopmentHostTests.cs` | 5 | 6 | Integration | kept | Development start-up migrates and seeds only in Development |
| Api.Tests | `Security/AuthenticationTests.cs` | 6 | 12 | Integration | kept | JWT, roles, partner key and HTTPS through the real pipeline (R4) |
| Api.Tests | `Security/AuthorizationTests.cs` | 8 | 9 | Integration | modified (step 02) | The cross-courier v2 test takes its scan time from the host clock (renamed with AAA); tokens come from `TestTokens` on the fixed clock (R4) |
| Api.Tests | `Security/DevTokenTests.cs` | 5 | 7 | Integration | modified (step 02) | Expiry is checked against the host clock: one hour after the request (renamed with AAA) |
| Api.Tests | `Security/HttpsTests.cs` | 3 | 4 | Integration | kept | JWT, roles, partner key and HTTPS through the real pipeline (R4) |
| Api.Tests | `Security/PartnerApiKeyTests.cs` | 6 | 10 | Integration | kept | JWT, roles, partner key and HTTPS through the real pipeline (R4) |
| Api.Tests | `Security/PartnerKeyStoreTests.cs` | 4 | 9 | Unit | kept | Constant-time partner key lookup, no host needed |
| Api.Tests | `Security/ProductionSecurityTests.cs` | 2 | 2 | Integration | kept | JWT, roles, partner key and HTTPS through the real pipeline (R4) |
| Api.Tests | `Sync/HttpUploaderEndToEndTests.cs` | 4 | 4 | Integration | modified (step 02) | Scan helper timed from `ApiFactory.FixedNow` instead of `DateTimeOffset.UtcNow` (helper only) (R3, R6) |
| Api.Tests | `Versioning/V1ContractSnapshotTests.cs` | 4 | 7 | Integration | kept | Exact Part B JSON snapshot guards the deprecated v1 contract (R9) |
| Api.Tests | `Versioning/V2ContractTests.cs` | 7 | 7 | Integration | modified (step 02) | Upload helper timed from `ApiFactory.FixedNow` instead of `DateTime.UtcNow` (helper only) (R9) |
| Api.Tests | `Versioning/VersionHeaderTests.cs` | 5 | 12 | Integration | kept | v1 snapshot, v2 shape and version headers through the real host (R9) |
| Courier.UnitTests | `Architecture/DomainDependencyTests.cs` (was Courier.Offline.Tests) | 3 | 3 | Unit | moved (step 02) | Was "kept": the reflection guard (only Offline references Couchbase) needs no database, so it moved to the unit project and Courier.Offline.Tests now holds only Couchbase integration tests |
| Courier.Offline.Tests | `Domain/ShipmentScanTests.cs` | 6 | 15 | Unit | removed (step 02) | Duplicate of Courier.UnitTests `Scans/ShipmentScanTests`; its 3 unique cases and its injection-style input moved there |
| Courier.Offline.Tests | `Indexes/IndexTests.cs` | 3 | 4 | Integration | removed (step 02) | Was "modified": both facts duplicate `CourierDatabaseTests` (indexes exist, reopen creates none); the Explain theory is a query concern and moved to `CouchbaseScanQueriesTests` |
| Courier.Offline.Tests | `Listener/ListenerDisposeTests.cs` | 1 | 1 | Integration | kept | Unique case: no listener callback after dispose (real Couchbase) |
| Courier.Offline.Tests | `Outbox/BatchOutboxTests.cs` | 10 | 10 | Integration | kept | Batch path of the outbox on real Couchbase: batches of 20, rejected -> Failed, network error keeps Pending (R6) |
| Courier.Offline.Tests | `Outbox/OutboxTests.cs` | 2 | 2 | Integration | removed (step 02) | Both cases are already proved by OutboxServiceTests (rejected -> Failed, synced not re-sent, failed only with includeFailed) and FakeShipmentUploaderTests (demo failure) |
| Courier.Offline.Tests | `Queries/ScanQueryTests.cs` | 5 | 9 | Integration | removed (step 02) | Was "modified": its order, pending and count cases duplicate CouchbaseScanQueriesTests; the courier-id injection case and the 5 tracking-number injection inputs moved there, so nothing unique is left |
| Courier.Offline.Tests | `Repository/KeyValueCrudTests.cs` | 4 | 4 | Integration | removed (step 02) | Was "modified": CRUD, duplicate id and unknown id duplicate CouchbaseScanRepositoryTests (the full-field round trip is also in ScanDocumentMapperTests); the reopen case moved there |

### 5.3 Classes added in Week 5

New classes are not part of the baseline review above; they follow every rule of section 7 from the start.

| Step | Project | Class | Level | Why a new class and not an existing one |
| --- | --- | --- | --- | --- |
| 03 | Tracking.UnitTests | `Rules/TrackingLimitsTests.cs` | Unit | `TrackingLimits.CheckPage` / `CheckUpload` had no test of their own; the service tests only showed that *some* bad page or upload fails. This class proves page 0/1, page size 0/1/50/51 and upload 0/1/100/101 with the exact `ValidationFailedException` field and message, once, at the lowest level |
| 04 | Tracking.UnitTests | `Services/ShipmentTrackingServiceMoqTests.cs` | Unit | The existing service classes use the stateful `InMemoryTracking` fake and prove outcomes; this class proves the *interactions* with Moq (`Verify` with `Times.Once` / `Never` / `Exactly`, `It.Is` on scan ids, token pass-through, `DiscardChanges` after `ThrowsAsync(DuplicateKeyException)`, `VerifyNoOtherCalls` on a forbidden upload). 15 methods, 19 cases (R1–R4) |
| 04 | Courier.UnitTests | `Offline/OutboxServiceMoqTests.cs` | Unit | `OutboxServiceTests` needs real Couchbase (integration); this class tests the single-scan outbox path with mocked `IScanQueries`, `IScanSyncStatusUpdater` and `IShipmentUploader`: Synced / Failed per scan, one throwing upload does not stop the rest, nothing pending → uploader never called, oldest first with `includeFailed`, token rules. 7 methods, 7 cases (R6) |
| 05 | Courier.ApiClient.Tests | `Client/ApexApiClientHttpHandlerTests.cs` | Unit | The client's HTTP cases were spread over four classes by mechanism, without the full pipeline for 404/409/400 and without any Moq handler. One test per case through auth + resilience: 200 v2 JSON, 404, 500 retried then failure, every attempt timing out, caller cancel, 409 ProblemDetails, 400 ValidationProblemDetails, and Moq `Protected()` `SendAsync` with `Verify` (section 4.4). 8 methods, 8 cases (R5) |
| 05 | Courier.ApiClient.Tests | `Sync/HttpShipmentUploaderHttpHandlerTests.cs` | Unit | The uploader had no test for attempt timeout, caller cancellation, ProblemDetails text or the 500 attempt count. Same 8 cases as above, asserting what the outbox gets (`Completed` / `Refused` / `Retryable` + message, or the exception). 8 methods, 8 cases (R3, R5, R6) |
| 05 | Courier.ApiClient.Tests | `Http/NetworkGuardHandlerTests.cs` | Unit | Proves the guard every client test now runs behind: fake host passes; `localhost`, a `.example` host and `10.0.2.2` fail the test before the fake is called; a real `SocketsHttpHandler` below the guard fails the test without sending. 3 methods, 5 cases |
| 06 | IntegrationTests | `Tracking/TrackingRepositorySqlTests.cs` | Integration | `TrackingSqlTests` proves the service round trip; this class proves the repository / queries / unit of work against rules only SQL Server enforces, with two contexts as two requests: duplicate tracking number past the check → `DuplicateKeyException` (`SqlException` 2601), same scan id twice → one row (service retry and repository race), status + city + page 2 filtered and paged in SQL (command text captured by `SqlCommandCapture`), stale `Version` → `ConcurrencyConflictException` with the `UPDATE … WHERE [Version] = @…` captured. 5 methods, 5 cases (R2, R3) |
| 06 | IntegrationTests | `Sql/CounterTransactionRowVersionTests.cs` | Integration | The tracking tables use an `int` `Version` token, so the real SQL Server `rowversion` is proved on the Week 3 table that has one and had no conflict test (`StockConcurrencyTests` covers `BranchStock`): two contexts, the second save of the same basket → `ConcurrencyConflictException`, row version moved. 1 method (R10) |
| 06 | Api.Tests | `EndToEnd/ShipmentLifecycleEndToEndTests.cs` | Integration | The one clearly named end-to-end flow on `WebApplicationFactory<Program>` + throw-away SQL Server + `TestAuthHandler` (`TestAuthApiFactory`, registered in `ConfigureTestServices`): POST v2 → 201 + Location → GET → 200 → POST scans (2 accepted) → same batch again (2 duplicates) → GET scans → shipment InTransit; plus 401 without test credentials and 403 for Courier on an Admin action. 3 methods (R2, R3, R4; section 6.3) |
| 07 | Tracking.UnitTests | `Services/ScanAgeRuleTests.cs` | Unit | The TDD class of the scan age rule (section 8, `TDD-LOG.md`): written before the production code, on a `FakeTimeProvider` upload time of 2026-11-10 12:00 UTC: 72 h 1 min → rejected with "Scan is older than 72 hours.", exactly 72 h → accepted, 72 h + 1 s → rejected; plus a theory for a configured limit (24 h / 96 h: at the limit accepted, one second older rejected with the matching reason). 4 methods, 7 cases (R12) |
| 07 | Api.Tests | `Hosting/TrackingOptionsRegistrationTests.cs` | Unit | Proves the API binds the "Tracking" section (default 72, configured 48) and refuses `MaxScanAgeHours` 0 and −1 with `OptionsValidationException` and the exact message. A plain `ServiceCollection`, no host. 3 methods, 4 cases (R12) |
| 07 | Api.Tests | `Endpoints/ScanAgeEndpointTests.cs` | Integration | The rule as a caller sees it, through the real host, its fixed clock and a throw-away SQL Server database: a 73 h old scan → 200 with `rejected: [{ scanId, "Scan is older than 72 hours." }]` and only the fresh scan stored; with `Tracking:MaxScanAgeHours = 24` set on the host, a 25 h old scan → "Scan is older than 24 hours.". 2 methods (R12) |

| 08 | Specs | `Features/ShipmentCreation.feature` (+ `Steps/ShipmentCreationSteps.cs`) | Spec | BDD at API level (section 9): valid registration, duplicate refused, Scenario Outline for invalid weights. 3 scenarios, 6 cases (R2, weight rule) |
| 08 | Specs | `Features/ScanSync.feature` (+ `Steps/ScanSyncSteps.cs`) | Spec | BDD at service level (section 9): offline scans synced, lost answer → stored once, unknown tracking number rejected, Scenario Outline for scan age. 4 scenarios, 6 cases (R3, R6, R12) |

Step 07 also changed the constructor call (only) of the Week 5 classes `ShipmentTrackingServiceMoqTests` and
`TrackingRepositorySqlTests`: both pass the `ScanAgePolicy` with its default 72 hours; no assertion changed.

## 6. Data access strategy

| Technique | Where | What it proves | What it does NOT prove |
| --- | --- | --- | --- |
| Moq on repository / query / unit-of-work ports | Service unit tests (`ShipmentTrackingService`, outbox) | The service calls the right port members in the right order, commits once, does not commit on failure, maps port exceptions to the right outcome | That the SQL is valid, that EF Core can translate the query, that the unique index exists, that rowversion detects a conflict, that the data really round-trips |
| Hand-written in-memory fakes (`InMemoryTracking`, `InMemoryRepository`) | Existing service tests, BDD `ScanSync.feature` | Business flow over several calls with believable state | Same gaps as Moq; plus the fake can drift from the real repository – each fake is kept minimal and checked against the port contract |
| Throw-away **SQL Server** database per fixture | `ApexPost.IntegrationTests/Sql`, `ApexPost.IntegrationTests/Tracking`, Api tests (`ApiFactory`); all through `SqlTestServer` | Real migrations, constraints (UNIQUE, CHECK, FK Restrict), filtered indexes, rowversion, stored procedures, `datetime2` precision, SQL translation, transaction behaviour | Production data volume and real network latency; SQL Server version differences beyond the one used (CI: `mssql/server:2022-latest`) |
| Embedded **Couchbase Lite** in a unique temp folder | `ApexPost.Courier.Offline.Tests` (since step 02 every Couchbase class except the harness tests) and `CourierHarnessTests` | Real document storage, SQL++ queries with parameters, value indexes (`Explain`), listeners, reopen behaviour | Sync over a real network; behaviour on Android/Windows device storage |
| `WebApplicationFactory<Program>` in-process | Api and Web integration tests | Routing, model binding, filters, middleware order, authentication/authorization, status codes, headers, ProblemDetails | Kestrel/TLS configuration and reverse proxies in a real deployment |

**Why not SQLite here.** The tracking and Week 3 schemas depend on SQL Server features that SQLite does not have or
emulates differently: `rowversion` concurrency tokens (`BranchStock`, `CounterTransactions`), filtered and INCLUDE
indexes, stored procedures (`StoredProcedureTests`), `datetime2` precision and `SYSUTCDATETIME()` defaults, and
`SqlException` numbers 2601/2627 that Infrastructure translates to `DuplicateKeyException`. A green SQLite run would
not prove any of these, and would need a second set of migrations.

**Local "no manual setup" chain (one shared helper, built in step 02).** Every SQL test fixture (`ApexFactory`,
`ApiFactory` and their subclasses, `Tracking/TrackingDatabase`) asks `tests/Common/SqlTestServer.cs` for a server. The
file is linked into `ApexPost.IntegrationTests` and `ApexPost.Api.Tests`, so there is one copy of the logic:

1. environment variable `ConnectionStrings__ApexPostTestServer` (CI container);
2. user-secrets of the web and API projects (`ConnectionStrings:ApexPostTestServer`, then `ConnectionStrings:ApexPostDb`,
   as written by `setup-secrets.bat`);
3. on Windows, LocalDB `(localdb)\MSSQLLocalDB` if it answers;
4. otherwise `Assert.Skip("SQL Server not available: …")` with the list of what was tried and why LocalDB failed.

A server given by 1 or 2 is used as it is: if it is down, the tests fail instead of hiding the problem. When the
variable `CI` is `true`, step 4 fails the test instead of skipping it, so CI can never report a skipped SQL test.

Each fixture creates its own database (`ApexPostTests_<area>_<guid>`, area `Web`, `Api` or `Tracking`) and drops it
when the fixture ends; the development database is never touched. xUnit v3 reports a skip thrown from a class fixture
constructor as a *failure* (checked with a small experiment before writing the helper), so fixtures never skip while
they are built: `ApexFactory` and `ApiFactory` skip in `CreateHost` (when a test first uses the host), and
`TrackingDatabase` skips in `NewContext()`. Checked in step 02 by hiding the user-secrets (`APPDATA` pointed to an empty
folder): the SQL tests were reported as skipped with the full message while the non-SQL tests in the same project
passed, and with `CI=true` the same tests failed with "CI must not skip SQL tests". On the step 02 machine the LocalDB
instance did not start in that run, so the LocalDB success path itself is not yet proved by a run.

### 6.1 Production code changed for testability

| Step | Change | Why | Behaviour change |
| --- | --- | --- | --- |
| 04 | New port `IScanSyncStatusUpdater` (`src/Courier/ApexPost.Courier.Domain/Ports/IScanSyncStatusUpdater.cs`, no packages); `CouchbaseScanRepository` implements it; the `OutboxService` constructor takes `IScanSyncStatusUpdater` instead of the concrete `CouchbaseScanRepository` | The outbox could only be built with a real Couchbase database, so it had no unit test and could not be mocked. A separate one-method port was chosen instead of adding the member to `IScanRepository`, which would have broken the other implementation (`InMemoryScanStore` in Presentation.Tests) | None: the method body is unchanged, the harness, `MauiProgram` and the existing tests still pass the same repository object |
| 07 | TDD rule (not testability): `ShipmentTrackingService.TryRecordAsync` rejects a scan older than the limit; new `TrackingOptions` (`MaxScanAgeHours`, default 72) and `ScanAgePolicy` (takes `IOptions<TrackingOptions>`) in `src/ApexPost.Application/Tracking/`; the service constructor takes `ScanAgePolicy`; `AddShipmentTracking` registers it as a singleton; `ApiRegistration` binds the "Tracking" section and validates it at start-up | W5.5 TDD rule. The limit lives in options so a deployment can change it and a test can set it; the policy is a small class with one job, injected by constructor so the service stays testable with a fixed `TimeProvider` | New behaviour by design: a scan more than 72 h older than the upload time is rejected with "Scan is older than 72 hours."; exactly 72 h is still accepted. Every other upload behaves as before (full suite green, no assertion changed). Because of the rule, the accepted scans in `src/ApexPost.Api/ApexPost.Api.http` now use relative times (`{{$datetime iso8601 -N m}}`) instead of fixed November dates, and two age-rule examples were added. Details in `TDD-LOG.md` |

### 6.2 Why mocking EF Core gives false confidence

We never mock `DbContext` or `DbSet`. A mocked `DbSet` runs LINQ to Objects in memory. A query that EF Core cannot
translate to SQL, or translates differently, still passes. Differences include string comparison, case sensitivity,
`null` handling and client evaluation. A mock has no constraints, so it never raises a duplicate key or a CHECK
violation. Its `SaveChangesAsync` returns whatever the setup says, so it never reports a concurrency conflict.
Finally, the setup has to repeat the implementation (`Setup(x => x.Shipments.Where(…))`). The test then copies the
code and passes even when both are wrong.

Three concrete examples from this code. Each one is proved by `TrackingRepositorySqlTests` (step 06) on a real
SQL Server database:

1. **City filter, case.** `SqlTrackingQueries.ListAsync` filters with `shipment.Recipient.City == city`, and the API
   documents the city filter as "ignoring case". On SQL Server the default collation is case-insensitive, so
   `HARBOUR POINT` finds `Harbour Point`. Over a mocked `DbSet` the same `==` is an ordinal C# comparison. That test
   would either fail, which pushes someone to add `ToLower()` (different SQL, and no index seek), or use the same
   case and never check the documented behaviour. `ListAsync_StatusCityAndSecondPage_FiltersAndPagesInSqlNotInMemory`
   searches with the city in upper case. It also checks the captured command text: `[RecipientCity] = @…`,
   `[Status] = @…` and `OFFSET … ROWS FETCH NEXT @…`. So the filter and the page run in SQL, not in memory after
   loading every row.
2. **Duplicate scan in two parallel requests.** `ShipmentTrackingService` asks `FindStoredScanIdsAsync` first. When
   two requests run at the same time, both get "not stored". Only the unique index `IX_TrackingScans_ScanId` stops
   the second row, and `TrackingUnitOfWork` turns `SqlException` 2601 into `DuplicateKeyException`. A mock would store
   both rows and no exception path would ever run.
   `SaveChangesAsync_SameScanIdStagedByTwoRequests_SecondThrowsDuplicateKeyAndOneRowStays` shows the exception and
   the single row.
3. **Lost update.** `TrackingUnitOfWork` raises `Version` and relies on EF Core to send `UPDATE … WHERE [Version] = @…`.
   It also relies on SQL Server reporting 0 affected rows when someone else changed the shipment first. A mocked
   `SaveChangesAsync` always succeeds. `SaveChangesAsync_ShipmentChangedByTwoRequests_…` captures that `UPDATE` and
   gets `ConcurrencyConflictException`, and the first change stays. The real `rowversion` column of
   `CounterTransactions` is proved the same way by `CounterTransactionRowVersionTests`.

The same Moq tests stay useful for what they do prove: a service commits once and never commits after a failure
(section 4.3). They are not used to prove anything about SQL.

### 6.3 Test authentication scheme versus real JWT tests

`ShipmentLifecycleEndToEndTests` runs on `TestAuthApiFactory`. That is `ApiFactory` with `TestAuthHandler` registered
as the default scheme in `ConfigureTestServices`. A request with the header `X-Test-Roles` becomes a principal with
those roles. A Courier principal gets `courierId` CR-101, with the same claim names a real token uses. A request
without the header is anonymous. Everything after authentication is real: the `[Authorize(Roles = …)]` rules,
`ClaimsCurrentCourier`, the services, EF Core and SQL Server. The flow test is therefore about the business flow,
and it does not depend on token building, signing keys or token lifetime.

The existing real-JWT tests (`Security/AuthenticationTests`, `AuthorizationTests`, `DevTokenTests`, and every endpoint
test that sends a `TestTokens` token through `ApiFactory`) are kept on purpose, because the test scheme replaces
exactly the part they prove:

- signature, issuer, audience, expiry and clock-skew checks, with expired, unsigned or wrong-key tokens → 401 with
  ProblemDetails (`BearerProblemEvents`);
- `MapInboundClaims = false`, so the short claim names `role` and `courierId` really arrive as written. A test scheme
  that builds claims by hand cannot show this;
- `/dev/token` issuing usable tokens in Development only.

If only the test scheme were used, a broken JWT configuration (wrong role claim type, mapping switched back on) would
still pass every test. If only real tokens were used, every flow test would also depend on key and clock setup. Each
approach covers what the other one cannot.

## 7. Conventions

- **AAA:** every new or modified test has `// Arrange`, `// Act`, `// Assert` comments.
- **Naming:** new tests use `Method_Scenario_ExpectedResult` (e.g. `CanMove_FromDeliveredToAnyStatus_ReturnsFalse`).
  Untouched older tests keep their sentence-style names (`Forward_moves_are_allowed`): renaming about 1,100 methods by
  hand adds review noise and merge risk and proves nothing new. How "touched" is applied (decided in step 02):
  a class that is rewritten or merged is renamed as a whole (`TrackingSqlTests`, `ShipmentScanTests`,
  `CouchbaseScanRepositoryTests`, `CouchbaseScanQueriesTests`); in other classes, each test method whose body changes
  gets the new name and AAA comments. A class that only gets a trait, a namespace, a new folder or a changed private
  helper keeps its names, because none of its tests changed. So some classes mix both styles for now.
- **Traits:** `[Trait("Category", "Unit" | "Integration" | "Spec" | "Measurement")]` on every class. CI excludes
  `Measurement`.
- **Parameterized tests** for rules with several cases: tracking number and courier id format, weight boundaries,
  the 6×6 status matrix, page / pageSize / upload-size limits, scan age (71 h / 72 h / 73 h).
- **Exceptions and boundaries:** assert the exact exception type and the message or parameter name; test at, just
  below and just above each limit.
- **Determinism:** no `DateTime.Now/UtcNow`, `DateTimeOffset.UtcNow` or `TimeProvider.System` in tests or code under
  test (`FakeTimeProvider` from `Microsoft.Extensions.TimeProvider.Testing`, or fixed fictional times). Both test hosts
  expose their clock (`factory.Clock`, `ApexFactory.Today`, `ApiFactory.FixedNow`). The one exception is
  `NotInFutureAttributeTests.IsValid_WithoutTimeProviderService_FallsBackToTheSystemClock`, whose subject is the
  system-clock fallback (one-hour margin). Elapsed-time checks with `Stopwatch` (simulated delays) are not wall-clock
  reads. No order dependency; no static mutable state; unique temp folders and databases per fixture.
- **Shared test packages:** `tests/Directory.Build.props` adds Moq, `Microsoft.Extensions.TimeProvider.Testing` and
  `coverlet.collector` (exact versions) to every test project and imports the root `Directory.Build.props` first, so
  `TreatWarningsAsErrors` and NuGet audit stay on. Production projects do not get these packages.
- **No real network, fictional data, no secrets:** fake or Moq `HttpMessageHandler` behind `NetworkGuardHandler` (base address `https://apexpost.test`, section 4.4); in-process hosts; `CR-101`,
  `APX-10000001`, `@apexpost.test`; keys and connection strings only from test configuration or user-secrets.
- **Commands:**

  ```bash
  dotnet build Apex_Post_Logistic.slnx -c Release
  dotnet test Apex_Post_Logistic.slnx -c Release --filter "Category!=Measurement"
  dotnet test Apex_Post_Logistic.slnx -c Release --filter "Category=Unit"
  dotnet test Apex_Post_Logistic.slnx -c Release --filter "Category=Integration"
  dotnet test Apex_Post_Logistic.slnx -c Release --filter "Category=Spec"
  ```

  In Visual Studio, Test Explorer can group by the `Category` trait.

## 8. TDD plan

Rule (W5.5): *the server rejects an uploaded scan whose `scannedAtUtc` is more than 72 hours before the upload time,
with the reason "Scan is older than 72 hours."; exactly 72 hours is accepted; the limit is configurable
(`TrackingOptions.MaxScanAgeHours`, default 72).*

- Checked before planning: no scan-age rule exists. A search of `src/` for `MaxScanAge`, `TrackingOptions`,
  "older than" and `72` finds nothing related; `ShipmentTrackingService.TryRecordAsync` checks format, shipment,
  ownership and status, but not age. Only the opposite bound exists (`NotInFutureAttribute`, two minutes ahead).
- **Why not the brief's examples:** forward-only transitions (`ShipmentStatusRules.CanMove`), the 100-scan upload limit
  (`TrackingLimits.MaximumScansPerUpload`) and the 30 kg weight limit (`TrackedShipment.MaximumWeightKg`) already
  exist with tests, so writing a "failing" test for them first would be fake TDD.
- Level: unit test on `ShipmentTrackingService` with `FakeTimeProvider` (the upload time) and in-memory or Moq ports.
- Commits, each pushed: `RED:` (compiles, fails on an assertion) → `GREEN:` (minimal code) → boundary `RED:`/`GREEN:`
  (or `TEST:` if the 72-hour boundary already passes, stated honestly) → `REFACTOR:` (`TrackingOptions`,
  `ScanAgePolicy`, `IOptions<TrackingOptions>` by constructor injection, no behaviour change).
- `TDD-LOG.md` records the requirement, the first failing test with its real output, the minimal implementation, the
  refactoring, every production change for testability, and the commit hashes. The TDD PR is merged with
  "Create a merge commit" so the RED/GREEN/REFACTOR history reaches `main`.
- **Done in step 07.** RED → GREEN → `TEST:` (both boundary tests passed at once, stated in the log) → two
  `REFACTOR:` commits; tests in `ScanAgeRuleTests`, `TrackingOptionsRegistrationTests` and `ScanAgeEndpointTests`.
  The full story with the real failing output and the commit hashes is in `TDD-LOG.md`.

## 9. BDD

**Done in step 08.** Project `tests/ApexPost.Specs` (in `Apex_Post_Logistic.slnx`, so plain `dotnet test` runs it)
uses `Reqnroll.xunit.v3` 3.3.4, the latest stable version, with the same `xunit.v3.mtp-off` 4.0.2 runner as every
other test project; no fallback was needed. Reqnroll turns the `@Spec` tag on each feature into the trait
`Category=Spec`, so `--filter "Category=Spec"` selects exactly the 12 scenario cases and the CI filter
`Category!=Measurement` includes them.

| Feature | Level | Real code under test | Replaced | Scenarios |
| --- | --- | --- | --- | --- |
| `Features/ShipmentCreation.feature` | **API** | The whole API host (`SpecApiFactory : WebApplicationFactory<Program>`): routing, request DTO validation, the Admin role rule, `ShipmentTrackingService`, EF Core, a throw-away SQL Server database per scenario | Authentication only (`SpecAuthHandler`, registered in `ConfigureTestServices`); the clock (`FakeTimeProvider`) | valid shipment registered as Created and found again; duplicate tracking number refused (409 "Duplicate"); Scenario Outline "Shipments with an invalid weight are refused" (0, 0.05, 30.001, −1 → "Weight must be between 0.1 and 30 kg.") |
| `Features/ScanSync.feature` | **Service** | Device: real `OutboxService` and `CouchbaseScanRepository` on Couchbase Lite in a temp folder per scenario. Server: real `ShipmentTrackingService` and `ScanAgePolicy` (default 72 h) | The network: `InProcessShipmentUploader` implements the device's `IBatchShipmentUploader` port by calling the service directly (one new service per call, like a request) and can be offline or lose an answer; the server's repositories: the stateful `InMemoryTracking` fake of the service tests (linked source file); the clock: one `FakeTimeProvider` shared by device and server | offline scans wait as Pending and are Synced when the connection returns, shipment In Transit; an answer lost on the way back → the re-sent scan counts as 1 duplicate and is stored once; a scan for an unknown tracking number → Failed with "Unknown tracking number."; Scenario Outline 71 h / 72 h accepted, 73 h → Failed with "Scan is older than 72 hours." |

**Why these levels.** The business outcome of registering a shipment *is* the HTTP answer (registered, refused as a
duplicate, refused with a message), and the weight rule for callers lives in the API request DTOs, so only the API
level can show it; a service-level scenario would skip the DTO validation that a dispatcher actually meets. Scan sync
is about what happens to the courier's scans across two components (device outbox and server rules). HTTP is
already proved by the step 05 handler tests and the step 06 end-to-end test, so the scenarios join the two real
components in process: they stay fast (about 20 ms each after the first), need no SQL Server, and can play the
network (offline, lost answer) and time (71/72/73 hours later) exactly, which a real host cannot do without waiting
or flakiness.

**Business language.** The feature files contain no URLs, JSON, status codes or class names: "the shipment is
refused as a duplicate", "the answer to the next sync is lost on the way back", "73 hours pass before the connection
returns". The status codes, ProblemDetails and sync statuses are checked in the step definitions.

**Bindings.**

- `Background` in both features (dispatcher signed in with the Admin role and an empty register; courier CR-101 with
  shipment APX-30000001, offline). Two `Scenario Outline`s with `Examples`.
- **Context injection, no static state:** `Contexts/ShipmentContext` and `Contexts/ScanSyncContext` are created by
  Reqnroll once per scenario and injected into `Steps/ShipmentCreationSteps`, `Steps/ScanSyncSteps` and
  `Hooks/ScenarioHooks` through their constructors. The only static members are constants and read-only values.
- **Hooks:** `ScenarioHooks` has `[BeforeScenario]` / `[AfterScenario]` methods scoped per feature
  (`[Scope(Feature = …)]`): Shipment creation gets a new `SpecApiFactory` (own database name; the host migrates it on
  start) and afterwards disposes the client, the last answer and the host and drops the database; Scan sync gets a
  unique temp folder with a new Couchbase database and afterwards disposes the outbox and the database and deletes
  the folder. Each context owns its `FakeTimeProvider`, so time never leaks between scenarios.
- **Reusable parameterised steps:** one step records any scan type for any shipment and place (`a`/`an` handled by
  one pattern) and serves both `Given` and `When`; "the server holds {int} scan(s) for shipment {string}",
  "{int} scan(s) is/are waiting to be sent", "the scan for shipment {string} is {string}" (string → `SyncStatus`
  by Reqnroll's enum conversion) and "the server answers {string}" are shared by several scenarios.
- **Empty register:** the Development host seeds ten demo shipments, so the Background removes them in the scenario's
  own database and then proves through the API that the list has 0 shipments.
- **SQL Server availability:** `SpecApiFactory.CreateHost` calls the shared `SqlTestServer.SkipIfUnavailable()`, so
  Shipment creation is skipped locally without SQL Server and fails in CI, like every other SQL test. Scan sync
  needs no SQL Server.

**SpecFlow:** none exists in the repository, so there is nothing to migrate. Reqnroll is the maintained successor of
SpecFlow and uses the same Gherkin and binding attributes.

## 10. Copilot plan

- The **user** runs GitHub Copilot. Claude prepares the prompts and reviews the output; Claude never writes Copilot
  output itself or presents its own code as Copilot's.
- Targets (step 09, prompts in `docs/copilot/PROMPTS.md`): (a) Moq + `FakeTimeProvider` unit tests for
  `ShipmentTrackingService.UploadScansAsync` and `ChangeStatusAsync`; (b) `ShipmentStatusUpdate.feature` with
  Reqnroll steps at service level, including an invalid backward move.
- Raw output is saved unchanged in `docs/copilot/raw/`. `COPILOT-LOG.md` lists each prompt verbatim, what Copilot
  produced, and every generated test as kept / changed / rejected with the reason.
- At least two mutation proofs: break the production code on purpose, save the failing output in
  `docs/copilot/evidence/`, restore, save the passing output. Broken code is never committed.
- Copilot-assisted classes start with
  `// Copilot-assisted: drafted by GitHub Copilot, reviewed and edited by a human (see COPILOT-LOG.md).`

## 11. What we will not test, and why

| Not tested | Why |
| --- | --- |
| MAUI pages, navigation, platform `SecureStorage`, device connectivity events | UI and platform code are out of scope (W5.2); the logic behind them is in view models and the API client, which are tested |
| Browser behaviour of the Week 3 MVC pages (JavaScript, CSS, layout) | Front-end testing is out of scope; server-side page responses stay covered by existing integration tests |
| Generated code (`src/ApexPost.Client/Generated`, EF migrations) | Generated, never edited by hand; covered indirectly (`GeneratedClientTests`, `TrackingModelTests.The_migrations_match_the_model`) |
| Swagger UI rendering | Third-party UI; only "Development only" is tested (`DevelopmentDocumentationTests`, `ProductionDocumentationTests`) |
| Console harness output formatting beyond the existing tests | Demo tool, not shipped |
| Performance and load | Measurements stay in `Category=Measurement` and are excluded from CI; numbers come only from real runs (Week 3 rule) |
| Real external services (couriers, identity verification, email) | Only simulated implementations exist; no real network in tests |

## 12. Reflection

To be written at the end of Week 5 (about half a page): what the strategy changed, what the pyramid looks like
after the week with real numbers, what TDD, BDD and Copilot taught, and what we would do differently.
