namespace BusinessLogic.Common;

/// <summary>
/// Raised when a domain invariant is violated (bad input, broken business rule).
/// Kept distinct from <see cref="EntityNotFoundException"/> so the presentation
/// layer can map it to <c>400 Bad Request</c> rather than <c>404</c>.
/// </summary>
public sealed class DomainValidationException : Exception
{
    public DomainValidationException(string message) : base(message) { }
}
