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

    /// <summary>Route name of GET /api/shipments.</summary>
    public const string ListShipments = "ListShipmentsV1";

    /// <summary>Route name of GET /api/shipments/{trackingNumber}; the Location of a created shipment.</summary>
    public const string GetShipment = "GetShipmentV1";

    /// <summary>Route name of POST /api/shipments.</summary>
    public const string CreateShipment = "CreateShipmentV1";

    /// <summary>Route name of PUT /api/shipments/{trackingNumber}.</summary>
    public const string UpdateShipment = "UpdateShipmentV1";

    /// <summary>Route name of PATCH /api/shipments/{trackingNumber}/status.</summary>
    public const string UpdateShipmentStatus = "UpdateShipmentStatusV1";

    /// <summary>Route name of DELETE /api/shipments/{trackingNumber}.</summary>
    public const string DeleteShipment = "DeleteShipmentV1";

    /// <summary>Route name of POST /api/shipments/{trackingNumber}/scans.</summary>
    public const string UploadScans = "UploadShipmentScansV1";

    /// <summary>Route name of GET /api/shipments/{trackingNumber}/scans.</summary>
    public const string ListScans = "ListShipmentScansV1";
}
