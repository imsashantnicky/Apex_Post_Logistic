namespace ApexPost.Infrastructure.Persistence.Json;

/// <summary>
/// Thrown when a data file cannot be trusted: malformed JSON, a duplicate key or an invalid record.
/// The file is never overwritten in that case, so nothing is lost silently.
/// </summary>
public sealed class DataFileException : Exception
{
    public DataFileException()
    {
    }

    public DataFileException(string message)
        : base(message)
    {
    }

    public DataFileException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
