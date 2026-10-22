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
        private static ConfigEntry<bool> _useLocal;
        private static ConfigEntry<string> _mode;
        private static ConfigEntry<int> _count;
        private static ConfigEntry<int> _chance;
        private static ConfigEntry<bool> _notifications;
        private static ConfigEntry<bool> _ground;
        private static ConfigEntry<bool> _scav;
        private static ConfigEntry<bool> _debug;
        private static string _jsonPath;
        private static DateTime _lastWrite;
        private static LocalContent _cached;
        private static bool _warned;
        private static ConfigEntry<int> _buyInCount;
        private static ConfigEntry<KeyboardShortcut> _share;
        private static ConfigEntry<KeyboardShortcut> _keep;
        public static int BuyInCount => _buyInCount.Value;
        public static string ShareKey => _share.Value.ToString();
        public static string KeepKey => _keep.Value.ToString();
        public static bool SharePressed => _share.Value.IsDown();
        public static bool KeepPressed => _keep.Value.IsDown();

        public static void Bind(ConfigFile file, string pluginDirectory)
        {
            _jsonPath = Path.Combine(pluginDirectory, "PocketRoulette.ClientConfig.jsonc");
            const string section = "Roll Settings";
            _useLocal = file.Bind("! Server Must Allow This", "Allow Client Config", false,
                "Requires allowClientOverrides=true on the server. Messages and items live in PocketRoulette.ClientConfig.jsonc beside this plugin.");
            _buyInCount = file.Bind("Buy-In Spins", "Spins Per PMC Raid", 0, new ConfigDescription(
                "Explicit opt-in: buy this many extra spins EVERY PMC raid until set back to 0. Server price/cap apply; payment is taken from stash roubles at raid start. Does not require client overrides.",
                new AcceptableValueRange<int>(0, 10)));
            _share = file.Bind("Jackpot Choices", "Share Jackpot", new KeyboardShortcut(UnityEngine.KeyCode.F8), "Share when a jackpot decision is open.");
            _keep = file.Bind("Jackpot Choices", "Keep Jackpot", new KeyboardShortcut(UnityEngine.KeyCode.F9), "Keep both jackpot prizes. Timeout also keeps them.");
            _mode = file.Bind(section, "Mode", "mixed", new ConfigDescription("Rarity distribution.",
                new AcceptableValueList<string>("mixed", "garbage", "useful", "jackpot", "chaos")));
            _count = file.Bind(section, "Item Count", 1, new ConfigDescription("Rolls at raid start.", new AcceptableValueRange<int>(1, 20)));
            _chance = file.Bind(section, "Chance Percent", 100, new ConfigDescription("Chance to run roulette this raid.", new AcceptableValueRange<int>(0, 100)));
            _notifications = file.Bind(section, "Enable Notification", true, "Show roulette notifications, including grenade warnings.");
            _ground = file.Bind(section, "Allow Ground Drop", true, "Drop rewards at your feet if pockets are full.");
            _scav = file.Bind(section, "Scav Enabled", true, "Allow player scav raid rewards.");
            _debug = file.Bind(section, "Debug Logging", false, "Log each completed roll.");
        }

        public static void EnsureJsonExists(PocketRouletteConfig source)
        {
            try
            {
                if (File.Exists(_jsonPath)) return;
                Directory.CreateDirectory(Path.GetDirectoryName(_jsonPath));
                var legacy = _jsonPath.Substring(0, _jsonPath.Length - 1);
                if (File.Exists(legacy))
                {
                    File.Copy(legacy, legacy + ".bak", true);
                    File.WriteAllText(_jsonPath, "// Optional local messages/items. Requires server permission plus the F12 client-config toggle.\n" + File.ReadAllText(legacy));
                    return;
                }
                var content = LocalContent.From(source);
                File.WriteAllText(_jsonPath, "// Optional local messages/items. Feature switches and luck remain server-owned.\n"
                    + "// Item fields: tpl, name, weight, rarity, width, height, minCount, maxCount. See the commented server config for details.\n"
                    + JsonConvert.SerializeObject(content, Formatting.Indented));
                _cached = content;
                _lastWrite = File.GetLastWriteTimeUtc(_jsonPath);
            }
            catch (Exception ex) { Plugin.LogSource.LogWarning($"[PocketRoulette] Cannot create client JSON: {ex.Message}"); }
        }

        public static PocketRouletteConfig Resolve(PocketRouletteConfig server)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            var config = JsonConvert.DeserializeObject<PocketRouletteConfig>(JsonConvert.SerializeObject(server));
            EnsureJsonExists(server);
            if (!_useLocal.Value) return config;
            if (!server.AllowClientOverrides)
            {
                if (!_warned) Plugin.LogSource.LogWarning("[PocketRoulette] Client overrides are disabled by the server. Using server config.");
                _warned = true;
                return config;
            }
            config.Mode = _mode.Value;
            config.ItemCount = _count.Value;
            config.ChancePercent = _chance.Value;
            config.EnableNotification = _notifications.Value;
            config.AllowGroundDrop = _ground.Value;
            config.ScavEnabled = _scav.Value;
            config.DebugLogging = _debug.Value;
            var content = ReadContent();
            if (content != null)
            {
                config.PocketMessages = content.PocketMessages ?? config.PocketMessages;
                config.GroundDropMessages = content.GroundDropMessages ?? config.GroundDropMessages;
                config.MissedRewardMessages = content.MissedRewardMessages ?? config.MissedRewardMessages;
                config.ChanceMissMessages = content.ChanceMissMessages ?? config.ChanceMissMessages;
                config.UltraRareMessages = content.UltraRareMessages ?? config.UltraRareMessages;
                config.UltraRareOddsComparisons = content.UltraRareOddsComparisons ?? config.UltraRareOddsComparisons;
                config.FailureMessages = content.FailureMessages ?? config.FailureMessages;
                config.MultiRollSummaryMessages = content.MultiRollSummaryMessages ?? config.MultiRollSummaryMessages;
                config.ItemPool = content.ItemPool ?? config.ItemPool;
            }
            ConfigRules.Normalize(config, message => Plugin.LogSource.LogWarning($"[PocketRoulette] Client config: {message}"));
            if (config.ItemPool.Count == 0) config.ItemPool = server.ItemPool;
            return config;
        }

        private static LocalContent ReadContent()
        {
            try
            {
                if (!File.Exists(_jsonPath)) return _cached;
                var modified = File.GetLastWriteTimeUtc(_jsonPath);
                if (_cached != null && modified == _lastWrite) return _cached;
                var next = JsonConvert.DeserializeObject<LocalContent>(File.ReadAllText(_jsonPath));
                if (next == null) throw new InvalidDataException("Client JSON must be an object.");
                _cached = next;
                _lastWrite = modified;
            }
            catch (Exception ex) { Plugin.LogSource.LogWarning($"[PocketRoulette] Invalid client JSON; keeping previous content: {ex.Message}"); }
            return _cached;
        }

        private sealed class LocalContent
        {
            [JsonProperty("pocketMessages")] public List<string> PocketMessages { get; set; }
            [JsonProperty("groundDropMessages")] public List<string> GroundDropMessages { get; set; }
            [JsonProperty("missedRewardMessages")] public List<string> MissedRewardMessages { get; set; }
            [JsonProperty("chanceMissMessages")] public List<string> ChanceMissMessages { get; set; }
            [JsonProperty("ultraRareMessages")] public List<string> UltraRareMessages { get; set; }
            [JsonProperty("ultraRareOddsComparisons")] public List<string> UltraRareOddsComparisons { get; set; }
            [JsonProperty("failureMessages")] public List<string> FailureMessages { get; set; }
            [JsonProperty("multiRollSummaryMessages")] public List<string> MultiRollSummaryMessages { get; set; }
            [JsonProperty("itemPool")] public List<PoolItem> ItemPool { get; set; }

            public static LocalContent From(PocketRouletteConfig config) => new LocalContent
            {
                PocketMessages = config.PocketMessages, GroundDropMessages = config.GroundDropMessages,
                MissedRewardMessages = config.MissedRewardMessages, ChanceMissMessages = config.ChanceMissMessages,
                UltraRareMessages = config.UltraRareMessages, UltraRareOddsComparisons = config.UltraRareOddsComparisons,
                FailureMessages = config.FailureMessages, MultiRollSummaryMessages = config.MultiRollSummaryMessages,
                ItemPool = config.ItemPool
            };
        }
    }
}
