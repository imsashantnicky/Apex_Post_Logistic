namespace ApexPost.Web.Security;

/// <summary>Endpoint metadata: only addresses on the allowlist may call this endpoint.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class IpRestrictedAttribute(IpAllowlist allowlist) : Attribute
{
    public IpAllowlist Allowlist { get; } = allowlist;
}
