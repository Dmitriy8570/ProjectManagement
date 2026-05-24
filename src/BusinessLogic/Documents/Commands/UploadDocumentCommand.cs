using BusinessLogic.Common;
using MediatR;

namespace BusinessLogic.Documents.Commands;

public record UploadDocumentCommand : IRequest<ProjectDocumentDto>
{
    public int ProjectId { get; init; }
    public string FileName { get; init; } = default!;
    public string ContentType { get; init; } = default!;
    public long SizeBytes { get; init; }

    /// <summary>
    /// Readable stream containing the file content. The handler drains it
    /// synchronously within the request lifetime — do not dispose before the
    /// command completes.
    /// </summary>
    public Stream Content { get; init; } = Stream.Null;
}

public sealed class UploadDocumentCommandHandler : IRequestHandler<UploadDocumentCommand, ProjectDocumentDto>
{
    private static readonly long MaxFileSizeBytes = 50L * 1024 * 1024; // 50 MB

    private readonly IDocumentRepository _documents;
    private readonly IFileStorage _storage;

    public UploadDocumentCommandHandler(IDocumentRepository documents, IFileStorage storage)
    {
        _documents = documents;
        _storage   = storage;
    }

    public async Task<ProjectDocumentDto> Handle(UploadDocumentCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FileName))
            throw new DomainValidationException("File name is required.");

        if (request.SizeBytes > MaxFileSizeBytes)
            throw new DomainValidationException(
                $"File '{request.FileName}' exceeds the 50 MB limit.");

        var extension  = Path.GetExtension(request.FileName);
        var storedName = await _storage.StoreAsync(request.Content, extension, ct);

        var document = new ProjectDocument(
            request.ProjectId,
            request.FileName,
            storedName,
            request.ContentType,
            request.SizeBytes);

        await _documents.AddAsync(document, ct);
        await _documents.SaveAsync(ct);

        return document.ToDto();
    }
}
