namespace ApexPost.Courier.ApiClient.Tests.Support;

/// <summary>Bodies shaped like real v2 answers (fictional data).</summary>
public static class SampleJson
{
    public const string Json = "application/json";

    public const string ProblemJson = "application/problem+json";

    public const string Shipment = """
        {
          "trackingNumber": "APX-20000005",
          "recipient": { "name": "Mia Example", "phone": "+10000000005", "address": "5 Harbour Road", "city": "Northgate" },
          "weightKg": 2.5,
          "status": "InTransit",
          "latestScan": { "scanType": "InTransit", "location": "Central Hub", "scannedAtUtc": "2026-11-05T09:30:00Z" },
          "createdAtUtc": "2026-11-05T08:00:00Z",
          "version": 3
        }
        """;

    public const string ShipmentWithoutScan = """
        {
          "trackingNumber": "APX-20000001",
          "recipient": { "name": "Leo Example", "phone": "+10000000001", "address": "1 Quay Street", "city": "Westfield" },
          "weightKg": 1.0,
          "status": "Created",
          "latestScan": null,
          "createdAtUtc": "2026-11-05T08:00:00Z",
          "version": 1
        }
        """;

    public const string NotFoundProblem = """
        {
          "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
          "title": "Not Found",
          "status": 404,
          "detail": "Shipment APX-00000000 was not found.",
          "traceId": "00-abc-def-01"
        }
        """;

    public const string ValidationProblem = """
        {
          "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
          "title": "One or more validation errors occurred.",
          "status": 400,
          "errors": {
            "Scans[0].Location": [ "The Location field is required." ],
            "Scans[0].RecipientSignatureName": [ "A Delivered scan needs the recipient's signature name." ]
          },
          "traceId": "00-abc-def-02"
        }
        """;
}
