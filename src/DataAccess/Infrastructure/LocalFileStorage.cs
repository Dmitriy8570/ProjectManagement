using BusinessLogic.Documents;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DataAccess.Infrastructure;

public class FileStorageOptions
{
    public string BasePath { get; set; } = "uploads";
}

public class LocalFileStorage : IFileStorage
{
    private readonly string _basePath;

    public LocalFileStorage(IOptions<FileStorageOptions> options, IHostEnvironment env)
    {
        var configured = options.Value.BasePath;
        _basePath = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(env.ContentRootPath, configured);

        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> StoreAsync(Stream content, string extension, CancellationToken ct)
    {
        var storedName = Guid.NewGuid().ToString("N") + extension.ToLowerInvariant();
        await using var fs = File.Create(Path.Combine(_basePath, storedName));
        await content.CopyToAsync(fs, ct);
        return storedName;
    }

    public Task<Stream> OpenReadAsync(string storedName, CancellationToken ct)
    {
        var path = Path.Combine(_basePath, Path.GetFileName(storedName));
        return Task.FromResult<Stream>(File.OpenRead(path));
    }

    public Task DeleteAsync(string storedName, CancellationToken ct)
    {
        var path = Path.Combine(_basePath, Path.GetFileName(storedName));
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }
}
