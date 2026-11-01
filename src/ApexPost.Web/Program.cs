using ApexPost.Infrastructure;
using ApexPost.Web.Composition;

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

var app = builder.Build();

await app.PrepareAsync();

// Static files first: they need neither routing nor a signed-in user.
app.UseStaticFiles();

// Routing picks the endpoint, so authorization can read its policy metadata.
app.UseRouting();

// Authentication builds the user that authorization then checks.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(ApexRoutes.DefaultName, ApexRoutes.DefaultPattern);

await app.RunAsync();
