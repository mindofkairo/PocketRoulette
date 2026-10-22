#if POCKET_ROULETTE_TESTING
using System.Text.Json;
using PocketRoulette.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;

namespace PocketRoulette.Server;

public sealed class TestingSettings
{
    public bool Enabled { get; set; } = true;
    public List<string> AllowedProfileIds { get; set; } = new();
}

public sealed partial class FeatureService
{
    private readonly FeatureStore _testingStore = FeatureStore.ForTesting();
    private readonly Dictionary<string, string> _testSources = new();
    private RaidEngine? _testingEngine;
    private string _testingSettingsPath = "";

    private void InitializeTesting(string directory)
    {
        _testingEngine = new RaidEngine(_testingStore, new RouletteRoller(new Random()), StackLimit, () => DateTime.UtcNow);
        _testingSettingsPath = Path.Combine(directory, "config", "test-tools.jsonc");
        if (!File.Exists(_testingSettingsPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_testingSettingsPath)!);
            File.WriteAllText(_testingSettingsPath, """
                {
                  // Temporary test-build tools. Changes apply on the next request.
                  "enabled": true,
                  // Empty allows all authenticated players. Otherwise list allowed PMC profile IDs.
                  // Include the Fika host's PMC ID when testing guest grenades.
                  "allowedProfileIds": []
                }
                """);
        }
        Console.WriteLine("[PocketRoulette] TEMPORARY TEST TOOLS included. In raid: END. Server gate: config/test-tools.jsonc.");
    }
    private void DemandTesting(string account)
    {
        if (_testingEngine == null) throw new InvalidOperationException("Test tools did not initialize; check server startup logs.");
        var settings = JsonSerializer.Deserialize<TestingSettings>(File.ReadAllText(_testingSettingsPath), new JsonSerializerOptions
        { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        if (settings == null || !settings.Enabled || settings.AllowedProfileIds == null
            || settings.AllowedProfileIds.Count > 0 && !settings.AllowedProfileIds.Contains(account, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Test tools are disabled or this PMC is not allowed by config/test-tools.jsonc.");
    }
    public async Task<RaidUpdate> Testing(MongoId session, TestingRequest request, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var account = profiles.GetPmcProfile(session)?.Id.ToString() ?? throw new InvalidOperationException("PMC profile unavailable.");
            DemandTesting(account);
            if (request.Command == "start")
            {
                var source = Engine.Require(account, request.RaidId);
                var existing = _testingStore.State.Raids.Values.LastOrDefault(raid => raid.AccountId == account && !raid.Closed
                    && _testSources.GetValueOrDefault(raid.Id) == source.Id);
                var raid = existing ?? _testingEngine!.StartTesting(source, Engine.Snapshot(source).StoredLuck, request.PartyIds);
                _testSources[raid.Id] = source.Id;
                return _testingEngine!.Snapshot(raid);
            }
            var testing = _testingEngine!.Require(account, request.RaidId);
            var normal = Engine.Require(account, _testSources.GetValueOrDefault(testing.Id) ?? "");
            if (request.Command == "stop") _testingEngine.End(testing);
            else
            {
                _testingEngine.Test(testing, request, EffectiveConfig(configs.Get(), normal.Config));
                Console.WriteLine($"[PocketRoulette TEST] {request.Command}: {_testingEngine.Snapshot(testing).Testing.Message}");
            }
            return _testingEngine.Snapshot(testing);
        }
        finally { _gate.Release(); }
    }
}

public sealed class TestingBody : TestingRequest, IRequestData;

[Injectable]
public sealed class TestingRouter(JsonUtil json, FeatureService features) : StaticRouter(json,
[
    new RouteAction<TestingBody>("/pocketroulette/testing", async (url, request, session, output, ct) =>
    {
        try { return JsonSerializer.Serialize(await features.Testing(session, request, ct), Options); }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new RaidUpdate { Success = false, Error = ex.Message }, Options);
        }
    })
])
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
}
#endif
