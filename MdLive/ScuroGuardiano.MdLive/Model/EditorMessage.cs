namespace ScuroGuardiano.MdLive.Model;

internal sealed record EditorMessage(string Type, string? Path, string? Text, int? Line);