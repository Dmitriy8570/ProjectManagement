using MediatR;

namespace BusinessLogic.Documents.Queries;

public record GetProjectDocumentsQuery : IRequest<IReadOnlyList<ProjectDocumentDto>>
{
    public int ProjectId { get; init; }
}

public class GetProjectDocumentsQueryHandler
    : IRequestHandler<GetProjectDocumentsQuery, IReadOnlyList<ProjectDocumentDto>>
{
    private readonly IDocumentRepository _documents;

    public GetProjectDocumentsQueryHandler(IDocumentRepository documents)
    {
        _documents = documents;
    }

    public async Task<IReadOnlyList<ProjectDocumentDto>> Handle(
        GetProjectDocumentsQuery request, CancellationToken ct)
    {
        var docs = await _documents.GetByProjectIdAsync(request.ProjectId, ct);
        return docs.Select(d => d.ToDto()).ToArray();
    }
}
