using BusinessLogic.Common;
using BusinessLogic.Documents;
using BusinessLogic.Documents.Commands;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Tests.BusinessLogic.Documents.Commands;

public class DeleteDocumentCommandHandlerTests
{
    private readonly IDocumentRepository _documents = Substitute.For<IDocumentRepository>();
    private readonly IFileStorage _storage         = Substitute.For<IFileStorage>();
    private readonly DeleteDocumentCommandHandler _handler;

    public DeleteDocumentCommandHandlerTests()
    {
        _handler = new DeleteDocumentCommandHandler(_documents, _storage);
    }

    private static ProjectDocument CreateDocument(int id = 1, string storedName = "abc.pdf")
    {
        var doc = new ProjectDocument(
            projectId: 1, fileName: "report.pdf",
            storedName: storedName, contentType: "application/pdf", sizeBytes: 100);
        typeof(ProjectDocument).GetProperty("Id")!.SetValue(doc, id);
        return doc;
    }

    // Happy path: file removed, DB row removed, save flushed. The order
    // matters — file first so a DB delete success isn't followed by an
    // orphaned file.
    [Fact]
    public async Task Handle_DocumentExists_DeletesFileThenDbRecordThenSaves()
    {
        var doc = CreateDocument(id: 5, storedName: "abc.pdf");
        _documents.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(doc);

        await _handler.Handle(new DeleteDocumentCommand { DocumentId = 5 }, CancellationToken.None);

        Received.InOrder(() =>
        {
            _storage.DeleteAsync("abc.pdf", Arg.Any<CancellationToken>());
            _documents.DeleteAsync(5,        Arg.Any<CancellationToken>());
            _documents.SaveAsync(            Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Handle_DocumentNotFound_ThrowsAndSkipsStorageAndSave()
    {
        _documents.GetByIdAsync(99, Arg.Any<CancellationToken>())
                  .Returns((ProjectDocument?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _handler.Handle(new DeleteDocumentCommand { DocumentId = 99 }, CancellationToken.None));

        await _storage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _documents.DidNotReceive().DeleteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _documents.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    // If the file deletion fails, the handler must NOT remove the DB row —
    // otherwise we'd lose the only reference to the orphaned file. The
    // exception propagates so the caller can retry.
    [Fact]
    public async Task Handle_StorageDeleteThrows_PropagatesAndKeepsDbRecord()
    {
        var doc = CreateDocument(id: 7, storedName: "boom.pdf");
        _documents.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(doc);
        _storage.DeleteAsync("boom.pdf", Arg.Any<CancellationToken>())
                .ThrowsAsync(new IOException("disk on fire"));

        await Assert.ThrowsAsync<IOException>(
            () => _handler.Handle(new DeleteDocumentCommand { DocumentId = 7 }, CancellationToken.None));

        await _documents.DidNotReceive().DeleteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _documents.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }
}
