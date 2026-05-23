using System.ComponentModel.DataAnnotations;
using BusinessLogic.Common;
using BusinessLogic.Identity;
using MediatR;

namespace BusinessLogic.Employees.Commands;

public record CreateEmployeeRequest
{
    [Required, MaxLength(100)]
    public string FirstName { get; init; } = default!;

    [Required, MaxLength(100)]
    public string LastName { get; init; } = default!;

    [MaxLength(100)]
    public string? Patronymic { get; init; }

    [Required, EmailAddress, MaxLength(100)]
    public string Email { get; init; } = default!;

    /// <summary>
    /// Login password for the Identity account that gets created alongside
    /// the employee. Validated by Identity's PasswordValidator on persist.
    /// </summary>
    [Required, MinLength(6), MaxLength(100)]
    public string Password { get; init; } = default!;

    /// <summary>
    /// Role assigned to the new user — one of <see cref="BusinessLogic.Identity.Roles"/>.
    /// Defaults to plain Employee when omitted; the handler validates it.
    /// </summary>
    public string? Role { get; init; }
}

public record CreateEmployeeCommand : IRequest<CreateEmployeeResponse>
{
    public CreateEmployeeRequest Data { get; init; } = default!;
}

public record CreateEmployeeResponse
{
    public int Id { get; init; }
}

public class CreateEmployeeCommandHandler
    : IRequestHandler<CreateEmployeeCommand, CreateEmployeeResponse>
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IUserAccountService _userAccountService;

    public CreateEmployeeCommandHandler(
        IEmployeeRepository employeeRepository,
        IUserAccountService userAccountService)
    {
        _employeeRepository = employeeRepository;
        _userAccountService = userAccountService;
    }

    public async Task<CreateEmployeeResponse> Handle(CreateEmployeeCommand request, CancellationToken ct)
    {
        // Pre-check unique email so the caller gets a clean 400-style error
        // instead of a raw DbUpdateException from the unique-index violation.
        if (await _userAccountService.EmailExistsAsync(request.Data.Email, excludingEmployeeId: null, ct))
            throw new DomainValidationException(
                $"An employee with email '{request.Data.Email}' already exists.");

        // Pre-validate the password (and email format) against Identity's
        // configured policy BEFORE inserting an Employee row. Without this,
        // a weak password gets caught only by CreateAccountAsync below,
        // after the Employee has already been persisted — leaving an
        // orphaned record on every failed attempt.
        await _userAccountService.ValidateNewAccountAsync(
            request.Data.Email, request.Data.Password, ct);

        var role = NormalizeRole(request.Data.Role);

        // Domain validation (non-blank fields) happens inside the constructor.
        var employee = new Employee(
            request.Data.FirstName,
            request.Data.LastName,
            request.Data.Patronymic);

        await _employeeRepository.AddEmployeeAsync(employee, ct);
        await _employeeRepository.SaveAsync(ct);

        try
        {
            // Account creation runs after the employee row is persisted so the
            // FK (AspNetUsers.EmployeeId → Employees.Id) is satisfied.
            await _userAccountService.CreateAccountAsync(
                employee.Id, request.Data.Email, request.Data.Password, role, ct);
        }
        catch
        {
            // Safety net for the race window between the upfront validation
            // and the actual CreateAsync call (e.g. someone else just registered
            // with the same email). Roll back the orphan Employee row by hand
            // — keeps the table consistent without pulling a transaction
            // abstraction into the domain layer.
            await _employeeRepository.DeleteEmployeeAsync(employee.Id, CancellationToken.None);
            throw;
        }

        return new CreateEmployeeResponse { Id = employee.Id };
    }

    /// <summary>
    /// Defaults a missing role to plain Employee and rejects anything that
    /// isn't one of the three known roles. Keeps untrusted form input from
    /// minting users with arbitrary string roles.
    /// </summary>
    private static string NormalizeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return Roles.Employee;

        var trimmed = role.Trim();
        if (!Roles.AllList.Contains(trimmed))
            throw new DomainValidationException($"Unknown role '{role}'.");

        return trimmed;
    }
}
