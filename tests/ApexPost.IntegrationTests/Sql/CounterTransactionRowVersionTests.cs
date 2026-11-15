using ApexPost.Application.Exceptions;
using ApexPost.Infrastructure.Repositories;
using ApexPost.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.IntegrationTests.Sql;

/// <summary>
/// The SQL Server <c>rowversion</c> column of CounterTransactions as the concurrency token of the repository: two
/// requests read the same basket, the first save moves the row version, and the second save is refused. Only a real
/// database changes a rowversion, so this cannot be shown with a mocked or in-memory DbContext.
/// </summary>
[Trait("Category", "Integration")]
public sealed class CounterTransactionRowVersionTests(ApexFactory factory) : IClassFixture<ApexFactory>
{
    private const string ConflictMessage = "The record was changed by someone else.";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SaveChangesAsync_StaleRowVersion_ThrowsConcurrencyConflictException()
    {
        // Arrange
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);
        var id = Guid.Parse(counter.Id);
        await using var first = factory.OpenDatabase();
        await using var second = factory.OpenDatabase();
        var firstRepository = new CounterTransactionRepository(first);
        var secondRepository = new CounterTransactionRepository(second);
        var firstCopy = await firstRepository.GetAsync(id, Ct);
        var secondCopy = await secondRepository.GetAsync(id, Ct);
        var versionBefore = await RowVersionAsync(id);
        firstRepository.Update(firstCopy!);
        await new UnitOfWork(first).SaveChangesAsync(Ct);
        secondRepository.Update(secondCopy!);

        // Act
        var save = () => new UnitOfWork(second).SaveChangesAsync(Ct);

        // Assert
        var error = await Assert.ThrowsAsync<ConcurrencyConflictException>(save);
        Assert.Equal(ConflictMessage, error.Message);
        Assert.IsType<DbUpdateConcurrencyException>(error.InnerException);
        Assert.NotEqual(versionBefore, await RowVersionAsync(id));
    }

    private async Task<string> RowVersionAsync(Guid id)
    {
        await using var database = factory.OpenDatabase();
        return await database.Database
            .SqlQuery<string>(
                $"SELECT CONVERT(varchar(18), RowVersion, 1) AS [Value] FROM CounterTransactions WHERE Id = {id}")
            .SingleAsync(Ct);
    }
}
