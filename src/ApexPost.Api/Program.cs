using ApexPost.Api.Composition;
using ApexPost.Api.OpenApi;
using ApexPost.Infrastructure;
using ApexPost.Infrastructure.Tracking;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

// Week 3 SQL Server persistence and the shared infrastructure (clock, hashing, simulated partners), unchanged.
builder.Services.AddSqlPersistence(builder.Configuration);
builder.Services.AddInfrastructureServices(builder.Configuration);

// Week 4 shipment tracking: its own context, migrations, ports and service on the same database.
builder.Services.AddShipmentTracking(builder.Configuration);

builder.Services.AddApexApi(builder.Configuration);

// JWT bearer tokens (issuer and audience from appsettings, signing key from user-secrets via dotnet user-jwts), the
// role rules on the actions, the courier of the request for the scan ownership check in the tracking service, and
// POST /dev/token for the fictional couriers CR-101 and CR-102 (Development only).
builder.Services.AddApexSecurity(builder.Environment);

// URL-segment versioning (v1 deprecated, v2 current; /api/shipments stays a v1 alias) and the built-in .NET OpenAPI
// generator with one document per version; ApexDocumentTransformer fills title, description, version and contact.
builder.Services.AddApexApiVersioning().AddApexOpenApi();

var app = builder.Build();

// Only a developer's database is migrated and seeded by the host; other environments deploy migrations on purpose.
if (app.Environment.IsDevelopment())
{
    await app.PrepareDevelopmentDatabaseAsync();
}

// Exceptions become ProblemDetails through ApiExceptionHandler; it sits above everything that can throw.
app.UseExceptionHandler();

// Tokens and API keys must never travel in clear text. Outside Development the browser is told to use HTTPS only
// (HSTS; a developer's localhost certificate should not be pinned), and every plain HTTP request is redirected.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

// Empty error responses (unknown route, wrong method, wrong content type) get a ProblemDetails body as well.
app.UseStatusCodePages();

// /openapi/v1.json, /openapi/v2.json and Swagger UI at /swagger exist in Development only; elsewhere both answer 404.
app.MapApexDocumentationInDevelopment();

// Who is calling (bearer token), then whether the action allows it; 401 and 403 are written as ProblemDetails.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await app.RunAsync();
