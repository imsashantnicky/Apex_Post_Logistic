using ApexPost.Application.Abstractions;
using ApexPost.Infrastructure.Repositories;
using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.IntegrationTests.Support;

/// <summary>Development logging as it is, so a test can read what EF Core sent, and a count of the commits.</summary>
public sealed class CommandLogFactory : ApexFactory
{
    private const string CommandCategory = "Microsoft.EntityFrameworkCore.Database.Command";
    private const string ExecutedCommand = "Executed DbCommand";

    private int _saves;

    /// <summary>How many times a unit of work has been saved so far.</summary>
    public int Saves => Volatile.Read(ref _saves);

    public int CommandsOf(HttpResponseMessage response)
    {
        var scope = $"CorrelationId={Assert.Single(response.Headers.GetValues(ApexHeaders.CorrelationId))}";
        return Logs.Entries.Count(entry => entry.Category == CommandCategory
            && entry.Scopes.Contains(scope)
            && entry.Message.Contains(ExecutedCommand, StringComparison.Ordinal));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.AddScoped<UnitOfWork>();
            services.AddScoped<IUnitOfWork>(provider =>
                new CountingUnitOfWork(provider.GetRequiredService<UnitOfWork>(), this));
        });
    }

    private sealed class CountingUnitOfWork(IUnitOfWork inner, CommandLogFactory factory) : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref factory._saves);
            return inner.SaveChangesAsync(cancellationToken);
        }

        public Task ExecuteInTransactionAsync(
            Func<CancellationToken, Task> work,
            CancellationToken cancellationToken) =>
            inner.ExecuteInTransactionAsync(work, cancellationToken);
    }
}
