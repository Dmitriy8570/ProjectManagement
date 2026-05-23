using System.ComponentModel.DataAnnotations;
using BusinessLogic.Common;
using BusinessLogic.Identity;
using MediatR;

namespace BusinessLogic.Employees.Commands;

/// <summary>
/// Partial update: only the fields the caller actually wants to change need
/// to be provided. Null values keep the existing data.
/// </summary>
public record EditEmployeeRequest
{
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
    public int Id { get; init; }
}

public record EditEmployeeResponse
{
    public int Id { get; init; }
}

public sealed class EditEmployeeCommandHandler : IRequestHandler<EditEmployeeCommand, EditEmployeeResponse>
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IUserAccountService _userAccountService;

    public EditEmployeeCommandHandler(
        IEmployeeRepository employeeRepository,
        IUserAccountService userAccountService)
    {
        _employeeRepository = employeeRepository;
        _userAccountService = userAccountService;
    }

    public async Task<EditEmployeeResponse> Handle(EditEmployeeCommand request, CancellationToken ct)
    {
        var employee = await _employeeRepository.GetEmployeeByIdAsync(request.Id, ct)
            ?? throw new EntityNotFoundException(nameof(Employee), request.Id);

        // Email lives on the Identity account, not the Employee entity, so a
        // change routes through the account service. Only do the conflict
        // check + update when the caller actually supplied an email — keeps
        // partial updates cheap.
        if (request.Data.Email is not null)
        {
            var currentEmail = await _userAccountService.GetEmailByEmployeeIdAsync(employee.Id, ct);

            if (!string.Equals(request.Data.Email, currentEmail, StringComparison.OrdinalIgnoreCase))
            {
                if (await _userAccountService.EmailExistsAsync(
                        request.Data.Email, excludingEmployeeId: employee.Id, ct))
                {
                    throw new DomainValidationException(
                        $"An employee with email '{request.Data.Email}' already exists.");
                }

                await _userAccountService.UpdateEmailAsync(employee.Id, request.Data.Email, ct);
            }
        }

        employee.Update(
            firstName: request.Data.FirstName,
            lastName: request.Data.LastName,
            patronymic: request.Data.Patronymic);

        await _employeeRepository.SaveAsync(ct);

        return new EditEmployeeResponse { Id = employee.Id };
    }
}
