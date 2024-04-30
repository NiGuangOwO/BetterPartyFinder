using Dalamud.Game.Gui.PartyFinder.Types;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Linq;
using System.Numerics;
using Addon = Lumina.Excel.GeneratedSheets.Addon;

namespace BetterPartyFinder
{
    public class PluginUi : IDisposable
    {
        private static readonly uint[] AllowedContentTypes =
        [
            2,
            3,
            4,
            5,
            6,
            16,
            21,
            26,
            28,
            29,
            30
        ];

        private Plugin Plugin { get; }

        private bool _visible;

        public bool Visible
        {
            get => _visible;
            set => _visible = value;
        }

        private bool _settingsVisible;

        public bool SettingsVisible
        {
            get => _settingsVisible;
            set => _settingsVisible = value;
        }

        private string DutySearchQuery { get; set; } = string.Empty;

        private string PresetName { get; set; } = string.Empty;

        internal PluginUi(Plugin plugin)
        {
            Plugin = plugin;

            Plugin.Interface.UiBuilder.Draw += Draw;
            Plugin.Interface.UiBuilder.OpenConfigUi += OnOpenConfig;
            Plugin.Interface.UiBuilder.OpenMainUi += OnOpenConfig;
        }

        public void Dispose()
        {
            Plugin.Interface.UiBuilder.Draw -= Draw;
            Plugin.Interface.UiBuilder.OpenConfigUi -= OnOpenConfig;
            Plugin.Interface.UiBuilder.OpenMainUi -= OnOpenConfig;
        }

        private void OnOpenConfig()
        {
            Visible = !Visible;
        }

        private static bool IconButton(FontAwesomeIcon icon, string? id = null)
        {
            ImGui.PushFont(UiBuilder.IconFont);

            var text = icon.ToIconString();
            if (id != null)
            {
                text += $"##{id}";
            }

            var result = ImGui.Button(text);

            ImGui.PopFont();

            return result;
        }

        private IntPtr PartyFinderAddon()
        {
            return Plugin.GameGui.GetAddonByName("LookingForGroup", 1);
        }

        private void Draw()
        {
            DrawFiltersWindow();
            DrawSettingsWindow();
        }

        private void DrawSettingsWindow()
        {
            ImGui.SetNextWindowSize(new Vector2(-1f, -1f), ImGuiCond.FirstUseEver);

            if (!SettingsVisible || !ImGui.Begin($"{Plugin.Name} 设置", ref _settingsVisible))
            {
                return;
            }

            var openWithPf = Plugin.Config.ShowWhenPfOpen;
            if (ImGui.Checkbox("随招募板打开", ref openWithPf))
            {
                Plugin.Config.ShowWhenPfOpen = openWithPf;
                Plugin.Config.Save();
            }

            var sideOptions = new[]
            {
                "左侧",
                "右侧",
            };
            var sideIdx = Plugin.Config.WindowSide == WindowSide.Left ? 0 : 1;

            ImGui.TextUnformatted("吸附在招募板的");
            if (ImGui.Combo("###window-side", ref sideIdx, sideOptions, sideOptions.Length))
            {
                Plugin.Config.WindowSide = sideIdx switch
                {
                    0 => WindowSide.Left,
                    1 => WindowSide.Right,
                    _ => Plugin.Config.WindowSide,
                };

                Plugin.Config.Save();
            }

            ImGui.End();
        }

