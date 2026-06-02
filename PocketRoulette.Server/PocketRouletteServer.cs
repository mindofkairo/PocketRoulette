using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using PocketRoulette.Server.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Spt.Mod;
using Range = SemanticVersioning.Range;

namespace PocketRoulette.Server;

public record ModMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = "com.kairo.pocketroulette.server";
    public override string Name { get; init; } = "Pocket Roulette (Server)";
    public override string Author { get; init; } = "kairo";
    public override SemanticVersioning.Version Version { get; init; } = new("1.2.1");
    public override Range SptVersion { get; init; } = new("~4.0.0");
    public override string License { get; init; } = "MIT";
    public override bool? IsBundleMod { get; init; } = false;
    public override Dictionary<string, Range>? ModDependencies { get; init; }
    public override string? Url { get; init; }
    public override List<string>? Contributors { get; init; }
    public override List<string>? Incompatibilities { get; init; }
}

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class PocketRouletteServer(
    ModHelper modHelper,
    ConfigRouter configRouter,
    InjectRouter injectRouter) : IOnLoad
{
    private string _configPath = string.Empty;
    private DateTime _lastConfigWriteTimeUtc = DateTime.MinValue;
    private PocketRouletteConfig? _currentConfig;

    public Task OnLoad()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var modPath = modHelper.GetAbsolutePathToModFolder(assembly);
        _configPath = Path.Combine(modPath, "config", "config.json");

        var config = LoadAndValidateConfig();

        Console.WriteLine($"[PocketRoulette] Loaded config - {config.ItemPool.Count} items in pool, mode: {config.Mode}, itemCount: {config.ItemCount}, clientOverrides: {config.AllowClientOverrides}, debugLogging: {config.DebugLogging}");
        WarnIfClientOverridesEnabled(config);

        configRouter.SetConfig(config);
        configRouter.SetRefreshConfig(RefreshConfigIfChanged);
        configRouter.SetReloadConfig(ForceReloadConfig);
        injectRouter.SetDebugLogging(config.DebugLogging);

        return Task.CompletedTask;
    }

    private PocketRouletteConfig LoadAndValidateConfig()
    {
        var config = LoadConfig(_configPath, out var shouldSave);
        ValidateConfig(config);
        if (shouldSave)
            SaveConfig(_configPath, config);

        _lastConfigWriteTimeUtc = File.Exists(_configPath)
            ? File.GetLastWriteTimeUtc(_configPath)
            : DateTime.MinValue;
        _currentConfig = config;
        injectRouter.SetDebugLogging(config.DebugLogging);
        return config;
    }

    private PocketRouletteConfig RefreshConfigIfChanged()
    {
        if (!File.Exists(_configPath))
            return _currentConfig ?? LoadAndValidateConfig();

        var writeTimeUtc = File.GetLastWriteTimeUtc(_configPath);
        if (_currentConfig == null || writeTimeUtc > _lastConfigWriteTimeUtc)
        {
            try
            {
                var config = LoadAndValidateConfig();
                Console.WriteLine($"[PocketRoulette] Auto-reloaded config - {config.ItemPool.Count} items in pool, mode: {config.Mode}, itemCount: {config.ItemCount}, clientOverrides: {config.AllowClientOverrides}, debugLogging: {config.DebugLogging}");
                WarnIfClientOverridesEnabled(config);
                return config;
            }
            catch (Exception ex)
            {
                _lastConfigWriteTimeUtc = writeTimeUtc;
                Console.WriteLine($"[PocketRoulette] Config reload failed. Keeping the last valid config. Error: {ex.Message}");
            }
        }

        return _currentConfig ?? PocketRouletteConfig.CreateDefault();
    }

    private PocketRouletteConfig ForceReloadConfig()
    {
        var config = LoadAndValidateConfig();
        WarnIfClientOverridesEnabled(config);
        return config;
    }

    private static void WarnIfClientOverridesEnabled(PocketRouletteConfig config)
    {
        if (!config.AllowClientOverrides)
            return;

        Console.WriteLine("[PocketRoulette] WARNING: allowClientOverrides is enabled. Players who enable 'Use Client Config' in F12 can control their own Pocket Roulette odds, item pool, messages, and reward behavior. Use this only for solo or trusted groups.");
    }

    private static PocketRouletteConfig LoadConfig(string configPath, out bool shouldSave)
    {
        shouldSave = false;

        if (!File.Exists(configPath))
        {
            var defaultConfig = PocketRouletteConfig.CreateDefault();
            SaveConfig(configPath, defaultConfig);
            Console.WriteLine($"[PocketRoulette] Created default config at {configPath}");
            return defaultConfig;
        }

        var configJson = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<PocketRouletteConfig>(configJson, JsonOptions)
            ?? PocketRouletteConfig.CreateDefault();

        using var document = JsonDocument.Parse(configJson);
        shouldSave = ApplyMissingDefaults(config, document.RootElement);
        if (shouldSave)
            Console.WriteLine("[PocketRoulette] Config was missing new options. Added them using default values.");

        return config;
    }

    private static void SaveConfig(string configPath, PocketRouletteConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        File.WriteAllText(configPath, JsonSerializer.Serialize(config, JsonOptions));
    }

    private static bool ApplyMissingDefaults(PocketRouletteConfig config, JsonElement root)
    {
        var changed = false;
        var defaults = PocketRouletteConfig.CreateDefault();

        changed |= AddMissing(root, "mode", () => config.Mode = defaults.Mode);
        changed |= AddMissing(root, "itemCount", () => config.ItemCount = defaults.ItemCount);
        changed |= AddMissing(root, "chancePercent", () => config.ChancePercent = defaults.ChancePercent);
        changed |= AddMissing(root, "enableNotification", () => config.EnableNotification = defaults.EnableNotification);
        changed |= AddMissing(root, "debugLogging", () => config.DebugLogging = defaults.DebugLogging);
        changed |= AddMissing(root, "allowClientOverrides", () => config.AllowClientOverrides = defaults.AllowClientOverrides);
        changed |= AddMissing(root, "allowGroundDrop", () => config.AllowGroundDrop = defaults.AllowGroundDrop);
        changed |= AddMissing(root, "scavEnabled", () => config.ScavEnabled = defaults.ScavEnabled);
        changed |= AddMissing(root, "pocketMessages", () => config.PocketMessages = defaults.PocketMessages);
        changed |= AddMissing(root, "groundDropMessages", () => config.GroundDropMessages = defaults.GroundDropMessages);
        changed |= AddMissing(root, "missedRewardMessages", () => config.MissedRewardMessages = defaults.MissedRewardMessages);
        changed |= AddMissing(root, "chanceMissMessages", () => config.ChanceMissMessages = defaults.ChanceMissMessages);
        changed |= AddMissing(root, "ultraRareMessages", () => config.UltraRareMessages = defaults.UltraRareMessages);
        changed |= AddMissing(root, "ultraRareOddsComparisons", () => config.UltraRareOddsComparisons = defaults.UltraRareOddsComparisons);
        changed |= AddMissing(root, "failureMessages", () => config.FailureMessages = defaults.FailureMessages);
        changed |= AddMissing(root, "multiRollSummaryMessages", () => config.MultiRollSummaryMessages = defaults.MultiRollSummaryMessages);
        changed |= AddMissing(root, "itemPool", () => config.ItemPool = defaults.ItemPool);
        changed |= AddMissingStackCountDefaults(config, root);

        return changed;
    }

    private static bool AddMissing(JsonElement root, string propertyName, Action addDefault)
    {
        if (root.TryGetProperty(propertyName, out _))
            return false;

        addDefault();
        return true;
    }

    private static bool AddMissingStackCountDefaults(PocketRouletteConfig config, JsonElement root)
    {
        if (!root.TryGetProperty("itemPool", out var itemPool) || itemPool.ValueKind != JsonValueKind.Array)
            return false;

        var changed = false;
        var itemIndex = 0;
        foreach (var itemJson in itemPool.EnumerateArray())
        {
            if (itemIndex >= config.ItemPool.Count)
                break;

            var item = config.ItemPool[itemIndex];
            if (DefaultStackCounts.TryGetValue(item.Tpl, out var stackCounts))
            {
                if (!itemJson.TryGetProperty("minCount", out _))
                {
                    item.MinCount = stackCounts.Min;
                    changed = true;
                }

                if (!itemJson.TryGetProperty("maxCount", out _))
                {
                    item.MaxCount = stackCounts.Max;
                    changed = true;
                }
            }

            itemIndex++;
        }

        return changed;
    }

    private static void ValidateConfig(PocketRouletteConfig config)
    {
        NormalizeConfig(config);

        var validModes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "mixed",
            "garbage",
            "useful",
            "jackpot",
            "chaos"
        };
        var validRarities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "common",
            "uncommon",
            "rare",
            "ultrarare"
        };

        if (!validModes.Contains(config.Mode))
        {
            Console.WriteLine($"[PocketRoulette] Config warning: mode '{config.Mode}' is invalid. Using 'mixed'.");
            config.Mode = "mixed";
        }

        config.ItemCount = Clamp(config.ItemCount, 1, 20, "itemCount");
        config.ChancePercent = Clamp(config.ChancePercent, 0, 100, "chancePercent");

        if (config.ItemPool.Count == 0)
            Console.WriteLine("[PocketRoulette] Config warning: itemPool is empty. No rewards can be rolled.");

        var seenTpls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in config.ItemPool)
        {
            if (string.IsNullOrWhiteSpace(item.Tpl))
                Console.WriteLine($"[PocketRoulette] Config warning: item '{item.Name}' has an empty tpl.");

            if (!string.IsNullOrWhiteSpace(item.Tpl) && !seenTpls.Add(item.Tpl))
                Console.WriteLine($"[PocketRoulette] Config warning: duplicate tpl '{item.Tpl}' appears in itemPool.");

            if (string.IsNullOrWhiteSpace(item.Name))
            {
                Console.WriteLine($"[PocketRoulette] Config warning: item '{item.Tpl}' has an empty name. Using tpl as display name.");
                item.Name = item.Tpl;
            }

            if (item.Weight < 1)
            {
                Console.WriteLine($"[PocketRoulette] Config warning: item '{item.Name}' has weight {item.Weight}. Using 1.");
                item.Weight = 1;
            }

            if (item.MinCount < 1)
            {
                Console.WriteLine($"[PocketRoulette] Config warning: item '{item.Name}' has minCount {item.MinCount}. Using 1.");
                item.MinCount = 1;
            }

            if (item.MaxCount < item.MinCount)
            {
                Console.WriteLine($"[PocketRoulette] Config warning: item '{item.Name}' has maxCount below minCount. Using minCount.");
                item.MaxCount = item.MinCount;
            }

            if (item.Width < 1)
                item.Width = 1;

            if (item.Height < 1)
                item.Height = 1;

            if (string.IsNullOrWhiteSpace(item.Rarity))
                item.Rarity = "common";

            if (!validRarities.Contains(item.Rarity))
            {
                Console.WriteLine($"[PocketRoulette] Config warning: item '{item.Name}' has unknown rarity '{item.Rarity}'. Using common.");
                item.Rarity = "common";
            }
        }
    }

    private static void NormalizeConfig(PocketRouletteConfig config)
    {
        config.Mode ??= "mixed";
        config.ItemPool ??= [];
        config.PocketMessages ??= [];
        config.GroundDropMessages ??= [];
        config.MissedRewardMessages ??= [];
        config.ChanceMissMessages ??= [];
        config.UltraRareMessages ??= [];
        config.UltraRareOddsComparisons ??= [];
        config.FailureMessages ??= [];
        config.MultiRollSummaryMessages ??= [];
        config.ItemPool = config.ItemPool.Where(item => item != null).ToList();
    }

    private static int Clamp(int value, int min, int max, string name)
    {
        if (value < min)
        {
            Console.WriteLine($"[PocketRoulette] Config warning: {name} {value} is below {min}. Using {min}.");
            return min;
        }

        if (value > max)
        {
            Console.WriteLine($"[PocketRoulette] Config warning: {name} {value} is above {max}. Using {max}.");
            return max;
        }

        return value;
    }

    private static readonly Dictionary<string, (int Min, int Max)> DefaultStackCounts = new(StringComparer.OrdinalIgnoreCase)
    {
        { "56d59d3ad2720bdb418b4577", (1, 45) },
        { "56dff3afd2720bba668b4567", (1, 45) },
        { "560d5e524bdc2d25448b4571", (1, 45) },
        { "5449016a4bdc2d6f028b456f", (1, 45) },
        { "5696686a4bdc2da3298b456a", (1, 45) },
        { "569668774bdc2da2298b4568", (1, 45) }
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}
