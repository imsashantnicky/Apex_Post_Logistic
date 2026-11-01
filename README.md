# Apex_Post_Logistic

ApexPost Counter Platform is an ASP.NET Core MVC application (.NET 10, C# 14) for postal counters. One counter sells
retail goods together with parcel and letter shipping, money orders and identity verification in a single basket. A
clerk builds the basket and takes payment, a Branch Manager voids or refunds, an Auditor reads reports and the audit
log, and an Administrator manages staff and branches. Data is kept in JSON files behind repository interfaces, so
next week's database replaces only the Infrastructure project.

The design and its reasoning are in [DESIGN.md](DESIGN.md). A sample of the audit log from a real run is in
[AUDIT-SAMPLE.json](AUDIT-SAMPLE.json).

## Solution layout

| Path | Purpose | References |
| --- | --- | --- |
| `src/ApexPost.Domain` | Entities, value objects, business rules and events | Nothing |
| `src/ApexPost.Application` | Use cases, ports, reports, settings and validation | Domain |
| `src/ApexPost.Infrastructure` | JSON storage, audit store, password hashing, cache, simulated partners, seed data | Application |
| `src/ApexPost.Web` | MVC front end, middleware, filters and the composition root | Application, Infrastructure |
| `tests/ApexPost.UnitTests` | Rules, services, infrastructure, layering scans and measurements | Domain, Application, Infrastructure |
| `tests/ApexPost.IntegrationTests` | The real application through `WebApplicationFactory` | Web |
| `.github/workflows/ci.yml` | Restore, Release build and tests on every push and pull request to `main` | |

## Run it

You need the .NET 10 SDK (see [global.json](global.json)). Demo data is created only in Development, and its shared
password and the partner API key come from user-secrets, never from the repository.

On Windows, run [setup-secrets.bat](setup-secrets.bat), which asks for both values. Anywhere else:

```bash
dotnet user-secrets set "Seed:DemoPassword" "<a password of your choice>" --project src/ApexPost.Web
dotnet user-secrets set "Integration:ApiKeys:AcmeCouriers" "<a long random key>" --project src/ApexPost.Web
```

Then start the application:

```bash
dotnet dev-certs https --trust
dotnet run --project src/ApexPost.Web --launch-profile https
```

Open <https://localhost:7242>. The sign-in cookie is `Secure`, so use the HTTPS address.

To build and test:

```bash
dotnet build Apex_Post_Logistic.slnx -c Release
dotnet test Apex_Post_Logistic.slnx -c Release --filter "Category!=Measurement"
```

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
  adjustments with a reason.
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
denied access are written to the append-only `audit.jsonl` in the data folder. Each entry names the staff member, role,
branch, action, key route values, outcome, duration and correlation ID. Auditors and Administrators read it at `/Audit`
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
| `Storage:DataFolder` | Folder of the JSON files, relative to the web project |
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
- `src/ApexPost.Infrastructure/Persistence/Json` holds the storage: `Records` for the file shape and `Mapping` for the
  conversion from and to the domain.
- `src/ApexPost.Web` holds `Controllers`, `Views`, `ViewModels`, `Middleware`, `Filters`, `Security` and `Health`.

## Screenshots

No screenshots are included in this release: `docs/screenshots/` is empty. The sample requests above show the
behaviour instead.

## Test results

Release build on 2026-11-01: 0 warnings, 0 errors.

| Project | Passed | Failed | Skipped |
| --- | --- | --- | --- |
| `ApexPost.UnitTests` (measurements excluded) | 471 | 0 | 0 |
| `ApexPost.IntegrationTests` | 269 | 0 | 0 |
| **Total** | **740** | **0** | **0** |

The 7 measurement tests are excluded from these counts and from CI. Their numbers are in
[DESIGN.md](DESIGN.md#8-async-taskwhenall-semaphoreslim-valuetask-and-measured-numbers).

## Known limitations

- The allowlists see the address of the direct connection. Behind a proxy that is the proxy, so all callers would
  share one address; forwarded headers are not trusted because a client can forge them.
- The partner tracking widget runs in end users' browsers, whose addresses are not on the integration allowlist, so a
  browser call only works from an allowed address. CORS does not replace the API key or the allowlist.
- A call refused by the IP allowlist, the API key or the rate limiter is logged but not audited: it stops in middleware,
  before any action filter runs. A failed sign-in is audited without the email that was typed.
- Paying, voiding and refunding change two files, which is not atomic. See
  [Path to a database](DESIGN.md#12-path-to-a-database).
- During maintenance the stylesheet is also blocked, so the 503 page shows unstyled.
- Notifications are kept in memory: the newest 200 survive until the application restarts, and the seeded history
  raises none.
- Setting a new password does not end the member's open sessions; deactivating the account does.
- Report rows are built in memory from the JSON files, so a report reads all transactions on every request.
- The audit log is append-only through its interface, but anyone with access to the data folder can edit the file.
- `setup-secrets.bat` has not been run: the project was developed on macOS.
