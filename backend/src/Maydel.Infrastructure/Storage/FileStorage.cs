using Microsoft.Extensions.Options;

namespace Maydel.Infrastructure.Storage;

/// <summary>Abstraction over blob storage. Swap <see cref="LocalFileStorage"/> for S3/Azure Blob in production.</summary>
public interface IFileStorage
{
    Task<string> SaveAsync(Stream content, string folder, string extension, CancellationToken ct = default);
    Task<Stream?> OpenAsync(string path, CancellationToken ct = default);
    Task DeleteAsync(string path, CancellationToken ct = default);
}

public sealed class StorageOptions
{
    public const string Section = "Storage";
    public string RootPath { get; set; } = "uploads";
}

public sealed class LocalFileStorage(IOptions<StorageOptions> options) : IFileStorage
{
    private readonly string _root = Path.GetFullPath(options.Value.RootPath);

    public async Task<string> SaveAsync(Stream content, string folder, string extension, CancellationToken ct = default)
    {
        // File names are always server-generated; user input never reaches the path.
        var relative = Path.Combine(SafeSegment(folder), $"{Guid.CreateVersion7():N}{SafeExtension(extension)}");
        var full = Resolve(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var file = File.Create(full);
        await content.CopyToAsync(file, ct);
        return relative.Replace('\\', '/');
    }

    public Task<Stream?> OpenAsync(string path, CancellationToken ct = default)
    {
        var full = Resolve(path);
        return Task.FromResult<Stream?>(File.Exists(full) ? File.OpenRead(full) : null);
    }

    public Task DeleteAsync(string path, CancellationToken ct = default)
    {
        var full = Resolve(path);
        if (File.Exists(full)) File.Delete(full);
        return Task.CompletedTask;
    }

    private string Resolve(string relative)
    {
        var full = Path.GetFullPath(Path.Combine(_root, relative));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Path escapes the storage root.");
        return full;
    }

    private static string SafeSegment(string s) => new(s.Where(char.IsAsciiLetterOrDigit).ToArray());

    private static string SafeExtension(string ext) => "." + new string(ext.TrimStart('.').Where(char.IsAsciiLetterOrDigit).ToArray()).ToLowerInvariant();
}
