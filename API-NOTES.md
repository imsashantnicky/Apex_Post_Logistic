# API notes – shipment tracking API (Week 4 Parts B and C)

`src/ApexPost.Api` is the REST API of the shipment tracking module. This page lists the endpoints of both API
versions and their status codes, the differences between v1 and v2, the status lifecycle and the rules that make the
scan upload safe to repeat. Every endpoint has a ready-to-run success and failure request for v1 and for v2 in
[src/ApexPost.Api/ApexPost.Api.http](src/ApexPost.Api/ApexPost.Api.http). The deprecation policy and the sunset plan
are in [VERSIONING.md](VERSIONING.md).

## Versions

| Version | Base route | State | Shape |
| --- | --- | --- | --- |
| v1 | `/api/v1/shipments`, alias `/api/shipments` | Deprecated, sunset 2027-05-31 | Part B contract: flat `recipientName`, `recipientPhone`, `recipientAddress`, `recipientCity`; enums as numbers |
| v2 | `/api/v2/shipments` | Current | Nested `recipient` object, `latestScan` (or `null`), `status` and `scanType` as strings |

- A URL without a version (`/api/shipments`) is treated as v1, so Part B clients keep working.
- An unknown version (`/api/v3/...`) is answered with 404 ProblemDetails.
- Every answer carries `api-supported-versions: 2.0` and `api-deprecated-versions: 1.0`. v1 answers also carry the
  `Deprecation`, `Sunset` and two `Link` headers.
- Both versions call the same `IShipmentTrackingService`, so the rules, status codes and stored data are identical.
  Only the JSON shape and the route names (operationIds) differ: v1 names end in `V1` (`GetShipmentV1`), v2 names
  have no suffix (`GetShipment`).
- OpenAPI documents: `/openapi/v1.json` (every operation `deprecated: true`) and `/openapi/v2.json`, committed as
  `contracts/v1.json` and `contracts/v2.json`. Swagger UI at `/swagger` shows both. Both are Development only.

## Conventions

- Bodies are `application/json`; any other content type gets 415.
- `{trackingNumber}` must match `^APX-\d{8}$` (route constraint). A value in another format never reaches an action
  and is answered with 404.
- Every error is ProblemDetails (`application/problem+json`) with `traceId`. Validation errors are
  ValidationProblemDetails with `errors` per field; nested v2 fields appear as `Recipient.Name`. The stack trace is
  added in Development only.
- v1 writes enums as numbers. `status`: 1 Created, 2 PickedUp, 3 InTransit, 4 OutForDelivery, 5 Delivered, 6 Failed.
  `scanType`: 1 Pickup, 2 InTransit, 3 Delivered, 4 Failed. v2 reads and writes the names (`"PickedUp"`,
  `"Delivered"`); an unknown name is a 400. The `status` query filter accepts the name or the number in both versions.
- Paging: `page` 1 or more, `pageSize` 1 to 50, default 20. `pageSize` above 50 is a 400, not silently capped, so a
  client learns the limit instead of receiving fewer items than it asked for. The page shape (`items`, `page`,
  `pageSize`, `totalCount`, `totalPages`, `hasNext`) is the same in both versions.

## Endpoints

The same eight operations exist in both versions. `{v}` is `v1` or `v2`; v1 is also reachable without the segment.

