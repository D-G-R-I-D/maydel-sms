using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Photos;
using Maydel.Domain.Homework;

namespace Maydel.Application.Homework;

/// <summary>A homework file to download, with its cleaned name.</summary>
public sealed record HomeworkFileContent(Stream Content, string ContentType, string FileName);

/// <summary>
/// Homework attachments and hand-ins. Allowed: PDF, images (re-encoded, so anything hidden is stripped), and Word, Excel or
/// PowerPoint (.docx/.xlsx/.pptx, which can't carry macros). Recognised by their first bytes, never by the name alone;
/// always served as downloads.
/// </summary>
public sealed class HomeworkFiles(IFileStorage storage, PhotoStore photos)
{
    public const int MaxBytes = 10 * 1024 * 1024;

    private static readonly Dictionary<string, string> Office = new(StringComparer.OrdinalIgnoreCase)
    {
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
    };

    public static readonly Error Invalid = Error.Validation("file", "Attach a PDF, a photo (JPEG, PNG or WebP), or a Word, Excel or PowerPoint file, up to 10 MB.");

    public async Task<HomeworkFile?> SaveAsync(byte[] upload, string? fileName, string folder, CancellationToken ct)
    {
        if (upload.Length is 0 or > MaxBytes) return null;
        var name = Clean(fileName);
        if (upload.Length >= 5 && upload.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
            return await StoreAsync(upload, folder, "pdf", "application/pdf", Named(name, ".pdf"), ct);
        if (upload.Length >= 4 && upload.AsSpan(0, 4).SequenceEqual("PK\u0003\u0004"u8) && Office.TryGetValue(Path.GetExtension(name), out var type))
            return await StoreAsync(upload, folder, Path.GetExtension(name)[1..].ToLowerInvariant(), type, name, ct);
        if (ImageSignature.Detect(upload) == ImageSignature.Kind.Unknown) return null;
        var saved = await photos.SaveAsync(upload, folder, transparent: false, 2000, ct);
        if (saved.IsFailure) return null;
        var ext = saved.Value.Image.ContentType == "image/png" ? ".png" : saved.Value.Image.ContentType == "image/webp" ? ".webp" : ".jpg";
        return new HomeworkFile { Key = saved.Value.Key, FileName = Named(name, ext), ContentType = saved.Value.Image.ContentType, Size = upload.Length };
    }

    public async Task<HomeworkFileContent?> OpenAsync(HomeworkFile file, CancellationToken ct) =>
        await storage.OpenAsync(file.Key, ct) is { } stream ? new HomeworkFileContent(stream, file.ContentType, file.FileName) : null;

    public Task DeleteAsync(HomeworkFile file, CancellationToken ct) => storage.DeleteAsync(file.Key, ct);

    private async Task<HomeworkFile> StoreAsync(byte[] upload, string folder, string ext, string type, string name, CancellationToken ct)
    {
        using var stream = new MemoryStream(upload);
        return new HomeworkFile { Key = await storage.SaveAsync(stream, folder, ext, type, ct), FileName = name, ContentType = type, Size = upload.Length };
    }

    /// <summary>A safe display name: no path, no control characters, at most 120 characters.</summary>
    private static string Clean(string? fileName)
    {
        var name = Path.GetFileName((fileName ?? "").Replace('\\', '/').Split('/').Last());
        name = new string([.. name.Where(c => !char.IsControl(c) && c is not ('"' or '<' or '>' or '|' or '*' or '?' or ':'))]).Trim();
        return name.Length == 0 ? "file" : name.Length > 120 ? name[^120..] : name;
    }

    private static string Named(string name, string ext) => Path.GetExtension(name).Equals(ext, StringComparison.OrdinalIgnoreCase) ? name : Path.GetFileNameWithoutExtension(name) + ext;
}
