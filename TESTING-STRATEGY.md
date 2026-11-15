# Testing strategy – ApexPost Logistics (Week 5)

This document was written **before any new Week 5 test**. Every number in it comes from the repository at commit
`51ed2f3` (`main` after Week 4) or from a real test run on that commit. It is updated in every Week 5 step so that it
stays true to the code; the Progress log in `docs/WEEK5-CONTEXT.md` says what changed when.

Baseline run (2026-11-15, `dotnet build Apex_Post_Logistic.slnx -c Release` then
`dotnet test Apex_Post_Logistic.slnx -c Release --no-build --filter "Category!=Measurement"`):
build 0 warnings, 0 errors; **1,769 test cases passed, 0 failed, 0 skipped**, run against a local SQL Server.

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
| R1 | A shipment moves backwards, stays, or leaves Delivered/Failed | `Domain/Tracking/ShipmentStatusRules.cs` (`CanMove`, `StatusFor`), `ShipmentTrackingService.ChangeStatusAsync` / `TryRecordAsync` | Medium | High (wrong tracking shown to customers, delivered parcel re-opened) | Unit (full 6×6 matrix); integration only for 409 wiring | `ShipmentStatusRulesTests` (only 4 `InlineData` rows today), `ShipmentTrackingServiceTests`, `ShipmentEndpointTests.Patch_status_backward_returns_409_invalid_transition` → full `TheoryData` matrix (step 03) |
| R2 | Two shipments get the same tracking number | `ShipmentTrackingService.CreateAsync` (check), unique index `IX_TrackedShipments_TrackingNumber` (`Infrastructure/Tracking/Configurations/TrackedShipmentConfiguration.cs`), `SqlException` 2601/2627 → `DuplicateKeyException` | Medium | High (scans attached to the wrong parcel) | Unit (service check with Moq), integration (real unique index), BDD (business outcome) | `ShipmentTrackingServiceTests`, `TrackingModelTests.Tracking_number_and_scan_id_have_unique_indexes`, `ShipmentEndpointTests.Create_with_a_tracking_number_in_use_returns_409` → `ShipmentCreation.feature` (step 06) |
| R3 | A re-sent scan is stored twice (offline retries make re-sends normal) | `ShipmentTrackingService.UploadAsync` / `UploadOnceAsync` (`DistinctBy`, `FindStoredScanIdsAsync`, retry on `DuplicateKeyException`), unique scan id index | High | High (history duplicated, status moved twice) | Unit (in-batch duplicates, stored ids), integration (parallel uploads on SQL Server), BDD | `ShipmentScanUploadTests`, `ShipmentScanEndpointTests.Parallel_uploads_of_the_same_batch_store_each_scan_once`, `HttpUploaderEndToEndTests` → `ScanSync.feature` (step 06) |
| R4 | A courier uploads scans as another courier, or a role does what it must not | `Application/Tracking/ShipmentTrackingService.CheckBatch`, `Api/Security/ClaimsCurrentCourier.cs`, `Composition/ApiSecurityRegistration.cs` (policies) | Medium | High (forged delivery proof) | Unit (ownership rule with Moq `ICurrentCourier`), integration (real JWT pipeline, 401/403) | `ScanOwnershipTests`, Api `AuthorizationTests`, `AuthenticationTests` → Moq tests + `TestAuthHandler` end-to-end test (step 04) |
| R5 | The client retries a 4xx or a non-idempotent request (duplicate PATCH, hammering on 400) | `ApiClient/Resilience/ApexResilience.cs`, `TransientFailure.cs`, `IdempotentRequest.cs` | Medium | Medium (duplicate status change, battery/network waste, locked account) | Unit (fake `HttpMessageHandler`, Moq `Protected()`) | `RetryRuleTests`, `RetryTests`, `CircuitBreakerTests`, `TimeoutTests` → typed client cases with Moq handler (step 04) |
| R6 | The outbox marks a scan `Synced` that the server did not store, or loses a scan on a network error | `Courier.Offline/Outbox/OutboxService.cs`, `OutboxService.Batches.cs` (`ApplyAsync`) | Medium | High (proof of delivery silently lost) | Integration (real Couchbase in a temp folder); unit with Moq `IShipmentUploader` | `OutboxServiceTests`, `BatchOutboxTests`, `OutboxTests` (duplicates, see section 5) → `ScanSync.feature` (step 06) |
| R7 | A validation limit is off by one (weight 0.1–30 kg, upload 1–100 scans, page size 1–50, scan time 2 min ahead) | `Api/Contracts/ContractRules.cs`, `TrackingLimits.cs`, `TrackedShipment.MaximumWeightKg`, `NotInFutureAttribute.cs` | High | Medium | Unit (at, below and above each limit) | `ShipmentRequestTests`, `ScanItemTests`, `ScanUploadRequestTests`, `TrackedShipmentTests` → parameterized boundary theories (step 03) |
| R8 | An unexpected error leaks a stack trace or internal message to callers | `Api/Errors/ApiExceptionHandler.cs` (`StackTraceKey` only in Development), Web `ProblemExceptionHandler` | Low | High (information disclosure) | Integration (Production host) | Api `ProductionErrorTests`, `ExceptionMappingTests`, Web `ExceptionHandlingTests`, `ProblemExceptionHandlerTests` |
| R9 | The deprecated v1 contract drifts and breaks existing partners | `Api/Contracts/V1/*`, `Controllers/V1/*`, `contracts/v1.json` | Medium | High (partner integrations break) | Integration (JSON snapshot through the real host) | `V1ContractSnapshotTests`, `ContractTrackingTests`, `VersionHeaderTests` |
| R10 | Week 3 money or stock is wrong (rounding, tax, negative stock, lost update) | `Domain/Common/Money.cs`, `Domain/Inventory/StockItem.cs`, `Application/Services/CheckoutService`, rowversion on `BranchStock` | Low (mature, heavily tested) | High (financial loss) | Unit (rules), integration (concurrency on SQL Server) | `MoneyTests`, `StockRuleTests`, `TaxRuleTests`, `CheckoutServiceTests`, `StockConcurrencyTests`, `BusinessRulesOnSqlTests` – kept, not extended |
| R11 | A test passes or fails depending on the wall clock | Tests using `DateTime.UtcNow` (section 5) | Medium | Medium (flaky CI, hidden bugs around midnight/expiry) | All | Fixed in step 02 (`FakeTimeProvider` / fixed times) |

