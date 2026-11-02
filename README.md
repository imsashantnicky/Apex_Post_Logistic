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
| `sql` | Report queries, stored procedures, transaction and blocking examples, the database role and performance scripts | |
| `docs/perf` | Evidence: raw measurements, `.sqlplan` files, query output | |
| `.github/workflows/ci.yml` | Restore, Release build and tests on every push and pull request to `main` | |

## Run it

You need the .NET 10 SDK (see [global.json](global.json)) and a SQL Server. Demo data is created only in Development,
and its shared password, the partner API key and the connection string come from user-secrets, never from the
repository.

On Windows, run [setup-secrets.bat](setup-secrets.bat), which asks for both values. Anywhere else:

```bash
dotnet user-secrets set "Seed:DemoPassword" "<a password of your choice>" --project src/ApexPost.Web
dotnet user-secrets set "Integration:ApiKeys:AcmeCouriers" "<a long random key>" --project src/ApexPost.Web
```

`setup-secrets.bat` stores only a placeholder for the database, so set a real connection string. Then create the tables,
because the application never runs migrations:

```bash
dotnet user-secrets set "ConnectionStrings:ApexPostDb" "<your connection string>" --project src/ApexPost.Web
dotnet tool restore
dotnet ef database update --project src/ApexPost.Infrastructure
```

Then start the application. The first start in Development fills an empty database; it is skipped while `Branches` has
rows. The demo accounts keep the password that was set at that moment, so changing `Seed:DemoPassword` later needs a new
seed.

```bash
dotnet dev-certs https --trust
dotnet run --project src/ApexPost.Web --launch-profile https
```

Open <https://localhost:7242>. The sign-in cookie is `Secure`, so use the HTTPS address.

[sql/perf/volume-seed.sql](sql/perf/volume-seed.sql) multiplies that data for performance tests, to about 200k
transactions and 600k retail lines of fictional data. Run it once, on a development database only. The row counts of
the seed and of a volume run are in [docs/perf/](docs/perf/).

```bash
sqlcmd -S localhost -E -C -d ApexPostDb -b -i sql/perf/volume-seed.sql
```

The reports, stored procedures and performance work are explained in [PERFORMANCE.md](PERFORMANCE.md). The procedures are
deployed by the migrations. The role for the running application is in
[sql/security/least-privilege-role.sql](sql/security/least-privilege-role.sql).

To build and test:

```bash
dotnet build Apex_Post_Logistic.slnx -c Release
dotnet test Apex_Post_Logistic.slnx -c Release --filter "Category!=Measurement"
```

The integration tests need a SQL Server. They use `ConnectionStrings:ApexPostTestServer` from the environment, or else
the server of `ConnectionStrings:ApexPostDb`. Each test class creates, migrates and seeds a temporary `ApexPostTests_...`
database and drops it afterwards, so your own database is not touched.

To see the measurements in [DESIGN.md](DESIGN.md#8-async-taskwhenall-semaphoreslim-valuetask-and-measured-numbers):

```bash
dotnet test tests/ApexPost.UnitTests -c Release --filter "Category=Measurement" --logger "console;verbosity=detailed"
```

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
`Security:IntegrationAllowedIps`. Each status below was observed against the running application on 2026-11-01,
except the 403, which cannot be produced from localhost and is covered by the test
`Integration_returns_403_from_blocked_ip`.

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
- `src/ApexPost.Web` holds `Controllers`, `Views`, `ViewModels`, `Middleware`, `Filters`, `Security` and `Health`.
- `sql/reports` holds the report queries, and `sql/procedures` the stored procedures that a migration deploys.

## Screenshots

No screenshots are included in this release: `docs/screenshots/` is empty. The sample requests above show the
behaviour instead.

## Test results

Release build on 2026-11-02: 0 warnings, 0 errors.

| Project | Passed | Failed | Skipped |
| --- | --- | --- | --- |
| `ApexPost.UnitTests` (measurements excluded) | 500 | 0 | 0 |
| `ApexPost.IntegrationTests` (on SQL Server) | 353 | 0 | 0 |
| **Total** | **853** | **0** | **0** |

The 8 measurement tests are excluded from these counts and from CI. Their numbers are in
[DESIGN.md](DESIGN.md#8-async-taskwhenall-semaphoreslim-valuetask-and-measured-numbers).

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
- `setup-secrets.bat` has not been run: the project was developed on macOS.
- What was not verified in the SQL work, such as a real sign-in with the application login, is listed in
  [PERFORMANCE.md](PERFORMANCE.md#not-verified).
