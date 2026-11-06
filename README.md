# Apex_Post_Logistic

ApexPost Counter Platform is an ASP.NET Core MVC application (.NET 10, C# 14) for postal counters. One counter sells
retail goods together with parcel and letter shipping, money orders and identity verification in a single basket. A
clerk builds the basket and takes payment, a Branch Manager voids or refunds, an Auditor reads reports and the audit
log, and an Administrator manages staff and branches. Data is kept in SQL Server through EF Core 10, behind the
repository and query ports of the Application project.

The design and its reasoning are in [DESIGN.md](DESIGN.md). A sample of the audit log from a real run is in
[AUDIT-SAMPLE.json](AUDIT-SAMPLE.json).

## Solution layout

| Path | Purpose | References |
| --- | --- | --- |
| `src/ApexPost.Domain` | Entities, value objects, business rules and events | Nothing |
| `src/ApexPost.Application` | Use cases, ports, reports, settings and validation | Domain |
| `src/ApexPost.Infrastructure` | EF Core storage, audit store, password hashing, cache, simulated partners, seed data | Application |
| `src/ApexPost.Web` | MVC front end, middleware, filters and the composition root | Application, Infrastructure |
| `tests/ApexPost.UnitTests` | Rules, services, infrastructure, layering scans and measurements | Domain, Application, Infrastructure |
| `tests/ApexPost.IntegrationTests` | The real application through `WebApplicationFactory` | Web |
| `sql` | Migration script, report queries, stored procedures, transaction and blocking examples, the database role and performance scripts | |
| `docs/perf` | Performance evidence: raw measurements, `.sqlplan` files, query output | |
| `docs/evidence` | Verification evidence and the [requirement checklist](docs/evidence/CHECKLIST.md) | |
| `docs/screenshots` | The workflows of a real run | |
| `.github/workflows/ci.yml` | Restore, Release build and tests on every push and pull request | |

## Prerequisites

| Tool | Version | Used for |
| --- | --- | --- |
| .NET SDK | 10.0.100 or later ([global.json](global.json)); verified on 10.0.402 | Build, test, run |
| SQL Server | Verified on SQL Server 2025 here and SQL Server 2022 in CI | `ApexPostDb` and the temporary test databases |
| `sqlcmd` | Any current version | Only for the SQL scripts |
| `dotnet-ef` | 10.0.14, from [dotnet-tools.json](dotnet-tools.json) | Migrations; `dotnet tool restore` installs it |

Your login must be allowed to create databases. On Windows, clone to a short path: see [Troubleshooting](#troubleshooting).

## Run it

Everything secret stays outside the repository, in user-secrets: the demo password, the partner API key and the
connection string.

### 1. Store the secrets

On Windows, run [setup-secrets.bat](setup-secrets.bat). It asks for the three values; press Enter at the last question
for `ApexPostDb` on this computer with Windows sign-in. Anywhere else:

```bash
dotnet user-secrets set "Seed:DemoPassword" "<a password of your choice>" --project src/ApexPost.Web
dotnet user-secrets set "Integration:ApiKeys:AcmeCouriers" "<a long random key>" --project src/ApexPost.Web
dotnet user-secrets set "ConnectionStrings:ApexPostDb" "<your connection string>" --project src/ApexPost.Web
```

A local connection string looks like
`Server=localhost;Database=ApexPostDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True`.

### 2. Create ApexPostDb

The application never migrates by itself. Either let EF Core create the database and its tables:

```bash
dotnet tool restore
dotnet ef database update --project src/ApexPost.Infrastructure
```

Or create it yourself and run the [idempotent script](sql/migrations/idempotent-migrations.sql), which is safe to run
again. The `-I` is required, because `sqlcmd` otherwise turns `QUOTED_IDENTIFIER` off:

```bash
sqlcmd -S localhost -E -C -Q "CREATE DATABASE ApexPostDb"
sqlcmd -S localhost -E -C -I -b -d ApexPostDb -i sql/migrations/idempotent-migrations.sql
```

After a change to the model, add a migration and regenerate the script:

```bash
dotnet ef migrations add <Name> --project src/ApexPost.Infrastructure --output-dir Persistence/Migrations
dotnet ef migrations script --idempotent --project src/ApexPost.Infrastructure -o sql/migrations/idempotent-migrations.sql
```

### 3. Build and test

```bash
dotnet restore Apex_Post_Logistic.slnx
dotnet build Apex_Post_Logistic.slnx -c Release
dotnet test Apex_Post_Logistic.slnx -c Release --filter "Category!=Measurement"
```

The integration tests need a SQL Server, but never touch `ApexPostDb`. They use
`ConnectionStrings:ApexPostTestServer` from the environment, or else the server of `ConnectionStrings:ApexPostDb`. Each
test class creates, migrates and seeds a temporary `ApexPostTests_<guid>` database and drops it afterwards.
`DisposableDatabaseTests` proves both.

### 4. Run

```bash
dotnet dev-certs https --trust
dotnet run --project src/ApexPost.Web --launch-profile https
```

Open <https://localhost:7242>. The sign-in cookie is `Secure`, so use the HTTPS address. The first start in Development
fills an empty database; it is skipped while `Branches` has rows. The demo accounts keep the password that was set at
that moment, so changing `Seed:DemoPassword` later needs a new seed.

### Optional: volume data and measurements

[sql/perf/volume-seed.sql](sql/perf/volume-seed.sql) multiplies the demo data for performance tests, to about 200k
transactions and 600k retail lines of fictional data. Run it once, on a separate database, never on one you care about.

```bash
sqlcmd -S localhost -E -C -I -d ApexPostDb_Perf -b -i sql/perf/volume-seed.sql
dotnet test tests/ApexPost.UnitTests -c Release --filter "Category=Measurement" --logger "console;verbosity=detailed"
```

The reports, stored procedures and performance work are explained in [PERFORMANCE.md](PERFORMANCE.md). The procedures are
deployed by the migrations. The role for the running application is in
[sql/security/least-privilege-role.sql](sql/security/least-privilege-role.sql).

## Troubleshooting

Each of these was met during verification, except where the table says otherwise.

| Symptom | Cause | Fix |
| --- | --- | --- |
| `Msg 1934` from `sqlcmd`, and the script stops | `sqlcmd` runs with `QUOTED_IDENTIFIER` off | Add `-I` to the command |
| `Unable to load DLL 'Microsoft.Data.SqlClient.SNI.dll'` ... `filename or extension is too long` | The clone sits so deep that a build path passes the Windows limit of 260 characters | Clone to a short path such as `C:\src` |
| `Set ConnectionStrings:ApexPostTestServer, or run setup-secrets.bat` in all integration tests | No connection string was found | Run step 1, or set the environment variable |
| `Login failed` or `Cannot open database "ApexPostDb"` | The database does not exist yet | Run step 2 |
| `The certificate chain was issued by an authority that is not trusted` (not met here) | A local server has a self-signed certificate | Add `TrustServerCertificate=True`, and `-C` for `sqlcmd` |
| `'setup-secrets.bat' is not recognized` | The shell does not search the current folder | Run `.\setup-secrets.bat` from the repository root |
| `The demo password and the API key cannot be empty` | An answer was left blank, or the answers were piped | Answer at the prompts, or redirect from a file with `<` |
| Signing in does nothing on `http://` | The cookie is `Secure` | Use <https://localhost:7242> |
| 429 on the sign-in page | The limit is 5 attempts per address per minute | Wait a minute |
| The demo password is refused after you changed it | The seed runs only on an empty database | Drop the database and repeat steps 2 and 4 |

## Demo accounts

All accounts share the password you stored as `Seed:DemoPassword`. The seed holds 26 staff; other branches follow the
same pattern, such as `clerk1.br003@apexpost.test` and `manager.br004@apexpost.test`.

| Role | Email | Home branch | Can do |
| --- | --- | --- | --- |
| Counter Clerk | `clerk1.br001@apexpost.test` | BR001 | Open baskets, add items and services, verify identity, take payment |
| Branch Manager | `manager.br001@apexpost.test` | BR001 | Everything a clerk can, plus void, refund, adjust stock and read own-branch reports |
| Auditor | `auditor1@apexpost.test` | BR001 | Read reports for every branch, the audit log and its export |
| Administrator | `admin1@apexpost.test` | BR001 | Manage staff and branches, read reports and the audit log |
| Counter Clerk (inactive) | `clerk4.br002@apexpost.test` | BR002 | Nothing: sign-in is refused |
| Branch Manager (closed branch) | `manager.br006@apexpost.test` | BR006 | Sign in, but cannot start transactions while the branch is closed |

## Features

- **Branches and counters:** six seeded branches, opening hours, close and reopen, add and remove counter terminals.
- **Staff:** add, edit, set a password, deactivate with a reason and activate, in all four roles. A change of role or
  home branch applies to an open session on the next request, and deactivation ends it. Administrators cannot
  deactivate themselves or change their own role, so an active administrator always remains.
- **Catalogue and stock:** products with SKU, category, price and tax category, stock per branch, and manager stock
  adjustments with a reason. Administrators revise the prices of a category in one set-based update; baskets keep the
  prices they were built with.
- **Counter transaction:** one basket mixing retail items, shipments and money orders, with tax previewed while the
  basket is open and frozen at payment. Shipping label preview, and cash rounding to the nearest 0.05.
- **Business rules:** stock never goes negative, parcels over the limit for their service level are rejected, money
  orders above 300.00 need a passed identity verification in the same transaction, voids and refunds need a Branch
  Manager and a reason, and closed branches and inactive staff cannot transact.
- **A closed branch** takes no new work, including on baskets opened before it closed. Managers can still void those
  baskets and refund earlier sales.
- **Security:** own cookie authentication with PBKDF2 hashes, named policies, antiforgery, security headers, IP
  allowlists, API keys, rate limiting and a CORS rule for the partner widget.
- **Observability:** correlation IDs, structured request logs with timing, a slow-action warning, RFC 7807 errors,
  maintenance mode and `/health`.

### Reports and notifications

| Page | Shows | Filters |
| --- | --- | --- |
| `/Reports/DailySales` | Paid sales per day and branch, split into retail and services | Branch, dates |
| `/Reports/LowStock` | Items at or below `Stock:LowStockThreshold` right now | Branch |
| `/Reports/ShippingVolume` | Paid shipments by zone and service level | Branch, dates |
| `/Reports/MoneyOrders` | Money orders and identity checks per branch, with masked document numbers | Branch, dates |

Dates default to the last 14 days. Voided and refunded sales are not counted as sales or shipments. Branch Managers see
their own branch; Auditors and Administrators see any branch or all of them. Counter Clerks have no reports.

`/Notifications` lists the simulated alerts for low stock, high-value money orders, voids and refunds. Everyone sees
their own branch; Auditors and Administrators can see all branches. The dashboard shows the latest five.

### Audit log

Sign-ins, sales, money orders, identity verification, voids, refunds, stock adjustments, staff and branch changes and
denied access are written to the append-only `AuditEntries` table, after the business commit and in a transaction of
their own, so a failed audit write can neither break nor undo a sale. Each entry names the staff member, role, branch,
action, key route values, outcome, duration and correlation ID. Auditors and Administrators read it at `/Audit`
and download it from `/Audit/Export`, from an address on `Security:AdminAllowedIps`. Passwords and document numbers are
not recorded, and anything shaped like a document or card number is masked before it reaches the store.

## Sample requests

`GET /integration/parcel-status/{trackingNumber}` needs a partner key in the `X-Api-Key` header and a caller address on
`Security:IntegrationAllowedIps`. Each status below was observed against the running application; the responses of 2026-11-02 are in
[docs/evidence/http-problem-details.txt](docs/evidence/http-problem-details.txt). The exception is the 403, which cannot
be produced from localhost and is covered by the test `Integration_returns_403_from_blocked_ip`.

| Request | Expected status |
| --- | --- |
| `curl -k -H "X-Api-Key: <key>" https://localhost:7242/integration/parcel-status/APX123456` | 200 |
| `curl -k https://localhost:7242/integration/parcel-status/APX123456` | 401 |
| `curl -k -H "X-Api-Key: wrong" https://localhost:7242/integration/parcel-status/APX123456` | 401 |
| `curl -k -H "X-Api-Key: <key>" https://localhost:7242/integration/parcel-status/FAIL1` | 502 |
| `curl -k -H "X-Api-Key: <key>" https://localhost:7242/integration/parcel-status/SLOW1` | 504 |
| The first request, from an address not on the allowlist | 403 |
| `curl -k https://localhost:7242/health` | 200 |
| `curl -k -i -H "X-Correlation-Id: sample-run-001" https://localhost:7242/Account/Login` | 200, header echoed |
| The sixth `POST /Account/Login` from one address within a minute | 429 |

## Configuration

Settings live in `src/ApexPost.Web/appsettings.json`. Secrets belong in user-secrets or environment variables.

| Key | Meaning |
| --- | --- |
| `ConnectionStrings:ApexPostDb` | SQL Server database, from user-secrets |
| `ConnectionStrings:ApexPostTestServer` | SQL Server for the integration tests, from the environment |
| `Seed:Enabled`, `Seed:DemoPassword` | Demo data, in Development only; the password comes from user-secrets |
| `Passwords:Iterations`, `Passwords:MinimumLength` | PBKDF2 cost and the shortest allowed password |
| `Tax:StandardPercent`, `Tax:ReducedPercent`, `Tax:ExemptPercent` | Tax rates |
| `MoneyOrders:IdentityVerificationThreshold`, `MoneyOrders:HighValueAlertAmount` | Verification limit and alert amount |
| `Stock:LowStockThreshold` | Items at or below this quantity count as low stock |
| `Shipping:StandardMultiplier`, `Shipping:ExpressMultiplier`, `Shipping:RegisteredMultiplier` | Service level prices |
| `Courier:TimeoutMilliseconds`, `Courier:MaxConcurrentRequests` | Tracking lookup timeout and concurrency limit |
| `Catalog:CacheSeconds` | How long products and zones stay in memory |
| `Performance:SlowActionMs` | Actions slower than this are logged as a warning |
| `Maintenance:Enabled`, `Maintenance:RetryAfterSeconds` | Answers 503 except on `/health`; applies without a restart |
| `Security:AdminAllowedIps`, `Security:IntegrationAllowedIps` | Addresses allowed on `[IpRestricted]` endpoints |
| `Integration:AllowedOrigins` | Browser origins allowed by CORS on the partner endpoint |
| `Integration:ApiKeys:<Partner>` | One API key per partner, from user-secrets or the environment |
| `RateLimiting:LoginPermitsPerMinute` | Sign-in attempts per address per minute |
| `RateLimiting:PartnerPermitsPerMinute` | Partner calls per partner per minute |

## How the code is organised

Requests travel inwards: a controller in Web calls a service in Application, which works through ports
(`src/ApexPost.Application/Abstractions`) and applies the rules in Domain. Infrastructure implements the ports, and
only `src/ApexPost.Web/Program.cs` and `src/ApexPost.Web/Composition` wire it in. Layering tests scan the compiled code
to keep it that way.

- `src/ApexPost.Domain` is grouped by subject: `Branches`, `Catalogue`, `Inventory`, `Personnel`, `Pricing`, `Rules`,
  `Shipping`, `Transactions` and `Verification`.
- `src/ApexPost.Application` has one folder per kind of code, such as `Services`, `Reports` and `Validation`.
- `src/ApexPost.Infrastructure/Persistence` holds the EF Core model and migrations, and `Repositories` holds what the
  application calls: `UnitOfWork`, the repositories, `SqlStockAdjuster`, `Queries` for read screens and
  `Sql/SqlAuditStore`.
  `Persistence/Json` is the Week 2 file storage, which the web host no longer uses.
- `sql/migrations` holds the idempotent script generated from the migrations; `MigrationScriptTests` keeps it complete.
- `src/ApexPost.Web` holds `Controllers`, `Views`, `ViewModels`, `Middleware`, `Filters`, `Security` and `Health`.
- `sql/reports` holds the report queries, and `sql/procedures` the stored procedures that a migration deploys.

## Screenshots

### Interface

The interface uses a charcoal sidebar grouped into Counter, Insights and Administration, showing only the pages the
role may open. Red marks primary actions and yellow marks the current page. Every status is shown as a word plus a dot,
never by colour alone. All styling is in `wwwroot/css/site.css` and all behaviour is in `wwwroot/js/site.js`: the menu
drawer, the confirmation prompts and the guard against a second submit. Every page also works without JavaScript.
These images were captured in headless Edge on 2026-11-02 against a newly seeded database.

| Sign-in | Clerk dashboard | Catalogue |
| --- | --- | --- |
| ![Sign-in](docs/screenshots/ui/01-sign-in.png) | ![Clerk dashboard](docs/screenshots/ui/02-dashboard-clerk.png) | ![Catalogue with low and out-of-stock pills](docs/screenshots/ui/07-catalogue.png) |

| Basket that needs an identity check | Paid, identity masked | Manager void and refund |
| --- | --- | --- |
| ![Counter screen](docs/screenshots/ui/03-counter-basket.png) | ![Paid transaction](docs/screenshots/ui/04-counter-paid.png) | ![Manager actions](docs/screenshots/ui/05-manager-actions.png) |

| Shipping label | Audit log | Tablet, 820 px |
| --- | --- | --- |
| ![Shipping label](docs/screenshots/ui/06-shipping-label.png) | ![Audit log](docs/screenshots/ui/08-audit-log.png) | ![Tablet dashboard](docs/screenshots/ui/09-tablet-dashboard.png) |

| Phone, 390 px | Phone menu |
| --- | --- |
| ![Phone counter screen](docs/screenshots/ui/10-phone-counter.png) | ![Phone menu drawer](docs/screenshots/ui/11-phone-drawer.png) |

### Workflow run

All 30 steps were driven in headless Edge on 2026-11-02, against a new clone and a new database, with the fictional demo
data. [docs/evidence/mvc-workflows.txt](docs/evidence/mvc-workflows.txt) lists the status and message of each. These
images were taken before the interface redesign above. The steps and messages are the same, but the page looks older.

### The counter

| Mixed basket with tax preview | Paid, identity masked |
| --- | --- |
| ![Basket of retail, parcel and money order](docs/screenshots/04-mixed-basket-tax-preview.png) | ![Paid transaction](docs/screenshots/09-paid-transaction.png) |

| Payment refused without a check | Identity check recorded |
| --- | --- |
| ![Refused payment](docs/screenshots/07-payment-refused-no-identity-check.png) | ![Masked document number](docs/screenshots/08-identity-verified-masked.png) |

### Roles

| Clerk dashboard | Manager dashboard |
| --- | --- |
| ![Clerk dashboard](docs/screenshots/02-dashboard-clerk.png) | ![Manager dashboard](docs/screenshots/13-dashboard-manager.png) |

| Manager void | Refund |
| --- | --- |
| ![Voided transaction](docs/screenshots/16-voided.png) | ![Refunded transaction](docs/screenshots/17-refunded.png) |

### Validation and errors

| Rule message | 403 page | Stale form, 409 |
| --- | --- | --- |
| ![Parcel over the limit](docs/screenshots/05-validation-parcel-over-limit.png) | ![Access denied](docs/screenshots/11-403-clerk-on-staff-page.png) | ![Conflict message](docs/screenshots/28-409-stale-form-refused.png) |

### Reports and audit

| Daily sales | Money orders | Audit log |
| --- | --- | --- |
| ![Daily sales report](docs/screenshots/21-report-daily-sales-14-days.png) | ![Money order report](docs/screenshots/24-report-money-orders.png) | ![Audit log](docs/screenshots/25-audit-log.png) |

### Build, tests and database

These images are drawn from the saved text output in [docs/evidence/](docs/evidence/). SSMS is not installed on the test
machine, so they show `sqlcmd` and `dotnet` output, not SSMS windows.

| Build | Tests | Schema |
| --- | --- | --- |
| ![Build output](docs/screenshots/31-build.png) | ![Test output](docs/screenshots/32-tests.png) | ![Schema output](docs/screenshots/33-database-schema.png) |

| Data | Stored procedures | Concurrency | Performance |
| --- | --- | --- | --- |
| ![Data output](docs/screenshots/34-database-data.png) | ![Procedure output](docs/screenshots/35-stored-procedures.png) | ![Row version conflict](docs/screenshots/36-concurrency.png) | ![Before and after](docs/screenshots/37-performance.png) |

The rest of the 30 steps are in [docs/screenshots/](docs/screenshots/). The images in
[docs/screenshots/earlier-run/](docs/screenshots/earlier-run/) come from an earlier run.

## Test results

Verified on 2026-11-02 after the evaluator fixes: Windows 11, .NET SDK 10.0.402, SQL Server 2025. The Release build
has 0 warnings and 0 errors.

| Project | Passed | Failed | Skipped |
| --- | --- | --- | --- |
| `ApexPost.UnitTests` (measurements excluded) | 534 | 0 | 0 |
| `ApexPost.IntegrationTests` (on SQL Server) | 440 | 0 | 0 |
| **Total** | **974** | **0** | **0** |

The 8 measurement tests are excluded from these counts and from CI. Run on their own on the same day, all 8 passed, so a
plain `dotnet test -c Release` runs 982. Their numbers are in
[DESIGN.md](DESIGN.md#8-async-taskwhenall-semaphoreslim-valuetask-and-measured-numbers).

The same steps passed from a new clone: [docs/evidence/fresh-clone.txt](docs/evidence/fresh-clone.txt). The checks and
what could not be verified are in the [checklist](docs/evidence/CHECKLIST.md).

## Week 4 Part A – courier offline store

A courier device saves shipment scans locally in Couchbase Lite (`apexpost_courier`) and uploads them later through
an outbox. Part A uses a fake uploader; Part D replaces it with the real API. The design is in
[OFFLINE-DESIGN.md](OFFLINE-DESIGN.md).

| Project | Purpose |
| --- | --- |
| `src/Courier/ApexPost.Courier.Domain` | `ShipmentScan`, `ScanRules` (`APX-########`, `CR-###`), enums and ports; no NuGet packages |
| `src/Courier/ApexPost.Courier.Offline` | `CourierDatabase`, key-value repository, parameterised SQL++ queries, outbox, change listener (`Couchbase.Lite` 3.4.2) |
| `src/Courier/ApexPost.Courier.Harness` | Console menu that runs the offline workflow |
| `tests/Courier/ApexPost.Courier.UnitTests`, `tests/ApexPost.Courier.Offline.Tests` | Unit and storage tests; each test opens its own temporary database folder |

### Run the harness

No SQL Server and no secrets are needed.

```bash
dotnet run --project src/Courier/ApexPost.Courier.Harness
```

The header shows the database name, its folder (`CouchbaseData/` under the build output) and the number of pending
scans. Choose **10 Seed Fictional Demo Data** once, then try the menu:

| Option | Action |
| --- | --- |
| 1 Add Scan | Saves a new scan as `Pending` |
| 2 Find by Tracking Number | All scans of one shipment, oldest first (index `idx_scan_trackingNumber`) |
| 3 List Pending | Scans waiting for upload (index `idx_scan_syncStatus`) |
| 4 Simulate Sync | Runs the outbox; `APX-99999999` always fails and becomes `Failed` |
| 5 Delete Scan | Deletes one scan by id |
| 6 Exit | Removes the listener token and closes the database |
| 7–11 | Scans per type for a courier, update notes, get by id, seed demo data, show query plans |

Every change is also written by the change listener to the console and to `logs/listener.log` in the folder the harness
was started from. `CouchbaseData/`, `*.cblite2/` and `logs/` are git-ignored.

### Run the Part A tests

```bash
dotnet test tests/Courier/ApexPost.Courier.UnitTests -c Release
dotnet test tests/ApexPost.Courier.Offline.Tests -c Release
```

Both projects are in `Apex_Post_Logistic.slnx`, so the solution-wide `dotnet test` and CI run them too. Screenshots of
the harness are listed in [docs/screenshots/week4-partA/](docs/screenshots/week4-partA/README.md).

## Week 4 Part B – shipment tracking API

`src/ApexPost.Api` is an ASP.NET Core Web API with minimal hosting in `Program.cs` (no `Startup.cs`). It registers the
Week 3 SQL persistence (`AddSqlPersistence`, `AddInfrastructureServices`) and the additive tracking module
(`AddShipmentTracking`). Every error is ProblemDetails with a `traceId`, written by the global `ApiExceptionHandler`:

| Exception | Status |
| --- | --- |
| `NotFoundException` | 404 |
| `DuplicateKeyException`, `InvalidStatusTransitionException`, `ShipmentLockedException`, `ConcurrencyException` | 409 |
| `ValidationFailedException` (with `errors` per field), `DomainException` | 400 |
| `ForbiddenException` | 403 |
| anything else | 500 with a generic detail |

The stack trace (`stackTrace`) is added in Development only. Endpoints, status codes, the status transition table and
the idempotency rules of the scan upload are in [API-NOTES.md](API-NOTES.md).

### Set up the user-secrets

The API shares the user-secrets store of `ApexPost.Web` (same `UserSecretsId`), so a connection string set by
`setup-secrets.bat` works for both. `appsettings.json` holds no connection string. To set it by hand:

```bash
dotnet user-secrets set ConnectionStrings:ApexPostDb "Server=localhost;Database=ApexPostDb;Trusted_Connection=True;TrustServerCertificate=True" --project src/ApexPost.Api
dotnet user-secrets list --project src/ApexPost.Api
```

### Run the API

```bash
dotnet run --project src/ApexPost.Api --launch-profile https
```

The API listens on `https://localhost:7310` and `http://localhost:5310`. In Development it applies the tracking
migrations (history table `__TrackingMigrationsHistory`, never the Week 3 migrations) and adds ten fictional shipments,
`APX-20000001` to `APX-20000010`, in ten cities and all six statuses. Existing shipments are left alone. Other
environments neither migrate nor seed.

Open [src/ApexPost.Api/ApexPost.Api.http](src/ApexPost.Api/ApexPost.Api.http) in Visual Studio or in VS Code with the
REST Client extension and send the requests top to bottom. Every endpoint has at least one success request and its
failure requests (400, 404, 409, 415). A quick check from the command line:

```bash
curl -k https://localhost:7310/api/shipments/APX-20000001
curl -k -i https://localhost:7310/api/shipments/APX-29999999
```

The first returns 200 with the shipment, the second 404 ProblemDetails.

### Run the Part B tests

```bash
dotnet test tests/ApexPost.Tracking.UnitTests -c Release
dotnet test tests/ApexPost.Api.Tests -c Release
```

The API tests start the API with `WebApplicationFactory` on a throw-away SQL Server database and drop it afterwards.
The server comes from `ConnectionStrings__ApexPostTestServer` (the CI container) or else from the user-secrets
connection string. Screenshots of the API are listed in
[docs/screenshots/week4-partB/](docs/screenshots/week4-partB/README.md).

## Week 4 Part C – API documentation

### OpenAPI, the built-in generator and Swagger UI

Three different things are often all called "Swagger":

| Name | What it is | In this repository |
| --- | --- | --- |
| OpenAPI specification | A vendor-neutral standard (JSON or YAML) that describes an HTTP API: paths, verbs, parameters, request and response bodies, status codes. Tools read it to show documentation or generate clients. | One document per version: `/openapi/v1.json` (deprecated) and `/openapi/v2.json`, committed as `contracts/v1.json` and `contracts/v2.json` |
| Built-in .NET OpenAPI generator | The `Microsoft.AspNetCore.OpenApi` package. `AddOpenApi("v1")` and `AddOpenApi("v2")` (called for each API Explorer group by `Asp.Versioning.OpenApi`) read the controllers, routes and response types at runtime and write an OpenAPI document; `MapOpenApi()` serves it. Document transformers change the result. | `AddApexOpenApi()` and `ApexDocumentTransformer` (title, description with the v1 deprecation notice, version, contact) in `src/ApexPost.Api/OpenApi` |
| Swagger UI | A web page that only renders an existing OpenAPI document and offers "Try it out". It generates nothing. | `Swashbuckle.AspNetCore.SwaggerUi` at `/swagger`; the drop-down offers "v2" (`/openapi/v2.json`) and "v1 (deprecated)" (`/openapi/v1.json`). The full `Swashbuckle.AspNetCore` package and `AddSwaggerGen` are not used. |

Run the API in Development (`dotnet run --project src/ApexPost.Api --launch-profile https`) and open:

- `https://localhost:7310/openapi/v2.json` and `https://localhost:7310/openapi/v1.json` – the OpenAPI documents
- `https://localhost:7310/swagger` – Swagger UI on both documents

### Versions and contract tracking

`/api/v2/shipments` is the current contract. `/api/v1/shipments` and the Part B alias `/api/shipments` are deprecated
and answer with `Deprecation`, `Sunset` and `Link` headers. [VERSIONING.md](VERSIONING.md) lists every v1 → v2 change,
the deprecation policy and the sunset plan. [API-NOTES.md](API-NOTES.md) compares the v1 and v2 shapes, and
[src/ApexPost.Api/ApexPost.Api.http](src/ApexPost.Api/ApexPost.Api.http) has a success and a failure request for every
endpoint in both versions. Screenshots of Part C are listed in
[docs/screenshots/week4-partC/](docs/screenshots/week4-partC/README.md).

`contracts/v1.json` and `contracts/v2.json` are the published contracts. `ContractTrackingTests` fails when the live
documents differ from them, so every contract change shows up as a reviewed diff. After an intended change, export
them again. The API starts in Development, so the user-secret connection string is needed:

```bash
./scripts/export-contracts.sh      # Linux, macOS, Git Bash
```

```powershell
./scripts/export-contracts.ps1     # Windows PowerShell
```

Both scripts build the API, start it on `http://127.0.0.1:5199`, download both documents, pretty-print them with
`scripts/format-json.cs` (the same bytes on every OS) and stop the API.

### Generated C# client and console consumer

`src/ApexPost.Client` is a typed C# client for v2, generated by NSwag from `contracts/v2.json`. The generator
settings are in `src/ApexPost.Client/nswag.json` (System.Text.Json, an `IApexPostApiClient` interface, an injected
`HttpClient`, `System.DateTime` for UTC times). The output `Generated/ApexPostApiClient.g.cs` is committed and marked
`<auto-generated>`, so a normal build does not need NSwag. Never edit it by hand: additions go into partial classes
in `src/ApexPost.Client/Extensions/` (for example `ApexPostApiClient.Partial.cs` reads and writes enums by name).
`Generated/.editorconfig` turns analyzers off for that folder only; hand-written code keeps warnings as errors.

Regenerate after exporting a changed contract. The scripts run the pinned NSwag CLI (`nswag.consolecore` 14.7.1)
through `dotnet tool exec`, so nothing is installed:

```bash
./scripts/generate-client.sh       # Linux, macOS, Git Bash
```

```powershell
./scripts/generate-client.ps1      # Windows PowerShell
```

The exact command in both is `dotnet tool exec nswag.consolecore@14.7.1 --yes -- run src/ApexPost.Client/nswag.json`.

`src/ApexPost.Client.Consumer` is a console app that uses only the generated client. It registers it with
`services.AddHttpClient<IApexPostApiClient, ApexPostApiClient>()` and reads the base URL from `ApexPostApi:BaseUrl` in
its `appsettings.json` (default `http://localhost:5310/`, the API's `http` launch profile). It creates a shipment,
reads it back, uploads two scans with new `scan::{Guid}` ids (Pickup, then InTransit), and asks for the unknown
`APX-00000000`: the client throws `ApiException` with `StatusCode` 404, and the consumer prints a friendly message.

Since Part D the API needs a bearer token. The demo creates shipments and uploads CR-101 scans, so give it a token
with both roles through the environment variable `ApexPostApi__AccessToken` (never in `appsettings.json`):

```bash
dotnet run --project src/ApexPost.Api --launch-profile http        # terminal 1
export ApexPostApi__AccessToken=$(dotnet user-jwts create --project src/ApexPost.Api \
  --role Admin --role Courier --claim courierId=CR-101 --output token)
dotnet run --project src/ApexPost.Client.Consumer                  # terminal 2
dotnet run --project src/ApexPost.Client.Consumer -- --ApexPostApi:BaseUrl=http://localhost:5199/
```

`GeneratedClientTests` runs the same scenario against the test host and checks that the client has one method per
operation in `contracts/v2.json`.

### Development only, and why that is not security

`MapApexDocumentationInDevelopment()` maps both only when the environment is Development. In every other environment
`/openapi/v1.json` and `/swagger` answer 404; `ProductionDocumentationTests` proves this. Leaving the documentation out
of Production keeps the surface small and avoids publishing internal details, but it is **not** a security measure:
anyone can still call the endpoints, guess routes or read a copy of the contract. The endpoints must protect themselves
with authentication and authorization (Part D adds JWT roles and a partner API key).

## Week 4 Part D – security and the courier app

### JWT bearer authentication and roles

`ApexPost.Api` accepts only requests with a valid JWT (`AddAuthentication().AddJwtBearer()` in
`Composition/ApiSecurityRegistration.cs`). Issuer, audience, lifetime (30 seconds clock skew) and signing key are all
validated. Issuer and audiences are read from `Authentication:Schemes:Bearer` in `appsettings.json` (Development:
`appsettings.Development.json`, issuer `dotnet-user-jwts`). The signing key is only in user-secrets, where
`dotnet user-jwts` creates and keeps it; it is never in a committed file.

| Endpoint (v1, v1 alias and v2) | Who may call it |
| --- | --- |
| `GET` shipments, one shipment, scans | any valid token |
| `POST .../scans` (upload) | role `Courier`, and only scans whose `courierId` equals the token's `courierId` claim |
| `PATCH .../status` | role `Courier` |
| `POST` shipments, `PUT` shipment, `DELETE` shipment | role `Admin` |

401 (no token, expired, bad signature, wrong issuer or audience) and 403 (wrong role) are ProblemDetails with a
`traceId`, written by `Security/BearerProblemEvents.cs`. The courierId rule is checked in
`ShipmentTrackingService` (through the `ICurrentCourier` port), not in the controller: a batch with a scan of
another courier is refused as a whole with 403 and nothing is stored. The OpenAPI documents declare the Bearer scheme,
so Swagger UI shows an **Authorize** button.

Create tokens for local testing (the first run also creates the signing key in user-secrets):

```bash
dotnet user-jwts create --project src/ApexPost.Api --role Courier --claim courierId=CR-101
dotnet user-jwts create --project src/ApexPost.Api --role Admin
dotnet user-jwts list --project src/ApexPost.Api
```

In Development, `POST /dev/token` with `{ "courierId": "CR-101" }` (or `CR-102`) returns a one-hour Courier token,
signed with the same key, for the MAUI login. It is not part of the OpenAPI documents, and outside Development the
controller is not registered, so the route answers 404. `ApexPost.Api.http` starts with these requests and sends
`{{courierToken}}` or `{{adminToken}}` with every call.

The tests in `tests/ApexPost.Api.Tests/Security` cover 401 (missing, expired, bad signature, wrong issuer or audience,
malformed), 403 (wrong role, other courierId, no courierId claim), success for each role and `/dev/token`.

### Partner API key, HTTPS and HSTS

Partners track parcels at `GET /api/v2/partner/shipments/{trackingNumber}` (v2 only, read-only). They send a key in
the `X-Api-Key` header instead of a user token. `[PartnerApiKey]` runs `Security/PartnerApiKeyFilter.cs`, which
looks the key up in the `PartnerKeys` configuration section (`PartnerKeys:<PartnerName> = <key>`, user-secrets or
environment variables only). The comparison is constant time (SHA-256 + `CryptographicOperations.FixedTimeEquals`
against every configured key). A missing or unknown key gets 401 ProblemDetails with `WWW-Authenticate: ApiKey`. Only
the partner name is logged, never the key. The answer holds status, destination city and the latest scan, never the
recipient's name, phone or address.

```bash
dotnet user-secrets set "PartnerKeys:ParcelTrackDemo" "<a long random value>" --project src/ApexPost.Api
```

`UseHttpsRedirection()` runs in every environment, and `UseHsts()` (one year, subdomains included) runs outside
Development. Both OpenAPI documents declare the `Bearer` scheme; v2 also declares `ApiKey` (header `X-Api-Key`), and
the partner operation requires only that one. In Swagger UI, **Authorize** therefore offers a token field and, in v2,
a key field. [SECURITY.md](SECURITY.md) explains the design, the OAuth 2.0 flows a real identity provider would use,
why API keys are weaker, HTTPS/HSTS and the Debug-only localhost certificate exception of the courier app.

### Resilience of the courier app

The typed `ApexApiClient` of the courier app runs every call through a `Microsoft.Extensions.Http.Resilience`
pipeline (`AddApexResilience()` in `MauiProgram`): total timeout 30 s → retry (3 retries, exponential backoff with
jitter) → circuit breaker (50 % failures of at least 4 attempts in 30 s, open for 15 s) → attempt timeout 8 s.
Only network errors, timeouts, 408 and 5xx are retried, never another 4xx, and only for GET/PUT/DELETE and the scan
upload, which is safe to repeat because the server de-duplicates by scan id. While the circuit is open the app shows
"Server unavailable – working offline" and scans keep saving locally. Every retry and state change is logged without
tokens or bodies. [RESILIENCE.md](RESILIENCE.md) explains the values; the tests are in
`tests/Mobile/ApexPost.Courier.ApiClient.Tests/Resilience`:

```bash
dotnet test tests/Mobile/ApexPost.Courier.ApiClient.Tests -c Release
```

## Known limitations

- The allowlists see the address of the direct connection. Behind a proxy that is the proxy, so all callers would
  share one address; forwarded headers are not trusted because a client can forge them.
- The partner tracking widget runs in end users' browsers, whose addresses are not on the integration allowlist, so a
  browser call only works from an allowed address. CORS does not replace the API key or the allowlist.
- A call refused by the IP allowlist, the API key or the rate limiter is logged but not audited: it stops in middleware,
  before any action filter runs. A failed sign-in is audited without the email that was typed.
- `StockGate` and the rate limiter work within one application instance; between servers the row versions decide.
- A failed audit write is only logged, so that action has no row in `AuditEntries`. See
  [ADR-016](DESIGN.md#adr-016-audit-store-versus-unit-of-work).
- A counter with past transactions cannot be removed, because the database keeps the history.
- A price revision clears the catalogue cache of one server; another server shows the old price for up to
  `Catalog:CacheSeconds`.
- During maintenance the stylesheet is also blocked, so the 503 page shows unstyled.
- Notifications are kept in memory: the newest 200 survive until the application restarts, and the seeded history
  raises none.
- Setting a new password does not end the member's open sessions; deactivating the account does.
- The audit log is append-only through its interface, but anyone with write access to the database can edit the table.
- What was not verified, such as a real sign-in with the application login, is listed in
  [PERFORMANCE.md](PERFORMANCE.md#not-verified) and [docs/evidence/CHECKLIST.md](docs/evidence/CHECKLIST.md).
