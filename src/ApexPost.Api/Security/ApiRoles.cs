namespace ApexPost.Api.Security;

/// <summary>The roles a bearer token can carry in its <c>role</c> claim.</summary>
public static class ApiRoles
{
    /// <summary>A courier device: uploads its own scans and moves a shipment's status.</summary>
    public const string Courier = "Courier";

    /// <summary>Back office: creates, replaces and deletes shipments.</summary>
    public const string Admin = "Admin";
}
