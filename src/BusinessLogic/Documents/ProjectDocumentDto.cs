namespace BusinessLogic.Documents;

public record ProjectDocumentDto
{
    public int Id { get; init; }
    public int ProjectId { get; init; }
    public string FileName { get; init; } = default!;
    public string ContentType { get; init; } = default!;
    public long SizeBytes { get; init; }
    public DateTime UploadedAt { get; init; }
}

internal static class DocumentMapping
{
    public static ProjectDocumentDto ToDto(this ProjectDocument doc) => new()
    {
        Id          = doc.Id,
        ProjectId   = doc.ProjectId,
        FileName    = doc.FileName,
        ContentType = doc.ContentType,
        SizeBytes   = doc.SizeBytes
    };
}
