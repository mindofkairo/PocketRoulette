using System;
using System.Collections.Generic;
using System.Linq;
using EFT.Communications;
using PocketRoulette.Models;
using UnityEngine;

namespace PocketRoulette
{
    internal static class RewardNotifications
    {
        private static readonly System.Random Random = new System.Random(Guid.NewGuid().GetHashCode());

        public static void ChanceMiss(PocketRouletteConfig config) =>
            Show(config, Pick(config.ChanceMissMessages, "The pocket gods looked away this raid."), Color.gray);

        public static void GrenadeWarning(PocketRouletteConfig config, RollPlan plan) =>
            Show(config, Pick(config.GrenadeMessages, "A live grenade is about to land where you are standing. RUN!")
                .Replace("{delay}", config.GrenadeSpawnDelaySeconds.ToString("0.##"))
                .Replace("{grenade}", plan.GrenadeName) + LuckText(config, plan), new Color(1f, 0.25f, 0.1f));

        public static void Single(PocketRouletteConfig config, RewardResult result, RollPlan plan)
        {
            if (result.Placement == RewardPlacement.Grenade) return;
            var name = Name(result);
            string message;
            switch (result.Placement)
            {
                case RewardPlacement.Pocket:
                    if (result.Item.Rarity == "ultrarare" && config.UltraRareMessages.Count > 0 && plan.OddsPercent > 0)
                    {
                        message = Pick(config.UltraRareMessages, "JACKPOT! {item}")
                            .Replace("{odds}", plan.OddsPercent.ToString("0.##") + "%")
                            .Replace("{comparison}", Pick(config.UltraRareOddsComparisons, "finding a GPU on Interchange"));
                    }
                    else message = Pick(config.PocketMessages, "Pocket Roulette gave you {item}.");
                    break;
                case RewardPlacement.Ground:
                    message = Pick(config.GroundDropMessages, "{item} appeared at your feet.").Replace("{location}", "at your feet");
                    break;
                case RewardPlacement.Missed:
                    message = Pick(config.MissedRewardMessages, "Your pockets were full. You missed {item}.");
                    break;
                default:
                    message = Pick(config.FailureMessages, "Pocket Roulette failed to spawn {item}.");
                    break;
            }
            message = message.Replace("{item}", name);
            if (message.IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0) message += " (" + name + ")";
            Show(config, message + LuckText(config, plan), result.Received ? RarityColor(result.Item.Rarity) : Color.red);
        }

        public static string LuckText(PocketRouletteConfig config, RollPlan plan)
        {
            var text = config.EnableLuck && config.Luck.ShowAtRoll ? $" [Luck {plan.Luck:0.##}]" : "";
#if POCKET_ROULETTE_TESTING
            if (plan.Origin == "test-forced") text = " [FORCED TEST]" + text;
#endif
            return text;
        }

        public static void Notice(PocketRouletteConfig config, string text) => Show(config, text, new Color(0.8f, 0.85f, 1f));

        public static void PartyHazard(string nickname, RollPlan plan)
        {
            var name = string.IsNullOrWhiteSpace(nickname) ? "A teammate" : nickname;
            var text = plan.Kind == "cursed" ? $"{name} rolled a CURSED JACKPOT. A live grenade will explode at their prize. MOVE AWAY!"
                : $"{name} rolled {plan.GrenadeName}. LIVE GRENADE at their roll location - MOVE AWAY!";
            try { NotificationManager.DisplayMessageNotification(text, ENotificationDurationType.Long, ENotificationIconType.Default, new Color(1f, 0.4f, 0.15f)); }
            catch (Exception ex) { Plugin.LogSource.LogWarning("[PocketRoulette] Group warning failed: " + ex.Message); }
        }

        public static void CursedExplanation(PocketRouletteConfig config, RollPlan plan) => Notice(config,
            $"CURSED JACKPOT! {plan.Reward.Name} will appear here. Grab it and run: unclaimed loot vanishes when the grenade explodes. Timer starts after this explanation."
            + LuckText(config, plan));

        public static void CursedActive(PocketRouletteConfig config, RollPlan plan, float remaining) => Notice(config,
            $"CURSED JACKPOT active: {plan.Reward.Name}. {Math.Max(0, remaining):0.0}s to grab it and run! Unclaimed loot disappears at detonation.");

