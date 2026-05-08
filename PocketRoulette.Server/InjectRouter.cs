using System;
using System.Collections.Concurrent;
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

    [JsonPropertyName("stackCount")]
    public int StackCount { get; set; } = 1;
    
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

public class RegisterGroundItemRequest : IRequestData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("tpl")]
    public string Tpl { get; set; } = string.Empty;

    [JsonPropertyName("stackCount")]
    public int StackCount { get; set; } = 1;

    [JsonPropertyName("position")]
    public RegisterGroundItemVector Position { get; set; } = new();

    [JsonPropertyName("rotation")]
    public RegisterGroundItemRotation Rotation { get; set; } = new();
}

public class RegisterGroundItemVector
{
    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    [JsonPropertyName("z")]
    public float Z { get; set; }
}

public class RegisterGroundItemRotation
{
    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    [JsonPropertyName("z")]
    public float Z { get; set; }

    [JsonPropertyName("w")]
    public float W { get; set; }
}

public record RegisteredGroundItem(
    string Id,
    string Tpl,
    int StackCount,
    RegisterGroundItemVector Position,
    RegisterGroundItemRotation Rotation);

[Injectable]
public class InjectRouter : StaticRouter
{
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, RegisteredGroundItem>> RegisteredGroundItems = new();
    private static bool _debugLogging;

    public InjectRouter(JsonUtil jsonUtil, ProfileHelper profileHelper) : base(jsonUtil, GetRoutes(profileHelper))
    {
    }

    public void SetDebugLogging(bool enabled)
    {
        _debugLogging = enabled;
    }

    private static List<RouteAction> GetRoutes(ProfileHelper profileHelper)
    {
        return
        [
            new RouteAction<InjectItemRequest>(
                "/pocketroulette/inject",
                (url, request, sessionId, output) => InjectItem(profileHelper, request, sessionId)
            ),
            new RouteAction<RegisterGroundItemRequest>(
                "/pocketroulette/register-ground",
                (url, request, sessionId, output) => RegisterGroundItem(request, sessionId)
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

            if (string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.Tpl))
            {
                Console.WriteLine("[PocketRoulette] Failed to sync item: missing id or tpl");
                return new ValueTask<string>("{\"success\":false,\"error\":\"bad_request\"}");
            }

            if (pmcData.Inventory.Items.Any(item => string.Equals(item.Id.ToString(), request.Id, StringComparison.OrdinalIgnoreCase)))
            {
                DebugLog($"Injected item {request.Id} is already in the PMC profile.");
                return new ValueTask<string>("{\"success\":true}");
            }

            var item = new Item
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
            };

            if (request.StackCount > 1)
            {
                item.Upd = new Upd
                {
                    StackObjectsCount = request.StackCount
                };
            }

            pmcData.Inventory.Items.Add(item);

            DebugLog($"Synced injected item {request.Tpl} to PMC profile.");
            return new ValueTask<string>("{\"success\":true}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PocketRoulette] Error syncing item: {ex}");
            return new ValueTask<string>("{\"success\":false,\"error\":\"exception\"}");
        }
    }

    private static ValueTask<string> RegisterGroundItem(RegisterGroundItemRequest request, MongoId sessionId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.Tpl))
            {
                Console.WriteLine("[PocketRoulette] Failed to register ground item: missing id or tpl");
                return new ValueTask<string>("{\"success\":false,\"error\":\"bad_request\"}");
            }

            var sessionGroundItems = RegisteredGroundItems.GetOrAdd(sessionId.ToString(), _ => new ConcurrentDictionary<string, RegisteredGroundItem>());
            sessionGroundItems[request.Id] = new RegisteredGroundItem(request.Id, request.Tpl, Math.Max(1, request.StackCount), request.Position, request.Rotation);

            DebugLog($"Registered ground item {request.Tpl} ({request.Id}) x{Math.Max(1, request.StackCount)} for raid pickup tracking.");
            return new ValueTask<string>("{\"success\":true}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PocketRoulette] Error registering ground item: {ex}");
            return new ValueTask<string>("{\"success\":false,\"error\":\"exception\"}");
        }
    }

    private static void DebugLog(string message)
    {
        if (_debugLogging)
            Console.WriteLine($"[PocketRoulette] {message}");
    }
}
