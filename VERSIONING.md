# API versioning – ApexPost Logistics API

The shipment API has two versions in the URL path. Both run on the same `IShipmentTrackingService`, the same database
and the same rules; only the JSON shape differs.

| Version | Base route | State | OpenAPI document | Contract file |
| --- | --- | --- | --- | --- |
| v2 | `/api/v2/shipments` | Current | `/openapi/v2.json` (Swagger UI: "v2") | `contracts/v2.json` |
| v1 | `/api/v1/shipments` and the Part B alias `/api/shipments` | Deprecated since 2026-11-05, sunset 2027-05-31 | `/openapi/v1.json` (Swagger UI: "v1 (deprecated)") | `contracts/v1.json` |

All dates in this document are fictional. They live in one place in the code:
`src/ApexPost.Api/Composition/VersionLifecycle.cs`.

## How a version is chosen

- The version is a URL segment: `api/v{version:apiVersion}/shipments` (Asp.Versioning, `UrlSegmentApiVersionReader`).
- A request without a version, which is only possible on `/api/shipments`, is answered by v1
  (`AssumeDefaultVersionWhenUnspecified`, default 1.0). This keeps Part B clients working unchanged.
- An unknown version such as `/api/v3/shipments` matches no route and gets a 404 ProblemDetails.
- Every answer reports the versions (`ReportApiVersions = true`):

```http
api-supported-versions: 2.0
api-deprecated-versions: 1.0
```

## Every change from v1 to v2

"Breaking" means a client written for v1 fails or misreads data if it is pointed at v2 without changes.

| Field / element | v1 | v2 | Breaking? | Why |
| --- | --- | --- | --- | --- |
| Base route | `/api/shipments`, `/api/v1/shipments` | `/api/v2/shipments` | Non-breaking (v1 keeps its routes until sunset) | The version in the URL is visible in logs, links and caches, and needs no special header. |
| Shipment response: recipient | flat `recipientName`, `recipientPhone`, `recipientAddress`, `recipientCity` | nested `recipient { name, phone, address, city }` | **Breaking** | The four fields belong together. One object is easier to map in clients and can grow later (for example a postcode) without more top-level fields. |
| Create / replace request: recipient | the same four flat fields | the same nested `recipient` object, required | **Breaking** | Requests and responses keep the same shape, so a client can send back what it read. |
| Shipment response: `status` | number `1`–`6` | name `Created`, `PickedUp`, `InTransit`, `OutForDelivery`, `Delivered`, `Failed` | **Breaking** | Numbers mean nothing without the table and break silently if the order of the enum ever changes. Names are self-describing. |
| PATCH `…/status` request: `status` | number only | name; a number is still accepted | Non-breaking for requests | The courier app and partners send names. Reading numbers as well costs nothing and eases the move. |
| Shipment response: `latestScan` | not present | `latestScan { scanType, location, scannedAtUtc }`, or `null` without scans | Non-breaking (added field) | A tracking screen needs the last event; v1 clients need a second call to `…/scans` for it. |
| Scan upload item: `scanType` | number only | name (`Pickup`, `InTransit`, `Delivered`, `Failed`); a number is still accepted | Non-breaking for requests | The courier device stores enums as strings (`OFFLINE-DESIGN.md`), so the uploader can send the document as it is. |
| Scan response: `scanType` | number `1`–`4` | name | **Breaking** | Same reason as `status`. |
| OpenAPI `operationId` | `ListShipmentsV1`, `GetShipmentV1`, … | `ListShipments`, `GetShipment`, … | Breaking for generated SDKs only; HTTP clients are not affected | The current version gets clean method names in the generated client. The suffix shows that a v1 method is the old one. |
| `Location` of a created shipment | `/api/v1/shipments/{trackingNumber}`, or `/api/shipments/…` on the alias | `/api/v2/shipments/{trackingNumber}` | Non-breaking | The Location always points to the version the client called. |
| OpenAPI document | operations `deprecated: true`, deprecation notice in `info.description` | no deprecation | Non-breaking | Tools such as Swagger UI and NSwag show v1 as deprecated. |
| Paging envelope `{ items, page, pageSize, totalCount, totalPages, hasNext }` | unchanged | unchanged | No change | Same types in both versions. |
| Scan upload answer `{ accepted, duplicatesSkipped, rejected: [{ scanId, reason }] }` | unchanged | unchanged | No change | It is the shared contract with the courier outbox and must stay identical. |
| Status codes, ProblemDetails, validation rules, `pageSize` 1–50, tracking number route `^APX-\d{8}$`, idempotent `scan::{GUID}` | unchanged | unchanged | No change | Same service and the same global exception handler. |

