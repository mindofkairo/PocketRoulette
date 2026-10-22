#nullable disable
using System.Collections.Generic;
#if SERVER_JSON
using JsonProperty = System.Text.Json.Serialization.JsonPropertyNameAttribute;
#else
using JsonProperty = Newtonsoft.Json.JsonPropertyAttribute;
#endif

namespace PocketRoulette.Models
{
    public sealed class LuckSettings
    {
        [JsonProperty("maxLuck")] public double MaxLuck { get; set; } = 20;
        [JsonProperty("rareWeightPerPoint")] public double RareWeightPerPoint { get; set; } = 0.10;
        [JsonProperty("ultraRareWeightPerPoint")] public double UltraRareWeightPerPoint { get; set; } = 0.20;
        [JsonProperty("positiveEventWeightPerPoint")] public double PositiveEventWeightPerPoint { get; set; } = 0.10;
        [JsonProperty("cursedDelayBiasPerPoint")] public double CursedDelayBiasPerPoint { get; set; } = 0.10;
        [JsonProperty("grenadeReductionPerPoint")] public double GrenadeReductionPerPoint { get; set; } = 0.10;
        [JsonProperty("challengeChancePerPoint")] public double ChallengeChancePerPoint { get; set; } = 0.05;
        [JsonProperty("showAtRoll")] public bool ShowAtRoll { get; set; } = true;
    }
    public sealed class BadLuckSettings
    {
        [JsonProperty("gainPerUnluckyRaid")] public double GainPerUnluckyRaid { get; set; } = 1;
        [JsonProperty("maxStoredLuck")] public double MaxStoredLuck { get; set; } = 10;
        [JsonProperty("winDecayPercent")] public double WinDecayPercent { get; set; } = 50;
        [JsonProperty("inactivityDecayPerDay")] public double InactivityDecayPerDay { get; set; } = 0.25;
        [JsonProperty("notifyChanges")] public bool NotifyChanges { get; set; } = true;
    }
    public sealed class EmptyPocketSettings
    {
        [JsonProperty("luckPoints")] public double LuckPoints { get; set; } = 2;
    }
    public sealed class CursedSettings
    {
        [JsonProperty("weight")] public double Weight { get; set; } = 1;
        [JsonProperty("minExplosionDelaySeconds")] public float MinExplosionDelaySeconds { get; set; } = 6;
        [JsonProperty("maxExplosionDelaySeconds")] public float MaxExplosionDelaySeconds { get; set; } = 10;
        [JsonProperty("explanationSeconds")] public float ExplanationSeconds { get; set; } = 2;
        [JsonProperty("minimumRarity")] public string MinimumRarity { get; set; } = "rare";
        [JsonProperty("grenadeTpl")] public string GrenadeTpl { get; set; } = "5448be9a4bdc2dfd2f8b456a";
    }
    public sealed class SharingSettings
    {
        [JsonProperty("weight")] public double Weight { get; set; } = 1;
        [JsonProperty("decisionSeconds")] public float DecisionSeconds { get; set; } = 20;
        [JsonProperty("winnerMinimumRarity")] public string WinnerMinimumRarity { get; set; } = "ultrarare";
        [JsonProperty("bonusMinimumRarity")] public string BonusMinimumRarity { get; set; } = "rare";
        [JsonProperty("giftMinimumRarity")] public string GiftMinimumRarity { get; set; } = "rare";
        [JsonProperty("notifyGroup")] public bool NotifyGroup { get; set; } = true;
        [JsonProperty("shareMessages")] public List<string> ShareMessages { get; set; } = new List<string>();
        [JsonProperty("keepMessages")] public List<string> KeepMessages { get; set; } = new List<string>();
    }
    public sealed class ChallengeSettings
    {
        [JsonProperty("assignmentChancePercent")] public double AssignmentChancePercent { get; set; } = 30;
        [JsonProperty("survivalEnabled")] public bool SurvivalEnabled { get; set; } = true;
        [JsonProperty("survivalSeconds")] public double SurvivalSeconds { get; set; } = 300;
        [JsonProperty("walkingEnabled")] public bool WalkingEnabled { get; set; } = true;
        [JsonProperty("walkingMeters")] public double WalkingMeters { get; set; } = 750;
        [JsonProperty("bonusSpins")] public int BonusSpins { get; set; } = 1;
        [JsonProperty("notifyProgress")] public bool NotifyProgress { get; set; } = true;
        [JsonProperty("killPresets")] public List<KillChallengePreset> KillPresets { get; set; } = new List<KillChallengePreset>();
    }
    public sealed class KillChallengePreset
    {
        [JsonProperty("id")] public string Id { get; set; } = "";
        [JsonProperty("name")] public string Name { get; set; } = "";
        [JsonProperty("enabled")] public bool Enabled { get; set; } = true;
        [JsonProperty("kills")] public int Kills { get; set; } = 1;
        [JsonProperty("targetType")] public string TargetType { get; set; } = "any";
        [JsonProperty("minDistanceMeters")] public double MinDistanceMeters { get; set; }
        [JsonProperty("maxDistanceMeters")] public double MaxDistanceMeters { get; set; }
    }
    public sealed class BuyInSettings
    {
        [JsonProperty("priceRoubles")] public int PriceRoubles { get; set; } = 25000;
        [JsonProperty("maxSpinsPerRaid")] public int MaxSpinsPerRaid { get; set; } = 2;
    }
    public sealed class GrenadeChoice
    {
        [JsonProperty("tpl")] public string Tpl { get; set; } = "";
        [JsonProperty("name")] public string Name { get; set; } = "";
        [JsonProperty("weight")] public int Weight { get; set; } = 1;
        [JsonProperty("dangerous")] public bool Dangerous { get; set; } = true;
        [JsonProperty("prewarmSeconds")] public float PrewarmSeconds { get; set; } = 0;
    }
}
