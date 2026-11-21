using System.Text.Json;
using ScuroGuardiano.MdLive.Scripting;

namespace ScuroGuardiano.MdLive.Commands;

/// <summary>
/// <para>
/// <c>mdlive trust</c>: lists every file <c>.mdlive/config.csx</c> consists of with its SHA-256 and marks
/// exactly this version as trusted.
/// </para>
/// <para>
/// Interactively it asks for confirmation. Non-interactively (from an editor) it requires <c>--hash</c> with
/// the hash the user was shown, so files changed between the review and the approval aren't trusted.
/// </para>
/// </summary>
internal static class TrustCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<int> RunAsync(IReadOnlyList<string> args, TrustStore? trustStore = null,
        string configDirectory = ConfiguratorProvider.DefaultConfigDirectory)
    {
        var show = false;
        var json = false;
        var revoke = false;
        string? expectedHash = null;

        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--show":
                    show = true;
                    break;
                case "--json":
                    json = true;
                    break;
                case "--revoke":
                    revoke = true;
                    break;
                case "--hash" when i + 1 < args.Count:
                    expectedHash = args[++i].Trim().ToLowerInvariant();
                    break;
                default:
                    await Console.Error.WriteLineAsync($"mdlive trust: unknown option '{args[i]}'");
                    return 1;
            }
        }

        trustStore ??= TrustStore.CreateDefault();

        var bundle = ScriptBundle.Read(configDirectory, ScriptCompiler.IsKnownAssemblyName);
        if (bundle is null)
        {
            await Console.Error.WriteLineAsync(
                $"mdlive trust: no {ScriptBundle.MainFileName} in {Path.GetFullPath(configDirectory)}");
            return 1;
        }

        if (revoke)
        {
            var removed = trustStore.Revoke(bundle.ConfigDirectory);
            Console.WriteLine(removed ? $"Revoked trust for {bundle.ConfigDirectory}" : $"{bundle.ConfigDirectory} wasn't trusted");
            return 0;
        }

        var state = trustStore.GetState(bundle);

        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                directory = bundle.ConfigDirectory,
                hash = bundle.Hash,
                status = state.Status.ToString().ToLowerInvariant(),
                problem = state.Problem,
                files = bundle.Files.Select(f => new
                {
                    path = f.RelativePath,
                    fullPath = f.FullPath,
                    kind = f.Kind.ToString().ToLowerInvariant(),
                    sha256 = f.Sha256,
                    size = f.Content.Length
                }),
                frameworkReferences = bundle.FrameworkReferences,
                errors = bundle.Errors,
                warnings = bundle.Warnings
            }, JsonOptions));
        }
        else
        {
            PrintSummary(bundle, state);
        }

        if (show)
        {
            return 0;
        }

        if (bundle.Errors.Count > 0)
        {
            await Console.Error.WriteLineAsync("mdlive trust: fix the errors above first");
            return 1;
        }

        if (expectedHash is not null)
        {
            if (expectedHash != bundle.Hash)
            {
                // Pliki zmieniły się od momentu, w którym użytkownik je oglądał
                await Console.Error.WriteLineAsync(
                    $"mdlive trust: the config changed since it was reviewed (expected {expectedHash}, got {bundle.Hash}), nothing trusted");
                return 2;
            }
        }
        else if (Console.IsInputRedirected)
        {
            await Console.Error.WriteLineAsync(
                "mdlive trust: stdin is not a terminal, pass --hash <hash> with the hash you reviewed");
            return 1;
        }
        else
        {
            Console.Write("Trust and run this code with your permissions? [y/N] ");
            var answer = Console.ReadLine()?.Trim();
            if (!string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Nothing trusted");
                return 1;
            }
        }

        trustStore.Trust(bundle);
        await Console.Error.WriteLineAsync($"Trusted {bundle.ConfigDirectory} ({bundle.Hash})");
        return 0;
    }

    private static void PrintSummary(ScriptBundle bundle, TrustState state)
    {
        Console.WriteLine($"Config: {bundle.ConfigDirectory}");
        Console.WriteLine($"Status: {state.Status.ToString().ToLowerInvariant()}{(state.Problem is null ? "" : $" ({state.Problem})")}");
        Console.WriteLine();

        foreach (var file in bundle.Files)
        {
            Console.WriteLine($"  {file.Kind.ToString().ToLowerInvariant(),-9} {file.Sha256}  {file.RelativePath} ({file.Content.Length} B)");
        }

        foreach (var name in bundle.FrameworkReferences)
        {
            Console.WriteLine($"  framework {name}");
        }

        foreach (var error in bundle.Errors)
        {
            Console.WriteLine($"  error: {error}");
        }

        foreach (var warning in bundle.Warnings)
        {
            Console.WriteLine($"  warning: {warning}");
        }

        Console.WriteLine();
        Console.WriteLine($"Hash: {bundle.Hash}");
    }
}
