namespace BusinessLogic.Identity;

/// <summary>
/// Read-side view of "who is calling": exposed as a domain abstraction so
/// handlers can filter results by the current user (e.g. "my projects")
/// without taking a direct dependency on <c>HttpContext</c>.
/// </summary>
public interface ICurrentUserService
{
    bool IsAuthenticated { get; }

    /// <summary>The Identity user id (string), or null if not authenticated.</summary>
    string? UserId { get; }

    /// <summary>
    /// The linked employee id, or null if not authenticated. Sourced from a
    /// custom claim populated when the user signs in, so this stays a pure
    /// in-memory lookup — no DB round-trip per request.
    /// </summary>
    int? EmployeeId { get; }

    bool IsInRole(string role);
}
