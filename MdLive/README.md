# MdLive

Live preview of Markdown rendered with **your own** [Markdig](https://github.com/xoofx/markdig) pipeline,
template and styles, so the browser shows exactly what your site or document generator will produce.

MdLive is not a standalone editor or app. It's a small preview server meant to be **started and driven
by an editor**: the editor sends the buffer content and cursor position, MdLive renders it and pushes
the result to the browser as you type. A ready-made [Neovim plugin](https://github.com/ScuroGuardiano/SGLibCS/tree/master/MdLive/nvim) is included.

- live updates while typing, before saving, morphed into the page without reloads
  ([Datastar](https://data-star.dev)),
- scroll sync: the block under the cursor stays in the middle of the preview,
- your HTML template, CSS and scripts from `.mdlive/`, re-applied as soon as you change them,
- server-side syntax highlighting with CSS classes you style yourself
  ([docs/syntax-highlight.md](https://github.com/ScuroGuardiano/SGLibCS/blob/master/MdLive/docs/syntax-highlight.md)),
- images referenced from the document, served from its directory.

## Install

Requires the .NET 11 runtime.

```sh
dotnet tool install -g ScuroGuardiano.MdLive
```

Make sure `~/.dotnet/tools` is in your `PATH`. To build it from source instead:

```sh
dotnet pack MdLive/ScuroGuardiano.MdLive -c Release -o ./nupkg
dotnet tool install -g --add-source ./nupkg ScuroGuardiano.MdLive
```

## Quick start with Neovim

1. Add the plugin, e.g. with [lazy.nvim](https://github.com/folke/lazy.nvim). The `mdlive/nvim` branch
   of this repository contains only the plugin:

   ```lua
   {
     "ScuroGuardiano/SGLibCS",
     branch = "mdlive/nvim",
     name = "mdlive",
     ft = "markdown",
     cmd = { "MdLiveStart", "MdLiveStop", "MdLiveOpen", "MdLiveLog", "MdLiveTrust" },
     opts = {},
   }
   ```

2. Optionally run `mdlive init` in your project to get an editable `.mdlive/` folder.
3. Open a Markdown file and run `:MdLiveStart`.

See [nvim/README.md](https://github.com/ScuroGuardiano/SGLibCS/blob/master/MdLive/nvim/README.md) for commands and options.

## Commands

| Command                     | Description                                                                   |
|-----------------------------|-------------------------------------------------------------------------------|
| `mdlive init [--force]`     | Create `.mdlive/` with default `style.css` and `template.html`. Refuses a non-empty `.mdlive/` unless `--force`, which overwrites only those two files. |
| `mdlive serve`              | Start the preview server for an editor, see [Editor integration](#editor-integration). |
| `mdlive help`               | Show usage.                                                                   |

## The `.mdlive` folder

MdLive looks for `.mdlive/` in its working directory, which the Neovim plugin sets to Neovim's
current directory. Every file is optional and re-read on each render.

| File            | Used as                                                                     |
|-----------------|-----------------------------------------------------------------------------|
| `template.html` | Page template. Placeholders: `<%title%>`, `<%styles%>`, `<%content%>`, `<%scripts%>`. |
| `style.css`     | Inlined in a `<style>` tag, after `styles.html`, so it can override it.     |
| `styles.html`   | Raw HTML for `<%styles%>`, e.g. `<link>` tags to external stylesheets.       |
| `script.js`     | Inlined in a `<script>` tag, after `scripts.html`.                          |
| `scripts.html`  | Raw HTML for `<%scripts%>`, e.g. third-party `<script>` tags.                |

Without `template.html` a minimal built-in template is used. The title comes from the first
level 1 heading, falling back to the file name.

The Markdown pipeline, template loading and the rest are defined in
[`MdLiveConfigurator`](https://github.com/ScuroGuardiano/SGLibCS/blob/master/MdLive/ScuroGuardiano.MdLive.ConfigBase/MdLiveConfigurator.cs), with virtual methods
meant to be overridden.

## Editor integration

`mdlive serve` talks to the editor over stdio: it prints one line on stdout telling the editor where
the preview is, reads one JSON message per line on stdin (buffer content, cursor line) and exits when
stdin is closed. Logs go to stderr. The exact protocol is described in
[nvim/README.md](https://github.com/ScuroGuardiano/SGLibCS/blob/master/MdLive/nvim/README.md#protocol), which is also the reference implementation for other editors.

## Security

The preview server is meant for the local user only:

- it listens only on `127.0.0.1`, on a random free port, and this can't be changed by configuration,
- requests with a `Host` header other than `localhost`, `127.0.0.1` or `[::1]` are rejected
  (DNS rebinding). Override with `--AllowedHosts=...` or the `AllowedHosts` environment variable,
- every request needs a session, so other local users and processes can't read the preview. On each start
  the server generates a random access token and gives it only to the editor, on stdout. The editor opens
  `http://127.0.0.1:<port>/#<token>`. The token is in the URL fragment, which the browser never sends
  to the server. The page removes it from the address bar and history and exchanges it for a session cookie:
  `HttpOnly`, `SameSite=Strict`, without an expiry date, so it disappears when the browser is closed,
- the cookie encryption keys are kept only in memory, so restarting the server invalidates all sessions
  and the old token. Open the preview from the editor again (`:MdLiveOpen`),
- only image files are served from the document's directory, never anything outside it.

## License

[MIT](https://github.com/ScuroGuardiano/SGLibCS/blob/master/MdLive/LICENSE)
