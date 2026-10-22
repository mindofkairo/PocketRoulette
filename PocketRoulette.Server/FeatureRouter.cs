using System.Text.Json;
using PocketRoulette.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;

namespace PocketRoulette.Server;

public sealed class BeginRequest : RaidBeginRequest, IRequestData;
public sealed class ActionRequest : RaidActionRequest, IRequestData;

[Injectable]
public sealed class FeatureRouter(JsonUtil json, FeatureService features) : StaticRouter(json, Routes(features))
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static List<RouteAction> Routes(FeatureService service)
    {
        var routes = new List<RouteAction>
        {
            new RouteAction<BeginRequest>("/pocketroulette/raid/begin", (url, request, session, output, ct) =>
                Respond(() => service.Begin(session, request, ct))),
            new RouteAction<ActionRequest>("/pocketroulette/event/authorize", (url, request, session, output, ct) =>
                Respond(() => service.AuthorizeEvent(session, request, ct)))
        };
        foreach (var operation in new[] { "update", "poll", "complete", "offer", "decide", "end", "menu", "buy" })
        {
            var action = operation;
            routes.Add(new RouteAction<ActionRequest>("/pocketroulette/raid/" + action, (url, request, session, output, ct) =>
                Respond(() => service.Action(session, request, action, ct))));
        }
        return routes;
    }
    private static async ValueTask<string> Respond(Func<Task<RaidUpdate>> action)
    {
        try { return JsonSerializer.Serialize(await action(), Options); }
        catch (Exception ex)
        {
            Console.WriteLine($"[PocketRoulette] Feature request failed: {ex.Message}");
            return JsonSerializer.Serialize(new RaidUpdate { Success = false, Error = ex.Message }, Options);
        }
    }
}
