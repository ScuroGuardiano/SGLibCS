using System.Text.Json;

namespace ScuroGuardiano.MdLive.Scripting;

/// <summary>
/// <para>
/// Remembers which config scripts the user has reviewed and trusts, like <c>direnv allow</c> or Neovim's
/// <c>:trust</c>. A config directory is trusted only with the exact <see cref="ScriptBundle.Hash"/> that was
/// approved: any change to a script, a <c>#load</c>-ed file or a referenced assembly needs approving again.
/// </para>
/// <para>
/// The store lives outside of any project (<c>$XDG_DATA_HOME/mdlive/trusted.json</c>, on Windows
/// <c>%LOCALAPPDATA%\mdlive</c>), so a cloned repository can't trust itself. On Unix the file is created
/// with <c>0600</c> in a <c>0700</c> directory and ignored when writable by group or others.
/// </para>
/// </summary>
internal sealed class TrustStore(string path)
{
    private const UnixFileMode PrivateFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode PrivateDirectory = PrivateFile | UnixFileMode.UserExecute;
    private const UnixFileMode WritableByOthers = UnixFileMode.GroupWrite | UnixFileMode.OtherWrite;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public string FilePath { get; } = path;

    /// <summary>
    /// Default location. On Unix <see cref="Environment.SpecialFolder.LocalApplicationData"/> is
    /// <c>$XDG_DATA_HOME</c> or <c>~/.local/share</c>.
    /// </summary>
    public static TrustStore CreateDefault() =>
        new(Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.DoNotVerify), "mdlive", "trusted.json"));

    public TrustState GetState(ScriptBundle bundle)
    {
        if (!TryLoad(out var entries, out var problem))
        {
            return new TrustState(TrustStatus.StoreRejected, problem);
        }

        if (!entries.TryGetValue(bundle.ConfigDirectory, out var hash))
        {
            return new TrustState(TrustStatus.Untrusted, null);
        }

        return hash == bundle.Hash
            ? new TrustState(TrustStatus.Trusted, null)
            : new TrustState(TrustStatus.Changed, null);
    }

    public void Trust(ScriptBundle bundle) => Update(entries => entries[bundle.ConfigDirectory] = bundle.Hash);

    public bool Revoke(string configDirectory)
    {
        var removed = false;
        Update(entries => removed = entries.Remove(configDirectory));
        return removed;
    }

    private void Update(Action<Dictionary<string, string>> change)
    {
        if (!TryLoad(out var entries, out var problem))
        {
            throw new InvalidOperationException(problem);
        }

        change(entries);

        var directory = Path.GetDirectoryName(FilePath)!;
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(directory);
        }
        else
        {
            Directory.CreateDirectory(directory, PrivateDirectory);
        }

        // Zapis do pliku tymczasowego i rename: przerwany zapis nie zostawi uciętego JSON-a,
        // a plik od początku istnieje z 0600, bez okna, w którym byłby czytelny dla innych
        var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = PrivateFile;
        }

        try
        {
            using (var stream = new FileStream(temporary, options))
            {
                JsonSerializer.Serialize(stream, new TrustFile { Trusted = entries }, JsonOptions);
            }

            File.Move(temporary, FilePath, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private bool TryLoad(out Dictionary<string, string> entries, out string? problem)
    {
        entries = new Dictionary<string, string>(StringComparer.Ordinal);
        problem = null;

        if (!File.Exists(FilePath))
        {
            return true;
        }

        if (!OperatingSystem.IsWindows())
        {
            var directoryMode = File.GetUnixFileMode(Path.GetDirectoryName(FilePath)!);
            var fileMode = File.GetUnixFileMode(FilePath);

            // Jak StrictModes w sshd: jeśli ktoś inny może dopisać sobie zaufanie, plik nic nie znaczy
            if ((fileMode & WritableByOthers) != 0 || (directoryMode & WritableByOthers) != 0)
            {
                problem = $"{FilePath} or its directory is writable by group or others, ignoring it. " +
                          "Fix with: chmod 600 the file and chmod 700 the directory";
                return false;
            }
        }

        try
        {
            using var stream = File.OpenRead(FilePath);
            var file = JsonSerializer.Deserialize<TrustFile>(stream, JsonOptions);
            if (file?.Trusted is not null)
            {
                entries = new Dictionary<string, string>(file.Trusted, StringComparer.Ordinal);
            }

            return true;
        }
        catch (JsonException ex)
        {
            problem = $"{FilePath} is corrupted: {ex.Message}";
            return false;
        }
    }

    private sealed class TrustFile
    {
        public int Version { get; init; } = 1;
        public Dictionary<string, string>? Trusted { get; init; }
    }
}

internal enum TrustStatus
{
    Trusted,
    Untrusted,

    /// <summary>Trusted before, but something in the bundle changed since.</summary>
    Changed,

    /// <summary>The trust store itself can't be used (bad permissions, corrupted), nothing is trusted.</summary>
    StoreRejected
}

internal sealed record TrustState(TrustStatus Status, string? Problem);
