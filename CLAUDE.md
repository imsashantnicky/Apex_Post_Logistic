# Apex_Post_Logistic — project rules for Claude

Read this file fully before every task. Do the task, do not explain unless asked.
End every task with a summary of at most 5 lines: what changed, build result, test counts.
The lab brief is in `docs/lab/week2-brief.md`. If anything here conflicts with it, the brief wins: say so before coding.

## 0. Reference project
Model: https://github.com/r109431968/ApexPost-Logistics — same folder structure, project names, features, demo
accounts, URLs and look. Built from scratch, NOT copied. Do NOT repeat its known weaknesses (fixed by this file):
broken Mermaid/ADR formatting in DESIGN.md, seeding + shared demo password active outside Development,
tax shown as 0.00 before payment, raw JSON error page for browser users, no CI, wildcard package versions,
NuGetAudit disabled, no optimistic concurrency, grab-bag files with many types, screenshots of only one role,
README claims without evidence, one giant commit instead of feature branches.

## 1. What we are building
ApexPost Counter Platform: ASP.NET Core MVC (.NET 10, C# 14) for postal counters. One counter sells retail goods
(packaging, stationery, office supplies) together with parcel/letter shipping, money orders and identity verification.
A clerk opens a transaction, builds ONE mixed basket, verifies identity if needed and takes payment. A Branch Manager
voids/refunds, an Auditor reads reports and the audit log, an Administrator manages staff and branches.
JSON files behind repository interfaces (next week SQL Server + EF Core replaces only Infrastructure). Secure, Fast
(no blocking calls, bounded concurrency) and Auditable (user, branch, time, correlation ID).
Built from scratch. DESIGN ADR-1 states: the Week 1 layered architecture (Domain / Application / Infrastructure + UI)
was kept as the design; the domain was designed new for ApexPost.

## 2. Hard constraints from the brief (never break)
- .NET 10, C# 14, Nullable enabled, **zero build warnings**. No database, no ORM. All I/O async.
  No `.Result`, `.Wait()`, `Thread.Sleep`, `async void`.
- **Allowed libraries only:** `Microsoft.AspNetCore.*`, `Microsoft.Extensions.*`, xUnit (+ its runner and
  `Microsoft.NET.Test.Sdk`), `Microsoft.AspNetCore.Mvc.Testing`. No Bootstrap/jQuery/Moq/FluentAssertions/others.
  No ASP.NET Core Identity — own cookie auth; passwords hashed with PBKDF2 from `System.Security.Cryptography`.
- `Program.cs` = composition root + pipeline only. No business logic in Program.cs, middleware or controllers.
- No secrets in source control (user-secrets / environment variables only).
- Conventional routing (`MapControllerRoute`), including a named route for the integration endpoint.

## 3. Solution layout (same as the reference, flat folders at the root)
Repo, folder, solution and README title: **Apex_Post_Logistic**. Projects/namespaces stay `ApexPost.*`
(underscores in namespaces trigger CA1707 and the build must have zero warnings).
```
Apex_Post_Logistic.slnx · Directory.Build.props · global.json · .editorconfig · .gitignore
README.md · DESIGN.md (ADRs inside) · AUDIT-SAMPLE.json · setup-secrets.bat · .github/workflows/ci.yml
docs/screenshots/
ApexPost.Domain/          Entities/ ValueObjects/ Enums/ Events/ Exceptions/ Rules/
ApexPost.Application/     Abstractions/ DTOs/ Exceptions/ Extensions/ Reports/ Services/ Settings/
                          Specifications/ Validation/
ApexPost.Infrastructure/  Auditing/ Caching/ Notifications/ Persistence/Json/{Mapping,Records}/ Security/
                          Seeding/ Shipping/ Verification/ SystemClock.cs  DependencyInjection.cs
ApexPost.Web/             Composition/ Controllers/ (ApexController base) Filters/ Health/ Middleware/ Security/
                          ViewModels/ Views/ wwwroot/css/site.css wwwroot/js/site.js Program.cs
ApexPost.UnitTests/       Domain/ Rules/ Services/ Infrastructure/ Architecture/ Measurements/ Support/
ApexPost.IntegrationTests/ Authentication/ Filters/ Middleware/ Security/ Support/ (ApexFactory)
```
References: Domain -> nothing; Application -> Domain; Infrastructure -> Application; Web -> Application +
Infrastructure (Infrastructure types only in Program.cs and Composition/); UnitTests -> Domain, Application,
Infrastructure; IntegrationTests -> Web.
Build: `global.json` sdk 10.0.100 rollForward latestFeature. `Directory.Build.props`: net10.0, LangVersion 14,
Nullable, ImplicitUsings, TreatWarningsAsErrors, AnalysisLevel latest-recommended, NuGetAudit true.
Package versions in .csproj: EXACT numbers (no `*`), resolved via `dotnet add package`.
`.gitignore`: bin/ obj/ .vs/ *.user TestResults/ publish/ `ApexPost.Web/data/` secrets.json
plus local helpers `CLAUDE.md`, `docs/lab/`, `VIVA.md`.
Fix warnings in code. Only exception: `.editorconfig` turns off CA1707 for test projects (underscored test names),
with a comment. Never `#pragma`, `NoWarn` or `[SuppressMessage]`.

## 4. Domain (ApexPost.Domain)
Value objects (`ValueObjects/`) — sealed records, validated in the constructor, throw `DomainException`:
- `Money(decimal Amount, string Currency = "USD")`: >= 0, 2 dp AwayFromZero, 3 letters, + - * (int), no currency mixing.
- `Weight` (grams; FromKilograms; > 0; comparisons). `Dimensions(LengthCm, WidthCm, HeightCm)` > 0,
  `VolumetricKg = L*W*H/5000`. `Address` (C# 14 `field` keyword in init setters). `BranchCode` ("BR" + 3 digits,
  e.g. BR001). `Sku` ("AAA-0000", e.g. PKG-0001). `IdentityVerification(DocumentType, MaskedNumber "****4567",
  VerificationResult, VerifiedAt)` — the full document number is never stored, logged or audited.
Entities (`Entities/`, base `Entity<TId>` with equality by Id):
- `Branch` (Code, Name, Region, OpensAt < ClosesAt, IsActive, Counters 1..n). Close only if open, Reopen only if closed.
- `Counter`; `Staff` (Id, Name, Email — `field` keyword validation, Role, HomeBranch, IsActive; Deactivate/Activate).
- `Product` (Sku, Name, Category, UnitPrice > 0, TaxCategory). `StockItem` (per branch + sku, never < 0,
  `InsufficientStockException`). `ShippingZone` (Code e.g. Z01, Name, BaseRate, PerKgRate).
- `CounterTransaction` aggregate (Branch, Counter, Clerk, Lines, Subtotal, Tax, Total, PaymentMethod, Status,
  Verification, ReversalReason): `Open(branch, counter, clerk, now)` is the only way to create one. Lines are
  `ILineItem` (composition via interface): `RetailLineItem`, `ShippingLineItem` (type, weight, dimensions, zone,
  level), `MoneyOrderLineItem` (amount, fee, sender, recipient; tax Exempt). Status Open -> Paid -> Refunded;
  Open|Paid -> Voided. `Pay(method, threshold, Func<ILineItem, Money> taxRule)`. `Restore(...)` validates consistency.
- `Rules/WeightLimitRule` (pattern matching on (type, level)). `Enums/` start at 1, one enum per file.
- `Events/`: TransactionCompletedEvent, StockLowEvent, MoneyOrderIssuedEvent, TransactionVoidedEvent (records).
- Business-rule violations always throw `DomainException` (or subclass), never ArgumentException.

Business rules — each needs a unit test, including the boundary:
1. Closed branch cannot accept transactions. 2. Inactive staff cannot open/process transactions or sign in.
3. A clerk transacts only at the assigned (home) branch. Auditor/Administrator cannot transact.
4. Money order amount > `IdentityVerificationThreshold` (config, 300) cannot be paid without a Passed verification
   in the same transaction (exactly 300 = no check needed).
5. Voids and refunds require the Branch Manager role (active, same branch) and a non-empty reason; refund only if Paid;
   stock returns on void of a paid transaction and on refund.
6. Stock never negative: a sale fails if stock is short; Pay checks every line first and changes nothing if one is short.
7. Weight limits per service level: Letter 2 kg; Parcel Standard 30, Express 20, Registered 10 — over the limit rejected.
8. Pricing strategy: `IShippingPricingStrategy` per service level (Strategy pattern, chosen by pattern matching);
   chargeable kg = max(actual, volumetric); price = (zone.BaseRate + zone.PerKgRate * kg) * level multiplier
   (Standard 1.0, Express 1.6, Registered 1.3; from config).
9. Tax via pattern matching on TaxCategory: Standard 20 %, Reduced 5 %, Exempt 0 % (config). Tax is previewed on the
   open basket and frozen at Pay (the basket never shows 0.00 tax before payment).
10. Money order fee = max(2.00, 2 % of amount). Cash total rounds to nearest 0.05 (extension member).
11. Low-stock rule is a configurable `Predicate<StockItem>` (quantity <= 5). After Pay: StockLow event;
    money order > 1000 (config) -> high-value alert; every void/refund -> notification. Observer: `CounterEvents`
    (C# events) + `NotificationSubscriber` -> `INotificationSink` (simulated, shown on the Notifications page).

## 5. Application (ApexPost.Application)
- `Abstractions/`: `IRepository<TEntity, TId> where TEntity : Entity<TId>` (Get, List, List(ISpecification<T>), Add,
  Update(entity, expectedVersion), Delete); `IAuditStore` (Append + Read only); `ICredentialStore`; `IPasswordHasher`;
  `IClock` (wraps TimeProvider); `IProductCatalog`/`IZoneCatalog` returning `ValueTask`; `ICourierTrackingService`;
  `IIdentityVerificationService`; `INotificationSink`; `ICurrentUser` (StaffId, Role, BranchCode);
  `ITransactionQueries`, `IReportQueries` (filters: branch, date range; paging).
- `Specifications/`: `ISpecification<in T>`, `PredicateSpecification<T>` — used by reports.
- `Services/`: CheckoutService, ReversalService, StockService (adjust with reason, manager only), ShippingPricer +
  strategies, TaxCalculator, StockGate (SemaphoreSlim per branch), LoginService (dummy hash for unknown emails),
  StaffService, BranchService, CatalogueService, CounterEvents, NotificationSubscriber.
  `Reports/ReportService`: 4 reports + dashboard via Task.WhenAll (sales, stock, alerts).
- Role and branch-ownership checks happen in services (`ICurrentUser`) and throw `ForbiddenException`.
- `Exceptions/`: NotFound, Forbidden, ValidationFailed, Concurrency, ExternalService, ExternalServiceTimeout.
- `Validation/`: `[NotBlank] [TextLength] [PositiveNumber] [Pattern]` + `AttributeModelValidator`
  (reflection metadata cached in `ConcurrentDictionary<Type, …>`).
- `Extensions/` (C# 14 extension members): Money (IsPositive, RoundedForCash, Percent), basket summary
  (RetailTotal, ServicesTotal), ClaimsPrincipal (StaffId, Role, BranchCode).
- `Settings/`: one class per config section (Shipping, Tax, MoneyOrders, Stock, Passwords, Courier, Catalog, Seed).
  No magic numbers.

## 6. Infrastructure (ApexPost.Infrastructure)
- `Persistence/Json/JsonRepository<TEntity,TId,TRecord>`: `Records/` + `Mapping/`, SemaphoreSlim per file,
  write temp file then `File.Move(overwrite)`, record `Version` (stale -> ConcurrencyException),
  JsonStringEnumConverter, malformed or duplicate-key file -> clear exception, never silently overwritten.
  Data folder `ApexPost.Web/data/` (git-ignored).
- `Auditing/JsonLinesAuditStore` (append-only `audit.jsonl`), `Security/JsonCredentialStore` +
  `Pbkdf2PasswordHasher` (random salt, 600 000 iterations, constant-time compare), `Caching/CachedCatalog`
  (ValueTask, TTL, hit/load counters), `Shipping/SimulatedCourierService` (SemaphoreSlim max 3; timeout via linked
  CancellationTokenSource; caller cancel ≠ timeout ≠ failure; "SLOW*" times out, "FAIL*" fails, e.g. APX123456 OK),
  `Verification/SimulatedIdentityVerificationService` ("FAIL*" Failed, "PEND*" Pending, else Passed — the clerk never
  chooses the result), `Notifications/InMemoryNotificationSink`, `SystemClock`.
- `Seeding/` (only when environment is Development AND `Seed:Enabled` is true; demo password from user-secrets):
  6 branches BR001 Central Counter, BR002 Harbour Point, BR003 Airport Terminal, BR004 Northgate, BR005 Westfield,
  BR006 Old Quay (closed); 26 staff across all 4 roles; 32 products PKG-/STA-/OFF- with all tax categories;
  10 zones Z01–Z10 (Z01 Local …); 48 historical transactions over 14 days (retail, shipping, money orders,
  verifications, voids, refunds). Demo emails: `clerk1.br001@apexpost.test`, `manager.br001@apexpost.test`,
  `auditor1@apexpost.test`, `admin1@apexpost.test`, `clerk4.br002@apexpost.test` (inactive),
  `manager.br006@apexpost.test` (closed branch); same pattern for other branches.
- `DependencyInjection.cs`: `AddJsonPersistence(configuration)` registers all storage in one place.

## 6a. Database-ready design (JSON today, SQL Server/EF Core next week — only Infrastructure changes)
- Domain and Application know nothing about files or JSON: no paths, no `System.Text.Json`, no JSON attributes.
- Persistence shape lives only in Infrastructure (`Records/` + `Mapping/`; later EF entities/configurations).
- Ids map to primary keys; every record has `Version` (later a concurrency token). Money = decimal + currency,
  enums as strings.
- Lists and reports go through `ITransactionQueries` / `IReportQueries` with filters + paging (later SQL queries).
- Multi-aggregate work (pay, void, refund, stock adjustment) happens in ONE service method so a DB transaction can wrap
  it later; the JSON version uses StockGate and DESIGN notes it is not atomic across files.
- One registration point: `AddJsonPersistence(...)`; a future `AddSqlPersistence(...)` replaces this line only.
- Seed data is plain C# objects, independent of storage. Ports are async with CancellationToken, no IQueryable.

## 7. Web (ApexPost.Web)
Pipeline (each line commented with its reason; identical order and reasons in DESIGN.md):
CorrelationId -> RequestLogging+Timing -> ExceptionHandling -> StatusCodePages (UseWhen: not /integration) ->
SecurityHeaders -> HSTS (not Development) -> HttpsRedirection -> MaintenanceMode -> StaticFiles -> Routing ->
IpRestriction -> CORS -> ApiKey -> RateLimiter -> Authentication -> Authorization -> /health + controllers.
- CorrelationId: reuse a safe incoming `X-Correlation-Id` or generate one, echo it, add to every log entry via
  `ILogger.BeginScope`. RequestLogging: structured log (method, path, status, ms) in try/finally, `X-Response-Time-Ms`.
- ExceptionHandling: always RFC 7807 ProblemDetails (title, status, safe detail, `traceId`, `correlationId`; stack
  trace only in Development). API/JSON callers get `application/problem+json`; browsers (Accept text/html) see the
  same ProblemDetails rendered by `ErrorController` + `Views/Shared/Status.cshtml`. Map: NotFound 404, Forbidden 403,
  ValidationFailed 400, Concurrency 409, Timeout 504, External 502, client cancel 499 (not logged as error).
- MaintenanceMode: config (reload on change), 503 + Retry-After, `/health` still works.
- SecurityHeaders via `Response.OnStarting`: CSP (no inline script/style), X-Content-Type-Options, X-Frame-Options,
  Referrer-Policy.
- `[IpRestricted]` metadata on admin controllers (Staff, Branches, Audit) and the integration endpoint; allowlists
  `Security:AdminAllowedIps` / `Security:IntegrationAllowedIps`; 403. DESIGN documents the forwarded-headers risk.
- `[RequireApiKey]` on `GET /integration/parcel-status/{trackingNumber}` (IntegrationController): keys under
  `Integration:ApiKeys:<Partner>` from user-secrets, constant-time compare, 401 missing/invalid, 500 not configured.
- Cookie auth (`.ApexPost.Auth`): claims StaffId, Role, BranchCode; HttpOnly, Secure, SameSite=Strict, sliding 30 min;
  principal revalidated each request. Policies: `BranchManagerOnly`, `SameBranchOnly`, `CanTransact`,
  `CanViewReports`, `AdministratorOnly`, `AuditViewers`; fallback = authenticated. Antiforgery on every form POST.
- Rate limiting: `login` (per IP, 5/min) on the login POST; `partner` (per partner, 30/min) on integration; 429 +
  Retry-After. CORS: only `https://tracking.partner.example` (config), GET only; DESIGN explains CORS is not a
  security boundary.
- `Filters/` (DESIGN explains why each is a filter, not middleware): `AuditActionFilter` (IAsyncActionFilter, global,
  order -1000) + `[Audit("Name")]`: StaffId, role, branch, controller, action, key route values, status, duration,
  correlation ID -> IAuditStore; covers login, sales, voids, refunds, money orders, identity verification, stock
  adjustments, staff/branch changes, access denied; never passwords, full document numbers, API keys or card data.
  `PerformanceFilter` (-900, `Performance:SlowActionMs`). `ValidationFilter` (-500: JSON -> ValidationProblemDetails 400,
  forms -> redisplay view). `DomainExceptionFilter` (form error -> message + PRG). `BranchAccessFilter`.
- Action results used: View, RedirectToAction (PRG after every successful POST), NotFound, BadRequest, Forbid,
  Json (audit export, integration), StatusCode.
- Controllers/Views (same as reference): Account (Login), Dashboard, Catalogue (search, category filter, stock adjust
  with reason), Transactions (Index, New, Details = counter screen, Label), Staff (Index, Create, Edit, _StaffForm;
  set password), Branches (open/close, counters), Reports (Index, DailySales, LowStock, ShippingVolume, MoneyOrders,
  _BranchFilter), Audit (Index + Export), Notifications, Error (Shared/Status). `/health` checks the data folder is writable.

## 8. Code quality
Max line 120 chars. One public type per file (no grab-bag files like Dtos.cs/Enums.cs). No magic strings/numbers.
CancellationToken from controller to file I/O. `ILogger` templates / `[LoggerMessage]`. Never log secrets or document
numbers. No empty catch, no commented-out code, no TODO. DI: singletons for stateful things (repositories, caches,
audit store, events, courier), scoped for services; middleware takes scoped services only as `InvokeAsync` parameters;
ValidateScopes + ValidateOnBuild.

## 9. Tests
- Unit (required): stock rule, money-order verification (299.99 / 300 / 300.01), weight limits per level (at and over),
  pricing (zone, weight, volumetric, level). Plus tax (incl. preview), fee, cash rounding, void/refund rules, closed
  branch, inactive staff, home branch, validator + cache, hasher, repository (restart, version conflict, parallel
  writers, cancelled write, malformed file), catalog cache, courier timeout vs cancel vs failure, StockGate.
- `Architecture/` layering tests with plain reflection: Domain references no ApexPost assembly; Application not
  Infrastructure/Web; Domain/Application use no System.Text.Json/System.IO; controllers use no Infrastructure type.
- Integration (`ApexFactory : WebApplicationFactory<Program>`, temp data folder, fast hashing, fake remote IP):
  `Security_headers_are_present`, `Integration_returns_401_without_api_key`, `Integration_returns_403_from_blocked_ip`,
  `Login_returns_429_after_limit`, `Unauthenticated_request_redirects_to_login`, `Clerk_gets_403_on_void`,
  `Correlation_id_header_is_returned`; plus wrong key 401, key missing 500, maintenance 503 + /health 200, partner 429,
  courier 504/502, ProblemDetails with traceId and no stack trace, HTML error page, antiforgery rejection,
  cross-branch 403, audit written without document number/password, CORS allowed vs other origin.
- `Measurements/` tests `[Trait("Category","Measurement")]` (cache, sequential vs WhenAll, semaphore 1/3/10), excluded
  from CI; numbers copied into DESIGN.md.

## 10. Git workflow (every phase)
1. `git switch main`, `git pull`, `git switch -c <branch from prompt>`.
2. Small commits per logical unit (3–8 per branch), Conventional Commits, e.g. `feat(domain): add Money value object`.
3. Before finishing: `dotnet build Apex_Post_Logistic.slnx -c Release` -> 0 warnings, 0 errors;
   `dotnet test Apex_Post_Logistic.slnx -c Release --filter "Category!=Measurement"` -> 0 failed, 0 skipped.
4. `git push -u origin <branch>`; `gh pr create --base main --title "<clear title>" --body "<what, why, how tested>"`;
   `gh pr merge --merge --delete-branch`; `git switch main && git pull`.
   Without gh: `git switch main && git merge --no-ff <branch>` then `git push`.
5. Never commit bin/, obj/, .vs/, data/, *.user, TestResults/, secrets, machine names or personal paths.

## 11. UI design (look of the reference, cleaner; hand-written CSS only)
- Only `wwwroot/css/site.css` + `wwwroot/js/site.js`, no CDN, no inline `style=`/`<script>` (CSP).
- Variables: `--bg #f4f6f9`, `--panel #ffffff`, `--text #1d2733`, `--muted #66758a`, `--brand #c8102e`,
  `--brand-dark #9d0c24`, `--line #dde3ea`, `--ok #1b7f4b`, `--bad #b3261e`, `--warn #b45309`, `--info #1d4ed8`;
  system font stack, radius 10px, soft shadow.
- Top bar: red background, "ApexPost Counter" in bold white, nav links (only pages the role may open), right side
  "Name · Role · BR001" and a "Sign out" link. Content max-width 1100px. Footer "ApexPost Counter Platform · Week 2"
  + correlation ID, small and muted.
- Page header: title + muted branch code/date (e.g. "Central Counter (BR001) · 2026-10-26") + one-line help text.
- Dashboard: 4 white KPI cards (Transactions today, Retail, Services, Total) with small label + big number + "USD";
  "Low stock" table card (SKU, Product, In stock, Threshold) and "Alerts" card; quick-action buttons.
- Tables: uppercase small muted headers on a light grey row, thin row lines, money right-aligned "13.60 USD",
  empty state text instead of an empty table, "Open" link per row.
- Status pills (light background + coloured text): Open blue, Paid green, Voided grey, Refunded red/amber;
  verification Passed/Failed/Pending.
- Counter screen (Transactions/Details): title "Transaction" + status pill + id/opened time; Basket card on top
  (description, qty, unit, line total, "Label preview" link on shipping lines; Retail, Services, Subtotal, Tax,
  bold Total); below, three cards side by side "Add retail item", "Add shipping" (type, weight, dimensions, zone,
  service level), "Add money order" (with "Amounts above 300.00 need a passed identity verification."); then
  "Identity verification" card (document type, number) and "Take payment" card (method). Manager sees Void/Refund
  with a required reason. After payment show "Identity: Passport ****4567 – Passed" and the reversal reason.
- Buttons: red filled primary, red outline for Void/Refund/Deactivate (confirm step requiring the reason).
  Flash messages as green/red alert bars at the top (`aria-live`).
- Error/403 page: centred white card "403 – Access denied", one sentence, Trace ID, Correlation ID, "Back to the start".
- Login: centred card with brand title, email + password, one generic error, demo hint only in Development.
- Label: print-friendly (`@media print` hides top bar), reference "APX-XXXXXXXX-1", from/to, weight, service.
- Accessibility: labels on all inputs, visible focus, AA contrast, keyboard usable, responsive down to 375px.
- Partials: `_StatusPill`, `_Money`, `_EmptyState`, `_KpiCard`, `_Flash`, `_BranchFilter`. No business logic in Razor.

## 12. Keep it lean and easy to understand
- Nothing unnecessary: no file, class, interface, package, config key or page without a clear use. An interface
  exists only if it has a second implementation (test fake or future database). Delete template/dead code.
- Root files are only those in section 3. Docs: README.md, DESIGN.md (ADRs inside), AUDIT-SAMPLE.json.
  One helper script: `setup-secrets.bat` (asks for demo password + API key, stores them with user-secrets).
- Clear names (`CheckoutService.PayAsync`), short methods (< 30 lines), folders as in section 3.
- `/// <summary>` only where the purpose is not obvious. README has "How the code is organised".

## 13. Documentation (structure like the reference, correct formatting)
- README: title, one-paragraph intro, Solution layout table, Run it (setup-secrets.bat or commands, run, test),
  Demo accounts table (role, email, home branch, can do), Features, Sample requests table (curl + expected status),
  Configuration, How the code is organised, Screenshots (embedded), Test results (real counts), Known limitations.
- DESIGN: 1 Overview · 2 Architecture (diagram) · 3 Domain model (class diagram) · 4 Middleware pipeline and order
  (diagram + table) · 5 Filters versus middleware · 6 ADRs (ADR-1…ADR-9, each **Context / Options / Decision /
  Consequences**) · 7 SOLID mapping · 8 async, Task.WhenAll, SemaphoreSlim, ValueTask + measured numbers ·
  9 C# 14 features · 10 Security and audit · 11 DI lifetimes · 12 Path to a database · 13 Known limitations ·
  Decisions index (the 8 decisions of the brief -> section links).
- Plain Markdown and Mermaid only (no backslash escapes, no blank lines inside tables). Docs describe only what
  exists; every referenced path exists; preview on GitHub before merging.


## WEEK 3 — SQL Server + EF Core 10 (brief: docs/lab/week3-brief.md)

### W3.1 Rules
- Domain references nothing. Application defines ports. Infrastructure implements them with EF Core. Web is the composition root.
- Packages: only Microsoft.EntityFrameworkCore.SqlServer and .Design (Infrastructure), plus the existing allowed ones. Use SqlClient only through the transitive reference.
- Async everywhere, with a CancellationToken. No .Result, .Wait() or Thread.Sleep. No string-concatenated SQL. Never call EnsureCreated().
- DbContext, repositories and the unit of work are Scoped. Repositories stage changes only and never call SaveChanges. No repository returns IQueryable.
- Connection string: user-secrets "ConnectionStrings:ApexPostDb". Never commit it.
- Do NOT edit any Week 2 test file in ApexPost.UnitTests/Domain, /Rules or /Services. Only the fakes in /Support may change.
- Never invent performance numbers. Every number must come from real sqlcmd/SSMS output saved under docs/perf/.

### W3.2 Infrastructure layout
Persistence/ApexPostDbContext.cs
Persistence/Configurations/<Entity>Configuration.cs    (one IEntityTypeConfiguration per entity)
Persistence/Converters/  MoneyConverter, SkuConverter, BranchCodeConverter, WeightConverter
Persistence/Records/     IdentityVerificationRecord, ShipmentRecord, MoneyOrderRecord, StockAdjustmentRecord, AuditEntryRecord
Persistence/Mappers/     one mapper per record (domain <-> record)
Persistence/Interceptors/AuditColumnsInterceptor.cs    (sets CreatedAtUtc/CreatedBy/ModifiedAtUtc shadow props via TimeProvider + ICurrentUser)
Persistence/Migrations/
Persistence/Seeding/DevelopmentSeeder.cs
Repositories/  UnitOfWork.cs, Queries/ (query services), Sql/SqlAuditStore.cs
DependencyInjection.AddSqlPersistence(IConfiguration)

### W3.3 Tables (17) and mapping
Branches, Counters, Staff, Roles, Products, ProductCategories, BranchStock, StockAdjustments, ShippingZones, ShippingRates,
Shipments, MoneyOrders, IdentityVerifications, CounterTransactions, TransactionLines, Payments, AuditEntries.
- Rich domain entities mapped directly with the Fluent API (private setters, backing fields): Branch, Counter, Staff, Role,
  Product, ProductCategory, BranchStock, CounterTransaction, TransactionLine, Payment, ShippingZone, ShippingRate.
- Separate persistence records + mappers: IdentityVerification (masked storage, the ADR example), Shipment and MoneyOrder
  (stored as 1:1 detail rows keyed by TransactionLineId), StockAdjustment and AuditEntry (append-only, no domain entity).
- Owned types: Address (Branch), Dimensions (Shipment).
- Shadow properties: RowVersion (rowversion) on BranchStock and CounterTransactions. CreatedAtUtc, CreatedBy and ModifiedAtUtc on every table.
- Types: decimal(18,2) for money; datetime2 UTC for times; date for business dates; varchar for codes, SKUs, statuses and emails; nvarchar for names.
  Enums are stored as varchar(20) with CHECK IN (...).
- Identity document: DocumentLast4 varchar(4) NOT NULL, CHECK LEN = 4. The domain already rejects full numbers.
- Constraints: UNIQUE on BranchCode, Sku and Staff.Email. CHECK: quantities >= 0, Amount > 0, allowed Status values, Weight > 0 and
  <= max. DEFAULTs: CreatedAtUtc = SYSUTCDATETIME(), Status defaults.
- Delete behaviour: Restrict on every business FK (history is never cascaded away). Cascade only parent -> owned children
  (CounterTransaction -> TransactionLines/Payments). Explain each in ADR-010.

### W3.4 Indexes (marks: model-level evidence)
- Every index on a Records/* class is declared with the [Index(..., Name = "IX_...")] attribute on the class.
- Indexes on directly mapped domain entities (BranchStock, CounterTransactions) use HasIndex in their Configuration, because Domain must not reference EF.
- INCLUDE and filter parts: same index name, completed in the Configuration with IncludeProperties/HasFilter.
- PERFORMANCE.md has an "Index Catalogue" table: Index | Table | Keys | Include | Filter | Declared in (file:line) | Migration.

### W3.5 Patterns
- IUnitOfWork: SaveChangesAsync(ct) and ExecuteInTransactionAsync(Func<CancellationToken,Task>, ct)
  (execution strategy + BeginTransactionAsync). One commit per use case.
- Repositories: IBranchRepository, IProductRepository, IStockRepository, ICounterTransactionRepository, IStaffRepository,
  IMoneyOrderRepository, IIdentityVerificationRepository. IAuditStore is separate.
- Reads: query services (AsNoTracking projections into DTOs, or stored procedures). Writes: command services (repositories + IUnitOfWork).
  One database, no separate read store; state this in DESIGN.md.
- Exceptions: Infrastructure translates DbUpdateConcurrencyException -> ConcurrencyConflictException and SqlException
  2601/2627 -> DuplicateKeyException (both Application types). Web maps 409/409/404 (NotFound)/400 (Domain) to Problem Details.
- Audit: SqlAuditStore uses its own short-lived DbContext (IDbContextFactory registered Scoped), commits separately AFTER the
  action and never throws into the business flow (it logs instead). See ADR-016.

### W3.6 ADRs (continue numbering in DESIGN.md)
010 schema + delete behaviours, 011 index choices with plan evidence, 012 stored procedures vs LINQ,
013 repository/UoW boundaries + where direct projections are allowed, 014 domain vs persistence separation,
015 concurrency strategy (rowversion + CHECK + 409), 016 audit store vs unit of work, 017 [Index] attribute vs Fluent placement.


# CLAUDE.md – ApexPost Logistics, Week 4

Standing instructions for every Claude Code session in this repository. Read this file and
`docs/WEEK4-CONTEXT.md` before doing anything else.

## 1. Repository facts

- Repository: `imsashantnicky/Apex_Post_Logistic`. Solution file: `Apex_Post_Logistic.slnx`.
- Base branch for Week 4 is `main`. (`chore/setup` holds the Week 3 history and was merged into `main`.)
- Build settings come from `Directory.Build.props`: `net10.0`, C# 14, nullable, implicit usings,
  `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-recommended`, NuGet audit on. Do not weaken these globally.
- SDK pinned in `global.json` (10.0.100, rollForward latestFeature).
- Tests: xUnit v3 (`xunit.v3.mtp-off`), `Microsoft.NET.Test.Sdk`. Integration tests use the SQL Server container
  from CI through the `ConnectionStrings__ApexPostTestServer` environment variable.
- CI: `.github/workflows/ci.yml` on `ubuntu-latest` builds and tests `Apex_Post_Logistic.slnx` in Release with
  `--filter "Category!=Measurement"`.

## 2. Week 4 goal (courier offline → API → mobile app)

| Part | Integration branch | What it delivers |
| --- | --- | --- |
| A | `week4-partA` | Couchbase Lite offline scan store (`ApexPost.Courier.*`), outbox with a fake uploader, console harness, `OFFLINE-DESIGN.md` |
| B | `week4-partB` | `ApexPost.Api` REST API: shipments, status lifecycle, idempotent scan upload, ProblemDetails, global exception handler |
| C | `week4-partC` | Built-in OpenAPI, Swagger UI (Development only), XML docs, v1 (deprecated) / v2 versioning, `/contracts`, NSwag `ApexPost.Client` + console consumer |
| D | `week4-partD` | JWT + roles, partner API key, HTTPS/HSTS, `ApexPost.Courier` MAUI app with typed client, Polly resilience, real uploader and auto-sync |

## 3. Project layout

| Path | Type | Rule |
| --- | --- | --- |
| `src/ApexPost.Domain`, `src/ApexPost.Application`, `src/ApexPost.Infrastructure`, `src/ApexPost.Web` | Week 3 | Do not change existing files (see section 4) |
| `tests/ApexPost.UnitTests`, `tests/ApexPost.IntegrationTests`, `sql/` | Week 3 | Do not change existing files |
| `src/Courier/ApexPost.Courier.Domain` | Part A class library | NO NuGet packages, never references Couchbase |
| `src/Courier/ApexPost.Courier.Offline` | Part A class library | Only project that references `Couchbase.Lite` (3.4.x) |
| `src/Courier/ApexPost.Courier.Harness` | Part A console app | Menu: Add Scan, Find by Tracking Number, List Pending, Simulate Sync, Delete Scan, Exit (+ extras) |
| `src/ApexPost.Api` | Part B/C/D Web API | Minimal hosting in `Program.cs`, no `Startup.cs` |
| `src/ApexPost.Client`, `src/ApexPost.Client.Consumer` | Part C | Generated code is never edited by hand |
| `src/Mobile/ApexPost.Courier` | Part D MAUI app | Lives in `ApexPost.Mobile.slnx`, NOT in `Apex_Post_Logistic.slnx` (ubuntu CI cannot build MAUI) |
| `contracts/v1.json`, `contracts/v2.json` | Part C | Updated only by `scripts/export-contracts.*` |

## 4. Hard rules (never break these)

1. **Week 3 is frozen.** Do not edit existing Week 3 files. Week 4 may only ADD new files, folders, projects and
   new service methods. New tracking data (shipments with `APX-########`, status lifecycle, scans) lives in a separate,
   additive tracking module (`Domain/Tracking`, `Application/Tracking`, `Infrastructure/Tracking` with its own
   `TrackingDbContext`, migrations folder and history table `__TrackingMigrationsHistory`). Never touch
   `ApexPostDbContext` or its migrations.
2. Shared files that MAY change: `Apex_Post_Logistic.slnx`, `.gitignore`, `.github/workflows/ci.yml`, `README.md`,
   `docs/`, and new root documents (`OFFLINE-DESIGN.md`, `API-NOTES.md`, `VERSIONING.md`, `SECURITY.md`, `RESILIENCE.md`).
3. **No secrets in the repository**: no connection strings, signing keys, API keys, tokens or production URLs in
   committed files. Use `dotnet user-secrets`. `appsettings.json` contains no connection string.
4. **Fictional data only** (couriers `CR-101`, `CR-102`; tracking numbers like `APX-10000001`; emails `@apexpost.example`).
5. Never disable certificate validation in Release. The MAUI localhost bypass is allowed only under `#if DEBUG` and only
   for `localhost` / `10.0.2.2`.
6. Never log tokens, API keys or full request bodies.
7. Hiding an endpoint from the documentation is not security. Protect it with authentication or authorization instead.

## 5. Shared contracts between parts (keep them identical everywhere)

- Tracking number: `^APX-\d{8}$`. Courier id: `^CR-\d{3}$`. Both live in `ScanRules` (Courier.Domain) and are reused.
- Scan document id: `scan::{Guid}`. It is the idempotency key for the Part B upload.
- Scan document fields (exact JSON names): `trackingNumber`, `scanType`, `courierId`, `location`, `scannedAtUtc`, `notes`,
  `syncStatus`. Enums are stored as strings. `scannedAtUtc` is ISO-8601 UTC (`"O"` format).
- `scanType`: Pickup, InTransit, Delivered, Failed. `syncStatus`: Pending, Synced, Failed.
- Shipment status lifecycle: Created → PickedUp → InTransit → OutForDelivery → Delivered | Failed. Forward moves only.
- Scan → status mapping: Pickup→PickedUp, InTransit→InTransit, Delivered→Delivered, Failed→Failed.
- Every new scan is saved locally first as `Pending`. Only the outbox changes `syncStatus`.
- `IShipmentUploader` is the port between the courier device and the API: `FakeShipmentUploader` in Part A,
  `HttpShipmentUploader` in Part D. The interface does not change. Batches use a separate port method.
- Scan upload response: `{ accepted, duplicatesSkipped, rejected: [{ scanId, reason }] }`. Accepted and duplicate scans
  become `Synced`; rejected scans become `Failed`.
- Couchbase SQL++: values ONLY through `Parameters` (`$name`). Never concatenate or interpolate into query text.

## 6. API conventions (Parts B–D)

- `[ApiController]`, attribute routing, route constraint `{trackingNumber:regex(^APX-\\d{{8}}$)}`.
- Controllers are thin: bind input → call a service → return a result. All business rules live in services.
- Request DTOs only (`CreateShipmentRequest`, `UpdateShipmentRequest`, `UpdateStatusRequest`, `ScanUploadRequest`);
  never bind to EF entities; explicit mapping classes.
- Status codes: 200 read/update, 201 + `CreatedAtAction` Location, 204 delete, 400 ValidationProblemDetails,
  404 unknown tracking number, 409 duplicate / invalid transition / delete when not Created, 415 wrong content type,
  500 through the global `IExceptionHandler`. Never 200 for an error. Every error is ProblemDetails; stack traces in Development only.
- Paging: `pageSize` default 20, maximum 50.
- OpenAPI: built-in `AddOpenApi` / `MapOpenApi` only; Swagger UI via `Swashbuckle.AspNetCore.SwaggerUi` only
  (never `AddSwaggerGen`). Both Development only.
- Every action: XML comments, `[ProducesResponseType]` for every code, `[Tags]`, `[EndpointSummary]`, a unique stable route name
  (v2 names without suffix, v1 names end in `V1`).
- Versioning: URL segment `/api/v{version:apiVersion}/...`, v1 `Deprecated = true` and unchanged from Part B
  (`/api/shipments` stays as a v1 alias), v2 with nested `recipient`, `latestScan` and string `status`.
  `ReportApiVersions = true`. Both versions share the same services.
- Security (Part D): Courier role for scan upload and status PATCH, Admin for DELETE, any authenticated user for GET.
  A courier can upload only scans whose `courierId` matches the token claim. 401/403 as ProblemDetails.
  Partner route uses `X-Api-Key`.

## 7. Git workflow (mandatory)

- Integration branch per part: `week4-partX`, created from the latest `main` and pushed once.
- Step branches: `week4-partX-NN-short-name` created from the integration branch. Use a HYPHEN, never a slash
  (git cannot hold both `week4-partA` and `week4-partA/...`).
- **Before starting a step:** `git fetch origin`, confirm the previous step PR is merged into the integration branch,
  then `git checkout week4-partX && git pull origin week4-partX && git checkout -b week4-partX-NN-name`.
  If the previous PR is not merged, STOP and tell the user.
- **When a step is done:**
  1. `dotnet build Apex_Post_Logistic.slnx -c Release` and
     `dotnet test Apex_Post_Logistic.slnx -c Release --filter "Category!=Measurement"` pass
     (Part D also builds `ApexPost.Mobile.slnx` when the MAUI workload is available).
  2. `git diff origin/main --stat` lists no changed Week 3 file.
  3. Update the Progress log in `docs/WEEK4-CONTEXT.md`.
  4. Stage only the relevant files. Commit with a Conventional Commit message (`feat(scope): ...`, `test`, `docs`, `chore`).
  5. `git push -u origin <step-branch>`.
  6. Write the PR description to `.git/PR_BODY.md` following section 7a, then run
     `gh pr create --base week4-partX --head <step-branch> --title "<title>" --body-file .git/PR_BODY.md`.
     If `gh` is unavailable or not authenticated, print
     `https://github.com/imsashantnicky/Apex_Post_Logistic/compare/week4-partX...<step-branch>?expand=1`
     and print the full title and description so the user can paste them.
  7. **STOP. Print the PR URL, the title and a 3-line summary. Do not continue to the next step.**
- **NEVER merge anything.** Never run `gh pr merge`, never `git merge` into `main` or any `week4-*` branch, never push
  directly to `main` or an integration branch, never force-push shared branches. The user merges every PR manually on GitHub.
- Final PR of a part (`week4-partX` → `main`) is created only when the user says "final PR X". It lists all step PRs and
  the marking checklist of that part. The user merges it with "Create a merge commit".

## 7a. Pull request title and description (mandatory format)

The user does not write PR text and only reviews and merges, so every PR must explain itself completely.
Write in plain, simple English. Describe what is actually in the diff; never claim something that was not done or tested.

**Title:** `Week4 Part<X> – Step <NN>: <short description>`
(final PR of a part: `Week 4 Part <X> – <part name>`). Keep it under 72 characters.

**Description template** (fill every section; write "None" where a section does not apply):

    ## Summary
    One or two sentences: what this step adds and why it is needed for Week 4 Part <X>.

    ## Changes
    | Area | File(s) | What changed |
    | --- | --- | --- |
    | <project/folder> | `path/to/File.cs` | <one line per meaningful change> |

    ## Requirement coverage
    | Brief requirement | Where it is implemented | Marks |
    | --- | --- | --- |
    | <quote the requirement from the brief> | `File.cs` / method | <criterion and marks> |

    ## How it was tested
    - Commands run: `dotnet build ... -c Release`, `dotnet test ... --filter "Category!=Measurement"`
    - Result: <number of tests passed / failed, build warnings = 0>
    - New tests: <test names and what each proves>
    - Manual checks: <harness menu actions, .http requests, Swagger "Try it out", app screens>

    ## Week 3 safety check
    - `git diff origin/main --stat`: no existing Week 3 file changed (list any shared file that changed: .slnx, .gitignore, ci.yml, README, docs)
    - No secrets, connection strings or real data added

    ## Design decisions
    Short bullets on choices a reviewer might question (e.g. additive TrackingDbContext, why 400 for pageSize > 50).

    ## Screenshots / evidence the user must add
    - [ ] <exact screenshot, the action that produces it, and the target path under docs/screenshots/week4-part<X>/>

    ## Follow-ups
    What the next step builds on this, or known limitations.

    ## How to review and merge
    1. Check the Files changed tab and the CI result.
    2. Merge on GitHub (step PR: any merge method; final PR into main: "Create a merge commit").
    3. After merging, tell Claude to start the next step.

**Final PR of a part (`week4-partX` → `main`)** also contains: a table of every step PR (number, title, link),
the full marking checklist of that part with a ✅ and the implementing file for each item, the list of deliverables
with their paths, and the screenshot list with ✅ for those already committed.

**Commit messages** follow the same honesty rule: a Conventional Commit subject (≤ 72 characters) plus a body of
2–5 lines explaining what and why.

## 8. Definition of done for every step

- Code compiles with zero warnings. New behaviour has tests.
- Public members have XML doc comments.
- No secrets, no real data, no Week 3 changes.
- The PR title and description follow section 7a: every section filled, the requirement coverage table maps the
  work to the marking criteria, and the screenshot checklist tells the user exactly what to capture.

## 9. Marking checklists (keep every item covered)

- **A (25):** database lifecycle and document model 5 · repository and key-value CRUD 5 · SQL++ queries, parameters, indexes 6 ·
  outbox / offline strategy 4 · change listener and cleanup 3 · `OFFLINE-DESIGN.md` 2.
- **B (25):** endpoints and verbs 6 · status codes and Location 5 · DTOs and validation incl. custom rule 6 ·
  ProblemDetails and global handler 4 · thin controllers, DI, idempotent scan upload 4.
- **C (25):** OpenAPI and document transformer 4 · Swagger UI, Development only, README explanation 3 ·
  XML comments, response types, tags, operationIds 5 · v1/v2 with deprecation 6 · version-specific documents 3 ·
  generated SDK and console consumer 4.
- **D (25):** JWT and roles incl. courierId check 5 · partner API key and HTTPS/HSTS 3 · `SECURITY.md` 2 ·
  typed client and auth handler 4 · Polly retry, circuit breaker, timeout with logging 5 · JSON and ProblemDetails 2 ·
  end-to-end offline sync and SecureStorage 4.

## 10. Deliverables to keep track of

- Branches `week4-partA`, `week4-partB`, `week4-partC`, `week4-partD` merged into `main`.
- `OFFLINE-DESIGN.md`, `API-NOTES.md`, `README.md` (OpenAPI vs Swagger UI explanation), `VERSIONING.md`, `SECURITY.md`, `RESILIENCE.md`.
- `src/ApexPost.Api/ApexPost.Api.http`: a success and a failure request per endpoint for v1 and v2, plus 401, 403,
  an invalid API key and a valid token.
- `/contracts/v1.json`, `/contracts/v2.json`, `nswag.json` and the generation script.
- Screenshots under `docs/screenshots/week4-partX/` (the user takes them; list what is needed in each PR).
- Part D demo video (3–5 minutes) following `docs/DEMO-SCRIPT.md`.



---

# WEEK 5 — Test Strategy & Automated Quality (brief: docs/lab/week5-brief.md) — ACTIVE

Week 5 is the active week. This section OVERRIDES every earlier rule it conflicts with (list in W5.0).
Read this section and `docs/WEEK5-CONTEXT.md` before every Week 5 task.

## W5.0 Earlier rules that Week 5 overrides

| Earlier rule | Week 5 rule |
| --- | --- |
| Week 2 §2 "Allowed libraries only … no Moq / FluentAssertions / others" | Test projects may also use **Moq**, **Reqnroll** (xUnit v3 integration), `Microsoft.Extensions.TimeProvider.Testing`, `coverlet.collector` and the `dotnet-reportgenerator-globaltool` tool. Production projects keep the old rule. FluentAssertions/NSubstitute stay forbidden. |
| Week 2 §10 step 4 "`gh pr merge --merge --delete-branch`" / "`git merge --no-ff`" | **Never merge anything** (W5.9). The user merges every PR on GitHub. |
| Week 2 §10 step 3 "0 skipped" | Locally, SQL tests may be skipped ONLY when no SQL Server is reachable, with a clear `Assert.Skip` message. In CI: 0 skipped. |
| Week 2 §3 flat folders at the root | The real layout is `src/…` and `tests/…` (see W5.2). |
| W3.1 "Do NOT edit any Week 2 test file in ApexPost.UnitTests/Domain, /Rules or /Services" | Existing tests may be kept / modified / moved / merged / removed / replaced, but ONLY with a row in the review table of `TESTING-STRATEGY.md`. |
| Week 4 §3 and §4.1 "Week 3 is frozen", "tests/… do not change existing files" | Same as above for tests. Production code may change ONLY for testability or the TDD rule (W5.4), recorded in `TDD-LOG.md` or `TESTING-STRATEGY.md`. Existing behaviour must not change. |
| Week 4 §7/§7a branch names and PR titles `week4-partX…` / `Week4 Part<X>…` | Branches `week5-testing` and `week5-testing-NN-name`; titles `Week5 – Step NN: …` (W5.9). |
| "The brief in docs/lab/week2-brief.md wins" | `docs/lab/week5-brief.md` wins for Week 5. |

## W5.1 Repository state at the start of Week 5

- All of Week 4 (Parts A–D) was merged into `chore/setup` (default branch), NOT into `main`; `main` was 70 commits
  behind. Step W0 opens a PR `chore/setup` → `main`. Week 5 starts only after the user merged it.
- Existing tests: 9 projects, about 1,134 `[Fact]/[Theory]`, all xUnit v3; about 56% unit-level / 44% integration-level.
  No Moq, no SQLite, no Reqnroll. Only 4 classes carry a `Category` trait (Measurement).
- Known review findings to decide on: SQL Server tests inside `ApexPost.Tracking.UnitTests`
  (`Infrastructure/TrackingSqlTests`, `TrackingModelTests`); overlap between `tests/Courier/ApexPost.Courier.UnitTests`
  and `tests/ApexPost.Courier.Offline.Tests`; tests using `DateTime.UtcNow` (e.g. `NotInFutureAttributeTests`,
  `AutomaticValidationTests`, `AuthorizationTests`, `DevTokenTests`, `Api.Tests/Support/ApiClient.cs`, Week 3 SQL tests);
  sentence-style test names instead of `Method_Scenario_ExpectedResult`.
- The brief's TDD examples already exist (forward-only transitions in `ShipmentStatusRules`,
  `TrackingLimits.MaximumScansPerUpload = 100`, `TrackedShipment.MaximumWeightKg = 30`), so they are NOT used for TDD.

## W5.2 Scope and test project structure

- In scope: domain and application services, validation rules, repositories (integration level), Web API
  controllers/endpoints (`src/ApexPost.Api`), typed API client (`src/Mobile/ApexPost.Courier.ApiClient`),
  Couchbase Lite outbox and repository (`src/Courier/ApexPost.Courier.Offline`).
- Out of scope: MAUI UI, pages, navigation, browser/front-end testing. `ApexPost.Courier.Presentation.Tests` is kept only
  for plain view-model logic; it is not extended.
- Structure (documented equivalent of the brief's three projects):

| Level | Projects |
| --- | --- |
| Unit | `tests/ApexPost.UnitTests`, `tests/ApexPost.Tracking.UnitTests`, `tests/Courier/ApexPost.Courier.UnitTests`, `tests/Mobile/ApexPost.Courier.ApiClient.Tests`, `tests/Mobile/ApexPost.Courier.Presentation.Tests` |
| Integration | `tests/ApexPost.IntegrationTests`, `tests/ApexPost.Api.Tests`, `tests/ApexPost.Courier.Offline.Tests` (real embedded Couchbase), plus SQL tests moved out of unit projects |
| BDD | `tests/ApexPost.Specs` (new, Reqnroll) |

  If a step merges or renames projects, update this table, the `.slnx` and the review table.

## W5.3 Test rules (every new or modified test)

- **Framework:** xUnit v3 everywhere (consistency with ~1,100 existing tests, new instance per test, Theories,
  Reqnroll support). Do not add NUnit.
- **AAA:** `// Arrange`, `// Act`, `// Assert` comments in every new/modified test.
- **Naming:** `Method_Scenario_ExpectedResult` for every new test and every test class touched in Week 5.
  Untouched older tests keep their sentence-style names; `TESTING-STRATEGY.md` explains why (mass renaming adds risk, no value).
- **Traits:** `[Trait("Category", "Unit" | "Integration" | "Spec" | "Measurement")]` on EVERY test class.
- **Parameterized:** `[Theory]` + `[InlineData]` / `[MemberData]` / `TheoryData<>` for rules with several cases
  (tracking number format, weight boundaries, full 6×6 status-transition matrix, page/pageSize/upload-size limits).
- **Exceptions and boundaries:** assert the exact exception type and message/parameter; test at, below and above every limit.
- **Deterministic:** no `DateTime.Now/UtcNow` / `DateTimeOffset.UtcNow` in tests or code under test (use `TimeProvider` /
  `FakeTimeProvider` or fixed fictional times); no test-order dependency; no static mutable state; unique temp folders
  and throw-away databases per fixture.
- **No real network:** unit tests use a fake or Moq `HttpMessageHandler`; API tests use `WebApplicationFactory` in-process;
  no emails, no external hosts, no production databases.
- **Fictional data and no secrets:** `CR-101`, `APX-10000001`, `@apexpost.test`; test configuration or user-secrets only.

## W5.4 Mocking, isolation and data access

- **Moq** isolates service dependencies: `ITrackedShipmentRepository` (the brief's IShipmentRepository),
  `ITrackingQueries`, `ITrackingUnitOfWork`, `ICurrentCourier`, Courier `IScanRepository` / `IScanQueries`,
  `IShipmentUploader`. Use `Setup`, `Returns`/`ReturnsAsync`/`ThrowsAsync`, `It.Is<>`, `It.IsAny<>`, `Verify(..., Times.*)`.
- Hand-written fakes stay where a stateful fake is clearer (in-memory repositories for BDD). The strategy says when each is used.
- For every mocked interface, `TESTING-STRATEGY.md` states **what the mock does NOT prove**.
- **HTTP:** typed client tests cover 200 + JSON, 404, 500, timeout, caller cancellation, ProblemDetails and
  ValidationProblemDetails, with a fake handler and at least one `Moq.Protected()` `SendAsync` setup + `Verify`.
- **Never mock `DbContext` / `DbSet`.** Repositories and query logic run against a throw-away **SQL Server** test database:
  CI env var `ConnectionStrings__ApexPostTestServer` → user-secrets → LocalDB `(localdb)\MSSQLLocalDB` on Windows →
  `Assert.Skip("SQL Server not available: …")`. One shared helper does this. SQLite is NOT used; the strategy explains why
  (rowversion, unique/filtered indexes, stored procedures, `datetime2`), and why mocking EF Core gives false confidence.
- **API end to end:** at least one test with `WebApplicationFactory<Program>` + a test database + a test authentication
  setup (`TestAuthHandler` scheme), flow create → get → upload scans → history, plus 401 and 403.

## W5.5 TDD rule

- Rule: **"The server rejects an uploaded scan whose `scannedAtUtc` is more than 72 hours before the upload time, reason
  'Scan is older than 72 hours.'; exactly 72 hours is accepted; the limit is configurable (`TrackingOptions.MaxScanAgeHours`,
  default 72)."** Confirm first that it does not exist.
- Commits, in order, each pushed: `RED:` (compiles, fails on an ASSERTION, never a compile error) → `GREEN:` (minimal code)
  → boundary `RED:`/`GREEN:` (or `TEST:` if the boundary already passes, stated honestly) → `REFACTOR:` (options,
  `ScanAgePolicy`, constructor injection of `IOptions<TrackingOptions>`, no behaviour change).
- `TDD-LOG.md`: requirement, first failing test (code + real failing output), minimal implementation, refactoring,
  production code changed for testability and why, commit list with hashes.
- The TDD PR is merged with **"Create a merge commit"** (no squash/rebase).

## W5.6 BDD (Reqnroll)

- `tests/ApexPost.Specs` with Reqnroll's xUnit v3 integration. If xUnit v3 is not supported, STOP and ask before a fallback.
- `Features/ShipmentCreation.feature` at **API level** (business outcome is the HTTP answer; validation lives in the API DTOs):
  valid creation, duplicate tracking number, Scenario Outline for invalid weights.
- `Features/ScanSync.feature` at **service level** (real `OutboxService` in a temp Couchbase folder + real
  `ShipmentTrackingService` with in-memory repositories through an in-process uploader; `FakeTimeProvider`):
  offline scans uploaded, duplicate skipped, invalid rejected, Scenario Outline for scan age (71 h / 72 h / 73 h).
- At least one Background and one Scenario Outline with Examples. Business language only (no URLs, JSON or class names).
- State through context injection (per-scenario context classes in step constructors), never static fields.
  `[BeforeScenario]` / `[AfterScenario]` hooks create and clean up factory, database, temp folder and time.
- No SpecFlow exists in the repo; state "nothing to migrate" in the strategy.

## W5.7 Copilot (honesty rule)

- The USER runs GitHub Copilot. Claude prepares prompts and reviews output, but NEVER writes Copilot output itself or
  pretends to be Copilot.
- Raw output is saved unchanged in `docs/copilot/raw/`. `COPILOT-LOG.md`: each prompt verbatim, what Copilot produced,
  every generated test kept / changed / rejected with the reason, at least two mutation proofs (break production code,
  failing output in `docs/copilot/evidence/`, restore, passing output). Broken code is never committed.
- Copilot-assisted classes start with:
  `// Copilot-assisted: drafted by GitHub Copilot, reviewed and edited by a human (see COPILOT-LOG.md).`

## W5.8 Commands

- Build: `dotnet build Apex_Post_Logistic.slnx -c Release` → 0 warnings, 0 errors.
- All tests: `dotnet test Apex_Post_Logistic.slnx -c Release --filter "Category!=Measurement"`.
- By level: `--filter "Category=Unit"` / `"Category=Integration"` / `"Category=Spec"`.
- Evidence: `--logger "trx;LogFileName=week5.trx" --logger "console;verbosity=normal"` → `docs/testing/test-run.txt`.
- Coverage (optional): `--collect:"XPlat Code Coverage"` + `reportgenerator` → `docs/testing/coverage/`.

## W5.9 Git workflow for Week 5 (the only valid one)

- Integration branch `week5-testing` from the latest `main` (pushed once). Step branches `week5-testing-NN-name` (hyphen).
- Before a step: `git fetch origin`; confirm the previous step PR is merged into `week5-testing` (if not, STOP and say so);
  `git checkout week5-testing && git pull origin week5-testing && git checkout -b week5-testing-NN-name`.
- During a step: 3–8 small Conventional Commits (TDD: `RED:` / `GREEN:` / `REFACTOR:`); subject ≤ 72 chars + 2–5 line body.
- After a step: build + full suite (W5.8); update the Progress log in `docs/WEEK5-CONTEXT.md`; stage only relevant files;
  `git push -u origin <step-branch>`; write the PR text to `.git/PR_BODY.md` (W5.10);
  `gh pr create --base week5-testing --head <step-branch> --title "<title>" --body-file .git/PR_BODY.md`.
  Without gh: print `https://github.com/imsashantnicky/Apex_Post_Logistic/compare/week5-testing...<step-branch>?expand=1`
  and the full title and description.
- Then STOP: print the PR URL, the title and a summary of at most 5 lines (what changed, build result,
  tests passed/failed/skipped).
- **NEVER merge:** no `gh pr merge`, no `git merge` into `main`, `chore/setup` or `week5-testing`, no direct push to them,
  no force-push. The final PR `week5-testing` → `main` is created only when the user says "final PR week5".
  The TDD PR and the final PR are merged by the user with "Create a merge commit".

## W5.10 PR title and description

Title: `Week5 – Step <NN>: <short description>` (final: `Week 5 – Test strategy & automated quality`), < 72 chars.
Plain, simple English; claim only what the diff contains and what was really run. Fill every section ("None" if empty):

    ## Summary
    ## Changes                     (table: Area | File(s) | What changed)
    ## Requirement coverage        (table: Brief requirement | Where implemented | Marks)
    ## How it was tested           (commands, real passed/failed/skipped, new tests and what each proves)
    ## Existing tests affected     (review-table rows added/changed: class, decision, reason)
    ## Production code changed     (testability or TDD only, with reason)
    ## Safety check                (no secrets/real data, no unrecorded behaviour change, shared files changed)
    ## Design decisions
    ## Screenshots / evidence the user must add   (checklist with exact path under docs/screenshots/week5/)
    ## Follow-ups
    ## How to review and merge     (Files changed + CI; merge method; then tell Claude to start the next step)

The final PR also lists every step PR (number, title, link), the 100-mark checklist with ✅ and the implementing file
per item, all deliverables with paths and the screenshot list.

## W5.11 Definition of done (every Week 5 step)

- 0 build warnings; full suite run with real counts; CI-equivalent run has 0 failed.
- Every changed or removed existing test has a review-table row; every production change is recorded.
- New tests follow W5.3; no secrets, no real data, no network, no `DateTime.Now`.
- `TESTING-STRATEGY.md` stays true to the code (pyramid numbers, review table, data-access strategy).
- PR created per W5.9/W5.10, then STOP.

## W5.12 Marking checklist (100) and deliverables

- Strategy document (risk map ≥ 5, pyramid + proportions, framework justification, existing-test review, data access) 20
- Unit tests (AAA, naming, parameterized ≥ 2 rules, boundaries, exceptions, Test Explorer + dotnet test, README command) 15
- Mocking & isolation (Moq interfaces, HTTP handler cases, no DbContext mocks, one WebApplicationFactory test + test auth) 20
- TDD (RED/GREEN/REFACTOR commits on main, TDD-LOG.md, testability improvements) 15
- BDD (business language, Background, Scenario Outline, Reqnroll bindings, hooks, context injection, level stated) 15
- Copilot (prompts, kept/changed/rejected decisions, ≥ 2 mutation proofs, class comments) 10
- Reflection (~half page) and viva 5

Deliverables: `week5-testing` merged into `main` with the TDD history; unit/integration/spec projects (W5.2);
`TESTING-STRATEGY.md`, `TDD-LOG.md`, `COPILOT-LOG.md`; README "Running the tests" with the real summary;
`docs/testing/test-run.txt` and screenshots in `docs/screenshots/week5/`.