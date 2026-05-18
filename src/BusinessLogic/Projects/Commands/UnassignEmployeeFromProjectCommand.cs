using BusinessLogic.Common;
using MediatR;

namespace BusinessLogic.Projects.Commands;

public record UnassignEmployeeFromProjectRequest
{
    public int ProjectId { get; init; }
    public int EmployeeId { get; init; }
}

public record UnassignEmployeeFromProjectCommand : IRequest
{
    public UnassignEmployeeFromProjectRequest Data { get; init; } = default!;
}

public class UnassignEmployeeFromProjectCommandHandler
    : IRequestHandler<UnassignEmployeeFromProjectCommand>
{
    private readonly IProjectRepository _projectRepository;

    public UnassignEmployeeFromProjectCommandHandler(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public async Task Handle(UnassignEmployeeFromProjectCommand request, CancellationToken ct)
    {
        var project = await _projectRepository.GetProjectByIdAsync(request.Data.ProjectId, ct)
            ?? throw new EntityNotFoundException(nameof(Project), request.Data.ProjectId);

        // Removing the project manager via the participants list is suspicious —
        // surface it instead of silently desyncing the two relationships.
        if (project.ProjectManagerId == request.Data.EmployeeId)
            throw new DomainValidationException(
                "Cannot unassign the project manager from their own project.");

        // Boolean return is informational only; an absent member is not an
        // error here for the same reason add is idempotent.
        project.RemoveEmployee(request.Data.EmployeeId);
        await _projectRepository.SaveAsync(ct);
    }
}