        private unsafe void DrawFiltersWindow()
        {
            AtkUnitBase* addon = null;
            var addonPtr = PartyFinderAddon();
            if (Plugin.Config.ShowWhenPfOpen && addonPtr != IntPtr.Zero)
            {
                addon = (AtkUnitBase*)addonPtr;
            }

            var showWindow = Visible || addon != null && addon->IsVisible;

            if (!showWindow)
            {
                return;
            }
            ImGui.SetNextWindowSize(new Vector2(550f, 510f), ImGuiCond.FirstUseEver);
            if (!ImGui.Begin(Plugin.Name, ref _visible, ImGuiWindowFlags.NoDocking))
            {
                if (ImGui.IsWindowCollapsed() && addon != null && addon->IsVisible)
                {
                    // wait until addon is initialised to show
                    var rootNode = addon->RootNode;
                    if (rootNode == null)
                    {
                        return;
                    }
                    ImGui.SetWindowPos(ImGuiHelpers.MainViewport.Pos +
                                       new Vector2(addon->X, addon->Y - ImGui.GetFrameHeight()));
                }

                ImGui.End();
                return;
            }

            if (addon != null && Plugin.Config.WindowSide == WindowSide.Right)
            {
                var rootNode = addon->RootNode;
                if (rootNode != null)
                {
                    ImGui.SetWindowPos(
                        ImGuiHelpers.MainViewport.Pos + new Vector2(addon->X + rootNode->Width, addon->Y));
                }
            }

            var selected = Plugin.Config.SelectedPreset;

            string selectedName;
            if (selected == null)
            {
                selectedName = "无";
            }
            else
            {
                if (Plugin.Config.Presets.TryGetValue(selected.Value, out var preset))
                {
                    selectedName = preset.Name;
                }
                else
                {
                    Plugin.Config.SelectedPreset = null;
                    selectedName = "<无效的预设>";
                }
            }

            ImGui.TextUnformatted("预设");
            if (ImGui.BeginCombo("###preset", selectedName))
            {
                if (ImGui.Selectable("<无>"))
                {
                    Plugin.Config.SelectedPreset = null;
                    Plugin.Config.Save();

                    Plugin.Common.Functions.PartyFinder.RefreshListings();
                }

                foreach (var preset in Plugin.Config.Presets)
                {
                    if (!ImGui.Selectable(preset.Value.Name))
                    {
                        continue;
                    }

                    Plugin.Config.SelectedPreset = preset.Key;
                    Plugin.Config.Save();

                    Plugin.Common.Functions.PartyFinder.RefreshListings();
                }

                ImGui.EndCombo();
            }

            ImGui.SameLine();

            if (IconButton(FontAwesomeIcon.Plus, "add-preset"))
            {
                var id = Guid.NewGuid();

                Plugin.Config.Presets.Add(id, ConfigurationFilter.Create());
                Plugin.Config.SelectedPreset = id;
                Plugin.Config.Save();
            }

            ImGui.SameLine();

            if (IconButton(FontAwesomeIcon.Trash, "delete-preset") && selected != null)
            {
                Plugin.Config.Presets.Remove(selected.Value);
                Plugin.Config.Save();
            }

            ImGui.SameLine();

            if (IconButton(FontAwesomeIcon.PencilAlt, "edit-preset") && selected != null)
            {
                if (Plugin.Config.Presets.TryGetValue(selected.Value, out var editPreset))
                {
                    PresetName = editPreset.Name;

                    ImGui.OpenPopup("###rename-preset");
                }
            }

            if (ImGui.BeginPopupModal("重命名预设###rename-preset"))
            {
                if (selected != null && Plugin.Config.Presets.TryGetValue(selected.Value, out var editPreset))
                {
                    ImGui.TextUnformatted("预设名");
                    ImGui.PushItemWidth(-1f);
                    var name = PresetName;
                    if (ImGui.InputText("###preset-name", ref name, 1_000))
                    {
                        PresetName = name;
                    }

                    ImGui.PopItemWidth();

                    if (ImGui.Button("保存") && PresetName.Trim().Length > 0)
                    {
                        editPreset.Name = PresetName;
                        Plugin.Config.Save();
                        ImGui.CloseCurrentPopup();
                    }

                    ImGui.EndPopup();
                }
            }

            ImGui.SameLine();

            if (IconButton(FontAwesomeIcon.Copy, "copy") && selected != null)
            {
                if (Plugin.Config.Presets.TryGetValue(selected.Value, out var copyFilter))
                {
                    var guid = Guid.NewGuid();

                    var copied = copyFilter.Clone();
                    copied.Name += " (复制)";
                    Plugin.Config.Presets.Add(guid, copied);
                    Plugin.Config.SelectedPreset = guid;
                    Plugin.Config.Save();
                }
            }

            ImGui.SameLine();

            if (IconButton(FontAwesomeIcon.Cog, "settings"))
            {
                SettingsVisible = true;
            }

            ImGui.Separator();

            if (selected != null && Plugin.Config.Presets.TryGetValue(selected.Value, out var filter))
            {
                DrawPresetConfiguration(filter);
            }

            if (addon != null && Plugin.Config.WindowSide == WindowSide.Left)
            {
                var rootNode = addon->RootNode;
                if (rootNode != null)
                {
                    var currentWidth = ImGui.GetWindowWidth();
                    ImGui.SetWindowPos(ImGuiHelpers.MainViewport.Pos + new Vector2(addon->X - currentWidth, addon->Y));
                }
            }

            ImGui.End();
        }

