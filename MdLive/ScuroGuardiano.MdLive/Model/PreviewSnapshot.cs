namespace ScuroGuardiano.MdLive.Model;

internal sealed record PreviewSnapshot(
    RenderedDocument? Document,
    long DocumentVersion,
    int CursorLine,
    long CursorVersion)
{
    public static PreviewSnapshot Empty { get; } = new(null, 0, 1, 0);
}
