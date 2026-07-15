using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using CheapLoc;
using Dalamud.Interface.Colors;
using Dalamud.Plugin.Services;
using Kapture.Kapture.Extensions;
using Newtonsoft.Json;

// ReSharper disable PatternIsRedundant
// ReSharper disable MergeIntoPattern
namespace Kapture
{
    /// <summary>
    /// Roll monitor service.
    /// </summary>
    public class RollMonitor
    {
        /// <summary>
        /// Queue of loot events.
        /// </summary>
        public readonly ConcurrentQueue<LootEvent> LootEvents = new ();

        private readonly IKapturePlugin plugin;
        private bool isProcessing;
        private long lastProcessTime;

        /// <summary>
        /// Initializes a new instance of the <see cref="RollMonitor"/> class.
        /// </summary>
        /// <param name="plugin">kapture plugin.</param>
        public RollMonitor(IKapturePlugin plugin)
        {
            this.plugin = plugin;

            // Run on the framework (main) thread instead of a background timer:
            // roll processing reads main-thread-only game state (ObjectTable.LocalPlayer,
            // PartyList, Condition) which throws "Not on main thread!" under Dalamud API 15.
            // Framework is null under the unit-test harness (no Dalamud services injected).
            var framework = KapturePlugin.Framework;
            if (framework is not null) framework.Update += this.OnFrameworkUpdate;
        }

        /// <summary>
        /// Dispose service.
        /// </summary>
        public void Dispose()
        {
            var framework = KapturePlugin.Framework;
            if (framework is not null) framework.Update -= this.OnFrameworkUpdate;
        }

        /// <summary>
        /// Update queued rolls.
        /// </summary>
        public void UpdateRolls()
        {
            try
            {
                if (this.plugin.LootRolls.Count == 0) return;
                var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                this.plugin.LootRolls.RemoveAll(roll => !roll.IsWon &&
                                                    currentTime - roll.Timestamp >
                                                    this.plugin.Configuration.RollMonitorAddedTimeout);
                this.plugin.LootRolls.RemoveAll(roll => roll.IsWon &&
                                                    currentTime - roll.Timestamp >
                                                    this.plugin.Configuration.RollMonitorObtainedTimeout);
                this.plugin.IsRolling = this.plugin.LootRolls.Count > 0;
                this.CreateDisplayList();
                this.SendRollReminder();
            }
            catch (Exception ex)
            {
                KapturePlugin.PluginLog.Error(ex, "Failed to remove old rolls.");
            }
        }

