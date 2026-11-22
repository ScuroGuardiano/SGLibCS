namespace ScuroGuardiano.MdLive.Scripting;

/// <summary>
/// <para>
/// Directories given with <c>mdlive serve --trust &lt;dir&gt;</c>. A <c>.mdlive</c> inside one of them (at any depth)
/// is treated as trusted without an entry in the <see cref="TrustStore"/>, meant for directories only the user
/// writes to, e.g. personal notes.
/// </para>
/// <para>
/// It skips only the trust store lookup. Everything else stays: files only from inside <c>.mdlive</c>, no
/// symlinks, compiling and loading the bytes that were read. Paths are compared as real paths, so neither a
/// symlink nor <c>..</c> can widen it, and <c>/notes</c> doesn't cover <c>/notes-evil</c>.
/// </para>
/// </summary>
internal sealed class TrustedDirectories
{
    public static readonly TrustedDirectories None = new([]);

    public TrustedDirectories(IReadOnlyList<string> realPaths) => Paths = realPaths;

    public IReadOnlyList<string> Paths { get; }

    /// <summary>The trusted directory covering <paramref name="configDirectory"/> (a real path), or null.</summary>
    public string? FindCovering(string configDirectory) =>
        Paths.FirstOrDefault(trusted => Scripting.Paths.IsSameOrInside(configDirectory, trusted));
}

/// <summary>
/// Arguments of <c>mdlive serve</c>. <c>--trust</c> is taken out here, everything else goes to the ASP.NET Core
/// configuration (e.g. <c>--AllowedHosts=...</c>). Parsed by hand on purpose: as a configuration key, a
/// <c>Trust</c> setting could also come from an environment variable or <c>appsettings.json</c>.
/// </summary>
internal sealed record ServeArguments(IReadOnlyList<string> TrustedDirectories, string[] ConfigurationArgs)
{
    public static ServeArguments Parse(IReadOnlyList<string> args, out IReadOnlyList<string> errors)
    {
        var trusted = new List<string>();
        var rest = new List<string>();
        var problems = new List<string>();

        for (var i = 0; i < args.Count; i++)
        {
            string? value;

            if (args[i] == "--trust")
            {
                if (i + 1 >= args.Count)
                {
                    problems.Add("--trust needs a directory");
                    continue;
                }

                value = args[++i];
            }
            else if (args[i].StartsWith("--trust=", StringComparison.Ordinal))
            {
                value = args[i]["--trust=".Length..];
            }
            else
            {
                rest.Add(args[i]);
                continue;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                problems.Add("--trust needs a directory");
                continue;
            }

            // Względna ścieżka względem katalogu, w którym serwer został uruchomiony
            var real = Paths.GetRealPath(value);
            if (!Directory.Exists(real))
            {
                problems.Add($"--trust {value}: no such directory");
                continue;
            }

            if (!trusted.Contains(real))
            {
                trusted.Add(real);
            }
        }

        errors = problems;
        return new ServeArguments(trusted, [.. rest]);
    }

    /// <summary>
    /// Trusted directories that cover the home directory or the whole file system. Not an error, but then
    /// every repository cloned anywhere under home would run its <c>config.csx</c>.
    /// </summary>
    public IEnumerable<string> TooBroad()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var realHome = string.IsNullOrEmpty(home) ? null : Paths.GetRealPath(home);

        return TrustedDirectories.Where(trusted =>
            Path.GetPathRoot(trusted) == trusted
            || (realHome is not null && Paths.IsSameOrInside(realHome, trusted)));
    }
}
