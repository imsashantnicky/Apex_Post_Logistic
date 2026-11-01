using ApexPost.Domain.Branches;

namespace ApexPost.Infrastructure.Seeding;

/// <summary>Six branches; BR006 Old Quay is closed, so it shows the closed-branch rule.</summary>
public static class DemoBranches
{
    public const string ClosedBranch = "BR006";

    private static readonly (string Code, string Name, string Region, int Opens, int Closes, int Counters)[] Rows =
    [
        ("BR001", "Central Counter", "City Centre", 8, 18, 3),
        ("BR002", "Harbour Point", "Coast", 8, 17, 2),
        ("BR003", "Airport Terminal", "Airport", 6, 22, 3),
        ("BR004", "Northgate", "North", 9, 17, 2),
        ("BR005", "Westfield", "West", 9, 19, 2),
        (ClosedBranch, "Old Quay", "Coast", 9, 16, 1),
    ];

    public static IReadOnlyList<Branch> Create() =>
    [
        .. Rows.Select(row => new Branch(
            new BranchCode(row.Code),
            row.Name,
            row.Region,
            new TimeOnly(row.Opens, 0),
            new TimeOnly(row.Closes, 0),
            Enumerable.Range(1, row.Counters).Select(number => new Counter(Guid.CreateVersion7(), number)),
            isActive: row.Code != ClosedBranch)),
    ];
}
