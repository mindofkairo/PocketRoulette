#if POCKET_ROULETTE_TESTING
using System.Text.Json;
using PocketRoulette.Models;

namespace PocketRoulette.Server;

public sealed partial class RaidEngine
{
    private readonly Dictionary<string, HashSet<string>> _testCommands = new();
    private readonly Dictionary<string, TestingStatus> _testStatus = new();

    public RaidLedger StartTesting(RaidLedger source, double luck, IEnumerable<string>? partyIds = null)
    {
        var raid = Begin(source.AccountId, source.ProfileId, source.Nickname, new RaidBeginRequest
        {
            RaidKey = Guid.NewGuid().ToString("N"), GroupId = source.GroupId,
            PartyIds = (partyIds ?? (source.CurrentPartyIds.Count > 0 ? source.CurrentPartyIds : source.PartyIds))
                .Where(ConfigRules.IsItemId).Distinct().Take(24).ToList()
        }, source.Config, false, false);
        raid.Eligible = true;
        Player(raid).StoredLuck = luck;
        Status(raid).Message = "Sandbox started. Test luck/progress are isolated. Loot and grenade damage are REAL.";
        return raid;
    }

    public TestingStatus? TestingStatusFor(RaidLedger raid)
    {
        if (!store.IsTesting) return null;
        var status = Status(raid);
        status.Pending = raid.Plans.Values.Count(plan => !plan.Completed);
        status.PityApplied = raid.PityApplied;
        status.WinApplied = raid.WinApplied;
        status.SharingTeammates = Party(raid).Count();
        return status;
    }
    private TestingStatus Status(RaidLedger raid)
    {
        if (!_testStatus.TryGetValue(raid.Id, out var status)) _testStatus[raid.Id] = status = new();
        return status;
    }

