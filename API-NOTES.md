# API notes – shipment tracking API (Week 4 Part B)

`src/ApexPost.Api` is the REST API of the shipment tracking module. This page lists the endpoints and their status
codes, the status lifecycle and the rules that make the scan upload safe to repeat. Every request below has a
ready-to-run example (success and failure) in [src/ApexPost.Api/ApexPost.Api.http](src/ApexPost.Api/ApexPost.Api.http).

## Conventions

- Base route: `/api/shipments` (the v1 contract). Bodies are `application/json`; any other content type gets 415.
- `{trackingNumber}` must match `^APX-\d{8}$` (route constraint). A value in another format never reaches an action
  and is answered with 404.
- Every error is ProblemDetails (`application/problem+json`) with `traceId`. Validation errors are
  ValidationProblemDetails with `errors` per field. The stack trace is added in Development only.
- v1 writes enums as numbers. `status`: 1 Created, 2 PickedUp, 3 InTransit, 4 OutForDelivery, 5 Delivered, 6 Failed.
  `scanType`: 1 Pickup, 2 InTransit, 3 Delivered, 4 Failed. The `status` query filter accepts the name or the number.
- Paging: `page` 1 or more, `pageSize` 1 to 50, default 20. `pageSize` above 50 is a 400, not silently capped, so a
  client learns the limit instead of receiving fewer items than it asked for.

## Endpoints

| Endpoint | Verb | Status codes | Description |
| --- | --- | --- | --- |
| `/api/shipments` | GET | 200, 400 | One page of shipments, filtered by `status` and `city` (ignoring case). 400 for a bad `page`, `pageSize` or `status`. |
| `/api/shipments` | POST | 201, 400, 409, 415 | Creates a shipment in status Created. 201 with a `Location` header to `GET /api/shipments/{trackingNumber}`; 409 when the tracking number exists. |
| `/api/shipments/{trackingNumber}` | GET | 200, 404 | One shipment. |
| `/api/shipments/{trackingNumber}` | PUT | 200, 400, 404, 409, 415 | Replaces recipient and weight. 409 when the shipment is Delivered or Failed. |
| `/api/shipments/{trackingNumber}/status` | PATCH | 200, 400, 404, 409, 415 | Moves the shipment forward. 409 for a move that the transition table below does not allow. |
| `/api/shipments/{trackingNumber}` | DELETE | 204, 404, 409 | Deletes a shipment that is still Created; 409 for any other status. |
| `/api/shipments/{trackingNumber}/scans` | POST | 200, 400, 404, 415 | Idempotent upload of 1 to 100 scans. 200 with `{ accepted, duplicatesSkipped, rejected }`. |
| `/api/shipments/{trackingNumber}/scans` | GET | 200, 404 | The scans of the shipment, oldest first by `scannedAtUtc`. |

Any unexpected exception is a 500 ProblemDetails with a generic detail, written by the global `ApiExceptionHandler`.

### Validation rules

| Field | Rule |
| --- | --- |
| `trackingNumber` | `APX-` followed by 8 digits |
| `recipientName`, `recipientAddress`, `recipientCity` | Required, maximum length from `Recipient` |
| `recipientPhone` | 7 to 15 digits or spaces, optional leading `+` |
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
designed so that a repeat changes nothing.

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