        private void DrawPresetConfiguration(ConfigurationFilter filter)
        {
            if (!ImGui.BeginTabBar("bpf-tabs"))
            {
                return;
            }

            DrawCategoriesTab(filter);

            DrawDutiesTab(filter);

            DrawItemLevelTab(filter);

            DrawJobsLimitTab(filter);

            DrawRestrictionsTab(filter);

            DrawPlayersTab(filter);
            DrawDescription(filter);
            DrawLikeDescription(filter);
            DrawDescriptionExclude(filter);
            ImGui.EndTabBar();
        }

        private void DrawCategoriesTab(ConfigurationFilter filter)
        {
            if (!ImGui.BeginTabItem("分类"))
            {
                return;
            }

            foreach (var category in (UiCategory[])Enum.GetValues(typeof(UiCategory)))
            {
                var selected = filter.Categories.Contains(category);
                if (!ImGui.Selectable(category.Name(Plugin.DataManager), ref selected))
                {
                    continue;
                }

                if (selected)
                {
                    filter.Categories.Add(category);
                }
                else
                {
                    filter.Categories.Remove(category);
                }

                Plugin.Config.Save();
            }


            ImGui.EndTabItem();
        }

        private void DrawDutiesTab(ConfigurationFilter filter)
        {
            if (!ImGui.BeginTabItem("任务"))
            {
                return;
            }

            var listModeStrings = new[]
            {
                "仅显示选中的任务",
                "不显示选中的任务",
            };
            var listModeIdx = filter.DutiesMode == ListMode.Blacklist ? 1 : 0;
            ImGui.TextUnformatted("列表模式");
            ImGui.PushItemWidth(-1);
            if (ImGui.Combo("###list-mode", ref listModeIdx, listModeStrings, listModeStrings.Length))
            {
                filter.DutiesMode = listModeIdx == 0 ? ListMode.Whitelist : ListMode.Blacklist;
                Plugin.Config.Save();
            }

            ImGui.PopItemWidth();

            var query = DutySearchQuery;
            ImGui.TextUnformatted("搜索");
            if (ImGui.InputText("###search", ref query, 1_000))
            {
                DutySearchQuery = query;
            }

            ImGui.SameLine();
            if (ImGui.Button("清除列表"))
            {
                filter.Duties.Clear();
                Plugin.Config.Save();
            }

            if (ImGui.BeginChild("duty-selection", new Vector2(-1f, -1f)))
            {
                var duties = Plugin.DataManager.GetExcelSheet<ContentFinderCondition>()!
                    .Where(cf => cf.Unknown29)
                    .Where(cf => AllowedContentTypes.Contains(cf.ContentType.Row));

                var searchQuery = DutySearchQuery.Trim();
                if (searchQuery.Trim() != "")
                {
                    duties = duties.Where(duty =>
                    {
                        var sestring = (SeString)duty.Name;
                        return sestring.TextValue.ContainsIgnoreCase(searchQuery);
                    });
                }

                foreach (var cf in duties)
                {
                    var sestring = (SeString)cf.Name;
                    var selected = filter.Duties.Contains(cf.RowId);
                    var name = sestring.TextValue;
                    name = char.ToUpperInvariant(name[0]) + name[1..];
                    if (!ImGui.Selectable(name, ref selected))
                    {
                        continue;
                    }

                    if (selected)
                    {
                        filter.Duties.Add(cf.RowId);
                    }
                    else
                    {
                        filter.Duties.Remove(cf.RowId);
                    }

                    Plugin.Config.Save();
                }

                ImGui.EndChild();
            }

            ImGui.EndTabItem();
        }

