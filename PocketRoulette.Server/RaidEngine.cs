using System.Text.Json;
using PocketRoulette.Models;

namespace PocketRoulette.Server;

public sealed partial class RaidEngine(FeatureStore store, RouletteRoller roller, Func<string, int> stackLimit, Func<DateTime> clock)
{
    public FeatureStore Store => store;
    private DateTime Now => clock();
    public RaidLedger Begin(string account, string profile, string nickname, RaidBeginRequest request,
        PocketRouletteConfig config, bool eligible, bool empty)
    {
        if (!store.State.Players.TryGetValue(account, out var player))
            store.State.Players[account] = player = new();
        if (store.State.Raids.TryGetValue(player.LatestRaidId, out var existing) && existing.RaidKey == request.RaidKey)
        {
            if (existing.Closed) throw new InvalidOperationException("This raid has already ended.");
            return existing;
        }
        if (existing != null) { existing.Closed = true; existing.Alive = false; }
        var raid = new RaidLedger
        {
            Id = Guid.NewGuid().ToString("N"), RaidKey = request.RaidKey, AccountId = account, ProfileId = profile,
            Nickname = nickname, GroupId = request.GroupId, PartyIds = request.PartyIds.Where(ConfigRules.IsItemId).Distinct().Take(24).ToList(),
            Config = Clone(config), StartedUtc = Now, LastHeartbeatUtc = Now, Eligible = eligible,
            EmptyBonus = config.EnableLuck && config.EnableEmptyPocketBonus && empty ? config.EmptyPocketBonus.LuckPoints : 0
        };
        store.State.Raids[raid.Id] = raid;
        raid.CurrentPartyIds = raid.PartyIds.ToList();
        player.LatestRaidId = raid.Id;
        if (config.EnableLuck && config.EnableBadLuckProtection)
        {
            var before = player.StoredLuck;
            var days = player.LastRaidUtc == default ? 0 : (Now - player.LastRaidUtc).TotalDays;
            player.StoredLuck = Math.Min(config.BadLuckProtection.MaxStoredLuck, LuckRules.Inactivity(config, before, days));
            LuckNotice(raid, before, player.StoredLuck, "inactivity decay");
        }
        player.LastRaidUtc = Now;
        if (!eligible) { store.Save(); return raid; }
        var items = Items(raid);
        if (items.Count == 0 && !(config.EnableGrenadeEvent && config.GrenadeWeight > 0))
        { raid.Eligible = false; Notice(raid, "error", "No valid rewards or events are available in this pool."); store.Save(); return raid; }
        raid.ChanceMissed = !roller.RunsThisRaid(config.ChancePercent);
        if (!raid.ChanceMissed)
            for (var i = 0; i < config.ItemCount; i++) raid.StartingPlans.Add(AddSpin(raid, "raid", true).Plan.Id);
        AssignChallenge(raid);
        FinalizeLuck(raid);
        Prune();
        store.Save();
        return raid;
    }

    public RaidUpdate Snapshot(RaidLedger raid) => new()
    {
#if POCKET_ROULETTE_TESTING
        Testing = TestingStatusFor(raid),
#endif
        RaidId = raid.Id, Config = raid.Config, Luck = Luck(raid), StoredLuck = Player(raid).StoredLuck,
        EmptyPocketLuck = raid.EmptyBonus, ChanceMissed = raid.ChanceMissed, BoughtSpins = raid.BoughtSpins,
        PaidRoubles = raid.PaidRoubles, Rolls = raid.Plans.Values.Where(plan => !plan.Completed).Select(plan => plan.Plan).ToList(),
        Notices = raid.Notices.ToList(), Challenge = raid.Challenge
    };

    public RaidLedger Require(string account, string id)
    {
        if (!store.State.Raids.TryGetValue(id, out var raid) || raid.AccountId != account || raid.Closed
            || Now - raid.StartedUtc > TimeSpan.FromHours(3)) throw new InvalidOperationException("This raid is no longer active.");
        return raid;
    }

    public void Poll(RaidLedger raid, RaidActionRequest request)
    {
        raid.LastHeartbeatUtc = Now;
        raid.Alive = request.Alive;
        if (request.PartyIds != null) raid.CurrentPartyIds = request.PartyIds.Where(ConfigRules.IsItemId).Distinct().Take(24).ToList();
        var changed = false;
        foreach (var offer in raid.Plans.Values.Where(plan => plan.Plan.Kind == "shared" && !plan.Completed
            && plan.Shared == null && plan.ChoiceOpenedUtc.HasValue).ToList())
            if (!raid.Alive || Now >= offer.ChoiceOpenedUtc!.Value.AddSeconds(raid.Config.SharedJackpots.DecisionSeconds))
            { Decide(raid, offer.Plan.Id, false); changed = true; }
        changed |= AdvanceChallenge(raid, request);
        if (changed) store.Save();
    }

