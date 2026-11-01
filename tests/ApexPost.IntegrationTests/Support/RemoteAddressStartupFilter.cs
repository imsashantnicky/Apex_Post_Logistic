using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace ApexPost.IntegrationTests.Support;

/// <summary>The test server has no real connection, so a test header provides the caller's address.</summary>
internal sealed class RemoteAddressStartupFilter : IStartupFilter
{
    public const string Header = "X-Test-Remote-Address";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((context, nextStep) =>
        {
            if (IPAddress.TryParse(context.Request.Headers[Header], out var address))
            {
                context.Connection.RemoteIpAddress = address;
            }

            return nextStep();
        });
        next(app);
    };
}
