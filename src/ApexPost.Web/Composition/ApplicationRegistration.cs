using ApexPost.Application.Reports;
using ApexPost.Application.Services;
using ApexPost.Application.Settings;
using ApexPost.Application.Validation;

namespace ApexPost.Web.Composition;

/// <summary>
/// The application layer: settings, then singletons for what holds state or is safe to share, then scoped
/// services that act for the signed-in user.
/// </summary>
public static class ApplicationRegistration
{
    public static IServiceCollection AddApexApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<TaxSettings>(configuration.GetSection(TaxSettings.SectionName));
        services.Configure<MoneyOrderSettings>(configuration.GetSection(MoneyOrderSettings.SectionName));
        services.Configure<StockSettings>(configuration.GetSection(StockSettings.SectionName));
        services.Configure<ShippingSettings>(configuration.GetSection(ShippingSettings.SectionName));

        services.AddSingleton<AttributeModelValidator>();
        services.AddSingleton<CounterEvents>();
        services.AddSingleton<NotificationSubscriber>();
        services.AddSingleton<StockGate>();
        services.AddSingleton<TaxCalculator>();
        services.AddSingleton<CheckoutRules>();
        services.AddSingleton<ShippingPricer>();
        services.AddSingleton<LoginService>();

        services.AddScoped<StockLedger>();
        services.AddScoped<TransactionAccess>();
        services.AddScoped<CheckoutService>();
        services.AddScoped<ReversalService>();
        services.AddScoped<StockService>();
        services.AddScoped<CatalogueService>();
        services.AddScoped<BranchService>();
        services.AddScoped<StaffService>();
        services.AddScoped<ReportService>();
        return services;
    }
}
