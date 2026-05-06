using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;

namespace PocketRoulette.Server;

public class InjectItemRequest : IRequestData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("tpl")]
    public string Tpl { get; set; } = string.Empty;
    
    [JsonPropertyName("parentId")]
    public string ParentId { get; set; } = string.Empty;
    
    [JsonPropertyName("slotId")]
    public string SlotId { get; set; } = string.Empty;
    
    [JsonPropertyName("location")]
    public InjectItemLocation Location { get; set; } = new();
}

public class InjectItemLocation
{
    [JsonPropertyName("x")]
    public int X { get; set; }
    
    [JsonPropertyName("y")]
    public int Y { get; set; }
    
    [JsonPropertyName("r")]
    public int R { get; set; }
    
    [JsonPropertyName("isSearched")]
    public bool IsSearched { get; set; }
}

[Injectable]
public class InjectRouter : StaticRouter
{
    public InjectRouter(JsonUtil jsonUtil, ProfileHelper profileHelper) : base(jsonUtil, GetRoutes(profileHelper))
    {
    }

    private static List<RouteAction> GetRoutes(ProfileHelper profileHelper)
    {
        return
        [
            new RouteAction<InjectItemRequest>(
                "/pocketroulette/inject",
                (url, request, sessionId, output) => InjectItem(profileHelper, request, sessionId)
            )
        ];
    }

    private static ValueTask<string> InjectItem(ProfileHelper profileHelper, InjectItemRequest request, MongoId sessionId)
    {
        try
        {
            PmcData? pmcData = profileHelper.GetPmcProfile(sessionId);

            if (pmcData?.Inventory?.Items == null)
            {
                Console.WriteLine($"[PocketRoulette] Failed to sync item: Could not access PMC inventory for profile {sessionId}");
                return new ValueTask<string>("{\"success\":false,\"error\":\"profile_not_found\"}");
            }

            pmcData.Inventory.Items.Add(new Item
            {
                Id = new MongoId(request.Id),
                Template = new MongoId(request.Tpl),
                ParentId = request.ParentId,
                SlotId = request.SlotId,
                Location = new ItemLocation
                {
                    X = request.Location.X,
                    Y = request.Location.Y,
                    R = (ItemRotation)request.Location.R,
                    IsSearched = request.Location.IsSearched
                }
            });

            Console.WriteLine($"[PocketRoulette] Synced injected item {request.Tpl} to PMC profile.");
            return new ValueTask<string>("{\"success\":true}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PocketRoulette] Error syncing item: {ex}");
            return new ValueTask<string>("{\"success\":false,\"error\":\"exception\"}");
        }
    }
}
