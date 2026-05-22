using System.ComponentModel.DataAnnotations;
using BusinessLogic.Common;
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

    public CreateEmployeeCommandHandler(IEmployeeRepository employeeRepository)
    {
        _employeeRepository = employeeRepository;
    }

    public async Task<CreateEmployeeResponse> Handle(CreateEmployeeCommand request, CancellationToken ct)
    {
        // Pre-check unique email so the caller gets a clean 400-style error
        // instead of a raw DbUpdateException from the unique-index violation.
        // A race window still exists; the DB constraint is the ultimate guard.
        if (await _employeeRepository.EmailExistsAsync(request.Data.Email, excludingId: null, ct))
            throw new DomainValidationException(
                $"An employee with email '{request.Data.Email}' already exists.");

        // Domain validation (non-blank fields, valid email) happens inside the
        // constructor, so we don't repeat it here.
        var employee = new Employee(
            request.Data.FirstName,
            request.Data.LastName,
            request.Data.Patronymic,
            request.Data.Email);

        await _employeeRepository.AddEmployeeAsync(employee, ct);
        await _employeeRepository.SaveAsync(ct);

        return new CreateEmployeeResponse { Id = employee.Id };
    }
}
