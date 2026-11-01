using ApexPost.Application.Abstractions;
using ApexPost.Domain.Personnel;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.IntegrationTests.Support;

/// <summary>Outside Development, and every sign-in fails with an exception nobody planned for.</summary>
public sealed class BrokenSignInFactory : ProductionFactory
{
    public const string Internals = "staff table secret detail";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services => services.AddScoped<IStaffRepository, BrokenStaff>());
    }

    private sealed class BrokenStaff : IStaffRepository
    {
        public Task<Staff?> GetAsync(Guid id, CancellationToken cancellationToken) => throw Failure();

        public Task<Staff?> FindByEmailAsync(string email, CancellationToken cancellationToken) => throw Failure();

        public Task<bool> EmailTakenAsync(string email, Guid? ownId, CancellationToken cancellationToken) =>
            throw Failure();

        public void Add(Staff member) => throw Failure();

        public void Update(Staff member, int expectedVersion) => throw Failure();

        private static InvalidOperationException Failure() => new(Internals);
    }
}
