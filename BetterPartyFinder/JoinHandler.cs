using System;
using Dalamud.Game.Gui.PartyFinder.Types;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

namespace BetterPartyFinder
{
    public class JoinHandler : IDisposable
    {
        private Plugin Plugin { get; }

        internal JoinHandler(Plugin plugin)
        {
            Plugin = plugin;
            Plugin.Common.Functions.PartyFinder.JoinParty += OnJoin;
        }

        public void Dispose()
        {
            Plugin.Common.Functions.PartyFinder.JoinParty -= OnJoin;
        }

        private void OnJoin(PartyFinderListing listing)
        {
            if (!Plugin.Config.ShowDescriptionOnJoin)
            {
                return;
            }

            SeString msg = "招募信息：";
            msg.Payloads.AddRange(listing.Description.Payloads);

            Plugin.ChatGui.PrintChat(new XivChatEntry
            {
                Name = "Better Party Finder",
                Type = XivChatType.SystemMessage,
                Message = msg,
            });
        }
    }
}