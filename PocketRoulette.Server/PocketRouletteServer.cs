using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Spt.Mod;
using Range = SemanticVersioning.Range;

namespace PocketRoulette.Server;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.kairo.pocketroulette.server";
    public string Name { get; init; } = "Pocket Roulette (Server)";
    public string Author { get; init; } = "kairo";
    public SemanticVersioning.Version Version { get; init; } = new("1.9.3");
    public Range SptVersion { get; init; } = new("~4.1.0");
    public string License { get; init; } = "MIT";
    public bool HasPrepatcher { get; init; } = false;
    public Dictionary<string, Range>? ModDependencies { get; init; }
    public string? Url { get; init; }
    public List<string>? Contributors { get; init; }
    public List<string>? Incompatibilities { get; init; }
}

[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.PostLoad)]
public class PocketRouletteServer(ModHelper modHelper, ConfigService configService, FeatureService features) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var folder = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        configService.Initialize(Path.Combine(folder, "config", "config.jsonc"));
        try { features.Initialize(folder); }
        catch (Exception ex) { Console.WriteLine($"[PocketRoulette] Feature state could not load; roulette requests will be refused: {ex.Message}"); }
        var config = configService.Get();
        Console.WriteLine($"[PocketRoulette] 1.9.3 / SPT 4.1.X: {config.ItemPool.Count} rewards, mode {config.Mode}, {config.ItemCount} roll(s), grenade event {(config.EnableGrenadeEvent ? "enabled" : "disabled")}.");
        return Task.CompletedTask;
    }
}