| Endpoint | Verb | v1 operationId | v2 operationId | Status codes | Description |
| --- | --- | --- | --- | --- | --- |
| `/api/{v}/shipments` | GET | `ListShipmentsV1` | `ListShipments` | 200, 400 | One page of shipments, filtered by `status` and `city` (ignoring case). 400 for a bad `page`, `pageSize` or `status`. |
| `/api/{v}/shipments` | POST | `CreateShipmentV1` | `CreateShipment` | 201, 400, 409, 415 | Creates a shipment in status Created. 201 with a `Location` header to `GET /api/{v}/shipments/{trackingNumber}` of the same version; 409 when the tracking number exists. |
| `/api/{v}/shipments/{trackingNumber}` | GET | `GetShipmentV1` | `GetShipment` | 200, 404 | One shipment. |
| `/api/{v}/shipments/{trackingNumber}` | PUT | `ReplaceShipmentV1` | `ReplaceShipment` | 200, 400, 404, 409, 415 | Replaces recipient and weight. 409 when the shipment is Delivered or Failed. |
| `/api/{v}/shipments/{trackingNumber}/status` | PATCH | `ChangeShipmentStatusV1` | `ChangeShipmentStatus` | 200, 400, 404, 409, 415 | Moves the shipment forward. 409 for a move that the transition table below does not allow. |
| `/api/{v}/shipments/{trackingNumber}` | DELETE | `DeleteShipmentV1` | `DeleteShipment` | 204, 404, 409 | Deletes a shipment that is still Created; 409 for any other status. |
| `/api/{v}/shipments/{trackingNumber}/scans` | POST | `UploadScansV1` | `UploadScans` | 200, 400, 404, 415 | Idempotent upload of 1 to 100 scans. 200 with `{ accepted, duplicatesSkipped, rejected }`. |
| `/api/{v}/shipments/{trackingNumber}/scans` | GET | `GetScansV1` | `GetScans` | 200, 404 | The scans of the shipment, oldest first by `scannedAtUtc`. |

Any unexpected exception is a 500 ProblemDetails with a generic detail, written by the global `ApiExceptionHandler`.

### The same shipment in v1 and v2

Real answers of the local Development API for the seeded `APX-20000005` after one InTransit scan.

`GET /api/v1/shipments/APX-20000005`:

```json
{
  "trackingNumber": "APX-20000005",
  "recipientName": "Eve Placeholder",
  "recipientPhone": "+44 20 7946 0105",
  "recipientAddress": "17 Runway Close",
  "recipientCity": "Southmere",
  "weightKg": 7.250,
  "status": 3,
  "createdAtUtc": "2026-11-05T11:34:02.8230222Z",
  "version": 2
}
```

`GET /api/v2/shipments/APX-20000005`:

```json
{
  "trackingNumber": "APX-20000005",
  "recipient": {
    "name": "Eve Placeholder",
    "phone": "+44 20 7946 0105",
    "address": "17 Runway Close",
    "city": "Southmere"
  },
  "weightKg": 7.250,
  "status": "InTransit",
  "latestScan": {
    "scanType": "InTransit",
    "location": "Southmere hub",
    "scannedAtUtc": "2026-11-01T09:00:00Z"
  },
  "createdAtUtc": "2026-11-05T11:34:02.8230222Z",
  "version": 2
}
```

### Request bodies

| Request | v1 | v2 |
| --- | --- | --- |
| Create (POST) | `trackingNumber`, `recipientName`, `recipientPhone`, `recipientAddress`, `recipientCity`, `weightKg` | `trackingNumber`, `recipient { name, phone, address, city }`, `weightKg` |
| Replace (PUT) | the same fields without `trackingNumber` | `recipient { name, phone, address, city }`, `weightKg` |
| Status (PATCH) | `{ "status": 2 }` | `{ "status": "PickedUp" }` |
| Scan upload (POST) | `scans[]` with `"scanType": 1` | `scans[]` with `"scanType": "Pickup"` |

A flat v1 body sent to v2 is a 400 (`Recipient` is required), so a client cannot mix the versions by accident. The
scan upload answer `{ accepted, duplicatesSkipped, rejected: [{ scanId, reason }] }` is the same in both versions.

### Validation rules

The rules are the same in both versions; in v2 the recipient fields sit inside `recipient`.

| Field | Rule |
| --- | --- |
| `trackingNumber` | `APX-` followed by 8 digits |
| `recipientName`, `recipientAddress`, `recipientCity` (v2: `recipient.name`, `recipient.address`, `recipient.city`) | Required, maximum length from `Recipient` |
| `recipientPhone` (v2: `recipient.phone`) | 7 to 15 digits or spaces, optional leading `+` |
| `weightKg` | 0.1 to 30 |
| `scanId` | `scan::` followed by a GUID |
| `courierId` | `CR-` followed by 3 digits |
| `scannedAtUtc` | Required; custom rule `[NotInFuture]`: at most 2 minutes ahead of the server clock |
| `recipientSignatureName` | Custom rule (`IValidatableObject`): required when `scanType` is Delivered; checked, not stored |

