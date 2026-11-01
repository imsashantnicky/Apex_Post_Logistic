namespace ApexPost.Application.Exceptions;

/// <summary>Thrown when a delete is refused because history still refers to the record (HTTP 409).</summary>
public sealed class RecordInUseException : Exception
{
    public RecordInUseException()
    {
    }

    public RecordInUseException(string message)
        : base(message)
    {
    }

    public RecordInUseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
