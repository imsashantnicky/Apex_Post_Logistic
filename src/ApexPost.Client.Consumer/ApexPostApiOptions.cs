namespace ApexPost.Client.Consumer;

/// <summary>
/// Where the consumer finds the API: section <c>ApexPostApi</c> of appsettings.json, overridable with the
/// environment variable <c>ApexPostApi__BaseUrl</c> or the argument <c>--ApexPostApi:BaseUrl=...</c>.
/// </summary>
public sealed class ApexPostApiOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "ApexPostApi";

    /// <summary>
    /// The root URL of the API host, for example http://localhost:5310/ (the "http" launch profile). Checked at
    /// start-up: it must be an absolute URL.
    /// </summary>
    public Uri? BaseUrl { get; set; }

    /// <summary>
    /// A bearer token sent as Authorization: Bearer on every request; without it the API answers 401. Never put it
    /// in appsettings.json: pass it with the environment variable <c>ApexPostApi__AccessToken</c>, for example the
    /// output of dotnet user-jwts create --project src/ApexPost.Api --role Admin --role Courier
    /// --claim courierId=CR-101.
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// The base URL with a trailing slash. The generated client appends relative paths ("api/v2/shipments"), and
    /// without the slash <see cref="Uri"/> would drop the last segment of a base URL that has a path.
    /// </summary>
    public Uri BaseAddress()
    {
        var url = BaseUrl ?? throw new InvalidOperationException($"{SectionName}:BaseUrl is not configured.");
        return url.AbsoluteUri.EndsWith('/') ? url : new Uri(url.AbsoluteUri + "/");
    }
}
