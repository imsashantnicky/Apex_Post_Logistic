using System.ComponentModel.DataAnnotations;

namespace ApexPost.Api.Contracts.Validation;

/// <summary>
/// Rejects a time later than now plus <see cref="ClockSkewMinutes"/>, because a device clock may run a little ahead
/// of the server. "Now" comes from the <see cref="TimeProvider"/> in the request services, so tests can fix it.
/// An empty value passes; [Required] decides whether one is needed. A time without a zone counts as UTC.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class NotInFutureAttribute : ValidationAttribute
{
    /// <summary>How far a time may lie ahead of the server clock.</summary>
    public const int ClockSkewMinutes = 2;

    /// <summary>Creates the rule with a message that names the field and the allowed clock difference.</summary>
    public NotInFutureAttribute()
        : base($"{{0}} must not be in the future (a clock difference of {ClockSkewMinutes} minutes is allowed).")
    {
    }

    /// <summary>
    /// Checks the value against now plus the clock skew. IsValid(object) passes no context; the system clock and the
    /// name "Value" are used then.
    /// </summary>
    protected override ValidationResult? IsValid(object? value, ValidationContext? validationContext)
    {
        var time = validationContext?.GetService(typeof(TimeProvider)) as TimeProvider ?? TimeProvider.System;
        var latest = time.GetUtcNow().AddMinutes(ClockSkewMinutes);

        var valid = value switch
        {
            null => true,
            DateTimeOffset offset => offset <= latest,
            DateTime { Kind: DateTimeKind.Unspecified } plain => DateTime.SpecifyKind(plain, DateTimeKind.Utc)
                <= latest.UtcDateTime,
            DateTime moment => moment.ToUniversalTime() <= latest.UtcDateTime,
            _ => throw new InvalidOperationException($"{nameof(NotInFutureAttribute)} applies to dates only."),
        };

        return valid
            ? ValidationResult.Success
            : new ValidationResult(
                FormatErrorMessage(validationContext?.DisplayName ?? "Value"),
                validationContext?.MemberName is { } member ? [member] : null);
    }
}