## Deprecation policy

1. **Breaking changes only in a new major version.** Added fields and endpoints can go into the current version.
   Removing or renaming a field, or changing its type or meaning, needs a new version.
2. **A deprecated version is frozen.** v1 gets only security and bug fixes, no new fields. `contracts/v1.json`
   changes only for such fixes.
3. **At least six months between deprecation and sunset.** v1 has 207 days (2026-11-05 → 2027-05-31).
4. **At most one deprecated version at a time,** so clients have one migration to plan, not several.
5. **Machine-readable signals from day one:** the response headers below, `deprecated: true` on every v1 operation,
   the notice in the v1 document description and "v1 (deprecated)" in Swagger UI.
6. **No contract change without a reviewed diff.** `ContractTrackingTests` fails when a live document differs from
   `contracts/`. The contracts are exported again only on purpose, and the diff is reviewed in the pull request.

### Deprecation and Sunset headers

Configured in `ApiVersioningRegistration` with `options.Policies.Deprecate(1.0)` and `options.Policies.Sunset(1.0)`.
v1 answers carry them, on `/api/v1/...` and on the alias; v2 answers do not. A real v1 answer:

```http
api-supported-versions: 2.0
api-deprecated-versions: 1.0
Deprecation: @1793836800
Sunset: Mon, 31 May 2027 00:00:00 GMT
Link: <https://developer.apexpost.example/api/versioning>; rel="deprecation"
Link: <https://developer.apexpost.example/api/versioning>; rel="sunset"
```

- `Deprecation` (RFC 9745) is the moment v1 became deprecated, as Unix seconds: `1793836800` = 2026-11-05 00:00 UTC.
- `Sunset` (RFC 8594) is the moment v1 stops answering.
- `Link` points to the migration guide (a fictional page) for both events.

`VersionHeaderTests` checks the values on `/api/shipments`, `/api/v1/shipments` and their absence on v2.

## Sunset plan for v1

| Date (fictional) | Step | Channel |
| --- | --- | --- |
| 2026-11-05 | v2 released; v1 deprecated. Headers, `deprecated: true` in the document and the Swagger UI label are live. | Release notes, changelog, `/openapi/v1.json` |
| 2026-11-06 | Announcement to all partners and the courier app team: what changes (table above), the sunset date and the migration guide. | Email to the partner mailing list `partners@apexpost.example`, developer portal news |
| 2026-12-01 | The courier app (Part D) and the generated client (`ApexPost.Client`) use v2 only. | Internal release |
| 2027-02-01 | Reminder to every partner that still calls v1, with the date of their last v1 call. | Direct email from the API team (`api-team@apexpost.example`) |
| 2027-05-01 | Final notice: 30 days left. | Email, banner on the developer portal |
| 2027-05-31 | Sunset: the v1 controllers and the `/api/shipments` alias are removed, together with `contracts/v1.json` and the v1 document. A v1 URL then matches no route and gets a 404 ProblemDetails, as `/api/v3/...` does today. | Release notes |

Planned, not built yet: before each reminder the API team counts v1 calls per partner. The partner API key that Part D
adds will identify the caller, and a request log grouped by the version route value can count the calls. Until then,
the headers and the emails are the only signals.

## Contract tracking

`contracts/v1.json` and `contracts/v2.json` are the published contracts, exported from the running API.

- `tests/ApexPost.Api.Tests/Documentation/ContractTrackingTests.cs` reads `/openapi/v1.json` and `/openapi/v2.json`
  from the test host and compares them with the committed files as JSON, not as text, so line endings do not matter.
  On a difference the test names the first JSON path that differs, for example `$.paths./api/v2/shipments.get`.
- To accept an intended change, run the export and commit the new files with the code change:

```bash
./scripts/export-contracts.sh      # Linux, macOS, Git Bash
```

```powershell
./scripts/export-contracts.ps1     # Windows PowerShell
```

Both scripts build `ApexPost.Api`, start it in Development on `http://127.0.0.1:5199` (needs the user-secret
`ConnectionStrings:ApexPostDb`), download both documents, pretty-print them with `scripts/format-json.cs` (two-space
indent, LF, readable characters; the same bytes on every OS) and stop the API.

The documents list no `servers`, so the contracts do not depend on the machine or port that produced them.
