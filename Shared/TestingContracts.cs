#if POCKET_ROULETTE_TESTING
#nullable disable
using System.Collections.Generic;
#if SERVER_JSON
using JsonProperty = System.Text.Json.Serialization.JsonPropertyNameAttribute;
#else
using JsonProperty = Newtonsoft.Json.JsonPropertyAttribute;
#endif

namespace PocketRoulette.Models
{
    public class TestingRequest
    {
        [JsonProperty("raidId")] public string RaidId { get; set; } = "";
        [JsonProperty("commandId")] public string CommandId { get; set; } = "";
        [JsonProperty("command")] public string Command { get; set; } = "";
        [JsonProperty("value")] public string Value { get; set; } = "";
        [JsonProperty("amount")] public double Amount { get; set; }
        [JsonProperty("samples")] public int Samples { get; set; } = 10000;
        [JsonProperty("seed")] public int Seed { get; set; } = 12345;
        [JsonProperty("emptyPockets")] public bool EmptyPockets { get; set; }
        [JsonProperty("partyIds")] public List<string> PartyIds { get; set; } = new List<string>();
        [JsonProperty("options")] public TestingOptions Options { get; set; } = new TestingOptions();
    }
    public sealed class TestingOptions
    {
        [JsonProperty("enableFeatures")] public bool EnableFeatures { get; set; }
        [JsonProperty("armGrenades")] public bool ArmGrenades { get; set; }
        [JsonProperty("chance")] public int? Chance { get; set; }
        [JsonProperty("count")] public int? Count { get; set; }
        [JsonProperty("mode")] public string Mode { get; set; } = "";
        [JsonProperty("placement")] public string Placement { get; set; } = "actual";
        [JsonProperty("groundFallback")] public bool? GroundFallback { get; set; }
        [JsonProperty("notifications")] public bool? Notifications { get; set; }
        [JsonProperty("cursedDelay")] public float? CursedDelay { get; set; }
        [JsonProperty("decisionSeconds")] public float? DecisionSeconds { get; set; }
    }
    public sealed class TestingStatus
    {
        public string Message { get; set; } = "";
        public int Pending { get; set; }
        public bool PityApplied { get; set; }
        public bool WinApplied { get; set; }
        public int SharingTeammates { get; set; }
        public double ChallengeProgress { get; set; }
        public string Placement { get; set; } = "actual";
        public Dictionary<string, int> Counts { get; set; } = new Dictionary<string, int>();
    }
}
#endif
