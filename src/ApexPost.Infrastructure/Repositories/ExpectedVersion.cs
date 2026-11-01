using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ApexPost.Infrastructure.Repositories;

/// <summary>
/// Optimistic concurrency for records edited on a form: the UPDATE only finds the row while it still has the version
/// the form was opened with, and moves the version on, so a stale form changes nothing.
/// </summary>
internal static class ExpectedVersion
{
    private const string Column = "Version";

    public static void Apply(EntityEntry entry, int expectedVersion)
    {
        var version = entry.Property(Column);
        version.OriginalValue = expectedVersion;
        version.CurrentValue = expectedVersion + 1;
    }
}
