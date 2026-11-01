using ApexPost.Domain.Branches;

namespace ApexPost.Application.DTOs;

/// <summary>Branch (null = all branches) and an inclusive range of UTC days.</summary>
public sealed record ReportFilter(BranchCode? Branch, DateOnly From, DateOnly To)
{
    public DateOnly To { get; } = To >= From ? To : From;

    public bool Covers(DateTimeOffset moment)
    {
        var day = DateOnly.FromDateTime(moment.UtcDateTime);
        return day >= From && day <= To;
    }
}
