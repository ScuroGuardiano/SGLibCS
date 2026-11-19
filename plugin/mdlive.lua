if vim.g.loaded_mdlive then
  return
end
vim.g.loaded_mdlive = true

vim.api.nvim_create_user_command("MdLiveStart", function() require("mdlive").start() end,
  { desc = "Start MdLive preview for the current Markdown buffer" })
vim.api.nvim_create_user_command("MdLiveStop", function() require("mdlive").stop() end,
  { desc = "Stop MdLive preview server" })
vim.api.nvim_create_user_command("MdLiveOpen", function() require("mdlive").open() end,
  { desc = "Open MdLive preview in the browser" })
vim.api.nvim_create_user_command("MdLiveLog", function() require("mdlive").show_log() end,
  { desc = "Show MdLive server log" })
