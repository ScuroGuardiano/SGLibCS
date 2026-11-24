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