## 3. Test pyramid

### 3.1 Current state (counted, commit 51ed2f3)

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

### 3.2 Target

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
`IShipmentUploader`, and `HttpMessageHandler` (protected `SendAsync`). The existing fakes stay; section 6 says what
each mock does **not** prove.

## 5. Existing tests review

One row per existing test class (190 classes, 1,134 methods). Level as defined in 3.1. Decision is one of
**kept / modified / removed / replaced / moved**; "planned in step NN" means the change happens in a later step and
this table is updated then.

Two changes apply to *every* class and are not repeated in each row: in step 02 every class gets
`[Trait("Category", "Unit" | "Integration" | "Measurement")]` (today only the 4 `Measurement` classes have a trait),
and any class touched later is renamed to `Method_Scenario_ExpectedResult` style (section 7).

### 5.1 Findings evaluated

1. **SQL Server tests inside a unit project.** `ApexPost.Tracking.UnitTests/Infrastructure/TrackingSqlTests` opens a
   real SQL Server database (`TrackingDatabase` fixture) – it is an integration test in a unit project, and it fails
   with `InvalidOperationException` instead of skipping when no server is configured. → **moved** to
   `tests/ApexPost.IntegrationTests/Tracking/` with the shared SQL helper (step 02).
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

   Plan (step 03): Courier.UnitTests keeps only real unit classes; the Couchbase classes **move** to
   `ApexPost.Courier.Offline.Tests` (the integration project for Couchbase); inside each pair the unique cases are
   merged into one class and the duplicate cases are **removed**.
