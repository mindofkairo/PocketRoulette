using System.Text.Json.Serialization;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;

namespace PocketRoulette.Server;

public class RewardRequest : IRequestData
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("tpl")] public string Tpl { get; set; } = string.Empty;
    [JsonPropertyName("stackCount")] public int StackCount { get; set; } = 1;
    [JsonPropertyName("profileId")] public string ProfileId { get; set; } = string.Empty;
    [JsonPropertyName("raidId")] public string RaidId { get; set; } = string.Empty;
    [JsonPropertyName("planId")] public string PlanId { get; set; } = string.Empty;
}

[Injectable]
public class RewardValidator(ProfileHelper profiles, ItemHelper items, ConfigService configs, FeatureService features)
{
    public string Validate(RewardRequest request, MongoId session)
    {
        if (!ConfigRules.IsItemId(request.Id) || !ConfigRules.IsItemId(request.Tpl))
            return Failure("invalid_item_id");
        var pmc = profiles.GetPmcProfile(session);
        var scav = profiles.GetScavProfile(session);
        var profile = string.Equals(scav?.Id.ToString(), request.ProfileId, StringComparison.OrdinalIgnoreCase) ? scav
            : string.Equals(pmc?.Id.ToString(), request.ProfileId, StringComparison.OrdinalIgnoreCase) ? pmc : null;
        var inventory = profile?.Inventory;
        if (inventory?.Items == null)
            return Failure("profile_not_found");
        if (!features.ValidateGrant(pmc!.Id.ToString() ?? "", request.RaidId, request.PlanId, request.Id, request.Tpl, request.StackCount))
            return Failure("invalid_reward_grant");
        if (inventory.Items.Any(item => string.Equals(item.Id.ToString(), request.Id, StringComparison.OrdinalIgnoreCase)))
            return Failure("duplicate_item_id");
        var template = items.GetItem(new MongoId(request.Tpl));
        if (!template.Key || template.Value?.Properties == null)
            return Failure("unknown_template");
        if (request.StackCount < 1 || request.StackCount > (template.Value.Properties.StackMaxSize ?? 1))
            return Failure("invalid_stack_count");
        var config = configs.Get();
        if (!config.AllowClientOverrides && !config.ItemPool.Any(item => item.Tpl == request.Tpl))
            return Failure("template_not_in_pool");
        // EFT owns the live inventory and the final raid save. Never insert into a
        // persistent profile here: doing both creates duplicate IDs at raid end.
        if (config.DebugLogging)
            Console.WriteLine($"[PocketRoulette] Validated reward {request.Tpl} ({request.Id}) x{request.StackCount}.");
        return "{\"success\":true}";
    }

    private static string Failure(string error) => $"{{\"success\":false,\"error\":\"{error}\"}}";
}

[Injectable]
public class InjectRouter(JsonUtil jsonUtil, RewardValidator validator) : StaticRouter(jsonUtil,
[
    Route("/pocketroulette/inject", validator),
    Route("/pocketroulette/register-ground", validator)
])
{
    private static RouteAction Route(string path, RewardValidator validator) => new RouteAction<RewardRequest>(path,
        (url, request, session, output, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            try { return ValueTask.FromResult(validator.Validate(request, session)); }
            catch (Exception ex)
            {
                Console.WriteLine($"[PocketRoulette] Reward validation failed: {ex.Message}");
                return ValueTask.FromResult("{\"success\":false,\"error\":\"validation_failed\"}");
            }
        });
}
