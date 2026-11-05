namespace ApexPost.Infrastructure.Persistence.Configurations;

/// <summary>The fixed moment recorded for the reference rows that the migrations insert.</summary>
internal static class Seed
{
    public static readonly DateTime At = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
}