3. **Tests that read the wall clock.** `DateTime.UtcNow` / `DateTimeOffset.UtcNow` appear in Api
   `AutomaticValidationTests`, `NotInFutureAttributeTests`, `AuthorizationTests`, `DevTokenTests`, `V2ContractTests`,
   `HttpUploaderEndToEndTests` and the helpers `Support/ApiClient.cs`, `Support/TestTokens.cs`; and in Week 3
   `AtomicSaleTests`, `OverPostingTests`, `StoredProcedureTests`, `ReportPagesTests`, `SaleToReportTests` and the
   helper `Support/CapturedLogs.cs`. No production code uses `DateTime.UtcNow` (all services take `TimeProvider` or
   `IClock`). Plan (step 02): the API test host gets a fixed `TimeProvider`; scan times and token expiry are computed
   from it; SQL default-value checks compare with the server's own `SELECT SYSUTCDATETIME()` instead of the test
   machine's clock; `CapturedLogs` waits with a `CancellationTokenSource` timeout. The one test whose *subject* is the
   system clock fallback (`NotInFutureAttributeTests.Without_a_TimeProvider_service_the_system_clock_is_used`) uses
   `TimeProvider.System` with a one-hour margin and is documented as the only exception.
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
   is not proved. → **replaced** by a `TheoryData<ShipmentStatus, ShipmentStatus, bool>` matrix test (step 03).

### 5.2 Review table