    public void Complete(RaidLedger raid, string planId, string placement)
    {
        if (!raid.Plans.TryGetValue(planId, out var ledger)) throw new InvalidOperationException("Unknown reward grant.");
        if (ledger.Completed) return;
        if (ledger.Plan.Kind == "shared") throw new InvalidOperationException("A sharing offer needs a decision before delivery.");
        if (!new[] { "pocket", "ground", "picked", "expired", "missed", "failed", "grenade" }.Contains(placement))
            throw new InvalidOperationException("Unknown placement result.");
        ledger.Completed = true;
        ledger.Placement = placement;
        var received = ledger.Plan.Kind == "cursed" ? placement == "picked" : placement == "pocket" || placement == "ground";
        if (received && ledger.Plan.Reward != null && LuckRules.Rank(ledger.Plan.Reward.Rarity) >= 3 && !raid.WinApplied)
        {
            raid.WinApplied = true;
            if (raid.Config.EnableLuck && raid.Config.EnableBadLuckProtection)
            {
                var before = Player(raid).StoredLuck;
                Player(raid).StoredLuck = LuckRules.Win(raid.Config, before);
                LuckNotice(raid, before, Player(raid).StoredLuck, "good-reward decay");
            }
        }
        if (ledger.Plan.ParentId.Length > 0 && raid.Plans.TryGetValue(ledger.Plan.ParentId, out var parent))
        { parent.Completed = true; parent.Placement = placement; }
        FinalizeLuck(raid);
        store.Save();
    }

    public PlanLedger OpenOffer(RaidLedger raid, string id)
    {
        var offer = raid.Plans.GetValueOrDefault(id) ?? throw new InvalidOperationException("Unknown jackpot offer.");
        if (offer.Plan.Kind != "shared") throw new InvalidOperationException("This is not a shared jackpot.");
        if (offer.ChoiceOpenedUtc == null && !offer.Completed)
        {
            offer.ChoiceOpenedUtc = Now;
            offer.Recipients = Party(raid).Select(member => member.Id).ToList();
            offer.Plan.TeammateCount = offer.Recipients.Count;
            offer.Plan.DecisionSeconds = raid.Config.SharedJackpots.DecisionSeconds;
            if (raid.Config.SharedJackpots.NotifyGroup)
                foreach (var member in Party(raid)) Notice(member, "party", $"{raid.Nickname} won a shared jackpot and is deciding whether to share!");
            if (offer.Recipients.Count == 0)
            {
                Decide(raid, id, false);
            }
            store.Save();
        }
        // Retrying an open offer must not restart the original decision window.
        if (offer.ChoiceOpenedUtc.HasValue)
            offer.Plan.DecisionSeconds = (float)Math.Max(0,
                (offer.ChoiceOpenedUtc.Value.AddSeconds(raid.Config.SharedJackpots.DecisionSeconds) - Now).TotalSeconds);
        return offer;
    }

    public void Decide(RaidLedger raid, string id, bool share)
    {
        var offer = raid.Plans.GetValueOrDefault(id) ?? throw new InvalidOperationException("Unknown jackpot offer.");
        if (offer.Plan.Kind != "shared" || offer.ChoiceOpenedUtc == null)
            throw new InvalidOperationException("Open the jackpot offer before deciding.");
        if (offer.Shared.HasValue) return;
        share = share && raid.Alive && Now <= offer.ChoiceOpenedUtc.Value.AddSeconds(raid.Config.SharedJackpots.DecisionSeconds);
        var recipients = offer.Recipients.Select(recipientId => store.State.Raids.GetValueOrDefault(recipientId))
            .Where(member => member != null && member.Eligible && member.Config.EnableSharedJackpots && Active(member)
                && member.GroupId == raid.GroupId && raid.CurrentPartyIds.Contains(member.ProfileId))
            .Cast<RaidLedger>().ToList();
        // Do not forfeit the bonus if every eligible teammate has left during the choice.
        share = share && recipients.Count > 0;
        offer.Shared = share;
        var decisionMessage = SharedDecisionMessage(raid.Config.SharedJackpots, share, raid.Nickname);
        offer.Plan.DecisionMade = share;
        offer.Completed = true;
        // Parent stays unresolved for luck until the main prize has actually been received.
        var main = AddReward(raid, offer.Plan.Reward, "jackpot-main");
        main.Plan.Luck = offer.Plan.Luck;
        main.Plan.ParentId = id;
        offer.Completed = false;
        if (!share)
        {
            var bonus = AddReward(raid, offer.ReservedBonus!, "jackpot-kept");
            bonus.Plan.Luck = offer.Plan.Luck;
        }
        else
        {
            foreach (var member in recipients)
            {
                var candidates = member.Config.ItemPool.Where(item => stackLimit(item.Tpl) > 0).ToList();
                if (candidates.Count == 0) { Notice(member, "party", $"{raid.Nickname} shared, but your item pool has no valid gift."); continue; }
                var config = Clone(member.Config);
                config.Mode = "mixed";
                var minimum = config.SharedJackpots.GiftMinimumRarity;
                if (!candidates.Any(item => LuckRules.Rank(item.Rarity) >= LuckRules.Rank(minimum)))
                    minimum = candidates.OrderByDescending(item => LuckRules.Rank(item.Rarity)).First().Rarity;
                AddReward(member, roller.Valuable(config, candidates, Luck(member), minimum), "shared-gift");
                Notice(member, "party", (raid.Config.SharedJackpots.NotifyGroup ? decisionMessage + " " : "")
                    + "Your shared jackpot gift is rolling now.");
            }
        }
        Notice(raid, "party", share ? decisionMessage + " You keep the main prize; eligible teammates receive personal gifts."
            : offer.Recipients.Count == 0 ? NoSharingRecipients()
            : decisionMessage + " The main prize and bonus prize are yours.");
        if (raid.Config.SharedJackpots.NotifyGroup)
            foreach (var recipientId in offer.Recipients)
                if (store.State.Raids.TryGetValue(recipientId, out var member) && Active(member) && !share)
                    Notice(member, "party", decisionMessage);
        store.Save();
    }

