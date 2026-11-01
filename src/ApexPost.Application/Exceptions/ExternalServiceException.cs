namespace ApexPost.Application.Exceptions;

/// <summary>Thrown when an external service such as the courier fails (HTTP 502).</summary>
public class ExternalServiceException : Exception
{
    public ExternalServiceException()
    {
    }

    public ExternalServiceException(string message)
        : base(message)
    {
    }

    public ExternalServiceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
