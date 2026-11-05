using ApexPost.Application.DTOs;

namespace ApexPost.Application.Abstractions;

/// <summary>Read-only staff data for the screens, ordered by home branch and name.</summary>
public interface IStaffQueries
{
    Task<IReadOnlyList<StaffSummary>> ListAsync(CancellationToken cancellationToken);

    Task<StaffSummary?> FindAsync(Guid id, CancellationToken cancellationToken);
}
