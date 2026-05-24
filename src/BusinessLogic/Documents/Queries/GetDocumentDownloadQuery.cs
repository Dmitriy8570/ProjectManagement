using BusinessLogic.Common;
using MediatR;

namespace BusinessLogic.Documents.Queries;

public record GetDocumentDownloadQuery : IRequest<DocumentDownloadResult>
{
    public int DocumentId { get; init; }
}

public record DocumentDownloadResult
{
    public Stream Content { get; init; } = Stream.Null;
    public string FileName { get; init; } = default!;
    public string ContentType { get; init; } = default!;
}

public sealed class GetDocumentDownloadQueryHandler
    : IRequestHandler<GetDocumentDownloadQuery, DocumentDownloadResult>
{
    private readonly IDocumentRepository _documents;
    private readonly IFileStorage _storage;

    public GetDocumentDownloadQueryHandler(IDocumentRepository documents, IFileStorage storage)
    {
        _documents = documents;
        _storage   = storage;
    }

    public async Task<DocumentDownloadResult> Handle(GetDocumentDownloadQuery request, CancellationToken ct)
    {
        var document = await _documents.GetByIdAsync(request.DocumentId, ct)
            ?? throw new EntityNotFoundException(nameof(ProjectDocument), request.DocumentId);

        var stream = await _storage.OpenReadAsync(document.StoredName, ct);

        return new DocumentDownloadResult
        {
            Content     = stream,
            FileName    = document.FileName,
            ContentType = document.ContentType
        };
    }
}
