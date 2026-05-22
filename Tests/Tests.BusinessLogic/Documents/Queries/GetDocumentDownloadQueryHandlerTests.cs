using System.Text;
using BusinessLogic.Common;
using BusinessLogic.Documents;
using BusinessLogic.Documents.Queries;
using NSubstitute;

namespace Tests.BusinessLogic.Documents.Queries;

public class GetDocumentDownloadQueryHandlerTests
{
    private readonly IDocumentRepository _documents = Substitute.For<IDocumentRepository>();
    private readonly IFileStorage _storage         = Substitute.For<IFileStorage>();
    private readonly GetDocumentDownloadQueryHandler _handler;

    public GetDocumentDownloadQueryHandlerTests()
    {
        _handler = new GetDocumentDownloadQueryHandler(_documents, _storage);
    }

    private static ProjectDocument CreateDocument(int id, string storedName, string fileName, string contentType)
    {
        var doc = new ProjectDocument(
            projectId: 1, fileName: fileName, storedName: storedName,
            contentType: contentType, sizeBytes: 100);
        typeof(ProjectDocument).GetProperty("Id")!.SetValue(doc, id);
        return doc;
    }

    // Happy path: handler returns the stream from storage and the metadata
    // from the entity (file name + content type the browser should see).
    [Fact]
    public async Task Handle_DocumentExists_ReturnsStreamAndMetadata()
    {
        var doc = CreateDocument(id: 5,
            storedName: "abc.pdf", fileName: "report.pdf", contentType: "application/pdf");
        _documents.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(doc);

        var stream = new MemoryStream(Encoding.UTF8.GetBytes("file body"));
        _storage.OpenReadAsync("abc.pdf", Arg.Any<CancellationToken>()).Returns(stream);

        var result = await _handler.Handle(
            new GetDocumentDownloadQuery { DocumentId = 5 }, CancellationToken.None);

        Assert.Same(stream,                   result.Content);
        Assert.Equal("report.pdf",            result.FileName);
        Assert.Equal("application/pdf",       result.ContentType);
    }

    // Missing entity must surface as a typed not-found exception — the storage
    // layer is never touched in this case (no point opening a file we can't
    // describe).
    [Fact]
    public async Task Handle_DocumentNotFound_ThrowsAndDoesNotTouchStorage()
    {
        _documents.GetByIdAsync(99, Arg.Any<CancellationToken>()).Returns((ProjectDocument?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _handler.Handle(new GetDocumentDownloadQuery { DocumentId = 99 }, CancellationToken.None));

        await _storage.DidNotReceive().OpenReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
