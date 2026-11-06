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

// Empty error responses (unknown route, wrong method, wrong content type) get a ProblemDetails body as well.
app.UseStatusCodePages();

// /openapi/v1.json, /openapi/v2.json and Swagger UI at /swagger exist in Development only; elsewhere both answer 404.
app.MapApexDocumentationInDevelopment();

app.MapControllers();

await app.RunAsync();
