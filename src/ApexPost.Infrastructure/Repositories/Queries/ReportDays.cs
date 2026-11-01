namespace ApexPost.Infrastructure.Repositories.Queries;

/// <summary>Turns UTC days into the half-open range a query compares with, so that an index can be used.</summary>
internal static class ReportDays
{
    public static DateTimeOffset Start(DateOnly day) =>
        new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    public static DateTimeOffset EndOf(DateOnly day) => Start(day.AddDays(1));
}
