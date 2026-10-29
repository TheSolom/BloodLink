namespace BloodLink.BuildingBlocks.Domain;

/// <summary>
/// Exception thrown when a domain business rule or invariant is violated.
/// Slices and endpoint filters map this to an RFC 9457 Problem Details response (typically 400 Bad Request or 422 Unprocessable).
/// </summary>
public class DomainException : Exception
{
    public string? Code { get; }

    public DomainException(string message)
        : base(message) { }

    public DomainException(string message, string code)
        : base(message)
    {
        Code = code;
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException) { }

    public DomainException(string message, string code, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }
}

