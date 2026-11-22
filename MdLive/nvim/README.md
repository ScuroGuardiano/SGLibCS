# MdLive for Neovim

Live preview of Markdown rendered by `mdlive serve`, so the browser shows exactly what your
Markdig pipeline and `.mdlive` styles produce. Requires Neovim 0.10+.

## Install

1. Install the `mdlive` tool (from a local feed, until it's published):

   ```sh
   dotnet pack MdLive/ScuroGuardiano.MdLive -c Release -o ./nupkg
   dotnet tool install -g --add-source ./nupkg ScuroGuardiano.MdLive
   ```

   Make sure `~/.dotnet/tools` is in your `PATH`.

2. Add the plugin. The `mdlive/nvim` branch of this repository contains only the plugin at its
   root, so plugin managers can use it directly, e.g. [lazy.nvim](https://github.com/folke/lazy.nvim):

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

   When working on the plugin itself, point lazy.nvim at the checkout instead:
   `dir = "~/src/SGLibCS/MdLive/nvim"` in place of the repository and `branch`.

   Without a plugin manager: `vim.opt.rtp:append("~/src/SGLibCS/MdLive/nvim")` and
   `require("mdlive").setup()`.

3. Optionally run `mdlive init` in your project to create `.mdlive/` with an editable
   `style.css` and `template.html`.

## Commands

| Command        | Description                                                      |
|----------------|------------------------------------------------------------------|
| `:MdLiveStart` | Start the server for the current Markdown buffer, open browser   |
| `:MdLiveStop`  | Stop the server                                                  |
| `:MdLiveOpen`  | Open the preview in the browser again                            |
| `:MdLiveLog`   | Show the server's stderr                                         |
| `:MdLiveTrust` | Review and trust `.mdlive/config.csx`, see below                 |

The server runs with Neovim's current directory as its working directory, so `.mdlive/` is
looked up there and in its parent directories. Switching to another Markdown buffer switches the preview to it.
Saving a file in `.mdlive/` re-renders the preview, picking up style and template changes.

### Trusting `config.csx`

A C# config script ([docs/scripting.md](https://github.com/ScuroGuardiano/SGLibCS/blob/master/MdLive/docs/scripting.md)) runs only
once trusted. Until then the preview uses the default config and shows a banner. `:MdLiveTrust` lists every
file the config consists of with its SHA-256 and offers:

- **Trust:** runs `mdlive trust --hash <hash of the shown list>`, so if anything changed in the meantime,
  nothing is trusted. The preview re-renders with the script right away,
- **Review:** puts the files on the quickfix list. Read them, then run `:MdLiveTrust` again,
- **Cancel.**

Editing any of the files makes the config untrusted again, and the preview falls back to the default.

In directories only you write to, such as your notes, skip this with the `trusted_dirs` option, e.g.
`trusted_dirs = { "~/notes" }`. A config under one of them runs right away and `:MdLiveTrust` says so.
Don't list a directory you clone other people's repositories into. Needs an `mdlive` with `serve --trust`,
older versions ignore the option.
## Options

Defaults:

```lua
require("mdlive").setup({
  -- Command starting the server
  cmd = { "mdlive", "serve" },
  -- Minimal interval between updates while typing
  throttle_ms = 100,
  -- Open the browser once the server reports its port
  open_browser = true,
  -- File types sent to the preview
  filetypes = { "markdown" },
  -- How many lines of server stderr to keep for :MdLiveLog
  log_lines = 500,
  -- Directories where .mdlive/config.csx runs without :MdLiveTrust (passed as mdlive serve --trust).
  -- Only ones nobody else writes to. ~ and $VARIABLES are expanded, missing directories skipped.
  trusted_dirs = {},
})
```

## Protocol

The plugin talks to `mdlive serve` over stdio:

- **stdout**: the server prints a single JSON line, `{"port":12345,"token":"..."}`, with the port
  it listens on, on `127.0.0.1`, and an access token. The preview is opened as
  `http://127.0.0.1:<port>/#<token>`: the page exchanges the token for a session cookie and
  removes it from the address bar. The cookie is a session cookie, gone when the browser closes,
  and both secrets are generated anew on every server start. Without the cookie the server
  answers only the login page, everything else is `401`.
- **stdin**: one JSON message per line:
  - `{"type":"content","path":"/abs/file.md","text":"..."}`, the whole buffer,
  - `{"type":"cursor","line":42}`, 1-based cursor line, used for scroll sync.
- Closing stdin stops the server. Logs go to stderr.

## Publishing the plugin branch

`MdLive/nvim` on `master` is the source of truth, the `mdlive/nvim` branch is generated from it
with `git subtree split`, which keeps the directory's history and moves it to the root.
The split is deterministic, so republishing after changes is a fast-forward:

```sh
git subtree split --prefix=MdLive/nvim -b mdlive/nvim master
git push origin mdlive/nvim
```

Don't commit to `mdlive/nvim` directly, the next split would not contain those commits.
