using PocketRoulette.Models;

namespace PocketRoulette.Server;

public sealed class FeatureState
{
    public int Version { get; set; } = 1;
    public Dictionary<string, PlayerLuckState> Players { get; set; } = new();
    public Dictionary<string, RaidLedger> Raids { get; set; } = new();
}
public sealed class PlayerLuckState
{
    public double StoredLuck { get; set; }
    public DateTime LastRaidUtc { get; set; }
    public string LatestRaidId { get; set; } = "";
    public int PaidSpinCredits { get; set; }
}
public sealed class RaidLedger
{
    public string Id { get; set; } = "";
    public string RaidKey { get; set; } = "";
    public string AccountId { get; set; } = "";
    public string ProfileId { get; set; } = "";
    public string Nickname { get; set; } = "";
    public string GroupId { get; set; } = "";
    public List<string> PartyIds { get; set; } = new();
    public List<string> CurrentPartyIds { get; set; } = new();
    public PocketRouletteConfig Config { get; set; } = new();
    public double EmptyBonus { get; set; }
    public DateTime StartedUtc { get; set; }
    public DateTime LastHeartbeatUtc { get; set; }
    public bool Closed { get; set; }
    public bool Alive { get; set; } = true;
    public bool Eligible { get; set; }
    public bool ChanceMissed { get; set; }
    public bool PityApplied { get; set; }
    public bool WinApplied { get; set; }
    public List<string> StartingPlans { get; set; } = new();
    public Dictionary<string, PlanLedger> Plans { get; set; } = new();
    public List<RaidNotice> Notices { get; set; } = new();
    public LuckyChallenge? Challenge { get; set; }
    public PaymentJournal? Payment { get; set; }
    public int BoughtSpins { get; set; }
    public long PaidRoubles { get; set; }
    public bool BuyInChecked { get; set; }
    public HashSet<string> CompletedPurchaseIds { get; set; } = new();
}
public sealed class PlanLedger
{
    public DateTime CreatedUtc { get; set; }
    public RollPlan Plan { get; set; } = new();
    public bool Completed { get; set; }
    public string Placement { get; set; } = "";
    public DateTime? ChoiceOpenedUtc { get; set; }
    public DateTime? EventStartedUtc { get; set; }
    public bool? Shared { get; set; }
    public List<string> Recipients { get; set; } = new();
    public PoolItem? ReservedBonus { get; set; }
}
public sealed class PaymentJournal
{
    public string Id { get; set; } = "";
    public int Spins { get; set; }
    public long Price { get; set; }
    public bool Committed { get; set; }
    public List<MoneyDebit> Debits { get; set; } = new();
}
public sealed class MoneyDebit
{
    public string ItemId { get; set; } = "";
    public int Before { get; set; }
    public int After { get; set; }
}
