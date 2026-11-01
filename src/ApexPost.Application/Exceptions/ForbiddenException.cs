namespace ApexPost.Application.Exceptions;

/// <summary>Thrown when the current user's role or branch does not allow the action (HTTP 403).</summary>
public sealed class ForbiddenException : Exception
{
    public ForbiddenException()
    {
    }

    public ForbiddenException(string message)
        : base(message)
    {
    }

    public ForbiddenException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
