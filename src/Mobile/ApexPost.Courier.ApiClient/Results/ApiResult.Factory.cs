namespace ApexPost.Courier.ApiClient.Results;

/// <summary>Creates <see cref="ApiResult{T}"/> values.</summary>
public static class ApiResult
{
    /// <summary>Creates a successful result.</summary>
    /// <param name="value">The value read from the response.</param>
    /// <typeparam name="T">Type of the value.</typeparam>
    public static ApiResult<T> Success<T>(T value)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ApiResult<T>(value, null);
    }

    /// <summary>Creates a failed result.</summary>
    /// <param name="failure">What went wrong.</param>
    /// <typeparam name="T">Type the call would have returned.</typeparam>
    public static ApiResult<T> Fail<T>(ApiFailure failure)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new ApiResult<T>(null, failure);
    }
}
