using ApexPost.Api.Controllers;
using ApexPost.Api.Security;
using ApexPost.Application.Tracking;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace ApexPost.Api.Composition;

/// <summary>
/// JWT bearer authentication and the current courier. Issuer and audiences come from
/// <c>Authentication:Schemes:Bearer</c> in appsettings; the signing key only from user-secrets, where
/// dotnet user-jwts keeps it (<c>Authentication:Schemes:Bearer:SigningKeys</c>).
/// </summary>
public static class ApiSecurityRegistration
{
    /// <summary>The clock skew allowed on exp and nbf, smaller than the 5-minute default.</summary>
    public static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Registers bearer authentication, authorization, the courier of the request and the dev token issuer. Outside
    /// Development the /dev/token controller is removed from the application.
    /// </summary>
    public static IServiceCollection AddApexSecurity(this IServiceCollection services, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        if (!environment.IsDevelopment())
        {
            services.AddControllers().ConfigureApplicationPartManager(parts =>
                parts.FeatureProviders.Add(new DevelopmentOnlyControllers()));
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Keep the claim names of the token (role, courierId, sub) instead of the long WS-* URIs.
                options.MapInboundClaims = false;
                options.Events = new BearerProblemEvents();
            });

        // Runs after the framework has bound the Bearer section. Every check is switched on explicitly, so a missing
        // issuer, audience or key refuses every token instead of skipping that check.
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            var validation = options.TokenValidationParameters;
            validation.ValidateIssuer = true;
            validation.ValidateAudience = true;
            validation.ValidateLifetime = true;
            validation.ValidateIssuerSigningKey = true;
            validation.RequireExpirationTime = true;
            validation.RequireSignedTokens = true;
            validation.ClockSkew = ClockSkew;
            validation.NameClaimType = ApiClaims.Subject;
            validation.RoleClaimType = ApiClaims.Role;
        });

        services.AddAuthorization();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentCourier, ClaimsCurrentCourier>();
        services.AddSingleton<DevTokenIssuer>();
        return services;
    }
}