        private void DrawItemLevelTab(ConfigurationFilter filter)
        {
            if (!ImGui.BeginTabItem("品级"))
            {
                return;
            }

            var hugePfs = filter.AllowHugeItemLevel;
            if (ImGui.Checkbox("显示高于品级的招募", ref hugePfs))
            {
                filter.AllowHugeItemLevel = hugePfs;
                Plugin.Config.Save();
            }

            var minLevel = (int?)filter.MinItemLevel ?? 0;
            ImGui.TextUnformatted("最低品级（设置0为禁用）");
            ImGui.PushItemWidth(-1);
            if (ImGui.InputInt("###min-ilvl", ref minLevel))
            {
                filter.MinItemLevel = minLevel == 0 ? null : (uint)minLevel;
                Plugin.Config.Save();
            }

            ImGui.PopItemWidth();

            var maxLevel = (int?)filter.MaxItemLevel ?? 0;
            ImGui.TextUnformatted("最高品级（设置0为禁用）");
            ImGui.PushItemWidth(-1);
            if (ImGui.InputInt("###max-ilvl", ref maxLevel))
            {
                filter.MaxItemLevel = maxLevel == 0 ? null : (uint)maxLevel;
                Plugin.Config.Save();
            }

            ImGui.PopItemWidth();

            ImGui.EndTabItem();
        }

        private void DrawJobsLimitTab(ConfigurationFilter filter)
        {
            if (!ImGui.BeginTabItem("职业限制"))
            {
                return;
            }
            ImGui.TextWrapped("如果某个招募中已经加入了下方勾选的职业之一，则该招募将不会显示。\n举个栗子：勾选诗人、舞者、机工后，最终筛选出的招募是队伍里没有远敏的，无需再点开招募信息查看坑位。");
            ImGui.Separator();
            if (filter.JobsLimit.Count == 0)
            {
                filter.JobsLimit.Add(0);
            }

            var slot = filter.JobsLimit[0];


            if (ImGui.Button("全选"))
            {
                filter.JobsLimit[0] = Enum.GetValues(typeof(JobFlags))
                    .Cast<JobFlags>()
                    .Aggregate(slot, (current, job) => current | job);
                Plugin.Config.Save();
            }

            ImGui.SameLine();

            if (ImGui.Button("清除"))
            {
                filter.JobsLimit[0] = 0;
                Plugin.Config.Save();
            }


            foreach (var job in (JobFlags[])Enum.GetValues(typeof(JobFlags)))
            {
                var selected = (slot & job) > 0;
                if (!ImGui.Selectable(job.ClassJob(Plugin.DataManager)?.Name ?? "???", ref selected))
                {
                    continue;
                }

                if (selected)
                {
                    slot |= job;
                }
                else
                {
                    slot &= ~job;
                }

                filter.JobsLimit[0] = slot;

                Plugin.Config.Save();
            }


            ImGui.EndTabItem();
        }

