using ApexPost.Domain.Branches;

namespace ApexPost.Application.DTOs;

public sealed record BranchSummary(
    BranchCode Code,
    string Name,
    string Region,
    TimeOnly OpensAt,
    TimeOnly ClosesAt,
    bool IsActive,
    IReadOnlyList<CounterView> Counters,
    int Version)
{
    public static BranchSummary From(Branch branch) =>
        new(
            branch.Code,
            branch.Name,
            branch.Region,
            branch.OpensAt,
            branch.ClosesAt,
            branch.IsActive,
            [.. branch.Counters.Select(counter => new CounterView(counter.Id, counter.Number))],
            branch.Version);
}
