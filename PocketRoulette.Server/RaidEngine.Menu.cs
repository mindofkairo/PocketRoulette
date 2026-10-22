using PocketRoulette.Models;

namespace PocketRoulette.Server;

public sealed partial class RaidEngine
{
    public PlayerMenuInfo Menu(RaidLedger raid, PocketRouletteConfig current, long balance, bool sandbox)
    {
        var reason = sandbox ? "Real purchases are disabled in the test sandbox."
            : raid.Closed || !raid.Alive ? "Extra spins are unavailable after the raid ends."
            : raid.ProfileId != raid.AccountId ? "Extra spins are available to PMCs."
            : !raid.Eligible || !current.EnableBuyInSpins || !raid.Config.EnableBuyInSpins || current.ChancePercent == 0 ? "Extra spins are disabled for this raid."
            : raid.BoughtSpins >= current.BuyInSpins.MaxSpinsPerRaid ? "All extra spins for this raid have been used."
            : raid.Plans.Values.Any(plan => !plan.Completed) ? "Finish your current roll first."
            : balance < current.BuyInSpins.PriceRoubles ? "Not enough stash roubles." : "";
        return new PlayerMenuInfo
        {
            StoredLuck = Player(raid).StoredLuck, EmptyPocketLuck = raid.EmptyBonus, EffectiveLuck = Luck(raid),
            LuckEnabled = raid.Config.EnableLuck, InactivityDecayPerDay = raid.Config.EnableLuck && raid.Config.EnableBadLuckProtection ? raid.Config.BadLuckProtection.InactivityDecayPerDay : 0,
            WinDecayPercent = raid.Config.EnableLuck && raid.Config.EnableBadLuckProtection ? raid.Config.BadLuckProtection.WinDecayPercent : 0,
            PriceRoubles = current.BuyInSpins.PriceRoubles,
            RemainingSpins = Math.Max(0, current.BuyInSpins.MaxSpinsPerRaid - raid.BoughtSpins), StashRoubles = balance,
            CanBuy = reason.Length == 0, Sandbox = sandbox, BuyUnavailableReason = reason,
            History = store.State.Raids.Values.Where(previous => previous.AccountId == raid.AccountId)
                .SelectMany(previous => previous.Plans.Values.Select(entry => new RollHistoryEntry
                {
                    Id = entry.Plan.Id, Name = entry.Plan.Kind == "shared" ? "Shared jackpot" : entry.Plan.Reward?.Name ?? entry.Plan.GrenadeName,
                    Rarity = entry.Plan.Reward?.Rarity ?? "event", Origin = entry.Plan.Origin, Count = entry.Plan.StackCount,
                    Placement = entry.Completed ? entry.Placement : "pending", OddsPercent = entry.Plan.OddsPercent, Luck = entry.Plan.Luck,
                    RolledUtc = entry.CreatedUtc == default ? previous.StartedUtc : entry.CreatedUtc
                })).OrderByDescending(entry => entry.RolledUtc).Take(40).ToList()
        };
    }

    public bool PrepareMenuPurchase(RaidLedger raid, string purchaseId, int expectedPrice, PocketRouletteConfig current,
        Dictionary<string, int> wallet)
    {
        if (!Guid.TryParse(purchaseId, out _)) throw new InvalidOperationException("Invalid purchase identifier.");
        if (raid.CompletedPurchaseIds.Contains(purchaseId)) return false;
        if (raid.Payment is { Committed: false })
        {
            if (raid.Payment.Id != purchaseId) throw new InvalidOperationException("Confirm your previous purchase before buying another spin.");
            return true;
        }
        if (expectedPrice != current.BuyInSpins.PriceRoubles) throw new InvalidOperationException("The price changed. Reopen the menu before buying.");
        var menu = Menu(raid, current, wallet.Values.Sum(value => (long)value), false);
        if (!menu.CanBuy) throw new InvalidOperationException(menu.BuyUnavailableReason);
        var debits = WalletRules.Plan(wallet, current.BuyInSpins.PriceRoubles)
            ?? throw new InvalidOperationException("Not enough stash roubles.");
        raid.Payment = new PaymentJournal { Id = purchaseId, Spins = 1, Price = current.BuyInSpins.PriceRoubles, Debits = debits };
        store.Save();
        return true;
    }
}
