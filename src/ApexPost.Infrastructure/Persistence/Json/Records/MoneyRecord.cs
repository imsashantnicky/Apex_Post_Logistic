namespace ApexPost.Infrastructure.Persistence.Json.Records;

/// <summary>An amount and its currency, stored side by side (two columns in a database).</summary>
public sealed record MoneyRecord
{
    public required decimal Amount { get; init; }

    public required string Currency { get; init; }
}
