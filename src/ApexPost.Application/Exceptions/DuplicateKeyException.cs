namespace ApexPost.Application.Exceptions;

/// <summary>Thrown when a save would break a unique key or index of the database (HTTP 409).</summary>
public sealed class DuplicateKeyException : Exception
{
    public DuplicateKeyException()
    {
    }

    public DuplicateKeyException(string message)
        : base(message)
    {
    }

    public DuplicateKeyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
