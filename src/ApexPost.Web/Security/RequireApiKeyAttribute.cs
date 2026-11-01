namespace ApexPost.Web.Security;

/// <summary>Endpoint metadata: the caller must present a partner's API key.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireApiKeyAttribute : Attribute;
