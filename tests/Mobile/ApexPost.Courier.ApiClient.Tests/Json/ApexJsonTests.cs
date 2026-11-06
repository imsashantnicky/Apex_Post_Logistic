using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ApexPost.Courier.ApiClient.Contracts;
using ApexPost.Courier.ApiClient.Json;
using ApexPost.Courier.Domain.Enums;

namespace ApexPost.Courier.ApiClient.Tests.Json;

public sealed class ApexJsonTests
{
    private static readonly DateTime Noon = new(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);

    public static TheoryData<object> EveryV2Dto => new()
    {
        new ShipmentResponse("APX-20000005", new RecipientResponse("Mia", "+1", "5 Road", "Northgate"), 2.5m,
            ShipmentStatus.InTransit, new LatestScanResponse(ScanType.InTransit, "Hub", Noon), Noon, 3),
        new ShipmentPage([], 1, 20, 0, 0, false),
        new CreateShipmentRequest("APX-20000099", new RecipientRequest("Leo", "+1", "1 Quay", "Westfield"), 1.2m),
        new UpdateShipmentRequest(new RecipientRequest("Leo", "+1", "1 Quay", "Westfield"), 1.2m),
        new UpdateStatusRequest(ShipmentStatus.OutForDelivery),
        new ScanUploadRequest([new ScanItem("scan::1", "APX-20000005", ScanType.Pickup, "CR-101", "Depot", Noon, null, null)]),
        new ScanUploadResult(1, 0, [new RejectedScan("scan::2", "Moves status back.")]),
        new ScanResponse("scan::1", "APX-20000005", ScanType.Failed, "CR-102", "Door", Noon, "Nobody home", Noon),
        new PartnerShipmentResponse("APX-20000005", ShipmentStatus.Delivered, "Northgate", null),
        new ApiProblem { Title = "Not Found", Status = 404, TraceId = "t" },
    };

    [Theory]
    [MemberData(nameof(EveryV2Dto))]
    public void Every_v2_dto_round_trips_through_the_source_generated_context(object dto)
    {
        var typeInfo = ApexJson.Context.GetTypeInfo(dto.GetType());
        Assert.NotNull(typeInfo);

        var json = JsonSerializer.Serialize(dto, typeInfo);
        var copy = JsonSerializer.Deserialize(json, typeInfo);

        Assert.Equal(json, JsonSerializer.Serialize(copy, typeInfo));
    }

    [Fact]
    public void The_shared_options_write_camel_case_names_and_enum_names()
    {
        var json = JsonSerializer.Serialize(new UpdateStatusRequest(ShipmentStatus.PickedUp), ApexJson.Options);

        Assert.Equal("""{"status":"PickedUp"}""", json);
    }

    [Fact]
    public void The_shared_options_are_case_insensitive_and_use_the_generated_context()
    {
        Assert.True(ApexJson.Options.PropertyNameCaseInsensitive);
        Assert.Same(JsonNamingPolicy.CamelCase, ApexJson.Options.PropertyNamingPolicy);
        Assert.Same(ApexJson.Context, ApexJson.Options.TypeInfoResolver);

        var status = JsonSerializer.Deserialize<UpdateStatusRequest>("""{"STATUS":"delivered"}""", ApexJson.Options);

        Assert.Equal(ShipmentStatus.Delivered, status?.Status);
    }

    [Fact]
    public void Metadata_comes_from_generated_code_not_reflection()
    {
        var typeInfo = ApexJson.Context.ShipmentResponse;

        Assert.Equal(JsonTypeInfoKind.Object, typeInfo.Kind);
        Assert.IsType<ApexJsonContext>(typeInfo.OriginatingResolver);
        Assert.Contains(typeInfo.Properties, property => property.Name == "latestScan");
    }

    [Fact]
    public void A_type_outside_the_context_is_refused()
    {
        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(new Uri("https://x.example"), ApexJson.Options));
    }
}
