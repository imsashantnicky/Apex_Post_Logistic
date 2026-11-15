using System.Text.Json;
using System.Text.Json.Serialization;
using ApexPost.Courier.ApiClient.Contracts;

namespace ApexPost.Courier.ApiClient.Json;

/// <summary>
/// Source-generated serializer metadata for every v2 DTO, so the app needs no runtime reflection for JSON.
/// Use it through <see cref="ApexJson.Context"/>, which carries the shared options.
/// </summary>
[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    UseStringEnumConverter = true,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(ShipmentResponse))]
[JsonSerializable(typeof(ShipmentPage))]
[JsonSerializable(typeof(RecipientResponse))]
[JsonSerializable(typeof(LatestScanResponse))]
[JsonSerializable(typeof(CreateShipmentRequest))]
[JsonSerializable(typeof(UpdateShipmentRequest))]
[JsonSerializable(typeof(RecipientRequest))]
[JsonSerializable(typeof(UpdateStatusRequest))]
[JsonSerializable(typeof(ScanUploadRequest))]
[JsonSerializable(typeof(ScanItem))]
[JsonSerializable(typeof(ScanUploadResult))]
[JsonSerializable(typeof(RejectedScan))]
[JsonSerializable(typeof(ScanResponse))]
[JsonSerializable(typeof(IReadOnlyList<ScanResponse>))]
[JsonSerializable(typeof(PartnerShipmentResponse))]
[JsonSerializable(typeof(ApiProblem))]
[JsonSerializable(typeof(DevTokenRequest))]
[JsonSerializable(typeof(DevTokenResponse))]
public sealed partial class ApexJsonContext : JsonSerializerContext;