    public void Test(RaidLedger raid, TestingRequest request, PocketRouletteConfig serverConfig)
    {
        if (!store.IsTesting) throw new InvalidOperationException("Test commands require a separate sandbox.");
        if (!Guid.TryParse(request.CommandId, out _)) throw new InvalidOperationException("A unique command ID is required.");
        if (!_testCommands.TryGetValue(raid.Id, out var commands)) _testCommands[raid.Id] = commands = new();
        if (commands.Contains(request.CommandId)) return;
        if (commands.Count >= 500) throw new InvalidOperationException("500 test commands reached; leave and restart the sandbox.");
        if (raid.Plans.Values.Any(plan => !plan.Completed))
            throw new InvalidOperationException("Finish the pending reward/event/choice before starting another test.");
        if (raid.Plans.Count >= 1500) throw new InvalidOperationException("Test grant limit reached; leave and restart the sandbox.");
        var config = TestConfig(serverConfig, request.Options);
        if (request.Command == "force") config.Mode = "mixed";
        var items = roller.EligibleItems(config).Where(item => stackLimit(item.Tpl) > 0).ToList();
        if (items.Count == 0) throw new InvalidOperationException("The configured reward pool has no usable templates.");
        var status = Status(raid);
        status.Placement = request.Options.Placement;
        // Validate before applying configuration or spending a command ID.
        if (!double.IsFinite(request.Amount) || request.Amount < 0 || request.Amount > 1000000)
            throw new InvalidOperationException("Test amount must be finite and between 0 and 1,000,000.");
        raid.CurrentPartyIds = request.PartyIds.Where(ConfigRules.IsItemId).Distinct().Take(24).ToList();
        raid.Config = config;
        raid.EmptyBonus = config.EnableLuck && config.EnableEmptyPocketBonus && request.EmptyPockets ? config.EmptyPocketBonus.LuckPoints : 0;
        raid.LastHeartbeatUtc = Now;
        raid.Alive = true;
        switch (request.Command)
        {
            case "normal":
                FreshBatch(raid);
                raid.Eligible = config.ChancePercent > 0 && (raid.ProfileId == raid.AccountId || config.ScavEnabled);
                raid.ChanceMissed = !raid.Eligible || !roller.RunsThisRaid(config.ChancePercent);
                if (raid.Eligible)
                {
                    if (!raid.ChanceMissed)
                        for (var i = 0; i < config.ItemCount; i++) raid.StartingPlans.Add(AddSpin(raid, "raid", true).Plan.Id);
                    AssignChallenge(raid);
                    FinalizeLuck(raid);
                }
                status.Message = raid.ChanceMissed ? $"Normal raid: no starting rolls ({config.ChancePercent}% configured chance)."
                    : $"Normal raid: {config.ItemCount} starting roll(s), normal weighted outcomes.";
                break;
            case "force":
                var kind = request.Value == "epic" ? "ultrarare" : request.Value;
                RollPlan plan;
                if (new[] { "grenade", "cursed", "shared" }.Contains(kind))
                {
                    if ((kind is "grenade" or "cursed") && !config.EnableGrenadeEvent)
                        throw new InvalidOperationException("Arm live grenades in the sandbox first. They cause real damage.");
                    if (kind == "cursed" && !config.EnableCursedJackpot || kind == "shared" && !config.EnableSharedJackpots)
                        throw new InvalidOperationException("Enable optional features in the sandbox to test this disabled event.");
                    plan = roller.CreateEvent(kind, config, items, Luck(raid));
                }
                else
                {
                    var candidates = items.Where(item => item.Rarity == kind || item.Tpl == kind).ToList();
                    if (candidates.Count == 0) throw new InvalidOperationException("No matching rarity or TPL in the configured pool.");
                    // Valuable uses weighted items; chaos retains every available rarity.
                    var selection = Clone(config); selection.Mode = "chaos";
                    var reward = roller.Valuable(selection, candidates, Luck(raid), "common");
                    plan = new RollPlan { Reward = reward, Luck = Luck(raid),
                        OddsPercent = 100.0 * reward.Weight / candidates.Sum(item => (double)item.Weight) };
                }
                FreshBatch(raid);
                raid.Eligible = true;
                plan.Origin = "test-forced";
                if (request.Amount >= 1 && plan.Reward != null)
                    plan.Reward = CopyWithStack(plan.Reward, (int)request.Amount);
                raid.StartingPlans.Add(RegisterSpin(raid, plan).Plan.Id);
                status.Message = $"FORCED {kind}; chance and rarity selection bypassed. Normal delivery/event/acknowledgement paths remain active.";
                break;
            case "grenade":
                if (!config.EnableGrenadeEvent) throw new InvalidOperationException("Arm live grenades first; real damage applies.");
                var choice = config.GrenadeWheel.FirstOrDefault(entry => entry.Tpl == request.Value)
                    ?? throw new InvalidOperationException("Choose a grenade from the configured wheel.");
                FreshBatch(raid); raid.Eligible = true;
                var grenade = new RollPlan { Kind = "grenade", Luck = Luck(raid), Origin = "test-forced",
                    GrenadeTpl = choice.Tpl, GrenadeName = choice.Name, GrenadePrewarmSeconds = choice.PrewarmSeconds };
                raid.StartingPlans.Add(RegisterSpin(raid, grenade).Plan.Id);
                status.Message = $"Forced {choice.Name}. Normal event authorization, asset loading and Fika replication.";
                break;
            case "luck":
                Player(raid).StoredLuck = Math.Min(config.BadLuckProtection.MaxStoredLuck, request.Amount);
                status.Message = $"Test stored luck set to {Player(raid).StoredLuck:0.##}. Effective luck: {Luck(raid):0.##}.";
                break;
            case "decay":
                var before = Player(raid).StoredLuck;
                Player(raid).StoredLuck = LuckRules.Inactivity(config, before, request.Amount);
                LuckNotice(raid, before, Player(raid).StoredLuck, $"test: {request.Amount:0.##} inactive days");
                status.Message = $"Inactivity decay: {before:0.##} -> {Player(raid).StoredLuck:0.##}.";
                break;
            case "reset":
                FreshBatch(raid); Player(raid).StoredLuck = 0; raid.EmptyBonus = 0;
                raid.BoughtSpins = 0; raid.PaidRoubles = 0; raid.Payment = null;
                status.Counts.Clear(); status.Message = "Test luck/challenge/buy-in counters reset. Spawned loot stays in the game.";
                break;
            case "challenge":
                if (!ChallengeRules.Enabled(config)) throw new InvalidOperationException("Lucky challenges require both luck and challenges to be enabled.");
                var preset = config.LuckyChallenges.KillPresets.FirstOrDefault(entry => entry.Id == request.Value && entry.Enabled);
                if (preset == null && (request.Value is not ("survive" or "walk") || request.Amount < 1))
                    throw new InvalidOperationException("Choose survival/walking with a positive target or an enabled kill preset.");
                raid.StartedUtc = Now;
                raid.Challenge = preset == null ? new LuckyChallenge { Kind = request.Value, Target = request.Amount } : ChallengeRules.FromPreset(preset);
                raid.Challenge.Id = Guid.NewGuid().ToString("N");
                status.ChallengeProgress = 0;
                status.Message = $"Assigned {request.Value} challenge: {ChallengeRules.Description(raid.Challenge)}. Play normally or inject progress below.";
                break;
            case "progress":
                if (raid.Challenge == null || raid.Challenge.Completed) throw new InvalidOperationException("Assign an unfinished test challenge first.");
                var progress = raid.Challenge.Target * Math.Clamp(request.Amount, 0, 100) / 100;
                // Only the sandbox clock is advanced. The normal plausibility/completion checks still run.
                raid.StartedUtc = Now.AddSeconds(-Math.Max((Now - raid.StartedUtc).TotalSeconds,
                    raid.Challenge.Kind == "survive" ? progress : progress / 5));
                var simulated = new List<ChallengeKillReport>();
                if (raid.Challenge.Kind == "kills")
                {
                    simulated.AddRange(raid.Challenge.Kills);
                    var minimum = raid.Challenge.MinDistanceMeters;
                    var maximum = raid.Challenge.MaxDistanceMeters;
                    var distance = maximum > 0 ? (minimum + maximum) / 2 : minimum + 10;
                    while (simulated.Count < (int)progress) simulated.Add(new ChallengeKillReport
                    {
                        VictimId = NewItemId(), TargetType = raid.Challenge.TargetType == "any" ? "scav" : raid.Challenge.TargetType,
                        DistanceMeters = distance
                    });
                }
                Poll(raid, new RaidActionRequest { SurvivedSeconds = progress, WalkedMeters = progress,
                    ChallengeId = raid.Challenge.Id, ChallengeKills = simulated });
                status.ChallengeProgress = progress;
                status.Message = $"Injected {Math.Min(100, request.Amount):0}% challenge progress through normal Poll().";
                break;
            case "purchase":
                if (!config.EnableBuyInSpins) throw new InvalidOperationException("Buy-in spins are disabled in these rules.");
                if (config.BuyInSpins.MaxSpinsPerRaid == 0) throw new InvalidOperationException("The server's buy-in cap is zero.");
                var spins = Math.Clamp((int)request.Amount, 1, config.BuyInSpins.MaxSpinsPerRaid);
                var balance = request.Value == "empty" ? 0 : int.MaxValue;
                var price = (long)spins * config.BuyInSpins.PriceRoubles;
                var debits = WalletRules.Plan(new Dictionary<string, int> { [NewItemId()] = balance }, price);
                if (debits == null) status.Message = $"Simulated insufficient funds: {price:N0} needed. No spins issued.";
                else
                {
                    raid.Eligible = true;
                    raid.Payment = new PaymentJournal { Spins = spins, Price = price, Debits = debits };
                    CommitPurchase(raid);
                    status.Message = $"Simulated {spins} paid spin(s) through normal purchase/grant logic. Actual stash NOT charged.";
                }
                break;
            case "sample":
                Sample(raid, request, status, items);
                break;
            default: throw new InvalidOperationException("Unknown test command.");
        }
        foreach (var pending in raid.Plans.Values.Where(entry => !entry.Completed)) pending.Plan.TestPlacement = request.Options.Placement;
        commands.Add(request.CommandId);
        store.Save();
    }

