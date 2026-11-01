using System.Net;
using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Personnel;
using ApexPost.IntegrationTests.Support;
using ApexPost.Web.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApexPost.IntegrationTests.Filters;

public sealed class FilterPipelineTests(SlowActionFactory factory) : IClassFixture<SlowActionFactory>
{
    [Fact]
    public void Global_filters_are_registered_in_their_documented_order()
    {
        var filters = factory.Services.GetRequiredService<IOptions<MvcOptions>>().Value.Filters
            .OfType<ServiceFilterAttribute>()
            .OrderBy(filter => filter.Order)
            .Select(filter => (filter.ServiceType, filter.Order));

        Assert.Equal(
            [
                (typeof(AuditActionFilter), -1000),
                (typeof(PerformanceFilter), -900),
                (typeof(ValidationFilter), -500),
                (typeof(BranchAccessFilter), -400),
                (typeof(DomainExceptionFilter), -300),
            ],
            filters);
    }

    [Fact]
    public async Task Performance_filter_warns_about_a_slow_action_by_name()
    {
        using var browser = new CounterBrowser(factory);

        using var response = await browser.GetAsync("/Account/Login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entry = await factory.Logs.WaitForAsync(log =>
            log.Level == LogLevel.Warning && log.Category == typeof(PerformanceFilter).FullName);
        Assert.Contains("AccountController.Login", entry.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(StaffRole.CounterClerk, "BR002", true)]
    [InlineData(StaffRole.BranchManager, "br002", true)]
    [InlineData(StaffRole.CounterClerk, "BR001", false)]
    [InlineData(StaffRole.CounterClerk, "br001", false)]
    [InlineData(StaffRole.CounterClerk, "", false)]
    [InlineData(StaffRole.CounterClerk, "nonsense", false)]
    [InlineData(StaffRole.Auditor, "BR002", false)]
    [InlineData(StaffRole.Administrator, "BR002", false)]
    public async Task Branch_filter_blocks_only_another_branch(StaffRole role, string branch, bool blocked)
    {
        var filter = new BranchAccessFilter(new StaffAt(role));
        var context = Executing(new Dictionary<string, object?> { ["branch"] = branch });
        var reachedAction = false;

        await filter.OnActionExecutionAsync(context, () =>
        {
            reachedAction = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        Assert.Equal(blocked, context.Result is ForbidResult);
        Assert.Equal(blocked, !reachedAction);
    }

    [Fact]
    public async Task Audit_entry_survives_a_client_that_hangs_up()
    {
        var store = new RecordingAuditStore();
        var filter = new AuditActionFilter(store, new FixedClock());
        var context = Executing([]);
        context.ActionDescriptor.EndpointMetadata = [new AuditAttribute(AuditEvents.Payment)];
        context.RouteData.Values["controller"] = "Transactions";
        context.RouteData.Values["action"] = "Pay";
        using var hungUp = new CancellationTokenSource();
        await hungUp.CancelAsync();
        context.HttpContext.RequestAborted = hungUp.Token;

        await filter.OnActionExecutionAsync(context, () =>
            Task.FromResult(new ActionExecutedContext(context, [], null!) { Result = new OkResult() }));

        Assert.True(store.Token.CanBeCanceled is false);
        Assert.Equal(200, Assert.Single(store.Entries).StatusCode);
    }

    private static ActionExecutingContext Executing(Dictionary<string, object?> arguments) =>
        new(
            new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
            [],
            arguments,
            controller: null!);

    private sealed class StaffAt(StaffRole role) : ICurrentUser
    {
        public Guid StaffId { get; } = Guid.NewGuid();

        public StaffRole Role => role;

        public BranchCode Branch { get; } = new("BR001");
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = DateTimeOffset.UnixEpoch;
    }

    private sealed class RecordingAuditStore : IAuditStore
    {
        public List<AuditEntry> Entries { get; } = [];

        public CancellationToken Token { get; private set; }

        public Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            Token = cancellationToken;
            return Task.CompletedTask;
        }

        public Task<PagedResult<AuditEntry>> ReadAsync(PageRequest page, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
