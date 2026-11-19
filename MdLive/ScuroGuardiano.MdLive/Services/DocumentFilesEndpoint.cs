using Microsoft.Net.Http.Headers;

namespace ScuroGuardiano.MdLive.Services;

/// <summary>
/// Serves images referenced from the previewed document (e.g. <c>![](assets/image.png)</c>),
/// resolved against the directory of the document.
/// </summary>
internal static class DocumentFilesEndpoint
{
    // Tylko obrazki: podgląd nie ma powodu wystawiać czegokolwiek innego z katalogu użytkownika (.env, .git/config...)
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".avif"] = "image/avif",
        [".svg"] = "image/svg+xml",
        [".bmp"] = "image/bmp",
        [".ico"] = "image/x-icon",
    };

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static IEndpointRouteBuilder MapDocumentFiles(this IEndpointRouteBuilder app)
    {
        // Catch-all ma najniższy priorytet, więc nie przykrywa "/", "/preview/sse" ani "/scripts/..."
        app.MapGet("/{**path}", (string path, PreviewHub hub, HttpContext context) =>
        {
            if (!ContentTypes.TryGetValue(Path.GetExtension(path), out var contentType))
            {
                return Results.NotFound();
            }

            var file = Resolve(path, hub.Current.Document?.Path);
            if (file is null)
            {
                return Results.NotFound();
            }

            // Plik może się zmienić pod tą samą nazwą (ponowne wklejenie obrazka), więc przeglądarka
            // ma pytać za każdym razem, a ETag / Last-Modified dają tanie 304, gdy nic się nie zmieniło.
            context.Response.Headers.CacheControl = "no-cache";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            // SVG otwarte bezpośrednio wykonałoby skrypty w originie podglądu
            context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'";

            var etag = new EntityTagHeaderValue($"\"{file.LastWriteTimeUtc.Ticks:x}-{file.Length:x}\"");
            return Results.File(file.FullName, contentType, lastModified: file.LastWriteTimeUtc, entityTag: etag);
        });

        return app;
    }

    private static FileInfo? Resolve(string requestPath, string? documentPath)
    {
        var baseDirectory = string.IsNullOrEmpty(documentPath)
            ? Directory.GetCurrentDirectory()
            : Path.GetDirectoryName(Path.GetFullPath(documentPath));

        if (baseDirectory is null)
        {
            return null;
        }

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(baseDirectory)) + Path.DirectorySeparatorChar;
        var file = new FileInfo(Path.GetFullPath(Path.Join(root, requestPath)));

        // Ochrona przed path traversal, także przez symlinki prowadzące poza katalog dokumentu
        // (https://owasp.org/www-community/attacks/Path_Traversal)
        if (!file.FullName.StartsWith(root, PathComparison) || !file.Exists)
        {
            return null;
        }

        if (file.LinkTarget is not null)
        {
            var target = file.ResolveLinkTarget(returnFinalTarget: true);
            if (target is not FileInfo { Exists: true } targetFile || !targetFile.FullName.StartsWith(root, PathComparison))
            {
                return null;
            }

            return targetFile;
        }

        return file;
    }
}