        private void DrawRestrictionsTab(ConfigurationFilter filter)
        {
            if (!ImGui.BeginTabItem("限制"))
            {
                return;
            }

            var practice = filter[ObjectiveFlags.Practice];
            if (ImGui.Checkbox("练习", ref practice))
            {
                filter[ObjectiveFlags.Practice] = practice;
                Plugin.Config.Save();
            }

            var dutyCompletion = filter[ObjectiveFlags.DutyCompletion];
            if (ImGui.Checkbox("完成任务", ref dutyCompletion))
            {
                filter[ObjectiveFlags.DutyCompletion] = dutyCompletion;
                Plugin.Config.Save();
            }

            var loot = filter[ObjectiveFlags.Loot];
            if (ImGui.Checkbox("反复攻略", ref loot))
            {
                filter[ObjectiveFlags.Loot] = loot;
                Plugin.Config.Save();
            }

            ImGui.Separator();

            var noCondition = filter[ConditionFlags.None];
            if (ImGui.Checkbox("无任务完成要求", ref noCondition))
            {
                filter[ConditionFlags.None] = noCondition;
                Plugin.Config.Save();
            }

            var dutyIncomplete = filter[ConditionFlags.DutyIncomplete];
            if (ImGui.Checkbox("任务未完成", ref dutyIncomplete))
            {
                filter[ConditionFlags.DutyIncomplete] = dutyIncomplete;
                Plugin.Config.Save();
            }

            var dutyComplete = filter[ConditionFlags.DutyComplete];
            if (ImGui.Checkbox("任务已完成", ref dutyComplete))
            {
                filter[ConditionFlags.DutyComplete] = dutyComplete;
                Plugin.Config.Save();
            }

            ImGui.Separator();

            var undersized = filter[DutyFinderSettingsFlags.UndersizedParty];
            if (ImGui.Checkbox("人数小于正常的小队", ref undersized))
            {
                filter[DutyFinderSettingsFlags.UndersizedParty] = undersized;
                Plugin.Config.Save();
            }

            var minItemLevel = filter[DutyFinderSettingsFlags.MinimumItemLevel];
            if (ImGui.Checkbox("最低品级", ref minItemLevel))
            {
                filter[DutyFinderSettingsFlags.MinimumItemLevel] = minItemLevel;
                Plugin.Config.Save();
            }

            var silenceEcho = filter[DutyFinderSettingsFlags.SilenceEcho];
            if (ImGui.Checkbox("超越之力无效化", ref silenceEcho))
            {
                filter[DutyFinderSettingsFlags.SilenceEcho] = silenceEcho;
                Plugin.Config.Save();
            }

            ImGui.Separator();

            var greedOnly = filter[LootRuleFlags.GreedOnly];
            if (ImGui.Checkbox("仅限贪婪", ref greedOnly))
            {
                filter[LootRuleFlags.GreedOnly] = greedOnly;
                Plugin.Config.Save();
            }

            var lootmaster = filter[LootRuleFlags.Lootmaster];
            if (ImGui.Checkbox("队长分配", ref lootmaster))
            {
                filter[LootRuleFlags.Lootmaster] = lootmaster;
                Plugin.Config.Save();
            }

            ImGui.Separator();

            var dataCentre = filter[SearchAreaFlags.DataCentre];
            if (ImGui.Checkbox("跨服队伍", ref dataCentre))
            {
                filter[SearchAreaFlags.DataCentre] = dataCentre;
                Plugin.Config.Save();
            }

            var world = filter[SearchAreaFlags.World];
            if (ImGui.Checkbox("本地队伍", ref world))
            {
                filter[SearchAreaFlags.World] = world;
                Plugin.Config.Save();
            }

            var onePlayerPer = filter[SearchAreaFlags.OnePlayerPerJob];
            if (ImGui.Checkbox("职业不重复", ref onePlayerPer))
            {
                filter[SearchAreaFlags.OnePlayerPerJob] = onePlayerPer;
                Plugin.Config.Save();
            }

            ImGui.EndTabItem();
        }

        private int _selectedWorld;
        private string _playerName = string.Empty;

