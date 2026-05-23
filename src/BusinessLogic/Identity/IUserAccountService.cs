namespace BusinessLogic.Identity;

/// <summary>
/// Operations against the Identity user store, exposed as a domain-level
/// abstraction so BusinessLogic doesn't take a direct dependency on
/// ASP.NET Core Identity. Implementations live in the infrastructure layer.
///
/// The email of an employee is owned by the user account, not by the
/// Employee entity — read-side queries project it via a join, write-side
/// commands change it through this service.
/// </summary>
public interface IUserAccountService
{
    /// <summary>
    /// True if a user account with the given email already exists,
    /// optionally excluding the account linked to <paramref name="excludingEmployeeId"/>
    /// (so editing a user without changing their email does not flag a conflict).
    /// </summary>
    Task<bool> EmailExistsAsync(string email, int? excludingEmployeeId, CancellationToken ct);

    /// <summary>
    /// Runs Identity's password and user validators against the supplied
    /// credentials WITHOUT touching the database. Throws
    /// <see cref="BusinessLogic.Common.DomainValidationException"/> if anything
    /// fails. Call before persisting an Employee so a bad password doesn't
    /// leave an orphan employee row behind when the account creation step
    /// would have rejected it.
    /// </summary>
    Task ValidateNewAccountAsync(string email, string password, CancellationToken ct);

    /// <summary>
    /// Creates a new Identity user linked to the given employee, optionally
    /// assigning a role (one of <see cref="Roles"/>). UserName is set equal
    /// to email — the system uses email-based login.
    /// </summary>
    Task CreateAccountAsync(int employeeId, string email, string password, string? role, CancellationToken ct);

    /// <summary>
    /// Changes the email (and UserName, kept in sync) of the user linked to
    /// the given employee.
    /// </summary>
    Task UpdateEmailAsync(int employeeId, string newEmail, CancellationToken ct);

    /// <summary>
    /// Returns the email of the user account linked to the given employee,
    /// or null if no such account exists.
    /// </summary>
    Task<string?> GetEmailByEmployeeIdAsync(int employeeId, CancellationToken ct);

    /// <summary>
    /// True if the user linked to <paramref name="employeeId"/> has at least
    /// one of the listed roles. Used to enforce role-based eligibility rules
    /// at the application layer (e.g. only a Director or ProjectManager can
    /// be appointed as a project's PM). False when the employee has no linked
    /// user account or none of the roles match.
    /// </summary>
    Task<bool> IsEmployeeInAnyRoleAsync(int employeeId, IReadOnlyCollection<string> roles, CancellationToken ct);
}
