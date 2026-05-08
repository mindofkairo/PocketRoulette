using System.Text.Json;
using PocketRoulette.Server.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Utils;

namespace PocketRoulette.Server;

[Injectable]
public class ConfigRouter : StaticRouter
{
    private static PocketRouletteConfig? _config;
    private static Func<PocketRouletteConfig>? _refreshConfig;
    private static Func<PocketRouletteConfig>? _reloadConfig;

    public ConfigRouter(JsonUtil jsonUtil) : base(jsonUtil, GetCustomRoutes())
    {
    }

    public void SetConfig(PocketRouletteConfig config)
    {
        _config = config;
    }

    public void SetReloadConfig(Func<PocketRouletteConfig> reloadConfig)
    {
        _reloadConfig = reloadConfig;
    }

    public void SetRefreshConfig(Func<PocketRouletteConfig> refreshConfig)
    {
        _refreshConfig = refreshConfig;
    }

    private static List<RouteAction> GetCustomRoutes()
    {
        return
        [
            new RouteAction<EmptyRequestData>(
                "/pocketroulette/config",
                (url, info, sessionId, output) => GetConfig()
            ),
            new RouteAction<EmptyRequestData>(
                "/pocketroulette/reload-config",
                (url, info, sessionId, output) => ReloadConfig()
            )
        ];
    }

    private static ValueTask<string> GetConfig()
    {
        try
        {
            if (_refreshConfig != null)
                _config = _refreshConfig();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PocketRoulette] Error refreshing config: {ex.Message}");
        }

        var config = _config ?? PocketRouletteConfig.CreateDefault();
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        return new ValueTask<string>(json);
    }

    private static ValueTask<string> ReloadConfig()
    {
        try
        {
            if (_reloadConfig == null)
                return new ValueTask<string>("{\"success\":false,\"error\":\"reload_not_ready\"}");

            var config = _reloadConfig();
            _config = config;
            Console.WriteLine($"[PocketRoulette] Reloaded config - {config.ItemPool.Count} items in pool, mode: {config.Mode}, itemCount: {config.ItemCount}, debugLogging: {config.DebugLogging}");
            return new ValueTask<string>("{\"success\":true}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PocketRoulette] Error reloading config: {ex}");
            return new ValueTask<string>("{\"success\":false,\"error\":\"exception\"}");
        }
    }
}
