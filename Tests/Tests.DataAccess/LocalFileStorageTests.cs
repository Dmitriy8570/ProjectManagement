using System.Text;
using DataAccess.Infrastructure;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Tests.DataAccess;

/// <summary>
/// Round-trip tests for LocalFileStorage against a real temp directory.
/// Each test owns its own directory and cleans it up via IDisposable so
/// the test runner's working directory is never polluted.
/// </summary>
public class LocalFileStorageTests : IDisposable
{
    private readonly string _basePath;
    private readonly LocalFileStorage _storage;

    public LocalFileStorageTests()
    {
        _basePath = Path.Combine(Path.GetTempPath(), "lfs-tests-" + Guid.NewGuid().ToString("N"));
        var options = Options.Create(new FileStorageOptions { BasePath = _basePath });
        _storage = new LocalFileStorage(options, new FakeHostEnvironment());
    }

    public void Dispose()
    {
        if (Directory.Exists(_basePath))
            Directory.Delete(_basePath, recursive: true);
    }

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    // ── Constructor ──────────────────────────────────────────────────────────

    // The constructor creates the base directory on demand so callers don't
    // need to provision storage out-of-band.
    [Fact]
    public void Constructor_CreatesBasePathIfMissing()
    {
        Assert.True(Directory.Exists(_basePath));
    }

    // Relative paths must be resolved against ContentRootPath so the storage
    // location is consistent regardless of the process working directory.
    [Fact]
    public void Constructor_RelativePath_IsResolvedAgainstContentRoot()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "cr-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(contentRoot);
        try
        {
            var env = new FakeHostEnvironment { ContentRootPath = contentRoot };
            var options = Options.Create(new FileStorageOptions { BasePath = "uploads" });

            _ = new LocalFileStorage(options, env);

            Assert.True(Directory.Exists(Path.Combine(contentRoot, "uploads")));
        }
        finally
        {
            if (Directory.Exists(contentRoot))
                Directory.Delete(contentRoot, recursive: true);
        }
    }

    // ── StoreAsync ───────────────────────────────────────────────────────────

    // StoreAsync writes the content to disk, returns a name with the supplied
    // extension, and stays inside the configured base path.
    [Fact]
    public async Task StoreAsync_WritesContent_AndReturnsNameWithExtension()
    {
        var bytes = Encoding.UTF8.GetBytes("hello world");
        await using var source = new MemoryStream(bytes);

        var storedName = await _storage.StoreAsync(source, ".txt", Ct);

        Assert.EndsWith(".txt", storedName);
        var actualPath = Path.Combine(_basePath, storedName);
        Assert.True(File.Exists(actualPath));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(actualPath, Ct));
    }

    // Two consecutive stores must produce different names — the GUID-based
    // naming scheme prevents collisions and is the reason FileName / StoredName
    // are kept separate on the entity.
    [Fact]
    public async Task StoreAsync_MultipleCalls_ProduceDistinctNames()
    {
        await using var s1 = new MemoryStream(new byte[] { 1 });
        await using var s2 = new MemoryStream(new byte[] { 2 });

        var name1 = await _storage.StoreAsync(s1, ".bin", Ct);
        var name2 = await _storage.StoreAsync(s2, ".bin", Ct);

        Assert.NotEqual(name1, name2);
    }

    // The implementation lowercases the extension; this is what lets us match
    // case-insensitively when reading the file back without storing extra
    // metadata.
    [Fact]
    public async Task StoreAsync_LowercasesExtension()
    {
        await using var src = new MemoryStream(new byte[] { 0xFF });

        var storedName = await _storage.StoreAsync(src, ".PDF", Ct);

        Assert.EndsWith(".pdf", storedName);
    }

    // ── OpenReadAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task OpenReadAsync_StoredFile_ReturnsIdenticalBytes()
    {
        var bytes = Encoding.UTF8.GetBytes("payload");
        await using var src = new MemoryStream(bytes);
        var storedName = await _storage.StoreAsync(src, ".bin", Ct);

        await using var read = await _storage.OpenReadAsync(storedName, Ct);
        await using var ms = new MemoryStream();
        await read.CopyToAsync(ms, Ct);

        Assert.Equal(bytes, ms.ToArray());
    }

    // OpenReadAsync uses Path.GetFileName on the supplied name, so any path
    // traversal attempts (e.g. "../passwd") are stripped — the test feeds an
    // attempted-traversal name to verify the safety guard.
    [Fact]
    public async Task OpenReadAsync_StripsDirectoryComponentsFromName()
    {
        // Set up an existing file with a plain name we can attempt to reach.
        await File.WriteAllTextAsync(Path.Combine(_basePath, "secret.txt"), "hi", Ct);

        await using var stream = await _storage.OpenReadAsync("subdir/../secret.txt", Ct);
        using var reader = new StreamReader(stream);

        Assert.Equal("hi", await reader.ReadToEndAsync(Ct));
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_RemovesStoredFile()
    {
        await using var src = new MemoryStream(new byte[] { 0 });
        var storedName = await _storage.StoreAsync(src, ".bin", Ct);
        Assert.True(File.Exists(Path.Combine(_basePath, storedName)));

        await _storage.DeleteAsync(storedName, Ct);

        Assert.False(File.Exists(Path.Combine(_basePath, storedName)));
    }

    // Deleting a non-existent file must be a no-op so the caller can retry
    // a botched cleanup without special-casing the "file already gone" path.
    [Fact]
    public async Task DeleteAsync_MissingFile_DoesNotThrow()
    {
        await _storage.DeleteAsync("ghost.bin", Ct);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Tests.DataAccess";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}
