# Week 4 Part C – screenshots

Screenshots of the API documentation, the two API versions and the generated client. Start the API in Development
with `dotnet run --project src/ApexPost.Api --launch-profile https` (it seeds `APX-20000001` to `APX-20000010`).
For the `.http` rows, open `src/ApexPost.Api/ApexPost.Api.http` and send the request named in the row; each screenshot
shows the request and the full response (status line, headers and body). Save the files in this folder with the
names shown.

| File | What it shows | How to produce it |
| --- | --- | --- |
| `swagger-v2.png` | Swagger UI with "v2" selected in the "Select a definition" drop-down: title "ApexPost Logistics API", version 2, the Shipments and Scans tags with all eight operations | Open `https://localhost:7310/swagger` (v2 is shown first) |
| `swagger-v1-deprecated.png` | Swagger UI with "v1 (deprecated)" selected: the deprecation notice with the sunset date in the description, and every operation struck through as deprecated | Choose "v1 (deprecated)" in the drop-down |
| `swagger-dropdown.png` | The open drop-down listing both "v2" and "v1 (deprecated)" | Click the "Select a definition" drop-down |
| `swagger-operation-details.png` (optional) | One v2 operation expanded: summary, description, request example, every response code (200, 400, 404, 409, 415, 500) with its schema | Expand `PATCH /api/v2/shipments/{trackingNumber}/status` |
| `400-validation-problem-v2.png` | `400` ValidationProblemDetails (`application/problem+json`) with `errors` for `TrackingNumber`, `Recipient.Name`, `Recipient.Phone` and `WeightKg`, plus `traceId` | Send **v2 400 - invalid tracking number, nested recipient errors** under `v2 POST /api/v2/shipments` |
| `409-conflict-v2.png` | `409` ProblemDetails, title "Invalid status transition": PickedUp cannot move back to Created | Send **v2 409 - PickedUp cannot go back to Created** under `v2 PATCH …/status` |
| `201-created-location-v2.png` | `201 Created` with the `Location` header `https://localhost:7310/api/v2/shipments/APX-5…`, the `api-supported-versions` header and the body with nested `recipient`, `"status": "Created"` and `"latestScan": null` | Send **v2 201 - created** under `v2 POST /api/v2/shipments` |
| `v1-deprecation-headers.png` | A v1 answer with `api-deprecated-versions: 1.0`, `Deprecation: @1793836800`, `Sunset: Mon, 31 May 2027 00:00:00 GMT` and both `Link` headers | Send **200 - the Part B alias /api/shipments is v1** under `Versioning` |
| `consumer-output.png` | The console consumer: "ApexPost API v2 at …", 1. Created, 2. Fetched, 3. Uploaded 2 scans (accepted 2, duplicates skipped 0, rejected 0) with the status now InTransit, 4. the friendly "No shipment with tracking number APX-00000000 exists" message | Start the API with `--launch-profile http`, then run `dotnet run --project src/ApexPost.Client.Consumer` in a second terminal |
