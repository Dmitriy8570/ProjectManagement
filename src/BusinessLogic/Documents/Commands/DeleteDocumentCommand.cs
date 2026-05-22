using BusinessLogic.Common;
using MediatR;

namespace BusinessLogic.Documents.Commands;

public record DeleteDocumentCommand : IRequest
{
    public int DocumentId { get; init; }
}

public class DeleteDocumentCommandHandler : IRequestHandler<DeleteDocumentCommand>
{
    private readonly IDocumentRepository _documents;
    private readonly IFileStorage _storage;

    public DeleteDocumentCommandHandler(IDocumentRepository documents, IFileStorage storage)
    {
        _documents = documents;
        _storage   = storage;
    }

    public async Task Handle(DeleteDocumentCommand request, CancellationToken ct)
    {
        var document = await _documents.GetByIdAsync(request.DocumentId, ct)
            ?? throw new EntityNotFoundException(nameof(ProjectDocument), request.DocumentId);

        // Remove the file first; if this fails, the DB record is kept intact
        // so the next retry can still attempt cleanup.
        await _storage.DeleteAsync(document.StoredName, ct);

        await _documents.DeleteAsync(request.DocumentId, ct);
        await _documents.SaveAsync(ct);
    }
}
