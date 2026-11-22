-- MdLive: podgląd Markdowna renderowany przez `mdlive serve`.
--
-- Protokół: jedna linia JSON na wiadomość na stdin serwera
--   {"type":"content","path":"/abs/file.md","text":"..."}
--   {"type":"cursor","line":42}
-- Serwer wypisuje na stdout jedną linię z portem, logi idą na stderr.

local M = {}

local config = {
  -- Komenda uruchamiająca serwer
  cmd = { "mdlive", "serve" },
  -- Minimalny odstęp między wysyłkami treści i kursora podczas pisania
  throttle_ms = 100,
  -- Otwórz przeglądarkę, gdy serwer poda port
  open_browser = true,
  -- Typy plików, dla których wysyłamy treść
  filetypes = { "markdown" },
  -- Ile linii stderr serwera trzymać dla :MdLiveLog
  log_lines = 500,
  -- Katalogi, w których .mdlive/config.csx działa bez :MdLiveTrust (mdlive serve --trust).
  -- Tylko takie, do których nikt poza Tobą nie pisze. ~ i $ZMIENNE są rozwijane.
  trusted_dirs = {},
}

local state = {
  job = nil,
  port = nil,
  -- Token dostępu do podglądu, ważny przez cały czas działania serwera
  token = nil,
  stdout_partial = "",
  log = {},
  augroup = nil,
  -- Bufor, którego treść pokazuje podgląd
  bufnr = nil,
  sent_tick = nil,
  sent_line = nil,
  -- Katalog roboczy serwera, w nim szuka .mdlive/
  cwd = nil,
}

local function base_url()
  return state.port and ("http://127.0.0.1:" .. state.port .. "/") or nil
end

local function notify(msg, level)
  vim.notify("MdLive: " .. msg, level or vim.log.levels.INFO)
end

local function is_previewable(bufnr)
  return vim.api.nvim_buf_is_valid(bufnr) and vim.tbl_contains(config.filetypes, vim.bo[bufnr].filetype)
end

local function send(message)
  if not state.job then
    return
  end

  vim.fn.chansend(state.job, vim.json.encode(message) .. "\n")
end

local function send_content(force)
  local bufnr = state.bufnr
  if not bufnr or not vim.api.nvim_buf_is_valid(bufnr) then
    return
  end

  local tick = vim.api.nvim_buf_get_changedtick(bufnr)
  if not force and tick == state.sent_tick then
    return
  end

  local lines = vim.api.nvim_buf_get_lines(bufnr, 0, -1, false)
  local text = table.concat(lines, "\n")
  if vim.bo[bufnr].eol then
    text = text .. "\n"
  end

  send({
    type = "content",
    path = vim.api.nvim_buf_get_name(bufnr),
    text = text,
  })
  state.sent_tick = tick
end

local function send_cursor(force)
  local bufnr = state.bufnr
  if not bufnr or bufnr ~= vim.api.nvim_get_current_buf() then
    return
  end

  local line = vim.api.nvim_win_get_cursor(0)[1]
  if not force and line == state.sent_line then
    return
  end

  send({ type = "cursor", line = line })
  state.sent_line = line
end

-- Throttle z wywołaniem na końcu: pierwsze zdarzenie wysyła od razu, kolejne w oknie
-- throttle_ms są zwijane do jednej wysyłki po jego upływie. Podgląd aktualizuje się
-- w trakcie pisania, a ostatnia zmiana nigdy nie przepada.
local function throttled(fn)
  local timer = assert(vim.uv.new_timer())
  local running = false
  local pending = false

  local function run()
    fn()
    running = true
    timer:start(config.throttle_ms, 0, vim.schedule_wrap(function()
      running = false
      if pending then
        pending = false
        run()
      end
    end))
  end

  return function()
    if running then
      pending = true
    else
      run()
    end
  end
end

local throttled_content
local throttled_cursor

local function track_buffer(bufnr)
  if state.bufnr == bufnr then
    return
  end

  state.bufnr = bufnr
  state.sent_tick = nil
  state.sent_line = nil
  send_content(true)
  send_cursor(true)
end

local function on_stdout(_, data)
  -- data to linie; pierwsza dokleja się do niedokończonej z poprzedniego wywołania
  data[1] = state.stdout_partial .. data[1]
  state.stdout_partial = table.remove(data)

  for _, line in ipairs(data) do
    -- {"port":12345,"token":"..."}
    local ok, info = pcall(vim.json.decode, line)
    if ok and type(info) == "table" and info.port and info.token and not state.port then
      state.port = info.port
      state.token = info.token
      if config.open_browser then
        M.open()
      else
        -- Bez tokenu: komunikat zostaje w :messages
        notify("serving on " .. base_url())
      end
    end
  end
end

