# Apex_Post_Logistic

ApexPost Counter Platform: an ASP.NET Core MVC application (.NET 10, C# 14) for postal counters that sell
retail goods together with shipping, money orders and identity verification in one basket.

## Requirements

- .NET 10 SDK (see `global.json`)

## Build and test

```bash
dotnet build Apex_Post_Logistic.slnx -c Release
dotnet test Apex_Post_Logistic.slnx -c Release --filter "Category!=Measurement"
```

## Run it

The demo data is created only in Development, and the shared demo password comes from user-secrets:

```bash
dotnet user-secrets set "Seed:DemoPassword" "<a password of your choice>" --project src/ApexPost.Web
dotnet dev-certs https --trust
dotnet run --project src/ApexPost.Web --launch-profile https
```

Open <https://localhost:7242> and sign in as `clerk1.br001@apexpost.test`, `manager.br001@apexpost.test`,
`auditor1@apexpost.test` or `admin1@apexpost.test`. The sign-in cookie is `Secure`, so use the HTTPS address.

## Courier partner endpoint

`GET /integration/parcel-status/{trackingNumber}` needs a partner key in the `X-Api-Key` header and a caller address
on `Security:IntegrationAllowedIps`. Keys are never stored in the repository; set one with user-secrets:

```bash
dotnet user-secrets set "Integration:ApiKeys:AcmeCouriers" "<a long random key>" --project src/ApexPost.Web
```

| Request | Expected status |
| --- | --- |
| `curl -k -H "X-Api-Key: <key>" https://localhost:7242/integration/parcel-status/APX123456` | 200 |
| `curl -k https://localhost:7242/integration/parcel-status/APX123456` | 401 |
| `curl -k -H "X-Api-Key: <key>" https://localhost:7242/integration/parcel-status/FAIL1` | 502 |
| `curl -k -H "X-Api-Key: <key>" https://localhost:7242/integration/parcel-status/SLOW1` | 504 |
| `curl -k https://localhost:7242/health` | 200 |

## Configuration

| Key | Meaning |
| --- | --- |
| `Performance:SlowActionMs` | Actions slower than this are logged as a warning |
| `Maintenance:Enabled`, `Maintenance:RetryAfterSeconds` | Answers 503 except on `/health`; applies without a restart |
| `Security:AdminAllowedIps`, `Security:IntegrationAllowedIps` | Addresses allowed on `[IpRestricted]` endpoints |
| `Integration:AllowedOrigins` | Browser origins allowed by CORS on the partner endpoint |
| `Integration:ApiKeys:<Partner>` | One API key per partner, from user-secrets or the environment |
| `RateLimiting:LoginPermitsPerMinute` | Sign-in attempts per address per minute |
| `RateLimiting:PartnerPermitsPerMinute` | Partner calls per partner per minute |

## Administration

Administrators manage staff at `/Staff` (add, edit, set a password, deactivate with a reason, activate) and branches at
`/Branches` (close, reopen, add and remove counters), from an address on `Security:AdminAllowedIps`. A change of role or
home branch applies to the member's open session on the next request, and deactivation ends it. An administrator cannot
deactivate the own account or change the own role, so an active administrator always remains.

A closed branch takes no new work, including on baskets opened before it closed. Managers can still void those baskets
and refund earlier sales. A counter with an open basket cannot be removed, and a branch always keeps one counter.

## Reports and notifications

| Page | Shows | Filters |
| --- | --- | --- |
| `/Reports/DailySales` | Paid sales per day and branch, split into retail and services | Branch, dates |
| `/Reports/LowStock` | Items at or below `Stock:LowStockThreshold` right now | Branch |
| `/Reports/ShippingVolume` | Paid shipments by zone and service level | Branch, dates |
| `/Reports/MoneyOrders` | Money orders and identity checks per branch, with masked document numbers | Branch, dates |

Dates default to the last 14 days. Voided and refunded sales are not counted as sales or shipments. Branch Managers see
their own branch; Auditors and Administrators see any branch or all of them. Counter Clerks have no reports.

`/Notifications` lists the simulated alerts for low stock, high-value money orders, voids and refunds. Everyone sees
their own branch; Auditors and Administrators can see all branches. The dashboard shows the latest five, and loads
sales, stock and alerts in parallel with `Task.WhenAll`.

## Audit log

Sign-ins, sales, money orders, identity verification, voids, refunds, stock adjustments, staff and branch changes and
denied access are written to the append-only `audit.jsonl` in the data folder. Each entry names the staff member, role, branch, action, key
route values, outcome, duration and correlation ID. Auditors and administrators read it at `/Audit` and download it
from `/Audit/Export`, from an address on `Security:AdminAllowedIps`. Passwords and document numbers are not recorded,
and anything shaped like a document or card number is masked before it reaches the store.

## How the code is organised

- `src/ApexPost.Domain` — domain model; references no other project.
- `src/ApexPost.Application` — use cases and ports; references Domain.
- `src/ApexPost.Infrastructure` — implementations of the ports; references Application.
- `src/ApexPost.Web` — ASP.NET Core MVC front end and composition root; references Application and Infrastructure.
- `tests/ApexPost.UnitTests` — xUnit unit tests and layering tests (`Architecture/`).
- `tests/ApexPost.IntegrationTests` — xUnit tests against the Web project (`Architecture/` holds the controller
  layering test).
- `.github/workflows/ci.yml` — restore, Release build and tests (measurement tests excluded) on every push and PR.

## Known limitations

- The allowlists see the address of the direct connection. Behind a proxy that is the proxy, so all callers would
  share one address; forwarded headers are not trusted because a client can forge them.
- The partner tracking widget runs in end users' browsers, whose addresses are not on the integration allowlist, so
  a browser call only works from an allowed address. CORS does not replace the API key or the allowlist.
- A partner call refused by the IP allowlist or the API key is logged but not audited: it stops in middleware, before
  any action filter runs.
- During maintenance the stylesheet is also blocked, so the 503 page shows unstyled.
- Notifications are kept in memory: the newest 200 survive until the application restarts, and the seeded history
  raises none.
- Setting a new password does not end the member's open sessions; deactivating the account does.
- Report rows are built in memory from the JSON files, so a report reads all transactions on every request.
