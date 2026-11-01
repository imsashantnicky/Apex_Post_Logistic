using ApexPost.Application.Abstractions;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Personnel;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.IntegrationTests.Support;

/// <summary>Reads the stored staff and branches, for the ids and versions that the forms post back.</summary>
internal static class AdminData
{
    public static async Task<Staff> StaffAsync(this ApexFactory factory, string email)
    {
        var staff = factory.Services.GetRequiredService<IRepository<Staff, Guid>>();
        var all = await staff.ListAsync(CancellationToken.None);
        return Assert.Single(all, member => member.Email == email);
    }

    public static async Task<Branch> BranchAsync(this ApexFactory factory, string code)
    {
        var branches = factory.Services.GetRequiredService<IRepository<Branch, BranchCode>>();
        var branch = await branches.GetAsync(new BranchCode(code), CancellationToken.None);
        Assert.NotNull(branch);
        return branch;
    }
}