        private void DrawPlayersTab(ConfigurationFilter filter)
        {
            var player = Plugin.ClientState.LocalPlayer;

            if (player == null || !ImGui.BeginTabItem("玩家"))
            {
                return;
            }

            ImGui.PushItemWidth(ImGui.GetWindowWidth() / 3f);

            ImGui.InputText("###player-name", ref _playerName, 64);

            ImGui.SameLine();

            var worlds = Util.WorldsOnDataCentre(Plugin.DataManager, player)
                .OrderBy(world => world.Name.RawString)
                .ToList();

            var worldNames = worlds
                .Select(world => world.Name.ToString())
                .ToArray();

            if (ImGui.Combo("###player-world", ref _selectedWorld, worldNames, worldNames.Length))
            {
            }

            ImGui.PopItemWidth();

            ImGui.SameLine();

            if (IconButton(FontAwesomeIcon.Plus, "add-player"))
            {
                var name = _playerName.Trim();
                if (name.Length != 0)
                {
                    var world = worlds[_selectedWorld];
                    filter.Players.Add(new PlayerInfo(name, world.RowId));
                    Plugin.Config.Save();
                }
            }

            PlayerInfo? deleting = null;

            foreach (var info in filter.Players)
            {
                var world = Plugin.DataManager.GetExcelSheet<World>()!.GetRow(info.World);
                ImGui.TextUnformatted($"{info.Name}@{world?.Name}");
                ImGui.SameLine();
                if (IconButton(FontAwesomeIcon.Trash, $"delete-player-{info.GetHashCode()}"))
                {
                    deleting = info;
                }
            }

            if (deleting != null)
            {
                filter.Players.Remove(deleting);
                Plugin.Config.Save();
            }

            ImGui.EndTabItem();
        }
        private string _description = string.Empty;
        private void DrawDescription(ConfigurationFilter filter)
        {

            var player = Plugin.ClientState.LocalPlayer;

            if (player == null || !ImGui.BeginTabItem("留言"))
            {
                return;
            }
            ImGui.TextWrapped("仅显示包含已添加关键词的招募");
            ImGui.PushItemWidth(ImGui.GetWindowWidth() / 3f);

            ImGui.InputText("###Description", ref _description, 64);

            ImGui.SameLine();


            ImGui.PopItemWidth();

            ImGui.SameLine();

            if (IconButton(FontAwesomeIcon.Plus, "add-description"))
            {
                var des = _description.Trim();
                if (des.Length != 0)
                {
                    //var world = worlds[_selectedWorld];
                    filter.Description.Add(des.ToLower());
                    Plugin.Config.Save();
                }
            }

            string? deleting = null;

            foreach (var info in filter.Description)
            {
                //var world = Plugin.DataManager.GetExcelSheet<World>()!.GetRow(info.World);
                ImGui.TextUnformatted(info);
                ImGui.SameLine();
                if (IconButton(FontAwesomeIcon.Trash, $"delete-{info.GetHashCode()}"))
                {
                    deleting = info;
                }
            }

            if (deleting != null)
            {
                filter.Description.Remove(deleting);
                Plugin.Config.Save();
            }

            ImGui.EndTabItem();
        }

        private string _descriptionLike = string.Empty;
        private void DrawLikeDescription(ConfigurationFilter filter)
        {

            var player = Plugin.ClientState.LocalPlayer;

            if (player == null || !ImGui.BeginTabItem("留言 (特别关心)"))
            {
                return;
            }
            ImGui.TextWrapped("关键词特别关心");
            ImGui.PushItemWidth(ImGui.GetWindowWidth() / 3f);

            ImGui.InputText("###DescriptionLike", ref _descriptionLike, 64);

            ImGui.SameLine();


            ImGui.PopItemWidth();

            ImGui.SameLine();

            if (IconButton(FontAwesomeIcon.Plus, "add-description"))
            {
                var des = _descriptionLike.Trim();
                if (des.Length != 0)
                {
                    //var world = worlds[_selectedWorld];
                    filter.DescriptionLike.Add(des.ToLower());
                    Plugin.Config.Save();
                }
            }

            string? deleting = null;

            foreach (var info in filter.DescriptionLike)
            {
                //var world = Plugin.DataManager.GetExcelSheet<World>()!.GetRow(info.World);
                ImGui.TextUnformatted(info);
                ImGui.SameLine();
                if (IconButton(FontAwesomeIcon.Trash, $"delete-{info.GetHashCode()}"))
                {
                    deleting = info;
                }
            }

            if (deleting != null)
            {
                filter.DescriptionLike.Remove(deleting);
                Plugin.Config.Save();
            }

            ImGui.EndTabItem();
        }

