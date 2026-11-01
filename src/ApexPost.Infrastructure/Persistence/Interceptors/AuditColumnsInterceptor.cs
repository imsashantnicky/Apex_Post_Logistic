using ApexPost.Application.Abstractions;
using ApexPost.Application.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ApexPost.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Fills CreatedAtUtc, CreatedBy and ModifiedAtUtc just before a save, so that no repository or service has to.
/// Work done without a signed-in user (login, seeding) is recorded as "system".
/// </summary>
public sealed class AuditColumnsInterceptor(TimeProvider time, ICurrentUser user) : SaveChangesInterceptor
{
    private const string SystemUser = "system";

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = time.GetUtcNow().UtcDateTime;
        var by = CurrentStaff();
        foreach (var entry in context.ChangeTracker.Entries().Where(IsChanged))
        {
            foreach (var property in entry.Properties.Where(property => property.Metadata.IsShadowProperty()))
            {
                Set(entry, property, now, by);
            }
        }
    }

    private static void Set(EntityEntry entry, PropertyEntry property, DateTime now, string by)
    {
        var name = property.Metadata.Name;
        if (name.EndsWith("CreatedAtUtc", StringComparison.Ordinal) && entry.State == EntityState.Added)
        {
            property.CurrentValue = now;
        }
        else if (name.EndsWith("CreatedBy", StringComparison.Ordinal) && entry.State == EntityState.Added)
        {
            property.CurrentValue = by;
        }
        else if (name.EndsWith("ModifiedAtUtc", StringComparison.Ordinal) && entry.State == EntityState.Modified)
        {
            property.CurrentValue = now;
        }
    }

    private static bool IsChanged(EntityEntry entry) => entry.State is EntityState.Added or EntityState.Modified;

    private string CurrentStaff()
    {
        try
        {
            return user.StaffId.ToString();
        }
        catch (ForbiddenException)
        {
            return SystemUser;
        }
    }
}
