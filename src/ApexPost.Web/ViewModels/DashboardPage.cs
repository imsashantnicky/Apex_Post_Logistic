using ApexPost.Application.Reports;

namespace ApexPost.Web.ViewModels;

public sealed record DashboardPage(Dashboard Dashboard, ReportToolbar Toolbar, bool CanTransact, bool CanViewReports);
