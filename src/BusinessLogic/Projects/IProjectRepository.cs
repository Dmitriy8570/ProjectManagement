using BusinessLogic.Common;

namespace BusinessLogic.Projects;

public interface IProjectRepository
{
    /// <summary>
    /// Returns one page of projects matching <paramref name="filter"/>, already
    /// sorted as requested, together with the total count of matches. The full
    /// <see cref="Project"/> aggregate (manager + members) is expected to be
    /// hydrated for read endpoints.
    /// </summary>
    Task<PagedResult<Project>> GetProjectsAsync(ProjectListFilter filter, CancellationToken ct);

    Task<Project?> GetProjectByIdAsync(int id, CancellationToken ct);

    Task AddProjectAsync(Project project, CancellationToken ct);

    /// <summary>
    /// Removes the project with the given id and returns whether a row was
    /// actually deleted (<c>false</c> means "not found"). Commits immediately
    /// at the SQL level — does not participate in the unit of work, so a
    /// follow-up <see cref="SaveAsync"/> call is not required.
    /// </summary>
    Task<bool> DeleteProjectAsync(int id, CancellationToken ct);

    /// <summary>
    /// Persists pending changes. EF-style: mutations on tracked entities are
    /// captured automatically, so there is no explicit Update method.
    /// </summary>
    Task SaveAsync(CancellationToken ct);
}
