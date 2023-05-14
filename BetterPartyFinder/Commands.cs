using System;
using System.Collections.Generic;
using Dalamud.Game.Command;

namespace BetterPartyFinder {
    public class Commands : IDisposable {
        private static readonly Dictionary<string, string> CommandNames = new() {
            ["/betterpartyfinder"] = "打开主界面。与指令“c”或“config”一起使用可以打开设置。",
            ["/bpf"] = "/betterpartyfinder 的别名",
        };

        private Plugin Plugin { get; }

        internal Commands(Plugin plugin) {
            Plugin = plugin;

            foreach (var (name, help) in CommandNames) {
                Plugin.CommandManager.AddHandler(name, new CommandInfo(OnCommand) {
                    HelpMessage = help,
                });
            }
        }

        public void Dispose() {
            foreach (var name in CommandNames.Keys) {
                Plugin.CommandManager.RemoveHandler(name);
            }
        }

        private void OnCommand(string command, string args) {
            if (args is "c" or "config") {
                Plugin.Ui.SettingsVisible = !Plugin.Ui.SettingsVisible;
            } else {
                Plugin.Ui.Visible = !Plugin.Ui.Visible;
            }
        }
    }
}
