using System.Text;
using ScuroGuardiano.MdLive.ConfigBase;

namespace ScuroGuardiano.MdLive.Commands;

/// <summary>
/// <para>
/// <c>mdlive init [--force]</c>: creates <c>.mdlive</c> in the current directory with default style and HTML template.
/// </para>
/// <para>
/// Refuses to run when <c>.mdlive</c> already exists and isn't empty. With <c>--force</c> the default files are
/// overwritten, any other files in <c>.mdlive</c> are left untouched.
/// </para>
/// </summary>
internal static class InitCommand
{
    private const string MdLiveDirectory = ".mdlive";

    public static async Task<int> RunAsync(IReadOnlyList<string> args)
    {
        var force = false;

        foreach (var arg in args)
        {
            switch (arg)
            {
                case "--force" or "-f":
                    force = true;
                    break;

                default:
                    await Console.Error.WriteLineAsync($"mdlive init: unknown option '{arg}'");
                    return 1;
            }
        }

        try
        {
            if (!force && Directory.Exists(MdLiveDirectory) && Directory.EnumerateFileSystemEntries(MdLiveDirectory).Any())
            {
                await Console.Error.WriteLineAsync(
                    $"mdlive init: directory '{MdLiveDirectory}' already exists and is not empty. " +
                    "Use --force to overwrite the default files.");
                return 1;
            }

            Directory.CreateDirectory(MdLiveDirectory);

            await WriteFileAsync(Path.Join(MdLiveDirectory, "style.css"), DefaultFiles.StyleCss, force);
            await WriteFileAsync(Path.Join(MdLiveDirectory, "template.html"), MdLiveConfigurator.DefaultHtmlTemplate, force);

            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await Console.Error.WriteLineAsync($"mdlive init failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task WriteFileAsync(string path, string content, bool overwrite)
    {
        var existed = File.Exists(path);

        // Bez --force CreateNew, żeby nie nadpisać pliku utworzonego w międzyczasie
        await using var stream = new FileStream(path, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        await writer.WriteAsync(content);

        await Console.Out.WriteLineAsync($"{(existed ? "Overwrote" : "Created")} {path}");
    }
}
