namespace BusinessLogic.Documents;

/// <summary>
/// Abstracts the underlying file storage (local filesystem, blob storage, etc.).
/// Implementations live in the infrastructure layer; domain code depends only
/// on this interface.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Persists <paramref name="content"/> and returns the unique stored name
    /// (including extension) that can later be used with the other methods.
    /// </summary>
    Task<string> StoreAsync(Stream content, string extension, CancellationToken ct);

    /// <summary>Opens a read stream for the file identified by <paramref name="storedName"/>.</summary>
    Task<Stream> OpenReadAsync(string storedName, CancellationToken ct);

    /// <summary>Removes the file from storage; no-op if it does not exist.</summary>
    Task DeleteAsync(string storedName, CancellationToken ct);
}