Decisions: 166 kept, 15 modified, 6 moved, 2 removed, 1 replaced (190 classes). "Methods" and "Cases" as in 3.1.

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
| Tracking.UnitTests | `Domain/TrackedShipmentTests.cs` | 9 | 23 | Unit | kept | Tracking domain invariant at unit level (R7) |
| Tracking.UnitTests | `Domain/TrackingFormatsTests.cs` | 4 | 12 | Unit | kept | Tracking domain invariant at unit level (R7) |
| Tracking.UnitTests | `Domain/TrackingScanTests.cs` | 4 | 7 | Unit | kept | Tracking domain invariant at unit level (R7) |
| Tracking.UnitTests | `Infrastructure/TrackingModelTests.cs` | 5 | 6 | Unit | kept | Reads EF model metadata with Server=unused and never connects, so it is a unit test |
| Tracking.UnitTests | `Infrastructure/TrackingSqlTests.cs` | 4 | 4 | Integration | moved - planned in step 02 | Opens a real SQL Server database inside a unit project and throws instead of skipping; moves to IntegrationTests/Tracking with the shared SQL helper |
| Tracking.UnitTests | `Rules/ShipmentStatusRulesTests.cs` | 5 | 28 | Unit | replaced - planned in step 03 | Only 4 InlineData rows; replaced by a full 6x6 TheoryData matrix with Method_Scenario_ExpectedResult names (R1) |
| Tracking.UnitTests | `Services/ScanOwnershipTests.cs` | 6 | 8 | Unit | kept | Tracking service rules with the stateful InMemoryTracking fake (R1-R4); Moq tests are added in new classes (step 04) |
| Tracking.UnitTests | `Services/ShipmentScanUploadTests.cs` | 5 | 6 | Unit | kept | Tracking service rules with the stateful InMemoryTracking fake (R1-R4); Moq tests are added in new classes (step 04) |
| Tracking.UnitTests | `Services/ShipmentTrackingServiceTests.cs` | 29 | 40 | Unit | kept | Tracking service rules with the stateful InMemoryTracking fake (R1-R4); Moq tests are added in new classes (step 04) |
| Courier.UnitTests | `Harness/CourierHarnessTests.cs` | 9 | 9 | Integration | kept | Runs the harness on real Couchbase (Integration trait); stays because only this project references the Harness |
| Courier.UnitTests | `Harness/TableWriterTests.cs` | 2 | 4 | Unit | kept | Pure text formatting of the harness tables |
| Courier.UnitTests | `Offline/CouchbaseScanQueriesTests.cs` | 10 | 10 | Integration | moved - planned in step 03 | Real Couchbase; moves to Courier.Offline.Tests and absorbs ScanQueryTests (duplicate query cases removed) |
| Courier.UnitTests | `Offline/CouchbaseScanRepositoryTests.cs` | 13 | 14 | Integration | moved - planned in step 03 | Real Couchbase; moves to Courier.Offline.Tests and absorbs KeyValueCrudTests (duplicate CRUD cases removed) |
| Courier.UnitTests | `Offline/CourierDatabaseTests.cs` | 6 | 6 | Integration | moved - planned in step 03 | Real Couchbase; moves to Courier.Offline.Tests and absorbs IndexTests (duplicate index cases removed) |
| Courier.UnitTests | `Offline/FakeShipmentUploaderTests.cs` | 5 | 6 | Unit | kept | Part A fake uploader rules (demo failures) without a database |
| Courier.UnitTests | `Offline/OutboxServiceTests.cs` | 8 | 8 | Integration | moved - planned in step 03 | Real Couchbase; moves to Courier.Offline.Tests next to BatchOutboxTests; covers both OutboxTests cases |
| Courier.UnitTests | `Offline/ScanChangeMonitorTests.cs` | 6 | 6 | Integration | moved - planned in step 03 | Real Couchbase listener; moves to Courier.Offline.Tests next to ListenerDisposeTests |
| Courier.UnitTests | `Offline/ScanDocumentMapperTests.cs` | 8 | 11 | Unit | kept | Exact JSON field names and round trip of the scan document (shared contract) |
| Courier.UnitTests | `Scans/ShipmentScanTests.cs` | 6 | 17 | Unit | modified - planned in step 03 | Duplicate of Offline.Tests Domain/ShipmentScanTests; becomes the single class, takes over its unique cases (scan:: id, unique ids, starts Pending) |
| Courier.ApiClient.Tests | `Auth/AccessTokenClaimsTests.cs` | 3 | 11 | Unit | kept | Auth header handler and token claims with a fake handler (R4) |
| Courier.ApiClient.Tests | `Auth/AuthHeaderHandlerTests.cs` | 4 | 6 | Unit | kept | Auth header handler and token claims with a fake handler (R4) |
| Courier.ApiClient.Tests | `Auth/HttpClientFactoryWiringTests.cs` | 1 | 1 | Unit | kept | Auth header handler and token claims with a fake handler (R4) |
| Courier.ApiClient.Tests | `Client/ApexApiClientTests.cs` | 10 | 13 | Unit | kept | Typed client results and ProblemDetails parsing with a fake handler |
| Courier.ApiClient.Tests | `Client/DevelopmentTokenTests.cs` | 3 | 3 | Unit | kept | Typed client results and ProblemDetails parsing with a fake handler |
| Courier.ApiClient.Tests | `Client/ProblemDetailsTests.cs` | 6 | 13 | Unit | kept | Typed client results and ProblemDetails parsing with a fake handler |
| Courier.ApiClient.Tests | `Client/ResponseRobustnessTests.cs` | 4 | 13 | Unit | kept | Typed client results and ProblemDetails parsing with a fake handler |
| Courier.ApiClient.Tests | `Http/ApiBaseAddressTests.cs` | 3 | 7 | Unit | kept | Base address rules and DEBUG-only development hosts |
| Courier.ApiClient.Tests | `Http/DevelopmentHostsTests.cs` | 3 | 9 | Unit | kept | Base address rules and DEBUG-only development hosts |
| Courier.ApiClient.Tests | `Json/ApexJsonTests.cs` | 5 | 14 | Unit | kept | Shared JSON options (string enums, camelCase) |
| Courier.ApiClient.Tests | `Resilience/CircuitBreakerTests.cs` | 5 | 5 | Unit | kept | Polly retry / circuit breaker / timeout with scripted handlers (R5) |
| Courier.ApiClient.Tests | `Resilience/ResilienceLoggingTests.cs` | 3 | 3 | Unit | kept | Polly retry / circuit breaker / timeout with scripted handlers (R5) |
| Courier.ApiClient.Tests | `Resilience/RetryRuleTests.cs` | 6 | 19 | Unit | kept | Polly retry / circuit breaker / timeout with scripted handlers (R5) |
| Courier.ApiClient.Tests | `Resilience/RetryTests.cs` | 8 | 16 | Unit | kept | Polly retry / circuit breaker / timeout with scripted handlers (R5) |
| Courier.ApiClient.Tests | `Resilience/TimeoutTests.cs` | 2 | 2 | Unit | kept | Polly retry / circuit breaker / timeout with scripted handlers (R5) |
| Courier.ApiClient.Tests | `Sync/HttpShipmentUploaderTests.cs` | 8 | 24 | Unit | kept | HttpShipmentUploader batch mapping and outcomes with a fake handler (R3, R6) |
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
| IntegrationTests | `Sql/AtomicSaleTests.cs` | 6 | 6 | Integration | modified - planned in step 02 | Uses DateTime.UtcNow as an arbitrary value; replace with a fixed time |
| IntegrationTests | `Sql/AuditPersistenceTests.cs` | 4 | 4 | Integration | kept | Real SQL Server behaviour (constraints, rowversion, procedures, translation) no unit test can prove (R10) |
| IntegrationTests | `Sql/BusinessRulesOnSqlTests.cs` | 9 | 21 | Integration | kept | Week 3 rules enforced by SQL CHECK/UNIQUE constraints (R10) |
| IntegrationTests | `Sql/CommandCountTests.cs` | 5 | 14 | Integration | kept | Real SQL Server behaviour (constraints, rowversion, procedures, translation) no unit test can prove (R10) |
| IntegrationTests | `Sql/DeadlockTests.cs` | 3 | 3 | Integration | kept | Real SQL Server behaviour (constraints, rowversion, procedures, translation) no unit test can prove (R10) |
| IntegrationTests | `Sql/DisposableDatabaseTests.cs` | 2 | 2 | Integration | kept | Real SQL Server behaviour (constraints, rowversion, procedures, translation) no unit test can prove (R10) |
| IntegrationTests | `Sql/EfMappingTests.cs` | 16 | 35 | Integration | kept | Real SQL Server behaviour (constraints, rowversion, procedures, translation) no unit test can prove (R10) |
| IntegrationTests | `Sql/ErrorMappingTests.cs` | 7 | 7 | Integration | kept | Real SQL Server behaviour (constraints, rowversion, procedures, translation) no unit test can prove (R10) |
| IntegrationTests | `Sql/LeastPrivilegeTests.cs` | 6 | 21 | Integration | kept | Real SQL Server behaviour (constraints, rowversion, procedures, translation) no unit test can prove (R10) |
| IntegrationTests | `Sql/OverPostingTests.cs` | 2 | 2 | Integration | modified - planned in step 02 | Compares a DB default with the test machine clock; compare with SELECT SYSUTCDATETIME() instead |
| IntegrationTests | `Sql/PriceRevisionTests.cs` | 5 | 6 | Integration | kept | Real SQL Server behaviour (constraints, rowversion, procedures, translation) no unit test can prove (R10) |
| IntegrationTests | `Sql/QueryTranslationTests.cs` | 11 | 11 | Integration | kept | Real SQL Server behaviour (constraints, rowversion, procedures, translation) no unit test can prove (R10) |
| IntegrationTests | `Sql/ReversalTests.cs` | 7 | 9 | Integration | kept | Real SQL Server behaviour (constraints, rowversion, procedures, translation) no unit test can prove (R10) |
| IntegrationTests | `Sql/StockConcurrencyTests.cs` | 5 | 5 | Integration | kept | rowversion lost-update protection on BranchStock (R10) |
| IntegrationTests | `Sql/StoredProcedureTests.cs` | 15 | 15 | Integration | modified - planned in step 02 | Now = DateTimeOffset.UtcNow and a clock window; use fixed times and the server's SYSUTCDATETIME() |
| IntegrationTests | `Sql/WriteOrderTests.cs` | 2 | 2 | Integration | kept | Real SQL Server behaviour (constraints, rowversion, procedures, translation) no unit test can prove (R10) |
| IntegrationTests | `Workflows/BranchAdministrationTests.cs` | 7 | 8 | Integration | kept | End-to-end Week 3 page flow; the only tests of the server-rendered pages |
| IntegrationTests | `Workflows/CounterWorkflowTests.cs` | 14 | 14 | Integration | kept | End-to-end Week 3 page flow; the only tests of the server-rendered pages |
| IntegrationTests | `Workflows/NotificationPageTests.cs` | 3 | 3 | Integration | kept | End-to-end Week 3 page flow; the only tests of the server-rendered pages |
| IntegrationTests | `Workflows/ReportPagesTests.cs` | 9 | 9 | Integration | modified - planned in step 02 | "Today" from DateTime.UtcNow can flip at midnight; use the factory's fixed clock |
| IntegrationTests | `Workflows/SaleToReportTests.cs` | 8 | 8 | Integration | modified - planned in step 02 | "Today" from DateTime.UtcNow can flip at midnight; use the factory's fixed clock |
| IntegrationTests | `Workflows/StaffAdministrationTests.cs` | 11 | 11 | Integration | kept | End-to-end Week 3 page flow; the only tests of the server-rendered pages |
| Api.Tests | `Client/ApexPostApiOptionsTests.cs` | 3 | 5 | Unit | kept | Generated client options defaults, no host needed |
| Api.Tests | `Client/GeneratedClientTests.cs` | 7 | 7 | Integration | kept | Generated NSwag client against the in-process API (contract wiring) |
| Api.Tests | `Contracts/AutomaticValidationTests.cs` | 7 | 7 | Integration | modified - planned in step 03 | Rule cases repeat ShipmentRequestTests/ScanItemTests and use DateTime.UtcNow; keep one valid and one invalid body per request type plus the nested Delivered-signature case |
| Api.Tests | `Contracts/NotInFutureAttributeTests.cs` | 7 | 7 | Unit | modified - planned in step 02 | Uses DateTime.UtcNow; switch to a fixed TimeProvider; the system-clock fallback test keeps TimeProvider.System with a 1 h margin (documented exception) |
| Api.Tests | `Contracts/ScanItemTests.cs` | 11 | 36 | Unit | kept | Request DTO validation with Validator, the lowest level for these rules (R7) |
| Api.Tests | `Contracts/ScanUploadRequestTests.cs` | 1 | 4 | Unit | kept | Upload size 1..100 at the DTO; boundary theories extended in a new class in step 03 (R7) |
| Api.Tests | `Contracts/ShipmentMappingTests.cs` | 7 | 7 | Unit | kept | Explicit DTO <-> command mapping, no host needed |
| Api.Tests | `Contracts/ShipmentRequestTests.cs` | 12 | 34 | Unit | kept | Request DTO validation with Validator, the lowest level for these rules (R7) |
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
| Api.Tests | `Security/AuthorizationTests.cs` | 8 | 9 | Integration | modified - planned in step 02 | Scan time from DateTime.UtcNow; use the host's fixed TimeProvider (R4) |
| Api.Tests | `Security/DevTokenTests.cs` | 5 | 7 | Integration | modified - planned in step 02 | Expiry checked against DateTime.UtcNow; compare with the fixed TimeProvider |
| Api.Tests | `Security/HttpsTests.cs` | 3 | 4 | Integration | kept | JWT, roles, partner key and HTTPS through the real pipeline (R4) |
| Api.Tests | `Security/PartnerApiKeyTests.cs` | 6 | 10 | Integration | kept | JWT, roles, partner key and HTTPS through the real pipeline (R4) |
| Api.Tests | `Security/PartnerKeyStoreTests.cs` | 4 | 9 | Unit | kept | Constant-time partner key lookup, no host needed |
| Api.Tests | `Security/ProductionSecurityTests.cs` | 2 | 2 | Integration | kept | JWT, roles, partner key and HTTPS through the real pipeline (R4) |
| Api.Tests | `Sync/HttpUploaderEndToEndTests.cs` | 4 | 4 | Integration | modified - planned in step 02 | Scan time from DateTimeOffset.UtcNow; use the fixed TimeProvider (R3, R6) |
| Api.Tests | `Versioning/V1ContractSnapshotTests.cs` | 4 | 7 | Integration | kept | Exact Part B JSON snapshot guards the deprecated v1 contract (R9) |
| Api.Tests | `Versioning/V2ContractTests.cs` | 7 | 7 | Integration | modified - planned in step 02 | Scan time from DateTime.UtcNow; use the fixed TimeProvider (R9) |
| Api.Tests | `Versioning/VersionHeaderTests.cs` | 5 | 12 | Integration | kept | v1 snapshot, v2 shape and version headers through the real host (R9) |
| Courier.Offline.Tests | `Architecture/DomainDependencyTests.cs` | 3 | 3 | Unit | kept | Reflection guard: only Offline references Couchbase |
| Courier.Offline.Tests | `Domain/ShipmentScanTests.cs` | 6 | 15 | Unit | removed - planned in step 03 | Duplicates Courier.UnitTests Scans/ShipmentScanTests; its 3 unique cases move there |
| Courier.Offline.Tests | `Indexes/IndexTests.cs` | 3 | 4 | Integration | modified - planned in step 03 | Overlaps CourierDatabaseTests and ExplainQueries; merged with the moved CourierDatabaseTests |
| Courier.Offline.Tests | `Listener/ListenerDisposeTests.cs` | 1 | 1 | Integration | kept | Unique case: no listener callback after dispose (real Couchbase) |
| Courier.Offline.Tests | `Outbox/BatchOutboxTests.cs` | 10 | 10 | Integration | kept | Batch path of the outbox on real Couchbase: batches of 20, rejected -> Failed, network error keeps Pending (R6) |
| Courier.Offline.Tests | `Outbox/OutboxTests.cs` | 2 | 2 | Integration | removed - planned in step 03 | Both cases are already proved by OutboxServiceTests and BatchOutboxTests |
| Courier.Offline.Tests | `Queries/ScanQueryTests.cs` | 5 | 9 | Integration | modified - planned in step 03 | Overlaps CouchbaseScanQueriesTests; only the courier-id injection case stays, merged with the moved class |
| Courier.Offline.Tests | `Repository/KeyValueCrudTests.cs` | 4 | 4 | Integration | modified - planned in step 03 | Overlaps CouchbaseScanRepositoryTests; only the unique reopen case stays, merged with the moved class |