        private string _descriptionExclude = string.Empty;
        private void DrawDescriptionExclude(ConfigurationFilter filter)
        {

            var player = Plugin.ClientState.LocalPlayer;

            if (player == null || !ImGui.BeginTabItem("留言屏蔽"))
            {
                return;
            }

            ImGui.PushItemWidth(ImGui.GetWindowWidth() / 3f);

            ImGui.InputText("###DescriptionExclude", ref _descriptionExclude, 64);

            ImGui.SameLine();


            ImGui.PopItemWidth();

            ImGui.SameLine();

            if (IconButton(FontAwesomeIcon.Plus, "add-description"))
            {
                var des = _descriptionExclude.Trim();
                if (des.Length != 0)
                {
                    //var world = worlds[_selectedWorld];
                    filter.DescriptionExclude.Add(des.ToLower());
                    Plugin.Config.Save();
                }
            }

            string? deleting = null;

            foreach (var info in filter.DescriptionExclude)
            {
                //var world = Plugin.DataManager.GetExcelSheet<World>()!.GetRow(info.World);
                ImGui.TextUnformatted(info);
                ImGui.SameLine();
                if (IconButton(FontAwesomeIcon.Trash, $"delete-{info.GetHashCode()}"))
                {
                    deleting = info;
                }
            }

            if (deleting != null)
            {
                filter.DescriptionExclude.Remove(deleting);
                Plugin.Config.Save();
            }

            ImGui.EndTabItem();
        }
    }

    public enum UiCategory
    {
        None,
        DutyRoulette,
        Dungeons,
        Guildhests,
        Trials,
        Raids,
        HighEndDuty,
        Pvp,
        QuestBattles,
        Fates,
        TreasureHunt,
        TheHunt,
        GatheringForays,
        DeepDungeons,
        AdventuringForays,
        VariantAndCriterionDungeons
    }

    internal static class UiCategoryExt
    {
        internal static string? Name(this UiCategory category, IDataManager data)
        {
            var ct = data.GetExcelSheet<ContentType>()!;
            var addon = data.GetExcelSheet<Addon>()!;

            return category switch
            {
                UiCategory.None => addon.GetRow(1_562)?.Text.ToString(), // best guess
                UiCategory.DutyRoulette => ct.GetRow((uint)ContentType2.DutyRoulette)?.Name.ToString(),
                UiCategory.Dungeons => ct.GetRow((uint)ContentType2.Dungeons)?.Name.ToString(),
                UiCategory.Guildhests => ct.GetRow((uint)ContentType2.Guildhests)?.Name.ToString(),
                UiCategory.Trials => ct.GetRow((uint)ContentType2.Trials)?.Name.ToString(),
                UiCategory.Raids => ct.GetRow((uint)ContentType2.Raids)?.Name.ToString(),
                UiCategory.HighEndDuty => addon.GetRow(10_822)?.Text.ToString(), // best guess
                UiCategory.Pvp => ct.GetRow((uint)ContentType2.Pvp)?.Name.ToString(),
                UiCategory.QuestBattles => ct.GetRow((uint)ContentType2.QuestBattles)?.Name.ToString(),
                UiCategory.Fates => ct.GetRow((uint)ContentType2.Fates)?.Name.ToString(),
                UiCategory.TreasureHunt => ct.GetRow((uint)ContentType2.TreasureHunt)?.Name.ToString(),
                UiCategory.TheHunt => addon.GetRow(8_613)?.Text.ToString(),
                UiCategory.GatheringForays => addon.GetRow(2_306)?.Text.ToString(),
                UiCategory.DeepDungeons => ct.GetRow((uint)ContentType2.DeepDungeons)?.Name.ToString(),
                UiCategory.AdventuringForays => addon.GetRow(2_307)?.Text.ToString(),
                UiCategory.VariantAndCriterionDungeons => ct.GetRow((uint)ContentType2.VariantAndCriterionDungeons)?.Name.ToString(),
                _ => null,
            };
        }

