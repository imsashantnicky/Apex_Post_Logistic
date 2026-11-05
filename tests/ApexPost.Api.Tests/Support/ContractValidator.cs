using System.ComponentModel.DataAnnotations;

namespace ApexPost.Api.Tests.Support;

/// <summary>
/// Runs the DataAnnotations rules of one contract the way MVC does: all property attributes first, then
/// IValidatableObject when they pass. The validation context offers a fixed <see cref="TimeProvider"/>.
/// </summary>
public static class ContractValidator
{
    public static IReadOnlyList<ValidationResult> Validate(object model, TimeProvider? time = null)
    {
        var services = new TimeServices(time ?? new FixedTimeProvider());
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model, services, null), results, true);
        return results;
    }

    /// <summary>The members named by the failed rules.</summary>
    public static IReadOnlyList<string> FailedMembers(object model, TimeProvider? time = null) =>
        [.. Validate(model, time).SelectMany(result => result.MemberNames).Distinct()];

    private sealed class TimeServices(TimeProvider time) : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == typeof(TimeProvider) ? time : null;
    }
}
