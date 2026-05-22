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
    public DateTime UploadedAt { get; private set; }

    // Required by EF Core.
    private ProjectDocument() { }

    public ProjectDocument(
        int projectId,
        string fileName,
        string storedName,
        string contentType,
        long sizeBytes)
    {
        if (projectId <= 0)
            throw new ArgumentException("Project ID must be positive.", nameof(projectId));
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name is required.", nameof(fileName));
        if (string.IsNullOrWhiteSpace(storedName))
            throw new ArgumentException("Stored name is required.", nameof(storedName));
        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("Content type is required.", nameof(contentType));
        if (sizeBytes < 0)
            throw new ArgumentException("Size cannot be negative.", nameof(sizeBytes));

        ProjectId   = projectId;
        FileName    = fileName.Trim();
        StoredName  = storedName;
        ContentType = contentType;
        SizeBytes   = sizeBytes;
        UploadedAt  = DateTime.UtcNow;
    }
}
