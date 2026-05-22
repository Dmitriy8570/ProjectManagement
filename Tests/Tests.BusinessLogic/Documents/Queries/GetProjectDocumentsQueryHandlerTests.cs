using BusinessLogic.Documents;
using BusinessLogic.Documents.Queries;
using NSubstitute;

namespace Tests.BusinessLogic.Documents.Queries;

public class GetProjectDocumentsQueryHandlerTests
{
    private readonly IDocumentRepository _documents = Substitute.For<IDocumentRepository>();
    private readonly GetProjectDocumentsQueryHandler _handler;

    public GetProjectDocumentsQueryHandlerTests()
    {
        _handler = new GetProjectDocumentsQueryHandler(_documents);
    }

    private static ProjectDocument CreateDocument(int id, int projectId, string fileName)
    {
        var doc = new ProjectDocument(
            projectId, fileName, $"stored-{id}.bin", "application/octet-stream", sizeBytes: 1);
        typeof(ProjectDocument).GetProperty("Id")!.SetValue(doc, id);
        return doc;
    }

    [Fact]
    public async Task Handle_RepositoryReturnsDocuments_MapsToDtos()
    {
        _documents.GetByProjectIdAsync(5, Arg.Any<CancellationToken>()).Returns(new[]
        {
            CreateDocument(id: 11, projectId: 5, fileName: "a.txt"),
            CreateDocument(id: 12, projectId: 5, fileName: "b.txt"),
        });

        var result = await _handler.Handle(
            new GetProjectDocumentsQuery { ProjectId = 5 }, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { "a.txt", "b.txt" }, result.Select(d => d.FileName));
        Assert.All(result, d => Assert.Equal(5, d.ProjectId));
    }

    [Fact]
    public async Task Handle_NoDocuments_ReturnsEmptyList()
    {
        _documents.GetByProjectIdAsync(1, Arg.Any<CancellationToken>())
                  .Returns(Array.Empty<ProjectDocument>());

        var result = await _handler.Handle(
            new GetProjectDocumentsQuery { ProjectId = 1 }, CancellationToken.None);

        Assert.Empty(result);
    }

    // The handler must forward the projectId to the repository unchanged —
    // a recent refactor renamed the property and we want a regression net.
    [Fact]
    public async Task Handle_ForwardsProjectIdToRepository()
    {
        _documents.GetByProjectIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
                  .Returns(Array.Empty<ProjectDocument>());

        await _handler.Handle(
            new GetProjectDocumentsQuery { ProjectId = 42 }, CancellationToken.None);

        await _documents.Received(1).GetByProjectIdAsync(42, Arg.Any<CancellationToken>());
    }
}
