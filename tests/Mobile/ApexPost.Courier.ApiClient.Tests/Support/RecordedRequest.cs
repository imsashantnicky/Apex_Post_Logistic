namespace ApexPost.Courier.ApiClient.Tests.Support;

public sealed record RecordedRequest(
    HttpMethod Method,
    Uri Uri,
    string? Authorization,
    string? ContentType,
    string? Body);
