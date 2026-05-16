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
        if (await _employeeRepository.GetEmployeeByIdAsync(request.Id, ct) is null)
            throw new EntityNotFoundException(nameof(Employee), request.Id);

        await _employeeRepository.DeleteEmployeeAsync(request.Id, ct);
        await _employeeRepository.SaveAsync(ct);
    }
}
