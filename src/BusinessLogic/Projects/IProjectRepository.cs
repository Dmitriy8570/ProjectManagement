namespace BusinessLogic.Projects;

public interface IProjectRepository
{
    /// <summary>
    /// Returns projects matching <paramref name="filter"/>, already sorted as
    /// requested. The full <see cref="Project"/> aggregate (manager + members)
    /// is expected to be hydrated for read endpoints.
    /// </summary>
    Task<IReadOnlyList<Project>> GetProjectsAsync(ProjectListFilter filter, CancellationToken ct);

    Task<Project?> GetProjectByIdAsync(int id, CancellationToken ct);

    Task AddProjectAsync(Project project, CancellationToken ct);

    Task DeleteProjectAsync(int id, CancellationToken ct);

    /// <summary>
    /// Persists pending changes. EF-style: mutations on tracked entities are
    /// captured automatically, so there is no explicit Update method.
    /// </summary>
    Task SaveAsync(CancellationToken ct);
}