## 6. Data access strategy

| Technique | Where | What it proves | What it does NOT prove |
| --- | --- | --- | --- |
| Moq on repository / query / unit-of-work ports | Service unit tests (`ShipmentTrackingService`, outbox) | The service calls the right port members in the right order, commits once, does not commit on failure, maps port exceptions to the right outcome | That the SQL is valid, that EF Core can translate the query, that the unique index exists, that rowversion detects a conflict, that the data really round-trips |
| Hand-written in-memory fakes (`InMemoryTracking`, `InMemoryRepository`) | Existing service tests, BDD `ScanSync.feature` | Business flow over several calls with believable state | Same gaps as Moq; plus the fake can drift from the real repository – each fake is kept minimal and checked against the port contract |
| Throw-away **SQL Server** database per fixture | `ApexPost.IntegrationTests/Sql`, Api tests (`ApiFactory`), moved `TrackingSqlTests` | Real migrations, constraints (UNIQUE, CHECK, FK Restrict), filtered indexes, rowversion, stored procedures, `datetime2` precision, SQL translation, transaction behaviour | Production data volume and real network latency; SQL Server version differences beyond the one used (CI: `mssql/server:2022-latest`) |
| Embedded **Couchbase Lite** in a unique temp folder | `ApexPost.Courier.Offline.Tests` (after step 03, also the moved Courier classes) | Real document storage, SQL++ queries with parameters, value indexes (`Explain`), listeners, reopen behaviour | Sync over a real network; behaviour on Android/Windows device storage |
| `WebApplicationFactory<Program>` in-process | Api and Web integration tests | Routing, model binding, filters, middleware order, authentication/authorization, status codes, headers, ProblemDetails | Kestrel/TLS configuration and reverse proxies in a real deployment |

