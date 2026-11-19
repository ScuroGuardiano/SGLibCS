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

2. Add the plugin. It lives in a subdirectory of this repository, so point your plugin manager
   at a local checkout, e.g. with [lazy.nvim](https://github.com/folke/lazy.nvim):

   ```lua
   {
     dir = "~/src/SGLibCS/MdLive/nvim",
     ft = "markdown",
     cmd = { "MdLiveStart", "MdLiveStop", "MdLiveOpen", "MdLiveLog" },
     opts = {},
   }
   ```

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

The server runs with Neovim's current directory as its working directory, so `.mdlive/` is
looked up there. Switching to another Markdown buffer switches the preview to it.
Saving a file in `.mdlive/` re-renders the preview, picking up style and template changes.

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
})
```

## Protocol

The plugin talks to `mdlive serve` over stdio:

- **stdout**: the server prints a single line with the port it listens on, on `127.0.0.1`.
- **stdin**: one JSON message per line:
  - `{"type":"content","path":"/abs/file.md","text":"..."}`, the whole buffer,
  - `{"type":"cursor","line":42}`, 1-based cursor line, used for scroll sync.
- Closing stdin stops the server. Logs go to stderr.
