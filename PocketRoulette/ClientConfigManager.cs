using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using Newtonsoft.Json;
using PocketRoulette.Models;

namespace PocketRoulette
{
    public static class ClientConfigManager
    {
        private const string ClientJsonFileName = "PocketRoulette.ClientConfig.json";
        private static ConfigEntry<bool> _useClientConfig;
        private static ConfigEntry<string> _mode;
        private static ConfigEntry<int> _itemCount;
        private static ConfigEntry<int> _chancePercent;
        private static ConfigEntry<bool> _enableNotification;
        private static ConfigEntry<bool> _debugLogging;
        private static ConfigEntry<bool> _allowGroundDrop;
        private static ConfigEntry<bool> _scavEnabled;
        private static string _clientJsonPath;
        private static DateTime _clientJsonWriteTimeUtc = DateTime.MinValue;
        private static ClientJsonConfig _cachedClientJson;
        private static bool _warnedServerDisabled;

        public static void Bind(ConfigFile config, string pluginDirectory)
        {
            const string serverCategory = "! Server Must Allow This";
            const string settingsCategory = "Roll Settings";

            _clientJsonPath = Path.Combine(pluginDirectory, ClientJsonFileName);
            RemoveLegacyEntries(config);

            _useClientConfig = config.Bind(serverCategory, "Allow Client Config", false, $"Only works when the server has allowClientOverrides=true. Edit items/messages in BepInEx/plugins/PocketRoulette/{ClientJsonFileName}.");
            _mode = config.Bind(settingsCategory, "Mode", "mixed", "mixed, garbage, useful, jackpot, or chaos.");
            _itemCount = config.Bind(settingsCategory, "Item Count", 1, "How many rewards to roll at raid start. Clamped from 1 to 20.");
            _chancePercent = config.Bind(settingsCategory, "Chance Percent", 100, "0-100 chance that Pocket Roulette gives rewards this raid.");
            _enableNotification = config.Bind(settingsCategory, "Enable Notification", true, "Show Pocket Roulette notifications.");
            _allowGroundDrop = config.Bind(settingsCategory, "Allow Ground Drop", false, "Drop rewards at your feet when pockets are full.");
            _scavEnabled = config.Bind(settingsCategory, "Scav Enabled", true, "Allow player scav raids to receive rewards.");
            _debugLogging = config.Bind(settingsCategory, "Debug Logging", false, "Reserved for client-side troubleshooting.");
        }

        public static void EnsureJsonExists(PocketRouletteConfig source)
        {
            EnsureClientJsonFile(source ?? PocketRouletteConfig.CreateDefault());
        }

        public static PocketRouletteConfig Resolve(PocketRouletteConfig serverConfig)
        {
            var config = Clone(serverConfig ?? PocketRouletteConfig.CreateDefault());
            var fallbackItemPool = serverConfig?.ItemPool ?? PocketRouletteConfig.CreateDefault().ItemPool;
            EnsureClientJsonFile(config);

            if (!config.AllowClientOverrides)
            {
                if (_useClientConfig.Value && !_warnedServerDisabled)
                {
                    Plugin.LogSource.LogWarning("[PocketRoulette] Use Client Config is enabled, but the server has allowClientOverrides=false. Client config will be ignored.");
                    _warnedServerDisabled = true;
                }

                return config;
            }

            if (!_useClientConfig.Value)
                return config;

            config.Mode = NormalizeMode(_mode.Value, config.Mode);
            config.ItemCount = Clamp(_itemCount.Value, 1, 20);
            config.ChancePercent = Clamp(_chancePercent.Value, 0, 100);
            config.EnableNotification = _enableNotification.Value;
            config.DebugLogging = _debugLogging.Value;
            config.AllowGroundDrop = _allowGroundDrop.Value;
            config.ScavEnabled = _scavEnabled.Value;

            var clientJson = LoadClientJsonConfig(config);
            config.PocketMessages = UseList(clientJson.PocketMessages, config.PocketMessages);
            config.GroundDropMessages = UseList(clientJson.GroundDropMessages, config.GroundDropMessages);
            config.MissedRewardMessages = UseList(clientJson.MissedRewardMessages, config.MissedRewardMessages);
            config.ChanceMissMessages = UseList(clientJson.ChanceMissMessages, config.ChanceMissMessages);
            config.UltraRareMessages = UseList(clientJson.UltraRareMessages, config.UltraRareMessages);
            config.UltraRareOddsComparisons = UseList(clientJson.UltraRareOddsComparisons, config.UltraRareOddsComparisons);
            config.FailureMessages = UseList(clientJson.FailureMessages, config.FailureMessages);
            config.MultiRollSummaryMessages = UseList(clientJson.MultiRollSummaryMessages, config.MultiRollSummaryMessages);
            config.ItemPool = NormalizeItemPool(clientJson.ItemPool, fallbackItemPool);

            if (config.ItemPool == null || config.ItemPool.Count == 0)
            {
                Plugin.LogSource.LogWarning("[PocketRoulette] Client item pool is empty. Using the server item pool.");
                config.ItemPool = fallbackItemPool;
            }

            return config;
        }

