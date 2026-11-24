if vim.g.loaded_mdlive then
  return
end
vim.g.loaded_mdlive = true

-- Ta gałąź nie jest już aktualizowana, plugin żyje w korzeniu ScuroGuardiano/MdLive
local function moved_notice()
  vim.notify_once(
    "MdLive moved to ScuroGuardiano/MdLive, this branch (SGLibCS mdlive/nvim) isn't updated anymore. " ..
      "In lazy.nvim use { \"ScuroGuardiano/MdLive\" } instead of \"ScuroGuardiano/SGLibCS\", branch = \"mdlive/nvim\".",
    vim.log.levels.WARN
  )
end

vim.api.nvim_create_user_command("MdLiveStart", function() moved_notice(); require("mdlive").start() end,
  { desc = "Start MdLive preview for the current Markdown buffer" })
vim.api.nvim_create_user_command("MdLiveStop", function() require("mdlive").stop() end,
  { desc = "Stop MdLive preview server" })
vim.api.nvim_create_user_command("MdLiveOpen", function() require("mdlive").open() end,
  { desc = "Open MdLive preview in the browser" })
vim.api.nvim_create_user_command("MdLiveLog", function() require("mdlive").show_log() end,
  { desc = "Show MdLive server log" })
vim.api.nvim_create_user_command("MdLiveTrust", function() require("mdlive").trust() end,
  { desc = "Review and trust .mdlive/config.csx" })