    private static string SharedDecisionMessage(SharingSettings settings, bool shared, string nickname)
    {
        var messages = shared ? settings.ShareMessages : settings.KeepMessages;
        // Cosmetic messages must not consume the seeded loot RNG.
        var message = messages.Count > 0 ? messages[Random.Shared.Next(messages.Count)]
            : shared ? "{user} shared their jackpot!" : "{user} kept their jackpot.";
        var name = string.IsNullOrWhiteSpace(nickname) ? "A teammate" : nickname;
        return message.Replace("{user}", name).Replace("(user)", name);
    }

    public void CommitPurchase(RaidLedger raid)
    {
        if (raid.Payment == null || raid.Payment.Committed) return;
        if (!raid.Eligible) throw new InvalidOperationException("Paid spins cannot be issued for a disabled or empty roulette pool.");
        raid.Payment.Committed = true;
        raid.BoughtSpins += raid.Payment.Spins;
        raid.PaidRoubles += raid.Payment.Price;
        if (raid.Payment.Id.Length > 0) raid.CompletedPurchaseIds.Add(raid.Payment.Id);
        for (var i = 0; i < raid.Payment.Spins; i++) AddSpin(raid, "buy-in", true);
        Notice(raid, "purchase", $"Bought {raid.Payment.Spins} bonus spin(s) for {raid.Payment.Price:N0} stash roubles.");
        store.Save();
    }

    public void GrantRecoveredCredits(RaidLedger raid)
    {
        if (!raid.Eligible || Player(raid).PaidSpinCredits <= 0) return;
        var amount = Math.Min(20, Player(raid).PaidSpinCredits);
        for (var i = 0; i < amount; i++) AddSpin(raid, "recovered-buy-in", true);
        Player(raid).PaidSpinCredits -= amount;
        Notice(raid, "purchase", $"Recovered {amount} paid spin(s) from an interrupted purchase. No additional charge.");
        store.Save();
    }

    public void End(RaidLedger raid)
    {
        raid.Closed = true;
        raid.Alive = false;
        store.Save();
    }

    public double Luck(RaidLedger raid) => LuckRules.Effective(raid.Config,
        raid.Config.EnableBadLuckProtection ? Player(raid).StoredLuck : 0, raid.EmptyBonus);

    private PlayerLuckState Player(RaidLedger raid) => store.State.Players[raid.AccountId];
    private List<PoolItem> Items(RaidLedger raid) => roller.EligibleItems(raid.Config).Where(item => stackLimit(item.Tpl) > 0).ToList();
    private bool Active(RaidLedger raid) => !raid.Closed && raid.Alive && Now - raid.StartedUtc < TimeSpan.FromHours(3);
    private string NoSharingRecipients()
    {
#if POCKET_ROULETTE_TESTING
        if (store.IsTesting)
            return "No eligible teammates are in your test sandbox. Each participating teammate must press END and select Enter sandbox with shared jackpots enabled. Players in normal raid mode cannot receive sandbox gifts. You keep both prizes.";
#endif
        return "No eligible teammates are available to share with. You automatically keep both jackpot prizes.";
    }
    private IEnumerable<RaidLedger> Party(RaidLedger raid) => string.IsNullOrEmpty(raid.GroupId) ? [] : store.State.Raids.Values
        .Where(member => member.AccountId != raid.AccountId && member.GroupId == raid.GroupId && member.Eligible
            && member.Config.EnableSharedJackpots && Active(member)
            && raid.CurrentPartyIds.Contains(member.ProfileId))
        .GroupBy(member => member.AccountId).Select(group => group.Last());

