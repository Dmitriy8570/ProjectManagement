using System.ComponentModel.DataAnnotations;
using MediatR;

namespace BusinessLogic.Employees.Commands;

public record CreateEmployeeRequest
{
    [Required, MaxLength(100)]
    public string FirstName { get; init; } = default!;

    [Required, MaxLength(100)]
    public string LastName { get; init; } = default!;

    [Required, MaxLength(100)]
    public string Patronymic { get; init; } = default!;

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
