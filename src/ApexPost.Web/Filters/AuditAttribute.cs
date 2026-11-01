namespace ApexPost.Web.Filters;

/// <summary>Marks an action as audited under the given event name.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AuditAttribute(string eventName) : Attribute
{
    public string EventName { get; } = eventName;

    /// <summary>When set, only a request ending with this status is audited.</summary>
    public int OnlyForStatus { get; init; }
}
