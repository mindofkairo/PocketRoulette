using System.Reflection;
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
    public override SemanticVersioning.Version Version { get; init; } = new("1.0.0");
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
    ConfigRouter configRouter) : IOnLoad
{
    public Task OnLoad()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var modPath = modHelper.GetAbsolutePathToModFolder(assembly);
        var configPath = Path.Combine(modPath, "config", "config.json");

        var config = LoadConfig(configPath);

        Console.WriteLine($"[PocketRoulette] Loaded config - {config.ItemPool.Count} items in pool, mode: {config.Mode}");

        configRouter.SetConfig(config);

        return Task.CompletedTask;
    }

    private static PocketRouletteConfig LoadConfig(string configPath)
    {
        if (!File.Exists(configPath))
        {
            var defaultConfig = PocketRouletteConfig.CreateDefault();
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            File.WriteAllText(configPath, JsonSerializer.Serialize(defaultConfig, JsonOptions));
            Console.WriteLine($"[PocketRoulette] Created default config at {configPath}");
            return defaultConfig;
        }

        var configJson = File.ReadAllText(configPath);
        return JsonSerializer.Deserialize<PocketRouletteConfig>(configJson, JsonOptions)
            ?? PocketRouletteConfig.CreateDefault();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
}
