using ApexPost.Application.Abstractions;
using ApexPost.Domain.Personnel;
using ApexPost.Web.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Composition;

/// <summary>MVC with antiforgery on every POST, cookie authentication and the named policies.</summary>
public static class WebRegistration
{
    private const string AuthCookieName = ".ApexPost.Auth";
    private static readonly TimeSpan SessionLength = TimeSpan.FromMinutes(30);

    public static IServiceCollection AddApexWeb(this IServiceCollection services)
    {
        services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, ClaimsCurrentUser>();
        services.AddScoped<RevalidatingCookieEvents>();
        services.AddScoped<IAuthorizationHandler, SameBranchHandler>();

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(ConfigureCookie);
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(ApexPolicies.BranchManagerOnly, policy => RequireRoles(policy, StaffRole.BranchManager))
            .AddPolicy(ApexPolicies.SameBranchOnly, policy => policy.AddRequirements(new SameBranchRequirement()))
            .AddPolicy(
                ApexPolicies.CanTransact,
                policy => RequireRoles(policy, StaffRole.CounterClerk, StaffRole.BranchManager))
            .AddPolicy(
                ApexPolicies.CanViewReports,
                policy => RequireRoles(policy, StaffRole.BranchManager, StaffRole.Auditor, StaffRole.Administrator))
            .AddPolicy(ApexPolicies.AdministratorOnly, policy => RequireRoles(policy, StaffRole.Administrator))
            .AddPolicy(
                ApexPolicies.AuditViewers,
                policy => RequireRoles(policy, StaffRole.Auditor, StaffRole.Administrator));
        return services;
    }

    private static void ConfigureCookie(CookieAuthenticationOptions options)
    {
        options.Cookie.Name = AuthCookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = SessionLength;
        options.SlidingExpiration = true;
        options.LoginPath = ApexRoutes.LoginPath;
        options.EventsType = typeof(RevalidatingCookieEvents);
    }

    private static void RequireRoles(AuthorizationPolicyBuilder policy, params StaffRole[] roles) =>
        policy.RequireRole(roles.Select(role => role.ToString()));
}
