using System.Reflection;
using System.Runtime.Loader;

namespace ScuroGuardiano.MdLive.Scripting;

/// <summary>
/// <para>
/// Collectible load context for one version of the config script, so editing <c>config.csx</c> doesn't leak
/// a new assembly on every change. Assemblies from <c>#r</c> are loaded from the bytes that were hashed,
/// everything else (framework, ConfigBase, Markdig...) comes from the default context.
/// </para>
/// <para>
/// This is isolation of loading, not a sandbox: script code runs with the same permissions as MdLive.
/// .NET has no in-process security boundary since Code Access Security was dropped. The only protection is
/// not running a script the user hasn't trusted.
/// </para>
/// </summary>
internal sealed class ScriptLoadContext(string name, IReadOnlyDictionary<string, byte[]> referencedAssemblies)
    : AssemblyLoadContext(name, isCollectible: true)
{
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name is not null && referencedAssemblies.TryGetValue(assemblyName.Name, out var image))
        {
            return LoadFromStream(new MemoryStream(image, writable: false));
        }

        // null: domyślny ALC, czyli te same typy co w serwerze
        return null;
    }
}
