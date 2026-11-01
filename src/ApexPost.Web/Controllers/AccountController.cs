using ApexPost.Application.DTOs;
using ApexPost.Application.Services;
using ApexPost.Application.Validation;
using ApexPost.Web.Filters;
using ApexPost.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ApexPost.Web.Controllers;

/// <summary>Sign-in and sign-out. Every failed sign-in shows the same message and redisplays the form.</summary>
public sealed class AccountController(LoginService login, AttributeModelValidator validator) : Controller
{
    private const string LoginFailed = "The email or password is not correct.";
    private const string DashboardName = "Dashboard";
    private const string DashboardAction = "Index";

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToDashboard();
        }

        ViewData[nameof(returnUrl)] = returnUrl;
        return View(new LoginCommand());
    }

    [AllowAnonymous]
    [Audit(AuditEvents.Login)]
    [EnableRateLimiting(ApexRateLimits.Login)]
    [HttpPost]
    public async Task<IActionResult> Login(LoginCommand command, string? returnUrl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var staff = ModelState.IsValid && validator.Validate(command).Count == 0
            ? await login.SignInAsync(command, cancellationToken)
            : null;
        if (staff is null)
        {
            AuditOutcome.Set(HttpContext, StatusCodes.Status401Unauthorized);
            ModelState.Clear();
            ModelState.AddModelError(string.Empty, LoginFailed);
            ViewData[nameof(returnUrl)] = returnUrl;
            return View(new LoginCommand { Email = command.Email });
        }

        // The new user applies from the next request; setting it here lets the audit entry name who signed in.
        HttpContext.User = StaffPrincipal.Create(staff);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, HttpContext.User);
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToDashboard();
    }

    [Audit(AuditEvents.Logout)]
    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    private RedirectToActionResult RedirectToDashboard() =>
        RedirectToAction(DashboardAction, DashboardName);
}
