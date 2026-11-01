namespace ApexPost.Application.Exceptions;

/// <summary>Thrown when an external service does not answer in time (HTTP 504).</summary>
public sealed class ExternalServiceTimeoutException : ExternalServiceException
{
    public ExternalServiceTimeoutException()
    {
    }

    public ExternalServiceTimeoutException(string message)
        : base(message)
    {
    }

    public ExternalServiceTimeoutException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