**Why not SQLite here.** The tracking and Week 3 schemas depend on SQL Server features that SQLite does not have or
emulates differently: `rowversion` concurrency tokens (`BranchStock`, `CounterTransactions`), filtered and INCLUDE
indexes, stored procedures (`StoredProcedureTests`), `datetime2` precision and `SYSUTCDATETIME()` defaults, and
`SqlException` numbers 2601/2627 that Infrastructure translates to `DuplicateKeyException`. A green SQLite run would
not prove any of these, and would need a second set of migrations.

**Why mocking `DbContext` / `DbSet` gives false confidence (never done here).** A mocked `DbSet` runs LINQ to Objects:
a query EF Core cannot translate to SQL, or translates differently (string comparison, case sensitivity, `null`
handling, client evaluation), still passes. There are no constraints, so a duplicate key or a CHECK violation is
never raised. And the mock setup has to repeat what the implementation does (`Setup(x => x.Shipments.Where(…))`),
so the test copies the implementation and passes even when both are wrong.

**Local "no manual setup" chain (one shared helper, step 02).** Every SQL test fixture asks one helper for a server:

1. environment variable `ConnectionStrings__ApexPostTestServer` (CI container);
2. user-secrets of the web project (`ConnectionStrings:ApexPostTestServer`, then `ConnectionStrings:ApexPostDb`);
3. on Windows, LocalDB `(localdb)\MSSQLLocalDB` if it answers;
4. otherwise `Assert.Skip("SQL Server not available: …")` with the list of what was tried.

