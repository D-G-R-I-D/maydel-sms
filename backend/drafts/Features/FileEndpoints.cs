using Maydel.Api.Common;
using Maydel.Infrastructure.Storage;
using Microsoft.AspNetCore.StaticFiles;

namespace Maydel.Api.Features;

public static class Files
{
    public const long MaxImageBytes = 12 * 1024 * 1024;
    private static readonly Dictionary<string, string> AllowedImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
    };

    // Only these folders are served anonymously; student photos are personal data and go through /api/students.
    public static readonly string[] PublicFolders = ["website", "news", "gallery", "staff"];

    public static string? Url(string? path) => path is null ? null : $"/api/files/{path}";

    /// <summary>Validates content type, size and magic bytes (not just the client-supplied header).</summary>
    public static async Task<(string? Extension, string? Error)> ValidateImageAsync(IFormFile file)
    {
        if (file.Length == 0) return (null, "The file is empty.");
        if (file.Length > MaxImageBytes) return (null, "Images must be 12 MB or smaller.");
        if (!AllowedImageTypes.TryGetValue(file.ContentType, out var ext)) return (null, "Only JPG, PNG or WebP images are allowed.");

        var header = new byte[12];
        await using var s = file.OpenReadStream();
        var read = await s.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);
        var ok = read >= 12 && ext switch
        {
            ".jpg" => header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".png" => header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47,
            ".webp" => header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F' && header[8] == 'W' && header[9] == 'E',
            _ => false,
        };
        return ok ? (ext, null) : (null, "The file content does not match its image type.");
    }
}

public static class FileEndpoints
{
    public static void MapFileEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/files/{folder}/{name}", async (string folder, string name, IFileStorage storage, CancellationToken ct) =>
        {
            if (!Files.PublicFolders.Contains(folder)) return Problems.NotFound();
            var stream = await storage.OpenAsync($"{folder}/{name}", ct);
            if (stream is null) return Problems.NotFound();
            new FileExtensionContentTypeProvider().TryGetContentType(name, out var type);
            return Results.File(stream, type ?? "application/octet-stream");
        }).AllowAnonymous().WithTags("Files");
    }
}
