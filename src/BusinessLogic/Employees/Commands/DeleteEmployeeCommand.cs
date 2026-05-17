using BusinessLogic.Common;
using MediatR;

namespace BusinessLogic.Employees.Commands;

public record DeleteEmployeeCommand : IRequest
{
    public int Id { get; init; }
}

public class DeleteEmployeeCommandHandler : IRequestHandler<DeleteEmployeeCommand>
{
    private readonly IEmployeeRepository _employeeRepository;

    public DeleteEmployeeCommandHandler(IEmployeeRepository employeeRepository)
    {
        _employeeRepository = employeeRepository;
    }

    public async Task Handle(DeleteEmployeeCommand request, CancellationToken ct)
    {
        // The PM foreign key is configured as RESTRICT, so deleting an
        // employee who currently manages a project would otherwise surface
        // as a raw DbUpdateException. Surface it as a domain error instead.
        if (await _employeeRepository.IsProjectManagerAsync(request.Id, ct))
            throw new DomainValidationException(
                "Cannot delete an employee who is currently a project manager.");

        var deleted = await _employeeRepository.DeleteEmployeeAsync(request.Id, ct);

        if (!deleted)
            throw new EntityNotFoundException(nameof(Employee), request.Id);
    }
}
