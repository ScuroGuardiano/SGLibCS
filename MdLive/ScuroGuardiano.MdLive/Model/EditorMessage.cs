using System.Text.Json.Serialization;

namespace ScuroGuardiano.MdLive.Model;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Content), typeDiscriminator: CommandTypes.Content)]
[JsonDerivedType(typeof(Cursor), typeDiscriminator: CommandTypes.Cursor)]
internal closed record EditorMessage()
{
    internal sealed record Content(string Path, string Text) : EditorMessage;
    internal sealed record Cursor(int Line) :  EditorMessage;
}