        private static void EnsureClientJsonFile(PocketRouletteConfig source)
        {
            try
            {
                if (File.Exists(_clientJsonPath))
                    return;

                Directory.CreateDirectory(Path.GetDirectoryName(_clientJsonPath));
                var clientJson = ClientJsonConfig.FromConfig(source);
                File.WriteAllText(_clientJsonPath, JsonConvert.SerializeObject(clientJson, Formatting.Indented));
                _cachedClientJson = clientJson;
                _clientJsonWriteTimeUtc = File.GetLastWriteTimeUtc(_clientJsonPath);
                Plugin.LogSource.LogInfo($"[PocketRoulette] Created client config JSON at {_clientJsonPath}");
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogWarning($"[PocketRoulette] Could not create client config JSON: {ex.Message}");
            }
        }

        private static void RemoveLegacyEntries(ConfigFile config)
        {
            var legacySettingsCategory = "2. Client Roll Settings";
            var legacyServerCategory = "1. Server Must Allow This";
            var jsonEntryNames = new[]
            {
                "READ ME",
                "Pocket Messages JSON",
                "Ground Drop Messages JSON",
                "Missed Reward Messages JSON",
                "Chance Miss Messages JSON",
                "Ultra Rare Messages JSON",
                "Ultra Rare Odds Comparisons JSON",
                "Failure Messages JSON",
                "Multi Roll Summary Messages JSON",
                "Item Pool JSON"
            };

            config.Remove(new ConfigDefinition(legacyServerCategory, "READ ME"));
            foreach (var entryName in jsonEntryNames)
                config.Remove(new ConfigDefinition(legacySettingsCategory, entryName));

            foreach (var entryName in new[] { "Mode", "Item Count", "Chance Percent", "Enable Notification", "Debug Logging", "Allow Ground Drop", "Scav Enabled" })
                config.Remove(new ConfigDefinition(legacySettingsCategory, entryName));
        }

        private static ClientJsonConfig LoadClientJsonConfig(PocketRouletteConfig fallback)
        {
            try
            {
                if (!File.Exists(_clientJsonPath))
                {
                    EnsureClientJsonFile(fallback);
                    return _cachedClientJson ?? ClientJsonConfig.FromConfig(fallback);
                }

                var writeTimeUtc = File.GetLastWriteTimeUtc(_clientJsonPath);
                if (_cachedClientJson != null && writeTimeUtc <= _clientJsonWriteTimeUtc)
                    return _cachedClientJson;

                var json = File.ReadAllText(_clientJsonPath);
                var config = JsonConvert.DeserializeObject<ClientJsonConfig>(json) ?? ClientJsonConfig.FromConfig(fallback);
                _cachedClientJson = config;
                _clientJsonWriteTimeUtc = writeTimeUtc;
                return config;
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogWarning($"[PocketRoulette] Could not read client config JSON: {ex.Message}. Keeping the last valid client JSON or server values.");
                return _cachedClientJson ?? ClientJsonConfig.FromConfig(fallback);
            }
        }

