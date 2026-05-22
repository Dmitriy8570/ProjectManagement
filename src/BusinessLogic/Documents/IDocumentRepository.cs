namespace BusinessLogic.Documents;

public interface IDocumentRepository
{
    Task AddAsync(ProjectDocument document, CancellationToken ct);
    Task<ProjectDocument?> GetByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<ProjectDocument>> GetByProjectIdAsync(int projectId, CancellationToken ct);

    /// <summary>Returns <c>true</c> when a record was found and removed.</summary>
    Task<bool> DeleteAsync(int id, CancellationToken ct);

    Task SaveAsync(CancellationToken ct);
}
