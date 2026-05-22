using BusinessLogic.Documents;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories;

public class DocumentRepository : IDocumentRepository
{
    private readonly AppDbContext _db;

    public DocumentRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(ProjectDocument document, CancellationToken ct) =>
        await _db.ProjectDocuments.AddAsync(document, ct);

    public Task<ProjectDocument?> GetByIdAsync(int id, CancellationToken ct) =>
        _db.ProjectDocuments.FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<IReadOnlyList<ProjectDocument>> GetByProjectIdAsync(int projectId, CancellationToken ct) =>
        await _db.ProjectDocuments
            .AsNoTracking()
            .Where(d => d.ProjectId == projectId)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync(ct);

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        var doc = await _db.ProjectDocuments.FindAsync([id], ct);
        if (doc is null) return false;
        _db.ProjectDocuments.Remove(doc);
        return true;
    }

    public Task SaveAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