Each fixture creates its own database (`ApexPostTests_<area>_<guid>`) and drops it when the fixture ends; the
development database is never touched. In CI, step 1 always succeeds, so CI has 0 skipped tests.

## 7. Conventions

- **AAA:** every new or modified test has `// Arrange`, `// Act`, `// Assert` comments.
- **Naming:** new tests and every class touched in Week 5 use `Method_Scenario_ExpectedResult`
  (e.g. `CanMove_FromDeliveredToAnyStatus_ReturnsFalse`). Untouched older tests keep their sentence-style names
  (`Forward_moves_are_allowed`): renaming about 1,100 methods by hand adds review noise and merge risk and proves
  nothing new; they are renamed only when a class is changed for another reason.
- **Traits:** `[Trait("Category", "Unit" | "Integration" | "Spec" | "Measurement")]` on every class. CI excludes
  `Measurement`.
- **Parameterized tests** for rules with several cases: tracking number and courier id format, weight boundaries,
  the 6×6 status matrix, page / pageSize / upload-size limits, scan age (71 h / 72 h / 73 h).
- **Exceptions and boundaries:** assert the exact exception type and the message or parameter name; test at, just
  below and just above each limit.
- **Determinism:** no `DateTime.Now/UtcNow` in tests or code under test (`FakeTimeProvider` from
  `Microsoft.Extensions.TimeProvider.Testing`, or fixed fictional times); no order dependency; no static mutable
  state; unique temp folders and databases per fixture.
