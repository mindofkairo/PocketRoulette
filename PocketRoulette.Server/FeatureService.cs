using PocketRoulette.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Servers;
using Path = System.IO.Path;

namespace PocketRoulette.Server;

// Keep one ledger, raid engine and lock across requests, routers and scopes.
[Injectable(InjectionType.Singleton)]
public sealed partial class FeatureService(ConfigService configs, ProfileHelper profiles, ItemHelper items, SaveServer saves)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly FeatureStore _store = new();
    private RaidEngine? _engine;
    private const string Roubles = "5449016a4bdc2d6f028b456f";

    public void Initialize(string directory)
    {
        _store.Initialize(Path.Combine(directory, "data", "player-state.json"));
        _engine = new RaidEngine(_store, new RouletteRoller(new Random()), StackLimit, () => DateTime.UtcNow);
#if POCKET_ROULETTE_TESTING
        InitializeTesting(directory);
#endif
    }
    private RaidEngine Engine => _engine ?? throw new InvalidOperationException("Pocket Roulette state is unavailable; check server logs.");

    public async Task<RaidUpdate> Begin(MongoId session, RaidBeginRequest request, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (!Guid.TryParse(request.RaidKey, out _) || request.GroupId?.Length > 180)
                throw new InvalidOperationException("Invalid raid identity.");
            var pmc = profiles.GetPmcProfile(session) ?? throw new InvalidOperationException("PMC profile unavailable.");
            var scav = profiles.GetScavProfile(session);
            var character = pmc.Id.ToString() == request.ProfileId ? pmc : scav?.Id.ToString() == request.ProfileId ? scav : null;
            if (character?.Inventory?.Items == null) throw new InvalidOperationException("Raid profile does not belong to this session.");
            request.GroupId ??= "";
            request.PartyIds ??= [];
            var config = EffectiveConfig(configs.Get(), request.ClientConfig);
            var isScav = character != pmc;
            var eligible = config.ChancePercent > 0 && (!isScav || config.ScavEnabled);
            var pockets = character.Inventory.Items.FirstOrDefault(item => item.SlotId == "Pockets"
                && item.ParentId == character.Inventory.Equipment?.ToString());
            var empty = pockets != null && !character.Inventory.Items.Any(item => item.ParentId == pockets.Id.ToString());
            var accountId = pmc.Id.ToString() ?? throw new InvalidOperationException("PMC profile has no ID.");
            foreach (var interrupted in _store.State.Raids.Values.Where(previous => previous.AccountId == accountId
                && previous.RaidKey != request.RaidKey && previous.Payment is { Committed: false }).ToList())
                await SettlePayment(session, pmc, interrupted, true, ct);
            var raid = Engine.Begin(accountId, request.ProfileId, character.Info?.Nickname ?? "A teammate", request,
                config, eligible, request.EmptyPockets && empty);
            if (!raid.BuyInChecked && raid.Payment == null)
            {
                var desired = raid.Eligible && !isScav && config.EnableBuyInSpins
                    ? Math.Clamp(request.BuyInCount, 0, config.BuyInSpins.MaxSpinsPerRaid) : 0;
                raid.BuyInChecked = true;
                if (desired > 0)
                {
                    var money = StashRoubles(pmc);
                    var price = (long)desired * config.BuyInSpins.PriceRoubles;
                    var debits = WalletRules.Plan(money.ToDictionary(item => item.Id.ToString(), Count), price);
                    if (debits == null)
                        AddNotice(raid, "purchase", $"Buy-in declined: {price:N0} stash roubles needed. Nothing was charged.");
                    else raid.Payment = new PaymentJournal { Spins = desired, Price = price, Debits = debits };
                }
                _store.Save();
            }
            if (raid.Payment is { Committed: false }) await SettlePayment(session, pmc, raid, false, ct);
            if (!isScav) Engine.GrantRecoveredCredits(raid);
            // A successful response must never expose grants that only exist in memory after a failed save/retry.
            _store.Save();
            return Engine.Snapshot(raid);
        }
        finally { _gate.Release(); }
    }

    public async Task<RaidUpdate> Action(MongoId session, RaidActionRequest request, string operation, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var account = profiles.GetPmcProfile(session)?.Id.ToString() ?? "";
            var engine = EngineFor(request.RaidId);
            var raid = engine.Require(account, request.RaidId);
            if (request.PartyIds != null) raid.CurrentPartyIds = request.PartyIds.Where(ConfigRules.IsItemId).Distinct().Take(24).ToList();
            if (operation is "menu" or "buy")
            {
                var pmc = profiles.GetPmcProfile(session) ?? throw new InvalidOperationException("PMC profile unavailable.");
                var sandbox = engine != Engine;
                if (operation == "buy")
                {
                    if (sandbox) throw new InvalidOperationException("Use simulated purchases in the sandbox.");
                    if (engine.PrepareMenuPurchase(raid, request.PurchaseId, request.ExpectedPrice, configs.Get(),
                        StashRoubles(pmc).ToDictionary(item => item.Id.ToString(), Count)))
                        await SettlePayment(session, pmc, raid, false, ct);
                }
                if (engine.Store.SaveNeedsRetry) engine.Store.Save();
                var reply = engine.Snapshot(raid);
                reply.Config = null!;
                reply.Menu = engine.Menu(raid, configs.Get(), StashRoubles(pmc).Sum(item => (long)Count(item)), sandbox);
                if (raid.CompletedPurchaseIds.Contains(request.PurchaseId)) reply.Menu.ConfirmedPurchaseId = request.PurchaseId;
                return reply;
            }
            switch (operation)
            {
                case "update":
                case "poll": engine.Poll(raid, request); break;
                case "complete": engine.Complete(raid, request.PlanId, request.Placement); break;
                case "offer": engine.OpenOffer(raid, request.PlanId); break;
                case "decide":
                    if (request.PartyIds != null) raid.CurrentPartyIds = request.PartyIds.Where(ConfigRules.IsItemId).Distinct().Take(24).ToList();
                    engine.Decide(raid, request.PlanId, request.Share); break;
                case "end": engine.End(raid); break;
                default: throw new InvalidOperationException("Unknown raid action.");
            }
            if (engine.Store.SaveNeedsRetry) engine.Store.Save();
            var update = engine.Snapshot(raid);
            update.Config = null!; // The begin response supplies the immutable per-raid config once.
            return update;
        }
        finally { _gate.Release(); }
    }

    public async Task<RaidUpdate> AuthorizeEvent(MongoId session, RaidActionRequest request, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var caller = profiles.GetPmcProfile(session)?.Id.ToString() ?? "";
            var engine = EngineFor(request.RaidId);
            if (!engine.Store.State.Raids.TryGetValue(request.RaidId, out var raid) || raid.Closed || !raid.Alive
                || !raid.Plans.TryGetValue(request.PlanId, out var plan) || plan.Completed
                || plan.Plan.Kind is not ("grenade" or "cursed")) throw new InvalidOperationException("Invalid event grant.");
            var partyCaller = raid.GroupId.Length > 0 && (engine.Store.State.Raids.Values.Any(member => member.AccountId == caller
                && member.GroupId == raid.GroupId && !member.Closed) || raid.GroupId.StartsWith(caller + ":", StringComparison.Ordinal));
            if (raid.AccountId != caller && !partyCaller) throw new InvalidOperationException("This event belongs to another party.");
            var current = configs.Get();
#if POCKET_ROULETTE_TESTING
            if (engine == _testingEngine) { DemandTesting(caller); current = raid.Config; }
#endif
            if (!current.EnableGrenadeEvent || plan.Plan.Kind == "cursed" && !current.EnableCursedJackpot)
                throw new InvalidOperationException("Live grenade events are disabled by the server.");
            plan.EventStartedUtc ??= DateTime.UtcNow;
            engine.Store.Save();
            return new RaidUpdate { RaidId = raid.Id, OwnerProfileId = raid.ProfileId, OwnerNickname = raid.Nickname, Config = raid.Config, Rolls = [plan.Plan] };
        }
        finally { _gate.Release(); }
    }

    public bool ValidateGrant(string account, string raidId, string planId, string itemId, string tpl, int count)
    {
        _gate.Wait();
        try
        {
            var raid = EngineFor(raidId).Require(account, raidId);
            return raid.Alive && raid.Plans.TryGetValue(planId, out var entry) && !entry.Completed
                && entry.Plan.Reward?.Tpl == tpl && entry.Plan.ItemId == itemId && entry.Plan.StackCount == count;
        }
        finally { _gate.Release(); }
    }

    private RaidEngine EngineFor(string raidId)
    {
#if POCKET_ROULETTE_TESTING
        if (_testingStore.State.Raids.TryGetValue(raidId, out var testingRaid))
        {
            DemandTesting(testingRaid.AccountId);
            return _testingEngine!;
        }
#endif
        return Engine;
    }

    private async Task SettlePayment(MongoId session, PmcData pmc, RaidLedger raid, bool carryCredit, CancellationToken ct)
    {
        if (raid.Payment == null || raid.Payment.Committed) return;
        if (saves.IsProfileInvalidOrUnloadable(session)) throw new InvalidOperationException("Cannot save buy-in payment for this profile.");
        var pending = WalletRules.PendingDebits(raid.Payment.Debits,
            pmc.Inventory!.Items!.ToDictionary(item => item.Id.ToString(), Count));
        foreach (var debit in pending)
        {
            var stack = pmc.Inventory!.Items!.FirstOrDefault(item => item.Id.ToString() == debit.ItemId);
            if (debit.After == 0) pmc.Inventory!.Items!.Remove(stack!);
            else { stack!.Upd ??= new Upd(); stack.Upd.StackObjectsCount = debit.After; }
        }
        await saves.SaveProfileAsync(session, ct);
        // SPT 4.1 updates its profile hash cache before writing. After an interrupted
        // write, another SaveProfileAsync can skip the disk write. Verify the debit
        // in the file SPT actually saves before committing any reward or credit.
        var savedProfile = await File.ReadAllTextAsync(Path.Combine("user", "profiles", $"{session}.json"), ct);
        WalletRules.VerifySavedPayment(savedProfile, raid.Payment.Debits);
        if (!carryCredit) Engine.CommitPurchase(raid);
        else
        {
            raid.Payment.Committed = true;
            raid.PaidRoubles += raid.Payment.Price;
            _store.State.Players[raid.AccountId].PaidSpinCredits += raid.Payment.Spins;
            _store.Save();
        }
    }
    private int StackLimit(string tpl)
    {
        if (!ConfigRules.IsItemId(tpl)) return 0;
        var template = items.GetItem(new MongoId(tpl));
        return template.Key && template.Value?.Properties != null
            ? Math.Max(1, (int)Math.Min(int.MaxValue, template.Value.Properties.StackMaxSize ?? 1)) : 0;
    }
    private static int Count(Item item) => Math.Max(0, Convert.ToInt32(item.Upd?.StackObjectsCount ?? 1));
    private static List<Item> StashRoubles(PmcData pmc)
    {
        var inventory = pmc.Inventory ?? throw new InvalidOperationException("Inventory unavailable for buy-in.");
        if (!inventory.Stash.HasValue || inventory.Items == null) return [];
        var parents = inventory.Items.ToDictionary(item => item.Id.ToString());
        var stash = inventory.Stash.Value.ToString();
        bool InStash(Item item)
        {
            var current = item.ParentId;
            var seen = new HashSet<string>();
            while (!string.IsNullOrEmpty(current) && seen.Add(current))
            {
                if (current == stash) return true;
                if (!parents.TryGetValue(current, out var parent)) return false;
                current = parent.ParentId;
            }
            return false;
        }
        return inventory.Items.Where(item => item.Template.ToString() == Roubles && InStash(item)).ToList();
    }
    private static void AddNotice(RaidLedger raid, string kind, string text) => raid.Notices.Add(new RaidNotice
    { Id = Guid.NewGuid().ToString("N"), Kind = kind, Text = text });

    private static PocketRouletteConfig EffectiveConfig(PocketRouletteConfig server, PocketRouletteConfig? local)
    {
        var config = RaidEngine.Clone(server);
        if (server.AllowClientOverrides && local != null)
        {
            config.Mode = local.Mode;
            config.ItemCount = local.ItemCount;
            config.ChancePercent = local.ChancePercent;
            config.EnableNotification = local.EnableNotification;
            config.DebugLogging = local.DebugLogging;
            config.AllowGroundDrop = local.AllowGroundDrop;
            config.ScavEnabled = local.ScavEnabled;
            config.ItemPool = local.ItemPool;
            config.PocketMessages = local.PocketMessages;
            config.GroundDropMessages = local.GroundDropMessages;
            config.MissedRewardMessages = local.MissedRewardMessages;
            config.ChanceMissMessages = local.ChanceMissMessages;
            config.UltraRareMessages = local.UltraRareMessages;
            config.UltraRareOddsComparisons = local.UltraRareOddsComparisons;
            config.FailureMessages = local.FailureMessages;
            config.MultiRollSummaryMessages = local.MultiRollSummaryMessages;
        }
        ConfigRules.Normalize(config, message => Console.WriteLine($"[PocketRoulette] {message}"));
        if (server.ChancePercent == 0) config.ChancePercent = 0;
        return config;
    }
}

