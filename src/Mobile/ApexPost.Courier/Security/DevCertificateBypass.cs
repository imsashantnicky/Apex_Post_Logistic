#if DEBUG
using System.Net.Security;
using ApexPost.Courier.ApiClient.Http;

namespace ApexPost.Courier.Security;

/// <summary>
/// Debug builds only: accepts the ASP.NET Core development certificate, which the Android emulator does not trust
/// and whose name (<c>localhost</c>) does not match <c>10.0.2.2</c>. Valid certificates are always accepted;
/// an invalid one only for <c>localhost</c> and <c>10.0.2.2</c>. Release builds do not contain this class.
/// </summary>
internal static class DevCertificateBypass
{
    public static HttpMessageHandler CreateHandler() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (request, _, _, errors) =>
            errors == SslPolicyErrors.None || DevelopmentHosts.IsDevelopmentHost(request.RequestUri),
    };
}
#endif
