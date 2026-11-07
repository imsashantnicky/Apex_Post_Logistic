namespace ApexPost.Courier.ApiClient.Results;

/// <summary>Why an API call did not return the expected value.</summary>
public enum ApiFailureKind
{
    /// <summary>The app refused the input before sending anything (for example a bad tracking number).</summary>
    InvalidInput = 1,

    /// <summary>400: the API rejected the request body or query.</summary>
    Validation = 2,

    /// <summary>401: no token, or the token expired or is invalid.</summary>
    Unauthorized = 3,

    /// <summary>403: the signed-in user may not do this.</summary>
    Forbidden = 4,

    /// <summary>404: the shipment does not exist.</summary>
    NotFound = 5,

    /// <summary>409: duplicate, invalid status move or version conflict.</summary>
    Conflict = 6,

    /// <summary>Any other 4xx answer.</summary>
    Rejected = 7,

    /// <summary>5xx: the server failed.</summary>
    Server = 8,

    /// <summary>The server could not be reached.</summary>
    Network = 9,

    /// <summary>The request took too long.</summary>
    Timeout = 10,

    /// <summary>The server answered with a body the app could not read.</summary>
    InvalidResponse = 11,

    /// <summary>The circuit breaker is open after repeated server failures; the call was not sent.</summary>
    ServerUnavailable = 12,
}
