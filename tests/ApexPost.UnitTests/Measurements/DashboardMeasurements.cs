using System.Globalization;
using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Reports;
using ApexPost.Application.Settings;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Personnel;
using ApexPost.Infrastructure.Seeding;
using ApexPost.UnitTests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using static ApexPost.UnitTests.Support.CounterSteps;

namespace ApexPost.UnitTests.Measurements;

[Trait("Category", "Measurement")]
[Collection(nameof(SerialMeasurements))]
public sealed class DashboardMeasurements(ITestOutputHelper output)
{
    private const int Repeats = 21;

    private static readonly TimeSpan SimulatedLatency = TimeSpan.FromMilliseconds(25);
    private static readonly BranchCode Branch = new(TestData.HomeBranch);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Sequential_loading_against_Task_WhenAll(bool withLatency)
    {
        using var folder = new TempDataFolder();
        await using var host = InfrastructureHost.Build(folder.Location, folder.Location);
        await host.GetRequiredService<DemoDataSeeder>().SeedIfAllowedAsync(Ct);
        await using var scope = host.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IReportQueries>();
        var sink = scope.ServiceProvider.GetRequiredService<INotificationSink>();
        if (withLatency)
        {
            queries = new SlowReportQueries(queries, SimulatedLatency);
            sink = new SlowNotificationSink(sink, SimulatedLatency);
        }

        var clock = new FixedClock();
        var user = new FakeCurrentUser { Role = StaffRole.BranchManager, Branch = Branch };
        var reports = new ReportService(queries, sink, user, clock, Options.Create(new StockSettings()));

        var sequential = await MeasuredTime.OfAsync(Repeats, () => LoadOneByOneAsync(queries, sink, clock));
        var parallel = await MeasuredTime.OfAsync(Repeats, () => reports.DashboardAsync(Branch, Ct));

        var source = withLatency ? $"JSON + {SimulatedLatency.TotalMilliseconds} ms per source" : "JSON files only";
        output.WriteLine($"{source}, sequential: {sequential}");
        output.WriteLine($"{source}, Task.WhenAll: {parallel}");
        output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{source}, speed-up x{sequential.MedianMs / parallel.MedianMs:F2}"));
        Assert.True(sequential.MedianMs > 0 && parallel.MedianMs > 0);
    }

    private static async Task LoadOneByOneAsync(IReportQueries queries, INotificationSink sink, IClock clock)
    {
        var today = clock.Today;
        await queries.DailySalesAsync(new ReportFilter(Branch, today, today), Ct);
        await queries.LowStockAsync(Branch, new StockSettings().LowStockThreshold, Ct);
        await sink.RecentAsync(Branch, ReportService.DashboardAlertCount, Ct);
    }
}
