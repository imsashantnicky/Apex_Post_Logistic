using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Api.Security;

/// <summary>
/// Puts <see cref="PartnerApiKeyFilter"/> on a controller or action: the X-Api-Key header must hold a partner key.
/// The OpenAPI transformer reads this attribute to mark the operation with the ApiKey security scheme.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class PartnerApiKeyAttribute : TypeFilterAttribute
{
    /// <summary>Uses <see cref="PartnerApiKeyFilter"/> with its services from DI.</summary>
    public PartnerApiKeyAttribute()
        : base(typeof(PartnerApiKeyFilter))
    {
    }
}
