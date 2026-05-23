using BusinessLogic.Common;
using BusinessLogic.Projects;

namespace BusinessLogic.Documents;

public class ProjectDocument
{
    public int Id { get; private set; }
    public int ProjectId { get; private set; }

    /// <summary>Original file name supplied by the uploader.</summary>
    public string FileName { get; private set; } = default!;

    /// <summary>UUID-based name used on the file system (prevents conflicts and path traversal).</summary>
    public string StoredName { get; private set; } = default!;

    public string ContentType { get; private set; } = default!;
    public long SizeBytes { get; private set; }

    // Required by EF Core.
    private ProjectDocument() { }

    public ProjectDocument(
        int projectId,
        string fileName,
        string storedName,
        string contentType,
        long sizeBytes)
    {
        ProjectId   = DomainGuard.NonNegative(projectId, nameof(projectId));
        FileName    = DomainGuard.NotBlank(fileName, nameof(fileName), 255);
        StoredName  = DomainGuard.NotBlank(storedName, nameof(storedName), 150);
        ContentType = DomainGuard.NotBlank(contentType, nameof(contentType), 100);
        SizeBytes   = DomainGuard.NonNegative(sizeBytes, nameof(sizeBytes));
    }
}
