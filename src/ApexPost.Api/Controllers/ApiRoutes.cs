namespace ApexPost.Api.Controllers;

/// <summary>The route templates and stable route names of the shipment endpoints.</summary>
public static class ApiRoutes
{
    /// <summary>The base route of the shipment endpoints (the v1 contract).</summary>
    public const string Shipments = "api/shipments";

    /// <summary>
    /// The tracking number segment. Only APX- followed by 8 digits matches, so any other value never reaches an
    /// action and is answered with 404. <c>{{8}}</c> is the route-template escape for the regex quantifier.
    /// </summary>
    public const string TrackingNumber = "{trackingNumber:regex(^APX-\\d{{8}}$)}";

    /// <summary>The scans of one shipment.</summary>
    public const string ShipmentScans = Shipments + "/" + TrackingNumber + "/scans";

    /// <summary>The only media type the endpoints read and write.</summary>
    public const string Json = "application/json";

    /// <summary>The media type of every error answer (RFC 9457 ProblemDetails).</summary>
    public const string ProblemJson = "application/problem+json";

    /// <summary>The OpenAPI tag of the shipment endpoints.</summary>
    public const string ShipmentsTag = "Shipments";

    /// <summary>The OpenAPI tag of the scan endpoints.</summary>
    public const string ScansTag = "Scans";

    // Route names are the operationIds of the OpenAPI document, so they never change once published. v1 names end
    // in V1; the v2 endpoints will use the same names without the suffix.

    /// <summary>Route name of GET /api/shipments.</summary>
    public const string ListShipments = "ListShipmentsV1";

    /// <summary>Route name of GET /api/shipments/{trackingNumber}; the Location of a created shipment.</summary>
    public const string GetShipment = "GetShipmentV1";

    /// <summary>Route name of POST /api/shipments.</summary>
    public const string CreateShipment = "CreateShipmentV1";

    /// <summary>Route name of PUT /api/shipments/{trackingNumber}.</summary>
    public const string ReplaceShipment = "ReplaceShipmentV1";

    /// <summary>Route name of PATCH /api/shipments/{trackingNumber}/status.</summary>
    public const string ChangeShipmentStatus = "ChangeShipmentStatusV1";

    /// <summary>Route name of DELETE /api/shipments/{trackingNumber}.</summary>
    public const string DeleteShipment = "DeleteShipmentV1";

    /// <summary>Route name of POST /api/shipments/{trackingNumber}/scans.</summary>
    public const string UploadScans = "UploadScansV1";

    /// <summary>Route name of GET /api/shipments/{trackingNumber}/scans.</summary>
    public const string GetScans = "GetScansV1";
}
