using ApexPost.Domain.Transactions;

namespace ApexPost.Infrastructure.Persistence.Json.Records;

public sealed record PaymentRecord
{
    public required PaymentMethod Method { get; init; }

    public required MoneyRecord Tax { get; init; }

    public required DateTimeOffset PaidAt { get; init; }
}
