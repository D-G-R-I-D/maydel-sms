using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Maydel.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Maydel.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string Section = "Storage";

    /// <summary><c>Local</c> (dev, default) or <c>S3</c> (any S3-compatible store: Cloudflare R2, Railway buckets, AWS S3, MinIO).</summary>
    public string Provider { get; set; } = "Local";

    /// <summary>Local provider: folder for files, outside the web root.</summary>
    public string RootPath { get; set; } = "uploads";

    public S3StorageOptions S3 { get; set; } = new();
}

public sealed class S3StorageOptions
{
    /// <summary>Endpoint, e.g. https://&lt;account&gt;.r2.cloudflarestorage.com. Empty = AWS S3 for <see cref="Region"/>.</summary>
    public string? ServiceUrl { get; set; }
    public string Bucket { get; set; } = "";
    public string Region { get; set; } = "auto";

    /// <summary>Secrets: supply via environment (Storage__S3__AccessKeyId / Storage__S3__SecretAccessKey), never config files.</summary>
    public string AccessKeyId { get; set; } = "";
    public string SecretAccessKey { get; set; } = "";

    /// <summary>Path-style URLs (bucket in the path). Needed by MinIO; R2 accepts both.</summary>
    public bool ForcePathStyle { get; set; } = true;
}

internal static class StorageKeys
{
    /// <summary><c>{folder}/{v7 guid}.{ext}</c> with folder and extension reduced to ASCII letters and digits.</summary>
    public static string New(string folder, string extension) =>
        $"{Safe(folder)}/{Guid.CreateVersion7():N}.{Safe(extension.TrimStart('.')).ToLowerInvariant()}";

    /// <summary>Accepts only keys of the shape we generate, so a stored value can never point elsewhere.</summary>
    public static bool IsValid(string key)
    {
        var parts = key.Split('/');
        return parts.Length == 2 && parts.All(p => p.Length > 0 && p.All(c => char.IsAsciiLetterOrDigit(c) || c == '.')) && !key.Contains("..");
    }

    private static string Safe(string s) => new(s.Where(char.IsAsciiLetterOrDigit).ToArray());
}

public sealed class LocalFileStorage(IOptions<StorageOptions> options) : IFileStorage
{
    private readonly string _root = Path.GetFullPath(options.Value.RootPath);

    public async Task<string> SaveAsync(Stream content, string folder, string extension, string contentType, CancellationToken ct = default)
    {
        var key = StorageKeys.New(folder, extension);
        var full = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var file = File.Create(full);
        await content.CopyToAsync(file, ct);
        return key;
    }

    public Task<Stream?> OpenAsync(string key, CancellationToken ct = default)
    {
        var full = Resolve(key);
        return Task.FromResult<Stream?>(File.Exists(full) ? File.OpenRead(full) : null);
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var full = Resolve(key);
        if (File.Exists(full)) File.Delete(full);
        return Task.CompletedTask;
    }

    private string Resolve(string key)
    {
        if (!StorageKeys.IsValid(key)) throw new UnauthorizedAccessException("Invalid storage key.");
        var full = Path.GetFullPath(Path.Combine(_root, key));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Path escapes the storage root.");
        return full;
    }
}

/// <summary>Private bucket on any S3-compatible service. Objects are never made public.</summary>
public sealed class S3FileStorage : IFileStorage, IDisposable
{
    private readonly AmazonS3Client _s3;
    private readonly string _bucket;

    public S3FileStorage(IOptions<StorageOptions> options)
    {
        var o = options.Value.S3;
        if (string.IsNullOrWhiteSpace(o.Bucket) || string.IsNullOrWhiteSpace(o.AccessKeyId) || string.IsNullOrWhiteSpace(o.SecretAccessKey))
            throw new InvalidOperationException("Storage:S3 needs Bucket, AccessKeyId and SecretAccessKey (Storage__S3__… environment variables).");
        _bucket = o.Bucket;
        var config = new AmazonS3Config { ForcePathStyle = o.ForcePathStyle, AuthenticationRegion = o.Region };
        if (!string.IsNullOrWhiteSpace(o.ServiceUrl)) config.ServiceURL = o.ServiceUrl;
        else config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(o.Region);
        _s3 = new AmazonS3Client(new BasicAWSCredentials(o.AccessKeyId, o.SecretAccessKey), config);
    }

    public async Task<string> SaveAsync(Stream content, string folder, string extension, string contentType, CancellationToken ct = default)
    {
        var key = StorageKeys.New(folder, extension);
        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket, Key = key, InputStream = content, ContentType = contentType,
            // R2 and some S3-compatible stores reject streaming chunked payload signatures.
            UseChunkEncoding = false,
        }, ct);
        return key;
    }

    public async Task<Stream?> OpenAsync(string key, CancellationToken ct = default)
    {
        if (!StorageKeys.IsValid(key)) throw new UnauthorizedAccessException("Invalid storage key.");
        try
        {
            using var response = await _s3.GetObjectAsync(_bucket, key, ct);
            // Buffer so the HTTP response from S3 can be released; photos are small (re-encoded, ≤ 800 px).
            var buffer = new MemoryStream();
            await response.ResponseStream.CopyToAsync(buffer, ct);
            buffer.Position = 0;
            return buffer;
        }
        catch (AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        if (!StorageKeys.IsValid(key)) throw new UnauthorizedAccessException("Invalid storage key.");
        await _s3.DeleteObjectAsync(_bucket, key, ct);
    }

    public void Dispose() => _s3.Dispose();
}
