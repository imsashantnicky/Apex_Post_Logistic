using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace ApexPost.UnitTests.Support;

internal sealed class FakeHostEnvironment(string environmentName, string contentRootPath) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = environmentName;

    public string ApplicationName { get; set; } = "ApexPost.UnitTests";

    public string ContentRootPath { get; set; } = contentRootPath;

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
