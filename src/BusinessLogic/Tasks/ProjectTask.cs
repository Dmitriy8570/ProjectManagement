using BusinessLogic.Common;
using BusinessLogic.Employees;
using BusinessLogic.Projects;

namespace BusinessLogic.Tasks;

public sealed class ProjectTask
{
    public int Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string Comment { get; private set; } = default!;
    public ProjectTaskStatus Status { get; private set; }
    public int Priority { get; private set; }

    public int ProjectId { get; private set; }
    public Project? Project { get; private set; }

    public int AuthorId { get; private set; }
    public Employee? Author { get; private set; }

    public int AssigneeId { get; private set; }
    public Employee? Assignee { get; private set; }

    private ProjectTask() { }

    public ProjectTask(
        Project project,
        Employee author,
        Employee assignee,
        string name,
        string? comment,
        int priority,
        ProjectTaskStatus status = ProjectTaskStatus.ToDo)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(author);
        ArgumentNullException.ThrowIfNull(assignee);

        Name = DomainGuard.NotBlank(name, nameof(name), maxLength: 200);
        Comment = DomainGuard.OptionalText(comment, nameof(comment), maxLength: 2000);
        Priority = DomainGuard.NonNegative(priority, nameof(priority));
        Status = status;

        Project = project;
        ProjectId = project.Id;
        Author = author;
        AuthorId = author.Id;

        AssignWorker(assignee);
    }

    /// <summary>
    /// Replaces the current assignee. The candidate must be involved with the
    /// task's project (either as project manager or as a participant) — task
    /// executors come from the project's team by domain rule.
    /// </summary>
    public void AssignWorker(Employee assignee)
    {
        ArgumentNullException.ThrowIfNull(assignee);

        // Project navigation is required for the membership check below; if
        // the caller fetched the task without Include(t => t.Project), this
        // method cannot enforce the invariant and refuses rather than guessing.
        if (Project is null)
            throw new InvalidOperationException(
                "ProjectTask.Project must be loaded before assigning a worker.");

        var isProjectManager = Project.ProjectManagerId == assignee.Id;
        var isParticipant = Project.Employees.Any(e => e.Id == assignee.Id);

        if (!isProjectManager && !isParticipant)
            throw new DomainValidationException(
                "Task assignee must be a member of the project (manager or participant).");

        Assignee = assignee;
        AssigneeId = assignee.Id;
    }

    public void ChangeStatus(ProjectTaskStatus status)
    {
        Status = status;
    }

    /// <summary>
    /// Partial update: a <c>null</c> argument leaves the corresponding field
    /// unchanged. Empty string for <paramref name="comment"/> clears it.
    /// </summary>
    public void Update(
        string? name = null,
        string? comment = null,
        int? priority = null,
        ProjectTaskStatus? status = null)
    {
        if (name is not null)
            Name = DomainGuard.NotBlank(name, nameof(name), maxLength: 200);

        if (comment is not null)
            Comment = DomainGuard.OptionalText(comment, nameof(comment), maxLength: 2000);

        if (priority.HasValue)
            Priority = DomainGuard.NonNegative(priority.Value, nameof(priority));

        if (status.HasValue)
            Status = status.Value;
    }
}
