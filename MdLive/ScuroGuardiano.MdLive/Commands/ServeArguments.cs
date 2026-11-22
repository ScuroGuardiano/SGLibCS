using ScuroGuardiano.MdLive.Scripting;

namespace ScuroGuardiano.MdLive.Commands;

/// <summary>
/// <para>
/// Arguments of <c>mdlive serve</c>. Only known options are accepted, anything else is an error. Before, the
/// rest of the arguments went to the ASP.NET Core configuration unchecked, so a typo, or an option an older
/// <c>mdlive</c> doesn't know (e.g. <c>--trust</c> from a newer editor plugin), was silently ignored.
/// It also kept <c>--urls</c> and <c>--Kestrel:...</c> working, which could move the server off loopback.
/// </para>
/// <para>
/// Options:
/// <list type="bullet">
/// <item><c>--trust &lt;dir&gt;</c>: parsed here, never a configuration key, since a <c>Trust</c> setting could
/// also come from an environment variable.</item>
/// <item><c>--AllowedHosts &lt;hosts&gt;</c> and <c>--Logging:&lt;key&gt; &lt;value&gt;</c>: passed to the
/// configuration as <c>--Key=value</c>. Names are case-insensitive, like configuration keys.</item>
/// <item><c>--help</c>, <c>-h</c>.</item>
/// </list>
/// Each option takes its value either as <c>--option value</c> or <c>--option=value</c>.
/// </para>
/// </summary>
internal sealed record ServeArguments(IReadOnlyList<string> TrustedDirectories, string[] ConfigurationArgs, bool ShowHelp)
{
    public static ServeArguments Parse(IReadOnlyList<string> args, out IReadOnlyList<string> errors)
    {
        var trusted = new List<string>();
        var configuration = new List<string>();
        var problems = new List<string>();
        var showHelp = false;

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];

            if (arg is "--help" or "-h")
            {
                showHelp = true;
                continue;
            }

            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                problems.Add($"unexpected argument '{arg}'");
                continue;
            }

            var separator = arg.IndexOf('=');
            var name = separator < 0 ? arg : arg[..separator];

            string? TakeValue()
            {
                if (separator >= 0)
                {
                    return arg[(separator + 1)..];
                }

                // "--opcja wartość", ale nie "--opcja --inna"
                return i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal) ? args[++i] : null;
            }

            if (name == "--trust")
            {
                AddTrusted(TakeValue(), trusted, problems);
            }
            else if (name.Equals("--AllowedHosts", StringComparison.OrdinalIgnoreCase)
                     || (name.StartsWith("--Logging:", StringComparison.OrdinalIgnoreCase) && name.Length > "--Logging:".Length))
            {
                var value = TakeValue();
                if (value is null)
                {
                    problems.Add($"{name} needs a value");
                }
                else
                {
                    configuration.Add($"{name}={value}");
                }
            }
            else
            {
                problems.Add($"unknown option '{name}'");
            }
        }

        errors = problems;
        return new ServeArguments(trusted, [.. configuration], showHelp);
    }

    private static void AddTrusted(string? value, List<string> trusted, List<string> problems)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            problems.Add("--trust needs a directory");
            return;
        }

        // Względna ścieżka względem katalogu, w którym serwer został uruchomiony
        var real = Paths.GetRealPath(value);
        if (!Directory.Exists(real))
        {
            problems.Add($"--trust {value}: no such directory");
            return;
        }

        if (!trusted.Contains(real))
        {
            trusted.Add(real);
        }
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
