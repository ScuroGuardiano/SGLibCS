# ScuroGuardiano.MdLive.ConfigBase

Base class for [MdLive](https://github.com/ScuroGuardiano/SGLibCS/tree/master/MdLive) config scripts. Override
`MdLiveConfigurator` in `.mdlive/config.csx` to change the Markdig pipeline, the document, the template, styles,
scripts or the title.

You don't need to install this package. MdLive compiles the script against the copy it ships with. The package
exists so that your editor can offer IntelliSense in `config.csx`:

```csharp
#r "nuget: ScuroGuardiano.MdLive.ConfigBase, 0.2.0"

using Markdig;
using Markdig.Syntax;
using Microsoft.Extensions.Logging;
using ScuroGuardiano.MdLive.ConfigBase;

public class MyConfig(ILogger<MdLiveConfigurator> logger) : MdLiveConfigurator(logger)
{
    public override Task<string?> ProvideTitle(MarkdownDocument document) =>
        Task.FromResult<string?>("My title");
}
```

MdLive doesn't download anything for this reference, it maps it to its own `ScuroGuardiano.MdLive.ConfigBase`.
Use the version matching your `mdlive`, otherwise it logs a warning. Any other `#r "nuget: ..."` is rejected.

See [docs/scripting.md](https://github.com/ScuroGuardiano/SGLibCS/blob/master/MdLive/docs/scripting.md) for
the whole guide, including trusting the script with `mdlive trust` and editor setup.
