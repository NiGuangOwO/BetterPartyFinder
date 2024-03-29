using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using XivCommon;

namespace BetterPartyFinder
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class Plugin : IDalamudPlugin
    {
        public string Name => "Better Party Finder";

        [PluginService]
        internal DalamudPluginInterface Interface { get; init; } = null!;

        [PluginService]
        internal IChatGui ChatGui { get; init; } = null!;

        [PluginService]
        internal IClientState ClientState { get; init; } = null!;

        [PluginService]
        internal ICommandManager CommandManager { get; init; } = null!;

        [PluginService]
        internal IDataManager DataManager { get; init; } = null!;

        [PluginService]
        internal IGameGui GameGui { get; init; } = null!;


        [PluginService]
        internal IPartyFinderGui PartyFinderGui { get; init; } = null!;

        internal Configuration Config { get; }
        private Filter Filter { get; }
        internal PluginUi Ui { get; }
        private Commands Commands { get; }
        internal XivCommonBase Common { get; }

        public Plugin(
            DalamudPluginInterface pluginInterface,
            IChatGui chatGui,
            IClientState clientState,
            ICommandManager commandManager,
            IDataManager dataManager,
            IGameGui gameGui,
            IPartyFinderGui partyFinderGui
            )
        {
            Interface = pluginInterface;
            ChatGui = chatGui;
            ClientState = clientState;
            CommandManager = commandManager;
            DataManager = dataManager;
            GameGui = gameGui;
            PartyFinderGui = partyFinderGui;

            Config = Configuration.Load(this) ?? new Configuration();
            Config.Initialise(this);

            Common = new XivCommonBase(Interface, Hooks.PartyFinder);
            Filter = new Filter(this);
            Ui = new PluginUi(this);
            Commands = new Commands(this);

            // start task to determine maximum item level (based on max chestpiece)
            Util.CalculateMaxItemLevel(DataManager);
        }

        public void Dispose()
        {
            Commands.Dispose();
            Ui.Dispose();
            Filter.Dispose();
            Common.Dispose();
        }
    }
}