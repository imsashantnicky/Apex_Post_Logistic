using ApexPost.Application.Reports;

namespace ApexPost.Web.ViewModels;

public sealed record DashboardPage(Dashboard Dashboard, string Scope, BranchFilter Filter, bool CanTransact);