    private PlanLedger AddSpin(RaidLedger raid, string origin, bool events)
    {
        var plan = roller.Select(raid.Config, Items(raid), Luck(raid), events);
        plan.Origin = origin;
        return RegisterSpin(raid, plan);
    }
    private PlanLedger RegisterSpin(RaidLedger raid, RollPlan plan)
    {
        var ledger = Register(raid, plan);
        if (plan.Kind == "shared")
        {
            ledger.ReservedBonus = roller.Valuable(raid.Config, Items(raid), plan.Luck, raid.Config.SharedJackpots.BonusMinimumRarity);
            plan.BonusReward = ledger.ReservedBonus;
        }
        return ledger;
    }
    private PlanLedger AddReward(RaidLedger raid, PoolItem reward, string origin) =>
        Register(raid, new RollPlan { Reward = reward, Kind = "item", Origin = origin, Luck = Luck(raid) });
    private PlanLedger Register(RaidLedger raid, RollPlan plan)
    {
#if POCKET_ROULETTE_TESTING
        if (store.IsTesting) plan.TestPlacement = Status(raid).Placement;
#endif
        plan.Id = Guid.NewGuid().ToString("N");
        plan.ItemId = NewItemId();
        plan.HazardId = plan.Kind == "cursed" ? NewItemId() : plan.ItemId;
        if (plan.Reward != null) plan.StackCount = roller.StackCount(plan.Reward, stackLimit(plan.Reward.Tpl));
        var entry = new PlanLedger { Plan = plan, CreatedUtc = Now };
        raid.Plans.Add(plan.Id, entry);
        return entry;
    }
    private void AssignChallenge(RaidLedger raid)
    {
        var config = raid.Config;
        if (!ChallengeRules.Enabled(config) || Items(raid).Count == 0 || roller.NextDouble() * 100 >= LuckRules.ChallengeChance(config, Luck(raid))) return;
        var types = ChallengeRules.Candidates(config);
        if (types.Count == 0) return;
        raid.Challenge = types[(int)(roller.NextDouble() * types.Count)];
        raid.Challenge.Id = Guid.NewGuid().ToString("N");
    }
    private void FinalizeLuck(RaidLedger raid)
    {
        if (!raid.Eligible || raid.PityApplied || raid.WinApplied || !raid.Config.EnableLuck || !raid.Config.EnableBadLuckProtection) return;
        if (raid.StartingPlans.Any(id => !raid.Plans[id].Completed)) return;
        // Technical failures must not become a repeatable source of luck.
        if (raid.StartingPlans.Any(id => raid.Plans[id].Placement == "failed")) return;
        raid.PityApplied = true;
        var before = Player(raid).StoredLuck;
        Player(raid).StoredLuck = LuckRules.Unlucky(raid.Config, before);
        LuckNotice(raid, before, Player(raid).StoredLuck, "bad-luck protection");
    }
    private void LuckNotice(RaidLedger raid, double before, double after, string reason)
    {
        if (Math.Abs(before - after) < 0.005 || !raid.Config.BadLuckProtection.NotifyChanges) return;
        Notice(raid, "luck", $"Stored luck {before:0.##} -> {after:0.##} ({reason}).");
    }
    private static void Notice(RaidLedger raid, string kind, string text)
    {
        raid.Notices.Add(new RaidNotice { Id = Guid.NewGuid().ToString("N"), Kind = kind, Text = text });
        if (raid.Notices.Count > 64) raid.Notices.RemoveAt(0);
    }
    private void Prune()
    {
        foreach (var old in store.State.Raids.Values.Where(raid => (raid.Closed || Now - raid.StartedUtc > TimeSpan.FromHours(3))
            && (raid.Payment == null || raid.Payment.Committed) && Now - raid.StartedUtc > TimeSpan.FromDays(2)).ToList())
            store.State.Raids.Remove(old.Id);
        foreach (var account in store.State.Raids.Values.GroupBy(raid => raid.AccountId).ToList())
            foreach (var old in account.Where(raid => raid.Closed && (raid.Payment == null || raid.Payment.Committed))
                .OrderByDescending(raid => raid.StartedUtc).Skip(3).ToList()) store.State.Raids.Remove(old.Id);
    }
    public static PocketRouletteConfig Clone(PocketRouletteConfig config) =>
        JsonSerializer.Deserialize<PocketRouletteConfig>(JsonSerializer.Serialize(config))!;
    public static string NewItemId() => Guid.NewGuid().ToString("N")[..24];
}
