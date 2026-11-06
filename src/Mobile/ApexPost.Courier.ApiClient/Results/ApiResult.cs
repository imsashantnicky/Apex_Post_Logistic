using System.Diagnostics.CodeAnalysis;

namespace ApexPost.Courier.ApiClient.Results;

/// <summary>The outcome of an API call: either a value or an <see cref="ApiFailure"/>, never an exception.</summary>
/// <typeparam name="T">Type of the value on success.</typeparam>
public sealed class ApiResult<T>
    where T : class
{
    internal ApiResult(T? value, ApiFailure? failure)
    {
        Value = value;
        Failure = failure;
    }

    /// <summary>The value; set only when <see cref="IsSuccess"/> is <see langword="true"/>.</summary>
    public T? Value { get; }

    /// <summary>The failure; set only when <see cref="IsSuccess"/> is <see langword="false"/>.</summary>
    public ApiFailure? Failure { get; }

    /// <summary>Whether the call returned a value.</summary>
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Failure))]
    public bool IsSuccess => Failure is null;
}
