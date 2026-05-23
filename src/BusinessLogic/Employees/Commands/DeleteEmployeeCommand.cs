using BusinessLogic.Common;
using BusinessLogic.Tasks;
using MediatR;

namespace BusinessLogic.Employees.Commands;

public record DeleteEmployeeCommand : IRequest
{
    public int Id { get; init; }
}

public sealed class DeleteEmployeeCommandHandler : IRequestHandler<DeleteEmployeeCommand>
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IProjectTaskRepository _taskRepository;

    public DeleteEmployeeCommandHandler(
        IEmployeeRepository employeeRepository,
        IProjectTaskRepository taskRepository)
    {
        _employeeRepository = employeeRepository;
        _taskRepository = taskRepository;
    }

    public async Task Handle(DeleteEmployeeCommand request, CancellationToken ct)
    {
        // The PM foreign key is configured as RESTRICT, so deleting an
        // employee who currently manages a project would otherwise surface
        // as a raw DbUpdateException. Surface it as a domain error instead.
        if (await _employeeRepository.IsProjectManagerAsync(request.Id, ct))
            throw new DomainValidationException(
                "Cannot delete an employee who is currently a project manager.");

        // Author and assignee FKs on ProjectTasks are also RESTRICT — block the
        // delete with a friendly error rather than letting EF Core throw.
        if (await _taskRepository.IsReferencedByAnyTaskAsync(request.Id, ct))
            throw new DomainValidationException(
                "Cannot delete an employee who is referenced by tasks (author or assignee).");

        var deleted = await _employeeRepository.DeleteEmployeeAsync(request.Id, ct);

        if (!deleted)
            throw new EntityNotFoundException(nameof(Employee), request.Id);
    }
}
