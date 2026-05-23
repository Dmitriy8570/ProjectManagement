using BusinessLogic.Common;
using BusinessLogic.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Identity;

/// <summary>
/// Infrastructure implementation of <see cref="IUserAccountService"/>: delegates
/// to <see cref="UserManager{TUser}"/> so password hashing, normalization and
/// validation policies stay in the hands of ASP.NET Core Identity.
/// </summary>
internal sealed class UserAccountService : IUserAccountService
{
    private readonly UserManager<ApplicationUser> _users;

    public UserAccountService(UserManager<ApplicationUser> users)
    {
        _users = users;
    }

    public async Task ValidateNewAccountAsync(string email, string password, CancellationToken ct)
    {
        // A throw-away user instance — never tracked, never saved. The
        // validators (UserValidator + PasswordValidator) only need it to
        // read UserName/Email and reason about the password against the
        // configured policy (length, digits, casing, symbols).
        var probe = new ApplicationUser
        {
            UserName = email,
            Email = email
        };

        List<string> errors = [];

        foreach (var validator in _users.UserValidators)
        {
            var result = await validator.ValidateAsync(_users, probe);
            if (!result.Succeeded)
                errors.AddRange(result.Errors.Select(e => e.Description));
        }

        foreach (var validator in _users.PasswordValidators)
        {
            var result = await validator.ValidateAsync(_users, probe, password);
            if (!result.Succeeded)
                errors.AddRange(result.Errors.Select(e => e.Description));
        }

        if (errors.Count > 0)
            throw new DomainValidationException(string.Join(" ", errors));
    }

    public Task<bool> EmailExistsAsync(string email, int? excludingEmployeeId, CancellationToken ct)
    {
        // UserManager exposes the underlying IQueryable so the existence check
        // stays a single round-trip. Compare against NormalizedEmail because
        // that's the indexed column Identity actually uses for lookups.
        var normalized = _users.NormalizeEmail(email);

        return _users.Users.AnyAsync(u =>
            u.NormalizedEmail == normalized &&
            (excludingEmployeeId == null || u.EmployeeId != excludingEmployeeId),
            ct);
    }

    public async Task CreateAccountAsync(int employeeId, string email, string password, string? role, CancellationToken ct)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmployeeId = employeeId
        };

        var create = await _users.CreateAsync(user, password);
        if (!create.Succeeded)
            throw ToDomainException(create);

        if (!string.IsNullOrWhiteSpace(role))
        {
            var addRole = await _users.AddToRoleAsync(user, role);
            if (!addRole.Succeeded)
                throw ToDomainException(addRole);
        }
    }

    public async Task UpdateEmailAsync(int employeeId, string newEmail, CancellationToken ct)
    {
        var user = await _users.Users.FirstOrDefaultAsync(u => u.EmployeeId == employeeId, ct)
            ?? throw new EntityNotFoundException("UserAccount", employeeId);

        // UserName is kept equal to Email — the system logs in by email, so
        // letting these diverge would just create a second login string per
        // user without a use case.
        var setEmail = await _users.SetEmailAsync(user, newEmail);
        if (!setEmail.Succeeded)
            throw ToDomainException(setEmail);

        var setUserName = await _users.SetUserNameAsync(user, newEmail);
        if (!setUserName.Succeeded)
            throw ToDomainException(setUserName);
    }

    public Task<string?> GetEmailByEmployeeIdAsync(int employeeId, CancellationToken ct) =>
        _users.Users
            .Where(u => u.EmployeeId == employeeId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(ct);

    public async Task<bool> IsEmployeeInAnyRoleAsync(
        int employeeId, IReadOnlyCollection<string> roles, CancellationToken ct)
    {
        if (roles.Count == 0)
            return false;

        var user = await _users.Users
            .FirstOrDefaultAsync(u => u.EmployeeId == employeeId, ct);
        if (user is null)
            return false;

        // UserManager.GetRolesAsync hits the joined Identity tables (AspNetUserRoles
        // → AspNetRoles) once and respects the configured key type — cleaner
        // than reproducing the join here.
        var userRoles = await _users.GetRolesAsync(user);
        return userRoles.Any(roles.Contains);
    }

    private static DomainValidationException ToDomainException(IdentityResult result) =>
        new(string.Join("; ", result.Errors.Select(e => e.Description)));
}
