using ApexPost.Domain.Branches;
using ApexPost.Domain.Personnel;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.IntegrationTests.Support;

/// <summary>Reads the stored staff and branches, for the ids and versions that the forms post back.</summary>
internal static class AdminData
{
    public static async Task<Staff> StaffAsync(this ApexFactory factory, string email)
    {
        await using var database = factory.OpenDatabase();
        return await database.Set<Staff>().AsNoTracking().SingleAsync(member => member.Email == email);
    }

    public static async Task<Branch> BranchAsync(this ApexFactory factory, string code)
    {
        var branchCode = new BranchCode(code);
        await using var database = factory.OpenDatabase();
        return await database.Set<Branch>()
            .AsNoTracking()
            .Include(branch => branch.Counters.OrderBy(counter => counter.Number))
            .SingleAsync(branch => branch.Id == branchCode);
    }
}