        /// <summary>
        /// Process new roll.
        /// </summary>
        /// <param name="lootEvent">loot event.</param>
        /// <exception cref="ArgumentOutOfRangeException">unrecognized loot event type.</exception>
        public void ProcessRoll(LootEvent lootEvent)
        {
            try
            {
                if (lootEvent.ContentId == 0) return;
                LootRoll? lootRoll;
                switch (lootEvent.LootEventType)
                {
                    case LootEventType.Add:
                        lootRoll = new LootRoll
                        {
                            Timestamp = lootEvent.Timestamp,
                            ItemId = lootEvent.LootMessage.ItemId,
                            ItemName = lootEvent.ItemName,
                            ItemNameAbbreviated = lootEvent.ItemNameAbbreviated,
                        };
                        this.plugin.LootRolls.Add(lootRoll);

                        foreach (var player in this.plugin.GetPartyMembers())
                        {
                            lootRoll.Rollers.Add(new LootRoller
                            {
                                PlayerName = this.plugin.FormatPlayerName(
                                    this.plugin.Configuration.ChatNameFormat,
                                    player.Name.ToString()),
                                RollColor = GetColorByNumber(0),
                                IsLocalPlayer = player.ObjectId == KapturePlugin.ObjectTable.LocalPlayer?.EntityId,
                            });
                        }

                        if (this.plugin.Configuration.WatchListItems.Contains(lootEvent.LootMessage.ItemId))
                        {
                            KapturePlugin.Chat.PluginPrintNotice(string.Format(
                             Loc.Localize("WatchListAddedAlert", "{0} dropped and is on your watch list."),
                             lootEvent.LootMessage.ItemName));
                        }

                        break;
                    case LootEventType.Cast:
                    {
                        lootRoll = this.plugin.LootRolls.FirstOrDefault(roll =>
                            roll.ItemId == lootEvent.LootMessage.ItemId &&
                            !roll.IsWon &&
                            !roll.Rollers.Any(roller => roller.PlayerName.Equals(lootEvent.PlayerName) && roller.HasRolled));
                        if (lootRoll == null) return;
                        var lootRoller =
                            lootRoll.Rollers.FirstOrDefault(roller => roller.PlayerName.Equals(lootEvent.PlayerName));
                        if (lootRoller != null)
                        {
                            lootRoller.HasRolled = true;
                        }
                        else
                        {
                            lootRoll.Rollers.Add(new LootRoller
                            {
                                PlayerName = this.plugin.FormatPlayerName(
                                    this.plugin.Configuration.ChatNameFormat,
                                    lootEvent.PlayerName),
                                RollColor = GetColorByNumber(0),
                                HasRolled = true,
                            });
                        }

                        lootRoll.RollerCount += 1;
                        break;
                    }

                    case LootEventType.Need:
                    case LootEventType.Greed:
                    {
                        lootRoll = this.plugin.LootRolls.FirstOrDefault(roll =>
                            roll.ItemId == lootEvent.LootMessage.ItemId &&
                            roll.Rollers.Any(roller =>
                                roller.PlayerName.Equals(lootEvent.PlayerName) && roller.Roll == 0));
                        var lootRoller = lootRoll?.Rollers.FirstOrDefault(roller =>
                            roller.PlayerName.Equals(lootEvent.PlayerName) && roller.Roll == 0);
                        if (lootRoller == null) return;
                        lootRoller.Roll = lootEvent.Roll;
                        lootRoller.RollColor = GetColorByNumber(lootRoller.Roll);
                        if (lootRoller.Roll != 0)
                        {
                            lootRoller.PlayerName = this.plugin.FormatPlayerName(
                                                                 this.plugin.Configuration.ChatNameFormat,
                                                                 lootRoller.PlayerName) + " [" + lootRoller.Roll + "]";
                        }

                        break;
                    }

                    case LootEventType.Obtain:
                    {
                        lootRoll =
                            this.plugin.LootRolls.FirstOrDefault(roll =>
                                roll.ItemId == lootEvent.LootMessage.ItemId && !roll.IsWon);
                        if (lootRoll == null) return;
                        lootRoll.Timestamp = lootEvent.Timestamp;
                        var winningRoller =
                            lootRoll.Rollers.FirstOrDefault(roller => roller.PlayerName.Equals(lootEvent.PlayerName));
                        if (winningRoller != null) winningRoller.IsWinner = true;
                        lootRoll.Timestamp = lootEvent.Timestamp;
                        lootRoll.IsWon = true;
                        lootRoll.Winner =
                            this.plugin.FormatPlayerName(this.plugin.Configuration.ChatNameFormat, lootEvent.PlayerName);
                        if (lootEvent.IsLocalPlayer && this.plugin.Configuration.WatchListItems.Contains(lootEvent.LootMessage.ItemId))
                        {
                            KapturePlugin.Chat.PluginPrintNotice(string.Format(
                                Loc.Localize("WatchListObtainedAlert", "{0} obtained so removing from your watch list."),
                                lootEvent.LootMessage.ItemName));
                            this.plugin.Configuration.WatchListItems.Remove(lootEvent.LootMessage.ItemId);
                        }

                        break;
                    }

                    case LootEventType.Lost:
                    {
                        lootRoll =
                            this.plugin.LootRolls.FirstOrDefault(roll =>
                                roll.ItemId == lootEvent.LootMessage.ItemId && !roll.IsWon);
                        if (lootRoll == null) return;
                        lootRoll.Timestamp = lootEvent.Timestamp;
                        lootRoll.IsWon = true;
                        lootRoll.Winner = Loc.Localize("RollMonitorLost", "Dropped to floor");

                        break;
                    }

                    case LootEventType.Craft:
                        break;
                    case LootEventType.Desynth:
                        break;
                    case LootEventType.Discard:
                        break;
                    case LootEventType.Gather:
                        break;
                    case LootEventType.Purchase:
                        break;
                    case LootEventType.Search:
                        break;
                    case LootEventType.Sell:
                        break;
                    case LootEventType.Use:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                this.CreateDisplayList();
            }
            catch (Exception ex)
            {
                KapturePlugin.PluginLog.Error(ex, "Failed to process for roll monitor.");
            }
        }

        private static Vector4 GetColorByNumber(uint num)
        {
            return num switch
            {
                0 => ImGuiColors.DalamudWhite,
                < 25 => ImGuiColors.ParsedGrey,
                >= 25 and <= 49 => ImGuiColors.ParsedGreen,
                >= 50 and <= 74 => ImGuiColors.ParsedBlue,
                >= 75 and <= 94 => ImGuiColors.ParsedPurple,
                >= 95 and <= 98 => ImGuiColors.ParsedOrange,
                99 => ImGuiColors.ParsedPink,
                _ => ImGuiColors.ParsedGold,
            };
        }

        private void OnFrameworkUpdate(IFramework framework)
        {
            // Throttle to the configured process frequency (the old System.Timers.Timer
            // interval); Framework.Update fires every frame.
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (now - this.lastProcessTime < this.plugin.Configuration.RollMonitorProcessFrequency) return;
            this.lastProcessTime = now;
            this.ProcessRolls();
        }

        private void ProcessRolls()
        {
            if (this.isProcessing) return;
            if (this.ShouldWait()) return;
            this.isProcessing = true;
            while (!this.LootEvents.IsEmpty && !this.ShouldWait())
            {
                var tryDequeue = this.LootEvents.TryDequeue(out var lootEvent);
                if (!tryDequeue) continue;
                if (lootEvent != null) this.ProcessRoll(lootEvent);
            }

            if (!this.ShouldWait()) this.UpdateRolls();
            this.isProcessing = false;
        }

        private bool ShouldWait()
        {
            if (!this.plugin.Configuration.Enabled) return true;
            if (this.plugin.Configuration.RestrictInCombat && this.plugin.InCombat()) return true;
            return false;
        }

        private void CreateDisplayList()
        {
            this.plugin.LootRollsDisplay =
                JsonConvert.DeserializeObject<List<LootRoll>>(JsonConvert.SerializeObject(this.plugin.LootRolls));
        }

        private void SendRollReminder()
        {
            if (!this.plugin.Configuration.SendRollReminder) return;
            foreach (var lootRoll in this.plugin.LootRolls.Where(roll => !roll.IsWon))
            {
                var rollerMatch = lootRoll.Rollers.FirstOrDefault(roller =>
                                                         roller.IsLocalPlayer &&
                                                         !roller.HasRolled && !roller.IsReminderSent &&
                                                         DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > lootRoll.Timestamp + 300000 - this.plugin.Configuration.RollReminderTime);
                if (rollerMatch != null)
                {
                    rollerMatch.IsReminderSent = true;
                    KapturePlugin.Chat.PluginPrintNotice(string.Format(Loc.Localize("RollReminder", "Roll soon on {0}!"), lootRoll.ItemName));
                }
            }
        }
    }
}