## Status transitions

Created → PickedUp → InTransit → OutForDelivery → Delivered | Failed. A shipment moves forward only. It may skip a
stage (couriers have no OutForDelivery scan, so Delivered can follow InTransit), but it never goes back, never
"moves" to its own status through PATCH, and never leaves Delivered or Failed. The rule is
`ShipmentStatusRules.CanMove`.

| From → To | Created | PickedUp | InTransit | OutForDelivery | Delivered | Failed |
| --- | --- | --- | --- | --- | --- | --- |
| Created | 409 | 200 | 200 | 200 | 200 | 200 |
| PickedUp | 409 | 409 | 200 | 200 | 200 | 200 |
| InTransit | 409 | 409 | 409 | 200 | 200 | 200 |
| OutForDelivery | 409 | 409 | 409 | 409 | 200 | 200 |
| Delivered | 409 | 409 | 409 | 409 | 409 | 409 |
| Failed | 409 | 409 | 409 | 409 | 409 | 409 |

A scan moves the status the same way: Pickup → PickedUp, InTransit → InTransit, Delivered → Delivered,
Failed → Failed. Unlike PATCH, a scan may repeat the current status (an InTransit scan at every hub); it is stored and
the status stays.

### Demo shipments (Development only)

| Tracking number | City | Status |
| --- | --- | --- |
| `APX-20000001`, `APX-20000002` | Harbour Point, Northgate | Created |
| `APX-20000003`, `APX-20000004` | Westfield, Old Quay | PickedUp |
| `APX-20000005`, `APX-20000006` | Southmere, Riverton | InTransit |
| `APX-20000007` | Lakeside | OutForDelivery |
| `APX-20000008`, `APX-20000009` | Hillcrest, Eastbrook | Delivered |
| `APX-20000010` | Kingsbridge | Failed |

## Idempotent scan upload

The courier device may send the same batch more than once (lost answer, retry after a timeout). The upload is
designed so that a repeat changes nothing. The rules hold for both versions.

1. **The scan id is the idempotency key.** Every scan carries the device document id `scan::{Guid}`. A unique index
   (`IX_TrackingScans_ScanId`) guarantees one stored row per id.
2. **Stored ids are duplicates.** One query reads which ids of the batch are already stored; those scans are counted
   in `duplicatesSkipped` and not applied again. Sending the same batch twice gives `accepted: 0` the second time and
   the shipment status does not change.
3. **Repeated ids in one batch count once.** The first copy is handled; every later copy is a duplicate.
4. **Oldest first.** New scans are applied in `scannedAtUtc` order, so a Pickup and a Delivered in the same batch end
   as Delivered.
5. **Per-item rejection.** A scan is rejected with a reason, while the valid scans of the batch are still stored,
   when it is for another shipment than the route, for an unknown shipment, would move the status back, or follows
   Delivered or Failed. Format errors in the body (scan id, courier id, missing signature, future time) are a 400 for
   the whole request instead, because the device sent a malformed batch.
6. **Parallel uploads.** When two uploads of the same scans commit at the same time, the database refuses the second
   commit (unique scan id or shipment version). The service discards its changes and runs once more, and the scans
   the other upload stored become duplicates.
7. **One commit per upload.** All accepted scans and the status change are saved together.
8. **The counts always add up:** `accepted + duplicatesSkipped + rejected.length` = number of scans sent.

The device marks accepted and duplicate scans `Synced`, and rejected scans `Failed`.

```json
{
  "accepted": 1,
  "duplicatesSkipped": 1,
  "rejected": [
    { "scanId": "scan::…", "reason": "The scan is for APX-20000001, not for shipment APX-31234567." }
  ]
}
```
