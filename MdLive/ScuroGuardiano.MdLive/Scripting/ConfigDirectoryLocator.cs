namespace ScuroGuardiano.MdLive.Scripting;

/// <summary>
/// <para>
/// Finds the <c>.mdlive</c> directory for a working directory: the working directory itself, then its parents,
/// like git looks for <c>.git</c>. The nearest one wins.
/// </para>
/// <para>
/// Walking up must not pick a <c>.mdlive</c> someone else planted above you. Git had exactly that bug
/// (CVE-2022-24765: a <c>.git</c> created by another user in a shared parent directory). The search therefore
/// stops before entering a directory that is writable by others (<c>/tmp</c>, <c>/var/tmp</c>...), and never
/// looks at a drive root on Windows, where any user can create directories.
/// </para>
/// </summary>
internal static class ConfigDirectoryLocator
{
    public const string DirectoryName = ".mdlive";

    /// <summary>Returns the real path of the nearest <c>.mdlive</c> directory, or null.</summary>
    public static string? Find(string workingDirectory)
    {
        var current = Paths.GetRealPath(workingDirectory);

        while (true)
        {
            var candidate = Path.Join(current, DirectoryName);
            if (Directory.Exists(candidate))
            {
                return Paths.GetRealPath(candidate);
            }

            var parent = Path.GetDirectoryName(current);
            if (parent is null || !IsSafeToSearch(parent))
            {
                return null;
            }

            current = parent;
        }
    }

    /// <summary>
    /// Where <c>.mdlive</c> would be when none exists yet: in the working directory, so that
    /// <c>mdlive init</c> run later is picked up.
    /// </summary>
    public static string Default(string workingDirectory) =>
        Path.Join(Paths.GetRealPath(workingDirectory), DirectoryName);

    private static bool IsSafeToSearch(string directory)
    {
        if (OperatingSystem.IsWindows())
        {
            // C:\ pozwala zwykłym użytkownikom tworzyć katalogi, stąd wariant CVE-2022-24765 z C:\.git
            return Path.GetPathRoot(directory) != directory;
        }

        try
        {
            return (File.GetUnixFileMode(directory) & UnixFileMode.OtherWrite) == 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

internal static class Paths
{
    private static readonly StringComparison Comparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>
    /// Absolute path with symbolic links resolved in every component, like <c>realpath</c>. A path that
    /// doesn't exist is resolved as far as it does.
    /// </summary>
    public static string GetRealPath(string path)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var root = Path.GetPathRoot(full)!;
        var resolved = root;

        // Ograniczenie na wypadek pętli symlinków
        var remainingLinks = 40;

        foreach (var segment in full[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            var next = Path.Join(resolved, segment);
            FileSystemInfo info = Directory.Exists(next) ? new DirectoryInfo(next) : new FileInfo(next);

            if (info.LinkTarget is not null && remainingLinks-- > 0)
            {
                var target = info.ResolveLinkTarget(returnFinalTarget: true);
                next = target is null ? next : GetRealPath(target.FullName);
            }

            resolved = next;
        }

        return Path.TrimEndingDirectorySeparator(resolved);
    }

    /// <summary>Whether <paramref name="path"/> is <paramref name="directory"/> or inside it. Both must be real paths.</summary>
    public static bool IsSameOrInside(string path, string directory)
    {
        if (string.Equals(path, directory, Comparison))
        {
            return true;
        }

        var prefix = directory.EndsWith(Path.DirectorySeparatorChar) ? directory : directory + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, Comparison);
    }
}
