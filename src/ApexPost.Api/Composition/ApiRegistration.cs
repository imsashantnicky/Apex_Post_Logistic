using System.Diagnostics;
using ApexPost.Api.Errors;
using ApexPost.Api.Seeding;
using ApexPost.Application.Abstractions;
using ApexPost.Application.Services;
using ApexPost.Application.Settings;
using Microsoft.AspNetCore.Http.Features;

namespace ApexPost.Api.Composition;

/// <summary>
/// The services of the API host: controllers, ProblemDetails, the global exception handler, the development seed
/// and the few application parts the Week 3 SQL persistence needs to resolve.
/// </summary>
public static class ApiRegistration
{
    /// <summary>The ProblemDetails extension that carries the trace id of the request.</summary>
    public const string TraceIdKey = "traceId";

    /// <summary>Registers the API host services. Call after the persistence and tracking registrations.</summary>
    public static IServiceCollection AddApexApi(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // The Week 3 SQL queries need the tax rule, and its save interceptor needs a current user.
        services.Configure<TaxSettings>(configuration.GetSection(TaxSettings.SectionName));
        services.AddSingleton<TaxCalculator>();
        services.AddScoped<ICurrentUser, NoSignedInStaff>();

        services.AddControllers();
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions.TryAdd(TraceIdKey, TraceIdOf(context.HttpContext)));
        services.AddExceptionHandler<ApiExceptionHandler>();

        services.AddScoped<DevelopmentShipmentSeeder>();
        return services;
    }

    private static string TraceIdOf(HttpContext context) =>
        Activity.Current?.Id ?? context.Features.Get<IHttpActivityFeature>()?.Activity.Id ?? context.TraceIdentifier;
}
