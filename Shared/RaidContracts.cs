#nullable disable
using System.Collections.Generic;
#if SERVER_JSON
using JsonProperty = System.Text.Json.Serialization.JsonPropertyNameAttribute;
#else
using JsonProperty = Newtonsoft.Json.JsonPropertyAttribute;
#endif

namespace PocketRoulette.Models
{
    public class RaidBeginRequest
    {
        [JsonProperty("raidKey")] public string RaidKey { get; set; } = "";
        [JsonProperty("profileId")] public string ProfileId { get; set; } = "";
        [JsonProperty("groupId")] public string GroupId { get; set; } = "";
        [JsonProperty("partyIds")] public List<string> PartyIds { get; set; } = new List<string>();
        [JsonProperty("emptyPockets")] public bool EmptyPockets { get; set; }
        [JsonProperty("buyInCount")] public int BuyInCount { get; set; }
        [JsonProperty("clientConfig")] public PocketRouletteConfig ClientConfig { get; set; }
    }
    public class RaidActionRequest
    {
        [JsonProperty("raidId")] public string RaidId { get; set; } = "";
        [JsonProperty("planId")] public string PlanId { get; set; } = "";
        [JsonProperty("placement")] public string Placement { get; set; } = "";
        [JsonProperty("share")] public bool Share { get; set; }
        [JsonProperty("alive")] public bool Alive { get; set; } = true;
        [JsonProperty("survivedSeconds")] public double SurvivedSeconds { get; set; }
        [JsonProperty("walkedMeters")] public double WalkedMeters { get; set; }
        [JsonProperty("challengeId")] public string ChallengeId { get; set; } = "";
        [JsonProperty("challengeKills")] public List<ChallengeKillReport> ChallengeKills { get; set; } = new List<ChallengeKillReport>();
        [JsonProperty("partyIds")] public List<string> PartyIds { get; set; }
        [JsonProperty("purchaseId")] public string PurchaseId { get; set; } = "";
        [JsonProperty("expectedPrice")] public int ExpectedPrice { get; set; }
    }
    public sealed class RaidUpdate
    {
#if POCKET_ROULETTE_TESTING
        public TestingStatus Testing { get; set; }
#endif
        public bool Success { get; set; } = true;
        public string Error { get; set; } = "";
        public string RaidId { get; set; } = "";
        public PocketRouletteConfig Config { get; set; }
        public string OwnerProfileId { get; set; } = "";
        public string OwnerNickname { get; set; } = "";
        public double Luck { get; set; }
        public double StoredLuck { get; set; }
        public double EmptyPocketLuck { get; set; }
        public bool ChanceMissed { get; set; }
        public int BoughtSpins { get; set; }
        public long PaidRoubles { get; set; }
        public List<RollPlan> Rolls { get; set; } = new List<RollPlan>();
        public List<RaidNotice> Notices { get; set; } = new List<RaidNotice>();
        public LuckyChallenge Challenge { get; set; }
        public PlayerMenuInfo Menu { get; set; }
    }
    public sealed class RollPlan
    {
#if POCKET_ROULETTE_TESTING
        public string TestPlacement { get; set; } = "actual";
#endif
        public string Id { get; set; } = "";
        public string ItemId { get; set; } = "";
        public string HazardId { get; set; } = "";
        public string Kind { get; set; } = "item";
        public string Origin { get; set; } = "raid";
        public string ParentId { get; set; } = "";
        public PoolItem Reward { get; set; }
        public PoolItem BonusReward { get; set; }
        public bool? DecisionMade { get; set; }
        public int StackCount { get; set; } = 1;
        public double Luck { get; set; }
        public double OddsPercent { get; set; }
        public string GrenadeTpl { get; set; } = "";
        public string GrenadeName { get; set; } = "grenade";
        public float GrenadePrewarmSeconds { get; set; }
        public float ExplosionDelaySeconds { get; set; }
        public float DecisionSeconds { get; set; }
        public int TeammateCount { get; set; }
    }
    public sealed class RaidNotice
    {
        public string Id { get; set; } = "";
        public string Kind { get; set; } = "info";
        public string Text { get; set; } = "";
    }
    public sealed class LuckyChallenge
    {
        public string Id { get; set; } = "";
        public string Kind { get; set; } = "";
        public double Target { get; set; }
        public string Name { get; set; } = "";
        public string TargetType { get; set; } = "any";
        public double MinDistanceMeters { get; set; }
        public double MaxDistanceMeters { get; set; }
        public List<ChallengeKillReport> Kills { get; set; } = new List<ChallengeKillReport>();
        public bool Completed { get; set; }
    }
    public sealed class ChallengeKillReport
    {
        public string VictimId { get; set; } = "";
        public string TargetType { get; set; } = "";
        public double DistanceMeters { get; set; }
    }
    public sealed class PartyContext
    {
        public string GroupId { get; set; } = "";
        public List<string> MemberIds { get; set; } = new List<string>();
    }
}
