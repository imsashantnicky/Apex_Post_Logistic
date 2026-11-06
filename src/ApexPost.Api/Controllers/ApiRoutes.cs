namespace ApexPost.Api.Controllers;

/// <summary>The route templates, API versions and stable route names of the shipment endpoints.</summary>
public static class ApiRoutes
{
    /// <summary>The first contract (Part B): flat recipient fields, status as a number. Deprecated.</summary>
    public const string Version1 = "1.0";

    /// <summary>The current contract: nested recipient, latest scan and status as a string.</summary>
    public const string Version2 = "2.0";

    /// <summary>The URL segment value of v2, used to build its Location headers.</summary>
    public const string Version2Segment = "2";

    /// <summary>The route value that holds the version of a URL-segment route.</summary>
    public const string VersionRouteValue = "version";

    /// <summary>The versioned base route: /api/v1/shipments or /api/v2/shipments.</summary>
    public const string VersionedShipments = "api/v{" + VersionRouteValue + ":apiVersion}/shipments";

    /// <summary>
    /// The unversioned base route of Part B. It stays as an alias of v1 so existing clients keep working; a request
    /// without a version is treated as v1.
    /// </summary>
    public const string Shipments = "api/shipments";

    /// <summary>
    /// The tracking number segment. Only APX- followed by 8 digits matches, so any other value never reaches an
    /// action and is answered with 404. <c>{{8}}</c> is the route-template escape for the regex quantifier.
    /// </summary>
    public const string TrackingNumber = "{trackingNumber:regex(^APX-\\d{{8}}$)}";

    /// <summary>One shipment, versioned.</summary>
    public const string VersionedShipment = VersionedShipments + "/" + TrackingNumber;

    /// <summary>One shipment on the v1 alias.</summary>
    public const string Shipment = Shipments + "/" + TrackingNumber;

    /// <summary>The status segment below one shipment.</summary>
    public const string StatusSegment = "/status";

    /// <summary>The scans of one shipment, versioned.</summary>
    public const string VersionedShipmentScans = VersionedShipment + "/scans";

    /// <summary>The scans of one shipment on the v1 alias.</summary>
    public const string ShipmentScans = Shipment + "/scans";

    /// <summary>The only media type the endpoints read and write.</summary>
    public const string Json = "application/json";

    /// <summary>The media type of every error answer (RFC 9457 ProblemDetails).</summary>
    public const string ProblemJson = "application/problem+json";

    /// <summary>The OpenAPI tag of the shipment endpoints.</summary>
    public const string ShipmentsTag = "Shipments";

    /// <summary>The OpenAPI tag of the scan endpoints.</summary>
    public const string ScansTag = "Scans";

    /// <summary>The OpenAPI tag of the read-only partner endpoint.</summary>
    public const string PartnerTag = "Partner";

    /// <summary>The partner base route, v2 only: /api/v2/partner/shipments.</summary>
    public const string VersionedPartnerShipments =
        "api/v{" + VersionRouteValue + ":apiVersion}/partner/shipments";

    // Route names are the operationIds of the OpenAPI documents, so they never change once published. v1 names end
    // in V1; v2 names have no suffix, so the generated SDK gets clean method names. Only the versioned v1 routes are
    // named; the alias routes share the actions and stay unnamed (a route name must map to one template).

    /// <summary>Route name of GET /api/v1/shipments.</summary>
    public const string ListShipmentsV1 = "ListShipmentsV1";

    /// <summary>Route name of GET /api/v1/shipments/{trackingNumber}.</summary>
    public const string GetShipmentV1 = "GetShipmentV1";

    /// <summary>Route name of POST /api/v1/shipments.</summary>
    public const string CreateShipmentV1 = "CreateShipmentV1";

    /// <summary>Route name of PUT /api/v1/shipments/{trackingNumber}.</summary>
    public const string ReplaceShipmentV1 = "ReplaceShipmentV1";

    /// <summary>Route name of PATCH /api/v1/shipments/{trackingNumber}/status.</summary>
    public const string ChangeShipmentStatusV1 = "ChangeShipmentStatusV1";

    /// <summary>Route name of DELETE /api/v1/shipments/{trackingNumber}.</summary>
    public const string DeleteShipmentV1 = "DeleteShipmentV1";

    /// <summary>Route name of POST /api/v1/shipments/{trackingNumber}/scans.</summary>
    public const string UploadScansV1 = "UploadScansV1";

    /// <summary>Route name of GET /api/v1/shipments/{trackingNumber}/scans.</summary>
    public const string GetScansV1 = "GetScansV1";

    /// <summary>Route name of GET /api/v2/shipments.</summary>
    public const string ListShipments = "ListShipments";

    /// <summary>Route name of GET /api/v2/shipments/{trackingNumber}; the Location of a created v2 shipment.</summary>
    public const string GetShipment = "GetShipment";

    /// <summary>Route name of POST /api/v2/shipments.</summary>
    public const string CreateShipment = "CreateShipment";

    /// <summary>Route name of PUT /api/v2/shipments/{trackingNumber}.</summary>
    public const string ReplaceShipment = "ReplaceShipment";

    /// <summary>Route name of PATCH /api/v2/shipments/{trackingNumber}/status.</summary>
    public const string ChangeShipmentStatus = "ChangeShipmentStatus";

    /// <summary>Route name of DELETE /api/v2/shipments/{trackingNumber}.</summary>
    public const string DeleteShipment = "DeleteShipment";

    /// <summary>Route name of POST /api/v2/shipments/{trackingNumber}/scans.</summary>
    public const string UploadScans = "UploadScans";

    /// <summary>Route name of GET /api/v2/shipments/{trackingNumber}/scans.</summary>
    public const string GetScans = "GetScans";

    /// <summary>Route name of GET /api/v2/partner/shipments/{trackingNumber}.</summary>
    public const string GetPartnerShipment = "GetPartnerShipment";
}