local function on_stderr(_, data)
  for _, line in ipairs(data) do
    if line ~= "" then
      table.insert(state.log, line)
    end
  end

  while #state.log > config.log_lines do
    table.remove(state.log, 1)
  end
end

local function clear_autocmds()
  if state.augroup then
    vim.api.nvim_del_augroup_by_id(state.augroup)
    state.augroup = nil
  end
end

local function on_exit(_, code)
  local was_running = state.job ~= nil
  state.job = nil
  state.port = nil
  state.token = nil
  state.stdout_partial = ""
  state.bufnr = nil
  state.cwd = nil
  clear_autocmds()

  if was_running and code ~= 0 and code ~= 143 then
    notify("server exited with code " .. code .. ", see :MdLiveLog", vim.log.levels.ERROR)
  end
end

-- Prawdziwe ścieżki katalogów z trusted_dirs; nieistniejące są pomijane z ostrzeżeniem,
-- bo ten sam config może trafić na maszynę, na której takiego katalogu nie ma
local function resolve_trusted_dirs(warn)
  local resolved, missing = {}, {}
  for _, dir in ipairs(config.trusted_dirs) do
    local real = vim.uv.fs_realpath(vim.fn.expand(dir))
    if real and vim.fn.isdirectory(real) == 1 then
      table.insert(resolved, real)
    else
      table.insert(missing, dir)
    end
  end
  if warn and #missing > 0 then
    notify("trusted_dirs: skipping missing " .. table.concat(missing, ", "), vim.log.levels.WARN)
  end
  return resolved
end

local function trust_args()
  local args = {}
  for _, dir in ipairs(resolve_trusted_dirs(true)) do
    vim.list_extend(args, { "--trust", dir })
  end
  return args
end

-- Katalog z trusted_dirs obejmujący path (prawdziwą ścieżkę), z granicą na separatorze:
-- ~/notes nie obejmuje ~/notes-old
local function trusted_dir_covering(path)
  for _, dir in ipairs(resolve_trusted_dirs(false)) do
    if path == dir or vim.startswith(path, dir:gsub("/$", "") .. "/") then
      return dir
    end
  end
end

local function create_autocmds()
  state.augroup = vim.api.nvim_create_augroup("MdLive", { clear = true })

  vim.api.nvim_create_autocmd({ "BufEnter", "FileType" }, {
    group = state.augroup,
    callback = function(args)
      if is_previewable(args.buf) then
        track_buffer(args.buf)
      end
    end,
  })

  vim.api.nvim_create_autocmd({ "TextChanged", "TextChangedI", "TextChangedP" }, {
    group = state.augroup,
    callback = function(args)
      if args.buf == state.bufnr then
        throttled_content()
      end
    end,
  })

  -- Po wyjściu z insertu podgląd ma od razu dogonić stan bufora
  vim.api.nvim_create_autocmd("InsertLeave", {
    group = state.augroup,
    callback = function(args)
      if args.buf == state.bufnr then
        send_content(false)
      end
    end,
  })

  vim.api.nvim_create_autocmd({ "CursorMoved", "CursorMovedI" }, {
    group = state.augroup,
    callback = function(args)
      if args.buf == state.bufnr then
        throttled_cursor()
      end
    end,
  })

  -- Zmiana szablonu, stylów albo skryptów: serwer czyta je przy każdym renderze,
  -- więc wystarczy wymusić ponowne wysłanie treści
  vim.api.nvim_create_autocmd("BufWritePost", {
    group = state.augroup,
    pattern = { "*/.mdlive/*" },
    callback = function()
      send_content(true)
    end,
  })
end

