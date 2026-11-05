namespace ApexPost.Application.Exceptions;

/// <summary>Thrown when the database refuses a save because the row changed after it was read (HTTP 409).</summary>
public sealed class ConcurrencyConflictException : ConcurrencyException
{
    public ConcurrencyConflictException()
    {
    }

    public ConcurrencyConflictException(string message)
        : base(message)
    {
    }

    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
