# C# config script (`.mdlive/config.csx`)

When the files in `.mdlive/` aren't enough, the whole rendering can be changed in C#. Override the virtual
methods of [`MdLiveConfigurator`](../ScuroGuardiano.MdLive.ConfigBase/MdLiveConfigurator.cs) in
`.mdlive/config.csx`: the Markdig pipeline, the document transformation, the template, styles, scripts or
the title.

**The script is a program that runs with your permissions.** MdLive only runs it after you've reviewed and
trusted its exact content with `mdlive trust`. See [Security](#security).

## Example

```csharp
#load "lib/headings.csx"

public class MyConfig(ILogger<MdLiveConfigurator> logger) : MdLiveConfigurator(logger)
{
    public override Task<MarkdownPipeline> CreateMarkdownPipeline() =>
        Task.FromResult(new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseEmojiAndSmiley()
            .Build());

    public override async Task TransformMarkdownDocument(MarkdownDocument document)
    {
        await base.TransformMarkdownDocument(document); // keeps data-line for scroll sync
        Headings.AddAnchors(document);
    }
}
```

With `.mdlive/lib/headings.csx`:

```csharp
using Markdig.Renderers.Html;

public static class Headings
{
    public static void AddAnchors(MarkdownDocument document)
    {
        foreach (var heading in document.Descendants<HeadingBlock>())
        {
            heading.GetAttributes().AddClass("anchor");
        }
    }
}
```

Then trust it once from the project directory:

```sh
mdlive trust
```

It lists every file the config consists of with its SHA-256 and asks for confirmation. Every change to any of
them needs approving again, until then the preview uses the default config and shows a banner saying why.

## How the configurator is picked

The script can:

- declare exactly one non-abstract class deriving from `MdLiveConfigurator`. It's created with dependency
  injection, so its constructor can ask for `ILogger<MdLiveConfigurator>` and other services,
- or end with `return typeof(MyConfig);` to pick one of several classes,
- or return an instance: `return new MyConfig(...);`.

Top-level statements run once, every time the script is (re)loaded.

`ConfigDirectory` (the `.mdlive` directory that was found, see the [README](https://github.com/ScuroGuardiano/SGLibCS/tree/master/MdLive#the-mdlive-folder))
is set after the configurator is created, so use it in methods, not in the constructor.

## What the script can use

Imported in every script: `System`, `System.Collections.Generic`, `System.IO`, `System.Linq`, `System.Text`,
`System.Threading`, `System.Threading.Tasks`, `Markdig`, `Markdig.Syntax`, `Microsoft.Extensions.Logging`,
`ScuroGuardiano.MdLive.ConfigBase`.

Referenced: the whole .NET shared framework (`Microsoft.NETCore.App`) and `ScuroGuardiano.MdLive.ConfigBase`
with its dependencies (Markdig, Markdown.ColorCode, ColorCode, Humanizer). These are the very assemblies
MdLive runs on, so `MdLiveConfigurator` in the script is the same type the server uses.

Language version: the latest C# supported by the Roslyn version bundled with MdLive. It doesn't depend on the
installed SDK, MdLive needs only the runtime.

### Directives

| Directive                    | Meaning                                                                         |
|------------------------------|---------------------------------------------------------------------------------|
| `#load "lib/helpers.csx"`    | Includes another script, path relative to the file with the directive.          |
| `#r "lib/Some.Library.dll"`  | References an assembly, path relative to the file with the directive.           |
| `#r "System.Net.Http"`       | By name, only assemblies MdLive already references (accepted for compatibility). |
| `#r "nuget: ScuroGuardiano.MdLive.ConfigBase, x.y.z"` | For IntelliSense, maps to the bundled ConfigBase, see [Editor support](#editor-support). |
| `#r "nuget: Package, 1.0.0"` | Any other package: **not supported**, see [NuGet packages](#nuget-packages). |

Rules, all checked before anything is compiled:

- every file must be inside `.mdlive/`. `..` and absolute paths leading out of it are errors,
- no symbolic links inside `.mdlive/`. `.mdlive` itself may be a link, then the trust is bound to its target,
- `#load` only `.csx`, `#r` only `.dll`, at most 64 files, 1 MiB per script, 32 MiB per assembly,
- an assembly can't have the name of one MdLive provides (e.g. a second `Markdig.dll`). Two copies would mean
  two different `MarkdownDocument` types and confusing cast errors,
- an assembly's own dependencies aren't resolved automatically, `#r` each of them.

## Trusting a whole directory: `mdlive serve --trust <dir>`

For directories only you write to, such as your own notes, approving every change gets tedious:

```sh
mdlive serve --trust ~/notes --trust ~/blog
```

A `config.csx` in `.mdlive` anywhere under these directories runs without an entry in the trust store, and
nothing is written there. Only the trust store lookup is skipped: files still have to be inside `.mdlive`,
without symlinks, and are compiled from the bytes that were read. Paths are compared after resolving symlinks,
so a link from a trusted directory to somewhere else doesn't make that place trusted, and `~/notes` doesn't
cover `~/notes-old`.

Don't point it at a directory where you clone other people's repositories: their `config.csx` would run as
soon as you open one of their Markdown files. MdLive logs a warning when a `--trust` directory covers your
whole home directory. The option is deliberately command line only: as a configuration key it could also come
from an environment variable.

With the Neovim plugin, list the directories in `trusted_dirs`. `~` and `$VARIABLES` are expanded and
directories that don't exist are skipped with a warning, so the same config works on machines without them:

```lua
require("mdlive").setup({
  trusted_dirs = { "~/notes", "~/blog" },
})
```

## `mdlive trust`

```text
mdlive trust               list the files, ask, trust
mdlive trust --show        only list the files, their hashes and the status
mdlive trust --show --json the same as JSON, for editor plugins
mdlive trust --hash <h>    trust only if the config still has hash <h> (non-interactive)
mdlive trust --revoke      forget the trust for this config
```

Without a terminal (from an editor) `--hash` is required: the editor shows the user `--show --json`, and passes
the hash they agreed to. If anything changed in between, nothing is trusted and the exit code is 2.

Status values: `trusted`, `untrusted`, `changed` (trusted before, different now), `storerejected` (the trust
store can't be used, see below).

## Security

### Why there's a trust step at all

`.mdlive/` is committed to repositories, and the Neovim plugin starts MdLive in whatever project you open.
Without the trust step, opening a Markdown file in a cloned repository would run code its author wrote. That's
[CWE-829: Inclusion of Functionality from Untrusted Control Sphere](https://cwe.mitre.org/data/definitions/829.html),
the same class of problem as Git running hooks or config from a repository someone else controls
([CVE-2022-24765](https://github.blog/engineering/git-security-vulnerability-announced/)). Tools that run
per-project code solve it the same way:

- [direnv](https://manpages.debian.org/unstable/direnv/direnv.1.en.html) refuses `.envrc` until
  `direnv allow`, and changing the file blocks it again,
- Neovim's `exrc` reads `.nvim.lua` only through `vim.secure.read`, which stores the SHA-256 of approved
  files in a trust database
  ([secure.lua](https://sources.debian.org/src/neovim/0.11.6-1/runtime/lua/vim/secure.lua)),
- VS Code opens unknown folders in
  [Restricted Mode](https://code.visualstudio.com/docs/editing/workspaces/workspace-trust) to prevent
  automatic code execution from workspace tasks and settings.

### No sandbox

There's no way to run the script with fewer permissions in-process. Code Access Security, the .NET Framework
sandbox, [isn't supported in .NET Core and .NET 5+](https://learn.microsoft.com/en-us/dotnet/framework/misc/code-access-security),
and even there Microsoft says it shouldn't be used as a security boundary.
[`AssemblyLoadContext`](https://learn.microsoft.com/dotnet/core/dependency-loading/understanding-assemblyloadcontext)
isolates loading of assemblies, not permissions. The script can reach anything the MdLive process can, through
reflection included. The protection is to not run what the user hasn't approved.

### The chain

1. **Read, don't compile.** `config.csx` and everything it `#load`s is read into memory and only *parsed*
   to find `#load` and `#r`. Referenced DLLs are read as bytes, not even parsed as PE files.
   [`ScriptBundle`](../ScuroGuardiano.MdLive/Scripting/ScriptBundle.cs)
2. **Hash.** A manifest lists every file as `kind sha256 relative/path` plus the bare-name references. Its
   SHA-256 is the version the user trusts. Paths are part of it, so renaming or moving files is a change too.
3. **Check trust.** [`TrustStore`](../ScuroGuardiano.MdLive/Scripting/TrustStore.cs) keeps
   `config directory → hash` in `$XDG_DATA_HOME/mdlive/trusted.json` (`~/.local/share/mdlive`,
   `%LOCALAPPDATA%\mdlive` on Windows):
    - outside any project, so a repository can't trust itself. It's bound to the directory too, so a
      copy of a trusted config somewhere else isn't trusted,
    - written atomically (temporary file + rename), created with `0600` in a `0700` directory,
    - ignored entirely when it or its directory is writable by group or others, like `StrictModes` in sshd.
      A corrupted file trusts nothing.
4. **Compile the same bytes.** [`ScriptCompiler`](../ScuroGuardiano.MdLive/Scripting/ScriptCompiler.cs)
   compiles with resolvers that serve `#load` and `#r` *only from the bundle in memory*. A file swapped on
   disk after the hash was computed (TOCTOU, [CWE-367](https://cwe.mitre.org/data/definitions/367.html))
   never gets in. Anything the reader didn't collect doesn't exist for the compiler. That's a second gate in
   case Roslyn interprets a path differently than the reader. No analyzers or source generators are loaded,
   so compilation itself runs no user code.
5. **Load the same bytes.** The script and its `#r` assemblies are loaded from memory into a collectible
   [`ScriptLoadContext`](../ScuroGuardiano.MdLive/Scripting/ScriptLoadContext.cs), everything else comes from
   the default context.
6. **Re-check before every render.** Steps 1–3 take ~0.2 ms, so they run before each render. A changed file
   or a revoked trust switches the preview to the default config immediately. Recompiling happens only when
   the hash changes.

### Threats and what handles them

| Threat                                                         | Mitigation                                                   |
|----------------------------------------------------------------|--------------------------------------------------------------|
| Cloned repository with a malicious `config.csx`                | Not trusted, never compiled or run, banner in the preview.   |
| `--trust` covering more than intended                           | Real paths compared component by component (no symlink or `notes-old` tricks), warning when it covers the home directory, command line only. |
| `.mdlive` planted in a shared parent directory (`/tmp`)          | Searching parents stops below world-writable directories and never looks at a drive root on Windows. Same class of bug as git's [CVE-2022-24765](https://github.blog/engineering/git-security-vulnerability-announced/), which git fixed with ownership checks. |
| Trusted repository updated by `git pull`                       | Hash changes, trust has to be given again after review.      |
| Change hidden in a `#load`-ed file or a `#r`-ed DLL            | They're part of the hash.                                    |
| `#load "../../x.csx"`, absolute paths, symlinks out of `.mdlive` | Rejected by the reader. The compiler only sees bundle files. |
| File swapped between hash and compilation or loading           | Compiled and loaded from the hashed bytes.                   |
| File changed between review and `mdlive trust`                 | `--hash` from the reviewed listing, or the interactive prompt shows what's trusted. |
| Repository writing to the trust store                          | Store is outside the project. Group- or world-writable store is ignored. |
| Huge files, `#load` cycles                                     | Size and count limits, visited set.                          |
| Diagnostics or exception messages containing HTML              | Banner text is HTML-encoded.                                 |
| Script hanging in an `await`                                   | 15 s timeout, default config is used. A busy loop can't be interrupted in .NET, it keeps a thread busy until MdLive exits. |

Out of scope: anyone who can write to your home directory or to the project while you review it can already
run code as you. Hard links can't be told apart from regular files, but their content is hashed like any other.

### What was tested

`MdLive/ScuroGuardiano.MdLive.Tests` (xUnit, 59 tests):

- the reader: path escapes in 7 spellings, symlinked file, symlinked directory, symlinked `config.csx`,
  `nuget:`, unknown names, wrong extensions, missing files, size limits, cycles, hash stability and
  sensitivity,
- the trust store: change detection, revoke, binding to the directory, `0600`/`0700`, rejected group-writable
  file or directory, corrupted JSON,
- the provider: an untrusted script never runs (checked with a file the script would create), a change in a
  `#load`-ed file or a `#r`-ed DLL stops it, the swapped-file case compiles and runs the hashed bytes, an
  untrusted DLL isn't parsed, a DLL shadowing Markdig is rejected, compile and runtime errors carry file and
  line, timeouts, the ways of picking a configurator, the old load context really unloads.

End to end with Neovim, `mdlive serve` from the installed tool and Chromium: banner for an untrusted script,
`mdlive trust --hash` enables it on the next render, editing the config in Neovim disables it immediately,
compile errors are shown escaped, `--revoke` works.

## Alternatives considered

- **`Microsoft.CodeAnalysis.CSharp.Scripting` (`CSharpScript.RunAsync`).** Simplest, but every run loads a
  new assembly that's never unloaded ([dotnet/roslyn#41722](https://github.com/dotnet/roslyn/issues/41722)),
  so every edit of the config would leak. It also loads `#r` assemblies from disk by path after compilation,
  which reopens the TOCTOU window. MdLive uses `CSharpCompilation.CreateScriptCompilation` from
  `Microsoft.CodeAnalysis.CSharp` directly and calls the synthesized `<Factory>` entry point itself, the same
  way the scripting package does
  ([ScriptBuilder](https://source.dot.net/Microsoft.CodeAnalysis.Scripting/ScriptBuilder.cs.html),
  [ScriptExecutionState](https://source.dot.net/Microsoft.CodeAnalysis.Scripting/ScriptExecutionState.cs.html)),
  with the result in a [collectible load context](https://learn.microsoft.com/dotnet/standard/assembly/unloadability).
- **A project (`.mdlive/config.csproj`) built with `dotnet build`**, or
  [file-based apps](https://learn.microsoft.com/dotnet/core/sdk/file-based-apps) (`#:package`). Proper NuGet
  support, but they need the SDK, take seconds to build, and MSBuild executes code from the project and from
  every `Directory.Build.props` above it, so the trust check would have to cover much more than the script.
- **dotnet-script.** Handles `#r "nuget:"` by generating a project and running `dotnet restore`
  ([Dotnet.Script](https://www.nuget.org/packages/Dotnet.Script/)), which brings back the SDK and MSBuild.

## NuGet packages

Not supported yet, apart from `ScuroGuardiano.MdLive.ConfigBase`, which isn't downloaded at all. Options, from the safest:

1. Copy the DLLs into `.mdlive/` and `#r` them (works now, they're hashed like any other file).
2. Download with the NuGet client libraries (`NuGet.Protocol`), without MSBuild, require an exact version and
   put the package hash into the manifest, so `mdlive trust` covers the package content too.
3. `dotnet restore` of a generated project, like dotnet-script. Needs the SDK and runs MSBuild.

## Editor support

The defaults MdLive adds (imported namespaces, referenced assemblies) are invisible to editors. To get
IntelliSense, write the script so that it stands on its own:

```csharp
#r "nuget: ScuroGuardiano.MdLive.ConfigBase, 0.2.0"

using Markdig;
using Markdig.Syntax;
using Microsoft.Extensions.Logging;
using ScuroGuardiano.MdLive.ConfigBase;

public class MyConfig(ILogger<MdLiveConfigurator> logger) : MdLiveConfigurator(logger)
{
}
```

- [`ScuroGuardiano.MdLive.ConfigBase`](https://www.nuget.org/packages/ScuroGuardiano.MdLive.ConfigBase) is
  published with the same version as the `mdlive` tool. Its dependencies are Markdig, Markdown.ColorCode and
  Humanizer, so the editor sees them too, and it includes the XML docs of `MdLiveConfigurator`.
- **MdLive doesn't download it.** The reference maps to the ConfigBase MdLive is built with, so it doesn't
  weaken the trust model: the script is still compiled only against what's listed in
  [What the script can use](#what-the-script-can-use). A different version only logs a warning, because the
  script is compiled against the bundled one anyway.
- The explicit `using` directives are harmless for MdLive, they repeat its defaults.
- `mdlive --version` prints the ConfigBase version to put in the reference.

### OmniSharp (works)

Tested with OmniSharp 2.0.0 in Neovim 0.12: hover with the XML docs from the package, completion of
Markdig types, and diagnostics without false errors. Without the setting below the same file is full of
`CS0246` errors. OmniSharp resolves `#r "nuget:"` in `.csx` only with script NuGet references enabled, in `omnisharp.json` in
the directory OmniSharp is started in (the project root), or globally in `~/.omnisharp/omnisharp.json`
([Configuration Options](https://github.com/OmniSharp/omnisharp-roslyn/wiki/Configuration-Options)):

```json
{
  "script": {
    "enabled": true,
    "defaultTargetFramework": "net11.0",
    "enableScriptNuGetReferences": true
  }
}
```

Neovim gives `.csx` the `cs` filetype. With nvim-lspconfig `vim.lsp.enable("omnisharp")` is enough, without it
(Neovim 0.11+):

```lua
vim.lsp.config("omnisharp", {
  cmd = { "OmniSharp", "-z", "--hostPID", tostring(vim.fn.getpid()), "--encoding", "utf-8", "--languageserver" },
  filetypes = { "cs" },
  root_markers = { "omnisharp.json", ".git" },
})
vim.lsp.enable("omnisharp")
```

Restoring is done by the editor, with its own NuGet configuration. That's outside of MdLive and its trust store.

The package has to be on a NuGet feed the editor can reach. The first version on nuget.org is the one released
together with this feature.

### Roslyn language server (doesn't work)

`roslyn-language-server` 5.13 (the server roslyn.nvim runs) opens `.cs` files as file-based programs, but
loads no project for `.csx`: no hover, completion or diagnostics at all, even with `languageId` `csharp`. Use
OmniSharp for `config.csx`, it can run next to Roslyn LS, which keeps handling regular projects.