        private static List<T> UseList<T>(List<T> preferred, List<T> fallback)
        {
            if (preferred == null)
                return fallback ?? new List<T>();

            return preferred;
        }

        private static PocketRouletteConfig Clone(PocketRouletteConfig config)
        {
            return JsonConvert.DeserializeObject<PocketRouletteConfig>(JsonConvert.SerializeObject(config))
                ?? PocketRouletteConfig.CreateDefault();
        }

        private static List<PoolItem> NormalizeItemPool(List<PoolItem> items, List<PoolItem> fallback)
        {
            if (items == null)
                return fallback;

            var normalized = new List<PoolItem>();

            foreach (var item in items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.Tpl))
                    continue;

                if (string.IsNullOrWhiteSpace(item.Name))
                    item.Name = item.Tpl;

                item.Weight = Math.Max(1, item.Weight);
                item.Width = Math.Max(1, item.Width);
                item.Height = Math.Max(1, item.Height);
                item.MinCount = Math.Max(1, item.MinCount);
                item.MaxCount = Math.Max(item.MinCount, item.MaxCount);
                item.Rarity = NormalizeRarity(item.Rarity);
                normalized.Add(item);
            }

            return normalized.Count > 0 ? normalized : fallback;
        }

        private static string NormalizeMode(string mode, string fallback)
        {
            switch ((mode ?? string.Empty).ToLower())
            {
                case "mixed":
                case "garbage":
                case "useful":
                case "jackpot":
                case "chaos":
                    return mode.ToLower();
                default:
                    Plugin.LogSource.LogWarning($"[PocketRoulette] Client mode '{mode}' is invalid. Keeping '{fallback}'.");
                    return fallback;
            }
        }

        private static string NormalizeRarity(string rarity)
        {
            switch ((rarity ?? string.Empty).ToLower())
            {
                case "common":
                case "uncommon":
                case "rare":
                case "ultrarare":
                    return rarity.ToLower();
                default:
                    return "common";
            }
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
                return min;

            return value > max ? max : value;
        }

        private class ClientJsonConfig
        {
            [JsonProperty("pocketMessages")]
            public List<string> PocketMessages { get; set; } = new List<string>();

            [JsonProperty("groundDropMessages")]
            public List<string> GroundDropMessages { get; set; } = new List<string>();

            [JsonProperty("missedRewardMessages")]
            public List<string> MissedRewardMessages { get; set; } = new List<string>();

            [JsonProperty("chanceMissMessages")]
            public List<string> ChanceMissMessages { get; set; } = new List<string>();

            [JsonProperty("ultraRareMessages")]
            public List<string> UltraRareMessages { get; set; } = new List<string>();

            [JsonProperty("ultraRareOddsComparisons")]
            public List<string> UltraRareOddsComparisons { get; set; } = new List<string>();

            [JsonProperty("failureMessages")]
            public List<string> FailureMessages { get; set; } = new List<string>();

            [JsonProperty("multiRollSummaryMessages")]
            public List<string> MultiRollSummaryMessages { get; set; } = new List<string>();

            [JsonProperty("itemPool")]
            public List<PoolItem> ItemPool { get; set; } = new List<PoolItem>();

            public static ClientJsonConfig FromConfig(PocketRouletteConfig config)
            {
                return new ClientJsonConfig
                {
                    PocketMessages = config.PocketMessages ?? new List<string>(),
                    GroundDropMessages = config.GroundDropMessages ?? new List<string>(),
                    MissedRewardMessages = config.MissedRewardMessages ?? new List<string>(),
                    ChanceMissMessages = config.ChanceMissMessages ?? new List<string>(),
                    UltraRareMessages = config.UltraRareMessages ?? new List<string>(),
                    UltraRareOddsComparisons = config.UltraRareOddsComparisons ?? new List<string>(),
                    FailureMessages = config.FailureMessages ?? new List<string>(),
                    MultiRollSummaryMessages = config.MultiRollSummaryMessages ?? new List<string>(),
                    ItemPool = config.ItemPool ?? new List<PoolItem>()
                };
            }
        }
    }
}
