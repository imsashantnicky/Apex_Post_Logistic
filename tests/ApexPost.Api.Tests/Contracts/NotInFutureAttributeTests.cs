using System.ComponentModel.DataAnnotations;
using ApexPost.Api.Contracts.Validation;
using ApexPost.Api.Tests.Support;

namespace ApexPost.Api.Tests.Contracts;

/// <summary>The custom [NotInFuture] rule on its own: tolerance, kinds of time and the clock it uses.</summary>
public sealed class NotInFutureAttributeTests
{
    private static readonly DateTimeOffset Now = FixedTimeProvider.DefaultNow;

    [Fact]
    public void The_tolerance_is_two_minutes() => Assert.Equal(2, NotInFutureAttribute.ClockSkewMinutes);

    [Fact]
    public void An_empty_value_passes_so_Required_decides() => Assert.True(IsValid(null));

    [Fact]
    public void A_DateTimeOffset_in_another_zone_is_compared_as_the_same_instant()
    {
        var zone = TimeSpan.FromHours(5);

        Assert.True(IsValid(Now.AddMinutes(2).ToOffset(zone)));
        Assert.False(IsValid(Now.AddMinutes(3).ToOffset(zone)));
    }

    [Fact]
    public void A_time_without_a_zone_counts_as_UTC()
    {
        var plain = DateTime.SpecifyKind(Now.UtcDateTime.AddMinutes(3), DateTimeKind.Unspecified);

        Assert.False(IsValid(plain));
        Assert.True(IsValid(DateTime.SpecifyKind(Now.UtcDateTime, DateTimeKind.Unspecified)));
    }

    [Fact]
    public void Without_a_TimeProvider_service_the_system_clock_is_used()
    {
        var attribute = new NotInFutureAttribute();

        Assert.True(attribute.IsValid(DateTime.UtcNow));
        Assert.False(attribute.IsValid(DateTime.UtcNow.AddHours(1)));
    }

    [Fact]
    public void The_message_names_the_field_and_the_tolerance()
    {
        var context = Context(Now.UtcDateTime);
        context.MemberName = "ScannedAtUtc";

        var result = new NotInFutureAttribute().GetValidationResult(Now.UtcDateTime.AddHours(1), context);

        Assert.NotNull(result);
        Assert.Contains("ScannedAtUtc must not be in the future", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("2 minutes", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(["ScannedAtUtc"], result.MemberNames);
    }

    [Fact]
    public void A_value_that_is_not_a_date_is_a_programming_error() =>
        Assert.Throws<InvalidOperationException>(() => IsValid("2026-11-05"));

    private static bool IsValid(object? value) =>
        new NotInFutureAttribute().GetValidationResult(value, Context(value ?? Now)) == ValidationResult.Success;

    private static ValidationContext Context(object instance) =>
        new(instance, new TimeOnlyServices(new FixedTimeProvider(Now)), null) { DisplayName = "ScannedAtUtc" };

    private sealed class TimeOnlyServices(TimeProvider time) : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == typeof(TimeProvider) ? time : null;
    }
}
