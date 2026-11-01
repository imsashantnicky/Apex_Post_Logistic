namespace ApexPost.Infrastructure.Persistence.Json.Records;

public sealed record MoneyOrderLineRecord : LineItemRecord
{
    public required MoneyRecord Amount { get; init; }

    public required MoneyRecord Fee { get; init; }

    public required string Sender { get; init; }

    public required string Recipient { get; init; }
}
