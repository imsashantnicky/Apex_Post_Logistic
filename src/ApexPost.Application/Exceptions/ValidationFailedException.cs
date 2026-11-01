namespace ApexPost.Application.Exceptions;

/// <summary>Thrown when input fails validation (HTTP 400). Carries the error messages per field.</summary>
public sealed class ValidationFailedException : Exception
{
    private const string DefaultMessage = "One or more fields are not valid.";

    public ValidationFailedException()
        : this(DefaultMessage)
    {
    }

    public ValidationFailedException(string message)
        : base(message)
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationFailedException(string message, Exception innerException)
        : base(message, innerException)
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationFailedException(IReadOnlyDictionary<string, string[]> errors)
        : base(DefaultMessage)
    {
        Errors = errors;
    }

    public ValidationFailedException(string field, string error)
        : this(new Dictionary<string, string[]> { [field] = [error] })
    {
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
