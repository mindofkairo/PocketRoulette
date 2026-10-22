using System.Text.Json;
using PocketRoulette.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Utils;

namespace PocketRoulette.Server;

[Injectable]
public class ConfigRouter(JsonUtil jsonUtil, ConfigService configs) : StaticRouter(jsonUtil,
[
    new RouteAction<EmptyRequestData>("/pocketroulette/config", (url, request, session, output, ct) =>
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(JsonSerializer.Serialize(configs.Get()));
    }),
    new RouteAction<EmptyRequestData>("/pocketroulette/reload-config", (url, request, session, output, ct) =>
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            configs.Reload();
            return ValueTask.FromResult("{\"success\":true}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PocketRoulette] Config reload failed; keeping previous values: {ex.Message}");
            return ValueTask.FromResult("{\"success\":false,\"error\":\"invalid_config\"}");
        }
    })
]);
