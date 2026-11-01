using ApexPost.Domain.Common;
using ApexPost.Infrastructure.Persistence.Json.Records;

namespace ApexPost.Infrastructure.Persistence.Json.Mapping;

public static class MoneyMapping
{
    public static MoneyRecord ToRecord(this Money money)
    {
        ArgumentNullException.ThrowIfNull(money);
        return new() { Amount = money.Amount, Currency = money.Currency };
    }

    public static Money ToMoney(this MoneyRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new(record.Amount, record.Currency);
    }
}
