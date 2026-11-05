# Week 4 Part B – screenshots

Screenshots of the shipment tracking API. Start it with
`dotnet run --project src/ApexPost.Api --launch-profile https` (Development seeds `APX-20000001` to `APX-20000010`),
open `src/ApexPost.Api/ApexPost.Api.http` and send the request named in each row. Each screenshot shows the request
and the full response (status line, headers and body). Save them in this folder with the file names shown.

| File | What it shows | How to produce it |
| --- | --- | --- |
| `201-created-location.png` | `201 Created` with the `Location` header pointing at `/api/shipments/APX-3…` and the new shipment in status 1 (Created) | Send **201 - created** under `POST /api/shipments` |
| `400-validation-problem.png` | `400` ValidationProblemDetails (`application/problem+json`) with `errors` for the tracking number, recipient name, phone and weight, plus `traceId` | Send **400 - invalid tracking number, phone and weight** |
| `409-conflict.png` | `409` ProblemDetails, title "Invalid status transition", PickedUp cannot move back to Created | Send **409 - PickedUp cannot go back to Created** under `PATCH …/status` |
| `404-not-found.png` | `404` ProblemDetails "Shipment APX-29999999 does not exist." with `traceId` | Send **404 - unknown tracking number** under `GET /api/shipments/{trackingNumber}` |
| `scan-upload-counts.png` | `200` with `accepted: 1`, `duplicatesSkipped: 1` and two `rejected` items with reasons | Send **200 - first run** under `POST …/scans`, then **200 - mixed batch** |
| `scan-upload-repeat.png` (optional) | The same batch sent again: `accepted: 0`, every scan counted in `duplicatesSkipped` | Send **200 - first run** a second time |