- **No real network, fictional data, no secrets:** fake or Moq `HttpMessageHandler`; in-process hosts; `CR-101`,
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

## 9. BDD plan

- New project `tests/ApexPost.Specs` with Reqnroll's xUnit v3 integration (`Reqnroll.xunit.v3`). If that package does
  not work with this repo's xUnit v3 setup, the step stops and asks before any fallback.
- **`Features/ShipmentCreation.feature` – API level.** The business outcome of creating a shipment *is* the HTTP answer
  (created, refused as duplicate, refused as invalid), and the validation lives in the API request DTOs, so the
  scenarios run through `WebApplicationFactory` with a test database and test authentication. Scenarios: valid
  creation, duplicate tracking number, Scenario Outline for invalid weights.
- **`Features/ScanSync.feature` – service level.** The outcome is what happens to the courier's offline scans, which
  spans the device outbox and the server rules. Real `OutboxService` on Couchbase in a temp folder, real
  `ShipmentTrackingService` with in-memory repositories, joined by an in-process uploader; `FakeTimeProvider` for
  time. Scenarios: offline scans uploaded, duplicate skipped, invalid rejected, Scenario Outline for scan age
  (71 h accepted, 72 h accepted, 73 h rejected).
- At least one `Background` and one `Scenario Outline` with `Examples`. Business language only – no URLs, JSON or
  class names in the feature files.
- State per scenario through context injection (context classes injected into step-definition constructors), never
  static fields. `[BeforeScenario]` / `[AfterScenario]` hooks create and dispose the factory, database, temp folder
  and time provider.
- **No SpecFlow exists in the repository**, so there is nothing to migrate.

## 10. Copilot plan

- The **user** runs GitHub Copilot. Claude prepares the prompts and reviews the output; Claude never writes Copilot
  output itself or presents its own code as Copilot's.
- Target: unit tests for an in-scope class with clear rules (candidate: Api request DTO validation or
  `TransientFailure` retry classification).
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
