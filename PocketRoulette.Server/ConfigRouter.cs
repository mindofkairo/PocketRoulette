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

    public ConfigRouter(JsonUtil jsonUtil) : base(jsonUtil, GetCustomRoutes())
    {
    }

    public void SetConfig(PocketRouletteConfig config)
    {
        _config = config;
    }

    private static List<RouteAction> GetCustomRoutes()
    {
        return
        [
            new RouteAction<EmptyRequestData>(
                "/pocketroulette/config",
                (url, info, sessionId, output) => GetConfig()
            )
        ];
    }

    private static ValueTask<string> GetConfig()
    {
        var config = _config ?? PocketRouletteConfig.CreateDefault();
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        return new ValueTask<string>(json);
    }
}
