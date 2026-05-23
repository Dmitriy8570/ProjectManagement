using BusinessLogic.Common;
using BusinessLogic.Documents;

namespace Tests.BusinessLogic.Documents;

public class ProjectDocumentTests
{
    private static ProjectDocument CreateDocument(
        int projectId = 1,
        string fileName = "report.pdf",
        string storedName = "abc123.pdf",
        string contentType = "application/pdf",
        long sizeBytes = 1024) =>
        new(projectId, fileName, storedName, contentType, sizeBytes);

    // ── Constructor: happy path ──────────────────────────────────────────────

    [Fact]
    public void Constructor_WithValidData_PopulatesAllFields()
    {
        var document = CreateDocument(
            projectId: 7,
            fileName: "notes.txt",
            storedName: "uuid.txt",
            contentType: "text/plain",
            sizeBytes: 42);

        Assert.Equal(7,            document.ProjectId);
        Assert.Equal("notes.txt",  document.FileName);
        Assert.Equal("uuid.txt",   document.StoredName);
        Assert.Equal("text/plain", document.ContentType);
        Assert.Equal(42,           document.SizeBytes);
    }

    // FileName is the visible label, so leading/trailing whitespace would be
    // confusing in the UI; the entity trims on construction.
    [Fact]
    public void Constructor_TrimsFileName()
    {
        var document = CreateDocument(fileName: "  notes.txt  ");

        Assert.Equal("notes.txt", document.FileName);
    }

    // ── Constructor: invalid arguments ───────────────────────────────────────

    [Fact]
    public void Constructor_WithNonPositiveProjectId_Throws()
    {
        Assert.Throws<DomainValidationException>(() => CreateDocument(projectId: -1));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Constructor_WithBlankFileName_Throws(string blank)
    {
        Assert.Throws<DomainValidationException>(() => CreateDocument(fileName: blank));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithBlankStoredName_Throws(string blank)
    {
        Assert.Throws<DomainValidationException>(() => CreateDocument(storedName: blank));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithBlankContentType_Throws(string blank)
    {
        Assert.Throws<DomainValidationException>(() => CreateDocument(contentType: blank));
    }

    [Fact]
    public void Constructor_WithNegativeSize_Throws()
    {
        Assert.Throws<DomainValidationException>(() => CreateDocument(sizeBytes: -1));
    }

    [Fact]
    public void Constructor_WithZeroSize_IsAccepted()
    {
        var document = CreateDocument(sizeBytes: 0);

        Assert.Equal(0, document.SizeBytes);
    }
}