        internal static bool ListingMatches(this UiCategory category, IDataManager data, PartyFinderListing listing)
        {
            var cr = data.GetExcelSheet<ContentRoulette>()!;

            var isDuty = listing.Category == DutyCategory.Duty;
            var isNormal = listing.DutyType == DutyType.Normal;
            var isOther = listing.DutyType == DutyType.Other;
            var isNormalDuty = isNormal && isDuty;

            return category switch
            {
                UiCategory.None => isOther && isDuty && listing.RawDuty == 0,
                UiCategory.DutyRoulette => listing.DutyType == DutyType.Roulette && isDuty &&
                                           (!cr.GetRow(listing.RawDuty)?.IsPvP ?? false),
                UiCategory.Dungeons => isNormalDuty &&
                                       listing.Duty.Value.ContentType.Row == (uint)ContentType2.Dungeons,
                UiCategory.Guildhests => isNormalDuty &&
                                         listing.Duty.Value.ContentType.Row == (uint)ContentType2.Guildhests,
                UiCategory.Trials => isNormalDuty && !listing.Duty.Value.HighEndDuty &&
                                     listing.Duty.Value.ContentType.Row == (uint)ContentType2.Trials,
                UiCategory.Raids => isNormalDuty && !listing.Duty.Value.HighEndDuty &&
                                    listing.Duty.Value.ContentType.Row == (uint)ContentType2.Raids,
                UiCategory.HighEndDuty => isNormalDuty && listing.Duty.Value.HighEndDuty,
                UiCategory.Pvp => listing.DutyType == DutyType.Roulette && isDuty &&
                                  (cr.GetRow(listing.RawDuty)?.IsPvP ?? false)
                                  || isNormalDuty && listing.Duty.Value.ContentType.Row == (uint)ContentType2.Pvp,
                UiCategory.QuestBattles => isOther && listing.Category == DutyCategory.QuestBattles,
                UiCategory.Fates => isOther && listing.Category == DutyCategory.Fates,
                UiCategory.TreasureHunt => isOther && listing.Category == DutyCategory.TreasureHunt,
                UiCategory.TheHunt => isOther && listing.Category == DutyCategory.TheHunt,
                UiCategory.GatheringForays => isNormal && listing.Category == DutyCategory.GatheringForays,
                UiCategory.DeepDungeons => isOther && listing.Category == DutyCategory.DeepDungeons,
                UiCategory.AdventuringForays => isNormal && listing.Category == DutyCategory.AdventuringForays,
                UiCategory.VariantAndCriterionDungeons => isNormal && (int)listing.Category == 128,
                _ => false,
            };
        }

        private enum ContentType2
        {
            DutyRoulette = 1,
            Dungeons = 2,
            Guildhests = 3,
            Trials = 4,
            Raids = 5,
            Pvp = 6,
            QuestBattles = 7,
            Fates = 8,
            TreasureHunt = 9,
            Levequests = 10,
            GrandCompany = 11,
            Companions = 12,
            BeastTribeQuests = 13,
            OverallCompletion = 14,
            PlayerCommendation = 15,
            DisciplesOfTheLand = 16,
            DisciplesOfTheHand = 17,
            RetainerVentures = 18,
            GoldSaucer = 19,
            DeepDungeons = 21,
            WondrousTails = 24,
            CustomDeliveries = 25,
            Eureka = 26,
            UltimateRaids = 28,
            VariantAndCriterionDungeons = 30
        }
    }
}