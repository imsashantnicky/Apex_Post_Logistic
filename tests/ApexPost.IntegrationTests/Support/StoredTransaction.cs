using ApexPost.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.IntegrationTests.Support;

/// <summary>What the database holds for one transaction, read past the application.</summary>
internal sealed record StoredTransaction(TransactionStatus Status, Payment? Payment, Reversal? Reversal)
{
    public static async Task<StoredTransaction> ReadAsync(ApexFactory factory, string id)
    {
        var key = Guid.Parse(id);
        await using var database = factory.OpenDatabase();
        return await database.Set<CounterTransaction>()
            .AsNoTracking()
            .Where(transaction => transaction.Id == key)
            .Select(transaction => new StoredTransaction(transaction.Status, transaction.Payment, transaction.Reversal))
            .SingleAsync();
    }
}
