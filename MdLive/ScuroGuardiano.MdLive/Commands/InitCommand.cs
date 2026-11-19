using System.Text;
using ScuroGuardiano.MdLive.ConfigBase;

namespace ScuroGuardiano.MdLive.Commands;

/// <summary>
/// <c>mdlive init</c>: creates <c>.mdlive</c> in the current directory with default style and HTML template.
/// Existing files are never overwritten, so running it again only adds what's missing.
/// </summary>
internal static class InitCommand
{
    private const string MdLiveDirectory = ".mdlive";

    public static async Task<int> RunAsync()
    {
        try
        {
            Directory.CreateDirectory(MdLiveDirectory);

            await CreateFileAsync(Path.Join(MdLiveDirectory, "style.css"), DefaultFiles.StyleCss);
            await CreateFileAsync(Path.Join(MdLiveDirectory, "template.html"), MdLiveConfigurator.DefaultHtmlTemplate);

            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await Console.Error.WriteLineAsync($"mdlive init failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task CreateFileAsync(string path, string content)
    {
        try
        {
            // CreateNew zamiast sprawdzania File.Exists, żeby nie nadpisać pliku utworzonego w międzyczasie
            await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            await writer.WriteAsync(content);

            await Console.Out.WriteLineAsync($"Created {path}");
        }
        catch (IOException) when (File.Exists(path))
        {
            await Console.Out.WriteLineAsync($"Skipped {path}, file already exists");
        }
    }
}
