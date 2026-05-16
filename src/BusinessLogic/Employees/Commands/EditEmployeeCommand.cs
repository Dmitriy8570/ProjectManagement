using System.ComponentModel.DataAnnotations;
using BusinessLogic.Common;
using MediatR;

namespace BusinessLogic.Employees.Commands;

/// <summary>
/// Partial update: only the fields the caller actually wants to change need
/// to be provided. Null values keep the existing data.
/// </summary>
public record EditEmployeeRequest
{
    [Required]
    public int Id { get; init; }

    [MaxLength(100)]
    public string? FirstName { get; init; }

    [MaxLength(100)]
    public string? LastName { get; init; }

    [MaxLength(100)]
    public string? Patronymic { get; init; }

    [EmailAddress, MaxLength(100)]
    public string? Email { get; init; }
}

public record EditEmployeeCommand : IRequest<EditEmployeeResponse>
{
    public EditEmployeeRequest Data { get; init; } = default!;
}

public record EditEmployeeResponse
{
    public int Id { get; init; }
}

public class EditEmployeeCommandHandler : IRequestHandler<EditEmployeeCommand, EditEmployeeResponse>
{
    private readonly IEmployeeRepository _employeeRepository;

    public EditEmployeeCommandHandler(IEmployeeRepository employeeRepository)
    {
        _employeeRepository = employeeRepository;
    }

    public async Task<EditEmployeeResponse> Handle(EditEmployeeCommand request, CancellationToken ct)
    {
        var employee = await _employeeRepository.GetEmployeeByIdAsync(request.Data.Id, ct)
            ?? throw new EntityNotFoundException(nameof(Employee), request.Data.Id);

        employee.Update(
            firstName: request.Data.FirstName,
            lastName: request.Data.LastName,
            patronymic: request.Data.Patronymic,
            email: request.Data.Email);

        await _employeeRepository.SaveAsync(ct);

        return new EditEmployeeResponse { Id = employee.Id };
    }
}
