using Microsoft.Extensions.Logging;

namespace ApexPost.IntegrationTests.Support;

public sealed record CapturedLog(string Category, LogLevel Level, string Message, IReadOnlyList<string> Scopes);
