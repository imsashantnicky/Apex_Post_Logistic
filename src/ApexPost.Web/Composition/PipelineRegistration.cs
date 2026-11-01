using ApexPost.Web.Filters;
using ApexPost.Web.Health;
using ApexPost.Web.Middleware;
using ApexPost.Web.Security;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace ApexPost.Web.Composition;

/// <summary>The settings and services behind the middleware pipeline.</summary>
public static class PipelineRegistration
{
    private const string DataStoreCheck = "data-store";

    public static IServiceCollection AddApexPipeline(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MaintenanceSettings>(configuration.GetSection(MaintenanceSettings.SectionName));
        services.Configure<SecuritySettings>(configuration.GetSection(SecuritySettings.SectionName));
        services.Configure<IntegrationSettings>(configuration.GetSection(IntegrationSettings.SectionName));
        services.Configure<RateLimitSettings>(configuration.GetSection(RateLimitSettings.SectionName));
        services.Configure<PerformanceSettings>(configuration.GetSection(PerformanceSettings.SectionName));

        services.AddExceptionHandler<ProblemExceptionHandler>();
        services.AddHealthChecks().AddCheck<DataStoreHealthCheck>(DataStoreCheck);
        services.AddRateLimiter(_ => { });
        services.AddSingleton<IConfigureOptions<RateLimiterOptions>, RateLimitSetup>();
        services.AddCors();
        services.AddOptions<CorsOptions>().Configure<IOptions<IntegrationSettings>>(ConfigureCors);
        return services;
    }

    private static void ConfigureCors(CorsOptions options, IOptions<IntegrationSettings> settings) =>
        options.AddPolicy(ApexCors.PartnerWidget, policy => policy
            .WithOrigins(settings.Value.AllowedOrigins)
            .WithMethods(HttpMethods.Get)
            .WithHeaders(ApexHeaders.ApiKey));
}
