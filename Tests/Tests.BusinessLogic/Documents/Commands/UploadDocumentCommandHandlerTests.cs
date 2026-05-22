using System.Text;
using BusinessLogic.Common;
using BusinessLogic.Documents;
using BusinessLogic.Documents.Commands;
using NSubstitute;

namespace Tests.BusinessLogic.Documents.Commands;

public class UploadDocumentCommandHandlerTests
{
    private readonly IDocumentRepository _documents = Substitute.For<IDocumentRepository>();
    private readonly IFileStorage _storage         = Substitute.For<IFileStorage>();
    private readonly UploadDocumentCommandHandler _handler;

    public UploadDocumentCommandHandlerTests()
    {
        _handler = new UploadDocumentCommandHandler(_documents, _storage);
    }

    private static UploadDocumentCommand CreateCommand(
        int projectId = 1,
        string fileName = "report.pdf",
        string contentType = "application/pdf",
        long sizeBytes = 1024,
        Stream? content = null) =>
        new()
        {
            ProjectId   = projectId,
            FileName    = fileName,
            ContentType = contentType,
            SizeBytes   = sizeBytes,
            Content     = content ?? new MemoryStream(Encoding.UTF8.GetBytes("payload"))
        };

    // Happy path: storage is called with the file extension, repo gets the
    // entity built from the stored name, and the returned DTO mirrors input.
    [Fact]
    public async Task Handle_ValidUpload_StoresFileAndReturnsDto()
    {
        _storage.StoreAsync(Arg.Any<Stream>(), ".pdf", Arg.Any<CancellationToken>())
                .Returns("guid-name.pdf");

        var dto = await _handler.Handle(
            CreateCommand(projectId: 9, fileName: "report.pdf", sizeBytes: 2048),
            CancellationToken.None);

        Assert.Equal(9,            dto.ProjectId);
        Assert.Equal("report.pdf", dto.FileName);
        Assert.Equal(2048,         dto.SizeBytes);

        await _storage.Received(1).StoreAsync(Arg.Any<Stream>(), ".pdf", Arg.Any<CancellationToken>());
        await _documents.Received(1).AddAsync(
            Arg.Is<ProjectDocument>(d =>
                d.ProjectId == 9 &&
                d.FileName == "report.pdf" &&
                d.StoredName == "guid-name.pdf" &&
                d.SizeBytes == 2048),
            Arg.Any<CancellationToken>());
        await _documents.Received(1).SaveAsync(Arg.Any<CancellationToken>());
    }

    // The extension passed to the storage must come from the original file
    // name, not a hardcoded default — covers files with no extension and
    // mixed-case extensions.
    [Theory]
    [InlineData("notes.txt",   ".txt")]
    [InlineData("photo.JPG",   ".JPG")]
    [InlineData("README",      "")]
    [InlineData("archive.tar.gz", ".gz")]
    public async Task Handle_DerivesExtensionFromFileName(string fileName, string expectedExtension)
    {
        _storage.StoreAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns("stored");

        await _handler.Handle(CreateCommand(fileName: fileName), CancellationToken.None);

        await _storage.Received(1).StoreAsync(
            Arg.Any<Stream>(), expectedExtension, Arg.Any<CancellationToken>());
    }

    // Blank file name fails the domain check before any storage call — the
    // file system must not be touched on invalid input.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_BlankFileName_ThrowsAndSkipsStorageAndSave(string blank)
    {
        var command = CreateCommand(fileName: blank);

        await Assert.ThrowsAsync<DomainValidationException>(
            () => _handler.Handle(command, CancellationToken.None));

        await _storage.DidNotReceive().StoreAsync(
            Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _documents.DidNotReceive().AddAsync(
            Arg.Any<ProjectDocument>(), Arg.Any<CancellationToken>());
        await _documents.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    // The 50 MB cap protects the file system and the database — exceeding it
    // must short-circuit the handler before any I/O happens.
    [Fact]
    public async Task Handle_FileExceedsSizeLimit_ThrowsAndSkipsStorageAndSave()
    {
        var oversize = (50L * 1024 * 1024) + 1;
        var command = CreateCommand(sizeBytes: oversize);

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("50 MB", ex.Message);

        await _storage.DidNotReceive().StoreAsync(
            Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _documents.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    // Exactly the limit is accepted — boundary test for the off-by-one risk
    // in the size check.
    [Fact]
    public async Task Handle_FileAtExactLimit_IsAccepted()
    {
        var atLimit = 50L * 1024 * 1024;
        _storage.StoreAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns("stored.bin");

        var dto = await _handler.Handle(
            CreateCommand(sizeBytes: atLimit), CancellationToken.None);

        Assert.Equal(atLimit, dto.SizeBytes);
        await _documents.Received(1).SaveAsync(Arg.Any<CancellationToken>());
    }
}