        public static string JackpotAnnouncementText(PocketRouletteConfig config, RollPlan plan) =>
            $"SHARED JACKPOT! Main prize: {plan.Reward.Name}. Bonus prize: {plan.BonusReward?.Name ?? "bonus loot"}. Choose whether to share with your squad."
            + LuckText(config, plan);

        public static void ChallengeAssigned(PocketRouletteConfig config, LuckyChallenge challenge) => Notice(config,
            "Lucky challenge: " + ChallengeRules.Description(challenge) + $" to earn {config.LuckyChallenges.BonusSpins} bonus spin(s).");

        public static void Summary(PocketRouletteConfig config, List<RewardResult> results, string luck = "")
        {
            var pocket = results.Count(result => result.Placement == RewardPlacement.Pocket);
            var ground = results.Count(result => result.Placement == RewardPlacement.Ground);
            var missed = results.Count(result => result.Placement == RewardPlacement.Missed);
            var failed = results.Count(result => result.Placement == RewardPlacement.Failed);
            var grenades = results.Count(result => result.Placement == RewardPlacement.Grenade);
            var received = results.Where(result => result.Received).OrderByDescending(result => Rank(result.Item.Rarity)).ToList();
            var best = string.Join(", ", received.Take(3).Select(Name));
            var text = Pick(config.MultiRollSummaryMessages,
                "Pocket Roulette rolled {total} rewards: {pocketCount} in pockets{groundPart}{missedPart}{failedPart}{grenadePart}.{bestPart}")
                .Replace("{total}", results.Count.ToString()).Replace("{pocketCount}", pocket.ToString())
                .Replace("{groundCount}", ground.ToString()).Replace("{missedCount}", missed.ToString())
                .Replace("{failedCount}", failed.ToString()).Replace("{grenadeCount}", grenades.ToString())
                .Replace("{groundPart}", ground > 0 ? $", {ground} at your feet" : "")
                .Replace("{missedPart}", missed > 0 ? $", {missed} missed" : "")
                .Replace("{failedPart}", failed > 0 ? $", {failed} failed" : "")
                .Replace("{grenadePart}", grenades > 0 ? $", {grenades} live grenade(s)" : "")
                .Replace("{bestFinds}", best).Replace("{bestPart}", best.Length > 0 ? " Best finds: " + best + "." : "");
            Show(config, text + luck, received.Count > 0 ? RarityColor(received[0].Item.Rarity) : Color.red);
        }

        public static void Interrupted(PocketRouletteConfig config, List<RewardResult> results)
        {
            if (results.Count > 0) Summary(config, results);
            Show(config, "Pocket Roulette stopped because a roll could not complete. Check the client log.", Color.red);
        }

        private static void Show(PocketRouletteConfig config, string message, Color color)
        {
            if (!config.EnableNotification) return;
            try { NotificationManager.DisplayMessageNotification(message, ENotificationDurationType.Long, ENotificationIconType.Default, color); }
            catch (Exception ex) { Plugin.LogSource.LogWarning($"[PocketRoulette] Notification failed: {ex.Message}"); }
        }

        private static string Pick(List<string> values, string fallback) => values.Count > 0 ? values[Random.Next(values.Count)] : fallback;

        private static string Name(RewardResult result)
        {
            var name = result.Item?.Name ?? "live grenade";
            if (result.StackCount <= 1) return name;
            if (name.StartsWith("1x ", StringComparison.OrdinalIgnoreCase)) name = name.Substring(3);
            else if (name.StartsWith("1 ", StringComparison.OrdinalIgnoreCase)) name = name.Substring(2);
            return name + " x" + result.StackCount;
        }

        private static int Rank(string rarity) => rarity == "ultrarare" ? 4 : rarity == "rare" ? 3 : rarity == "uncommon" ? 2 : 1;
        private static Color RarityColor(string rarity) => rarity == "ultrarare" ? new Color(1f, 0.84f, 0f)
            : rarity == "rare" ? new Color(0.4f, 0.6f, 1f) : rarity == "uncommon" ? new Color(0.3f, 0.85f, 0.3f) : Color.gray;
    }
}
