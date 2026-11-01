using ApexPost.Infrastructure;
using ApexPost.Web.Composition;
using ApexPost.Web.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

builder.Services.AddJsonPersistence(builder.Configuration);
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApexApplication(builder.Configuration);
builder.Services.AddApexWeb();
builder.Services.AddApexPipeline(builder.Configuration);

var app = builder.Build();

await app.PrepareAsync();

// Correlation ID first: every later step, its log entries and every response carry it.
app.UseMiddleware<CorrelationIdMiddleware>();

// Logging and timing wrap everything below, so failed and rejected requests are measured too.
app.UseMiddleware<RequestLoggingMiddleware>();

// Exception handling sits above all that can throw; its own log entries are written by ProblemExceptionHandler.
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    ExceptionHandlingPath = ApexRoutes.ExceptionPagePath,
    AllowStatusCode404Response = true,
    SuppressDiagnosticsCallback = _ => true,
});

// Status pages turn empty error responses into pages or JSON; partners get what the endpoint wrote, unchanged.
app.UseWhen(
    context => !ApexRoutes.IsIntegration(context),
    branch => branch.UseStatusCodePagesWithReExecute(ApexRoutes.StatusPagePattern));

// Security headers before anything that can answer, so that every response below carries them.
app.UseMiddleware<SecurityHeadersMiddleware>();

// HSTS only outside Development, where a browser would remember it for localhost.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// HTTPS redirection before any content is served or any cookie is read.
app.UseHttpsRedirection();

// Maintenance before static files and routing: a closed site answers nothing but /health.
app.UseMiddleware<MaintenanceModeMiddleware>();

// Static files need neither routing nor a signed-in user.
app.UseStaticFiles();

// Routing picks the endpoint, so the steps below can read its metadata.
app.UseRouting();

// IP restriction first among the endpoint checks: the cheapest refusal, before any key or session is touched.
app.UseMiddleware<IpRestrictionMiddleware>();

// CORS before the API key: a browser preflight carries no key and must be answered here.
app.UseCors();

// The API key names the partner, which the rate limiter needs next.
app.UseMiddleware<ApiKeyMiddleware>();

// Rate limiting before authentication: a rejected login attempt costs no cookie or password work.
app.UseRateLimiter();

// Authentication builds the user that authorization then checks.
app.UseAuthentication();
app.UseAuthorization();

// Health is anonymous: load balancers do not sign in.
app.MapHealthChecks(ApexRoutes.HealthPath).AllowAnonymous();
app.MapControllerRoute(ApexRoutes.ParcelStatusName, ApexRoutes.ParcelStatusPattern, ApexRoutes.ParcelStatusTarget);
app.MapControllerRoute(ApexRoutes.DefaultName, ApexRoutes.DefaultPattern);

await app.RunAsync();