    private void FreshBatch(RaidLedger raid)
    {
        raid.StartingPlans.Clear(); raid.PityApplied = false; raid.WinApplied = false;
        raid.ChanceMissed = false; raid.Challenge = null; raid.StartedUtc = Now;
        Status(raid).ChallengeProgress = 0;
    }
    private static PoolItem CopyWithStack(PoolItem reward, int count)
    {
        var copy = JsonSerializer.Deserialize<PoolItem>(JsonSerializer.Serialize(reward))!;
        copy.MinCount = copy.MaxCount = count;
        return copy;
    }
    public static PocketRouletteConfig TestConfig(PocketRouletteConfig source, TestingOptions options)
    {
        var config = Clone(source);
        if (options.EnableFeatures)
        {
            config.EnableLuck = config.EnableBadLuckProtection = config.EnableEmptyPocketBonus = true;
            config.EnableSharedJackpots = config.EnableLuckyChallenges = config.EnableBuyInSpins = true;
            config.EnableCursedJackpot = config.EnableGrenadeWheel = true;
        }
        // An explicit per-session arm switch is required even if the real server enables grenades.
        config.EnableGrenadeEvent = options.ArmGrenades;
        if (options.Chance.HasValue) config.ChancePercent = options.Chance.Value;
        if (options.Count.HasValue) config.ItemCount = options.Count.Value;
        if (options.Mode.Length > 0) config.Mode = options.Mode;
        if (options.GroundFallback.HasValue) config.AllowGroundDrop = options.GroundFallback.Value;
        if (options.Notifications.HasValue) config.EnableNotification = options.Notifications.Value;
        if (options.CursedDelay.HasValue) config.CursedJackpot.MinExplosionDelaySeconds = config.CursedJackpot.MaxExplosionDelaySeconds = options.CursedDelay.Value;
        if (options.DecisionSeconds.HasValue) config.SharedJackpots.DecisionSeconds = options.DecisionSeconds.Value;
        ConfigRules.Normalize(config);
        if (!new[] { "actual", "full", "ground", "miss" }.Contains(options.Placement))
            throw new InvalidOperationException("Unknown test placement.");
        return config;
    }
    private void Sample(RaidLedger raid, TestingRequest request, TestingStatus status, List<PoolItem> items)
    {
        var samples = Math.Clamp(request.Samples, 1, 10000);
        var sampling = new RouletteRoller(new Random(request.Seed));
        var counts = new Dictionary<string, int> { ["raids"] = samples, ["triggered"] = 0, ["missed"] = 0, ["challenges"] = 0, ["rolls"] = 0 };
        void Count(string key) => counts[key] = counts.GetValueOrDefault(key) + 1;
        var eligible = raid.Config.ChancePercent > 0 && (raid.ProfileId == raid.AccountId || raid.Config.ScavEnabled);
        for (var i = 0; i < samples; i++)
        {
            if (eligible && sampling.RunsThisRaid(raid.Config.ChancePercent))
            {
                Count("triggered");
                for (var j = 0; j < raid.Config.ItemCount; j++)
                {
                    var plan = sampling.Select(raid.Config, items, Luck(raid));
                    Count("rolls"); Count(plan.Kind == "item" ? plan.Reward.Rarity : plan.Kind);
                    if (plan.Kind == "grenade") Count("wheel:" + plan.GrenadeName);
                }
            }
            else Count("missed");
            if (eligible && ChallengeRules.Candidates(raid.Config).Count > 0
                && sampling.NextDouble() * 100 < LuckRules.ChallengeChance(raid.Config, Luck(raid))) Count("challenges");
        }
        status.Counts = counts;
        status.Message = $"Dry sample: {samples:N0} raids, seed {request.Seed}, fixed luck {Luck(raid):0.##}. "
            + $"Chance {raid.Config.ChancePercent}%; observed {100.0 * counts["triggered"] / samples:0.##}%. No loot, payments or luck gains.";
    }
}
#endif