--- Adres podglądu z tokenem dostępu we fragmencie (#), który nie trafia do serwera ani logów
function M.url()
  local url = base_url()
  return url and (url .. "#" .. state.token) or nil
end

function M.is_running()
  return state.job ~= nil
end

function M.start()
  if state.job then
    notify("already running" .. (state.port and (" on " .. base_url()) or ""))
    return
  end

  local bufnr = vim.api.nvim_get_current_buf()
  if not is_previewable(bufnr) then
    notify("current buffer is not " .. table.concat(config.filetypes, "/"), vim.log.levels.WARN)
    return
  end

  if vim.fn.executable(config.cmd[1]) ~= 1 then
    notify("'" .. config.cmd[1] .. "' not found in PATH, install it with `dotnet tool install -g`",
      vim.log.levels.ERROR)
    return
  end

  state.log = {}
  state.cwd = vim.fn.getcwd()
  local cmd = vim.list_extend(vim.deepcopy(config.cmd), trust_args())
  local job = vim.fn.jobstart(cmd, {
    cwd = state.cwd,
    on_stdout = on_stdout,
    on_stderr = on_stderr,
    on_exit = on_exit,
  })

  if job <= 0 then
    notify("failed to start " .. table.concat(config.cmd, " "), vim.log.levels.ERROR)
    return
  end

  state.job = job
  create_autocmds()
  track_buffer(bufnr)
end

function M.stop()
  if not state.job then
    return
  end

  -- Zamknięcie stdin to sygnał dla serwera, żeby się zakończył
  vim.fn.chanclose(state.job, "stdin")
end

function M.open()
  local url = M.url()
  if not url then
    notify("server is not running", vim.log.levels.WARN)
    return
  end

  vim.ui.open(url)
end

-- Komenda mdlive bez "serve" i tego, co po nim, np. { "dotnet", "mdlive.dll" } z { "dotnet", "mdlive.dll", "serve" }
local function mdlive_base_cmd()
  for i, arg in ipairs(config.cmd) do
    if arg == "serve" then
      return vim.list_slice(config.cmd, 1, i - 1)
    end
  end
  return { config.cmd[1] }
end

local function run_mdlive(args, callback)
  local cmd = vim.list_extend(mdlive_base_cmd(), args)
  vim.system(cmd, { cwd = state.cwd or vim.fn.getcwd(), text = true }, vim.schedule_wrap(callback))
end

local function trimmed(text)
  return vim.trim(text or "")
end

--- Pokazuje pliki, z których składa się .mdlive/config.csx, z hashami i po potwierdzeniu ufa dokładnie
--- tej wersji. "Review" wrzuca je na quickfix. Zaufanie idzie przez --hash z pokazanej listy, więc jeśli
--- coś zmieni się między przejrzeniem a potwierdzeniem, nic nie zostanie zaufane.
function M.trust()
  run_mdlive({ "trust", "--show", "--json" }, function(result)
    if result.code ~= 0 then
      notify(trimmed(result.stderr), vim.log.levels.ERROR)
      return
    end

    local ok, info = pcall(vim.json.decode, result.stdout)
    if not ok or type(info) ~= "table" or not info.hash then
      notify("unexpected output of mdlive trust --show --json", vim.log.levels.ERROR)
      return
    end

    if #info.errors > 0 then
      notify("fix config.csx first:\n" .. table.concat(info.errors, "\n"), vim.log.levels.ERROR)
      return
    end

    if info.status == "trusted" then
      notify(info.directory .. " is already trusted")
      return
    end

    local covering = trusted_dir_covering(info.directory)
    if covering then
      notify(info.directory .. " is trusted by trusted_dirs (" .. covering .. "), nothing to do")
      return
    end

    if info.status == "storerejected" then
      notify(info.problem, vim.log.levels.ERROR)
      return
    end

    local lines = {
      info.status == "changed" and "config.csx changed since it was trusted." or "config.csx is not trusted.",
      "Trust and run this code with your permissions?",
      "",
      info.directory,
    }
    for _, file in ipairs(info.files) do
      table.insert(lines, string.format("  %-9s %s  %s", file.kind, file.sha256:sub(1, 12), file.path))
    end
    for _, name in ipairs(info.frameworkReferences) do
      table.insert(lines, "  framework " .. name)
    end

    local choice = vim.fn.confirm(table.concat(lines, "\n"), "&Trust\n&Review\n&Cancel", 3)

    if choice == 2 then
      local items = {}
      for _, file in ipairs(info.files) do
        table.insert(items, {
          filename = file.fullPath,
          lnum = 1,
          text = file.kind .. " sha256:" .. file.sha256 .. (file.kind == "reference" and " (binary)" or ""),
        })
      end
      vim.fn.setqflist({}, " ", { title = "MdLive: review before :MdLiveTrust", items = items })
      vim.cmd("copen")
      notify("review the files, then run :MdLiveTrust again")
      return
    end

    if choice ~= 1 then
      return
    end

    run_mdlive({ "trust", "--hash", info.hash }, function(trust_result)
      if trust_result.code == 0 then
        notify("trusted " .. info.directory)
        -- Przerenderuj, żeby podgląd od razu użył skryptu
        send_content(true)
      elseif trust_result.code == 2 then
        notify("config changed while you were reviewing it, nothing trusted. Run :MdLiveTrust again",
          vim.log.levels.WARN)
      else
        notify(trimmed(trust_result.stderr), vim.log.levels.ERROR)
      end
    end)
  end)
end

function M.show_log()
  vim.cmd("new")
  local bufnr = vim.api.nvim_get_current_buf()
  vim.bo[bufnr].buftype = "nofile"
  vim.bo[bufnr].bufhidden = "wipe"
  vim.api.nvim_buf_set_name(bufnr, "mdlive://log")
  vim.api.nvim_buf_set_lines(bufnr, 0, -1, false, state.log)
  vim.bo[bufnr].modifiable = false
end

function M.setup(opts)
  config = vim.tbl_deep_extend("force", config, opts or {})
end

throttled_content = throttled(function() send_content(false) end)
throttled_cursor = throttled(function() send_cursor(false) end)

return M
