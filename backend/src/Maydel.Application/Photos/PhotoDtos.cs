using Maydel.Application.Abstractions;

namespace Maydel.Application.Photos;

/// <summary>Image bytes to stream back. <c>ETag</c> changes whenever the photo is replaced.</summary>
public sealed record PhotoContent(Stream Content, string ContentType, string ETag);

/// <summary><c>Version</c> changes with every upload; append it to the photo URL (<c>?v=</c>) to bust caches.</summary>
public sealed record PhotoInfo(string Version, int Width, int Height);

public static class PhotoErrors
{
    public static readonly Common.Error NotFound = Common.Error.NotFound("photo.not_found", "No photo has been uploaded.");
    public static readonly Common.Error Invalid = Common.Error.Validation("file", "Upload a JPEG, PNG or WebP photo (up to 12 MB).");
    public static readonly Common.Error Empty = Common.Error.Validation("file", "Choose a photo to upload.");
}

/// <summary>Store/replace/serve photos. Folder per owner type; only re-encoded JPEGs are ever stored.</summary>
public sealed class PhotoStore(IFileStorage storage, IImageProcessor images)
{
    public const int MaxUploadBytes = 12 * 1024 * 1024;
    public const int MaxDimension = 800;

    /// <summary>Version string for a stored key (the generated file name).</summary>
    public static string? VersionOf(string? key) =>
        key is null ? null : Path.GetFileNameWithoutExtension(key);

    /// <summary>Re-encodes and stores the upload; returns the new key and image info, or an error for anything that isn't a usable photo.</summary>
    public async Task<Common.Result<(string Key, ProcessedImage Image)>> SaveAsync(byte[] upload, string folder, CancellationToken ct)
    {
        if (upload.Length == 0) return PhotoErrors.Empty;
        if (upload.Length > MaxUploadBytes) return PhotoErrors.Invalid;
        var processed = images.ToJpeg(upload, MaxDimension);
        if (processed is null) return PhotoErrors.Invalid;

        using var stream = new MemoryStream(processed.Content);
        var key = await storage.SaveAsync(stream, folder, processed.Extension, processed.ContentType, ct);
        return (key, processed);
    }

    public async Task<Common.Result<PhotoContent>> OpenAsync(string? key, CancellationToken ct)
    {
        if (key is null || await storage.OpenAsync(key, ct) is not { } stream) return PhotoErrors.NotFound;
        return new PhotoContent(stream, "image/jpeg", $"\"{VersionOf(key)}\"");
    }

    /// <summary>Best effort: an orphaned file is harmless, a failed request because of one is not.</summary>
    public async Task DeleteQuietlyAsync(string? key, CancellationToken ct)
    {
        if (key is null) return;
        try { await storage.DeleteAsync(key, ct); } catch (Exception) { /* left for a cleanup job */ }
    }
}
