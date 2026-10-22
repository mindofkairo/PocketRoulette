#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using PocketRoulette.Models;

namespace PocketRoulette
{
    public static class ConfigRules
    {
        public static void Normalize(PocketRouletteConfig config, Action<string> warn = null)
        {
            var defaults = PocketRouletteConfig.CreateDefault();
            config.Mode = (config.Mode ?? "mixed").Trim().ToLowerInvariant();
            if (!new[] { "mixed", "garbage", "useful", "jackpot", "chaos" }.Contains(config.Mode))
            {
                warn?.Invoke($"Unknown mode '{config.Mode}'; using mixed.");
                config.Mode = "mixed";
            }
            config.ItemCount = Clamp(config.ItemCount, 1, 20, "itemCount", warn);
            config.ChancePercent = Clamp(config.ChancePercent, 0, 100, "chancePercent", warn);
            config.GrenadeWeight = Clamp(config.GrenadeWeight, 0, 1000, "grenadeWeight", warn);
            config.GrenadeSpawnDelaySeconds = ClampSeconds(config.GrenadeSpawnDelaySeconds, 0, 10, defaults.GrenadeSpawnDelaySeconds);
            config.GrenadeTimeSpentSeconds = ClampSeconds(config.GrenadeTimeSpentSeconds, 0, 30, defaults.GrenadeTimeSpentSeconds);
            config.GrenadeTpl = string.IsNullOrWhiteSpace(config.GrenadeTpl) ? defaults.GrenadeTpl : config.GrenadeTpl.Trim();
            config.PocketMessages = Clean(config.PocketMessages);
            config.GroundDropMessages = Clean(config.GroundDropMessages);
            config.MissedRewardMessages = Clean(config.MissedRewardMessages);
            config.ChanceMissMessages = Clean(config.ChanceMissMessages);
            config.UltraRareMessages = Clean(config.UltraRareMessages);
            config.UltraRareOddsComparisons = Clean(config.UltraRareOddsComparisons);
            config.FailureMessages = Clean(config.FailureMessages);
            config.MultiRollSummaryMessages = Clean(config.MultiRollSummaryMessages);
            config.GrenadeMessages = Clean(config.GrenadeMessages);
            if (config.GrenadeMessages.Count == 0)
                config.GrenadeMessages = defaults.GrenadeMessages;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var items = new List<PoolItem>();
            foreach (var item in config.ItemPool ?? new List<PoolItem>())
            {
                if (item == null || !IsItemId(item.Tpl?.Trim()))
                {
                    warn?.Invoke("Ignoring a reward with a missing or invalid template ID.");
                    continue;
                }
                item.Tpl = item.Tpl.Trim().ToLowerInvariant();
                if (!seen.Add(item.Tpl))
                {
                    warn?.Invoke($"Ignoring duplicate template {item.Tpl}.");
                    continue;
                }
                item.Name = string.IsNullOrWhiteSpace(item.Name) ? item.Tpl : item.Name.Trim();
                item.Rarity = (item.Rarity ?? "common").Trim().ToLowerInvariant();
                if (!new[] { "common", "uncommon", "rare", "ultrarare" }.Contains(item.Rarity))
                    item.Rarity = "common";
                item.Weight = Math.Max(1, item.Weight);
                item.Width = Math.Max(1, item.Width);
                item.Height = Math.Max(1, item.Height);
                item.MinCount = Math.Max(1, item.MinCount);
                item.MaxCount = Math.Max(item.MinCount, item.MaxCount);
                items.Add(item);
            }
            config.ItemPool = items;
            FeatureRules.Normalize(config);
        }

        public static bool IsItemId(string value)
        {
            return value != null && value.Length == 24 && value.All(Uri.IsHexDigit);
        }

        private static List<string> Clean(List<string> values) =>
            (values ?? new List<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToList();

        private static int Clamp(int value, int min, int max, string name, Action<string> warn)
        {
            var result = Math.Max(min, Math.Min(max, value));
            if (value != result) warn?.Invoke($"{name} {value} is outside {min}-{max}; using {result}.");
            return result;
        }

        private static float ClampSeconds(float value, float min, float max, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Math.Max(min, Math.Min(max, value));
    }
}
