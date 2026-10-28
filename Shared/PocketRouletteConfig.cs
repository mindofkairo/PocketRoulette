#nullable disable
using System.Collections.Generic;
using System.IO;
#if SERVER_JSON
using System.Text.Json;
using JsonProperty = System.Text.Json.Serialization.JsonPropertyNameAttribute;
#else
using Newtonsoft.Json;
#endif

namespace PocketRoulette.Models
{
    public class PocketRouletteConfig
    {
        [JsonProperty("enableLuck")] public bool EnableLuck { get; set; } = true;
        [JsonProperty("enableBadLuckProtection")] public bool EnableBadLuckProtection { get; set; } = true;
        [JsonProperty("enableEmptyPocketBonus")] public bool EnableEmptyPocketBonus { get; set; } = true;
        [JsonProperty("enableSharedJackpots")] public bool EnableSharedJackpots { get; set; } = true;
        [JsonProperty("enableLuckyChallenges")] public bool EnableLuckyChallenges { get; set; } = true;
        [JsonProperty("enableBuyInSpins")] public bool EnableBuyInSpins { get; set; } = true;
        [JsonProperty("enableCursedJackpot")] public bool EnableCursedJackpot { get; set; } = true;
        [JsonProperty("enableGrenadeWheel")] public bool EnableGrenadeWheel { get; set; } = true;
        [JsonProperty("luck")] public LuckSettings Luck { get; set; } = new LuckSettings();
        [JsonProperty("badLuckProtection")] public BadLuckSettings BadLuckProtection { get; set; } = new BadLuckSettings();
        [JsonProperty("emptyPocketBonus")] public EmptyPocketSettings EmptyPocketBonus { get; set; } = new EmptyPocketSettings();
        [JsonProperty("cursedJackpot")] public CursedSettings CursedJackpot { get; set; } = new CursedSettings();
        [JsonProperty("sharedJackpots")] public SharingSettings SharedJackpots { get; set; } = new SharingSettings();
        [JsonProperty("luckyChallenges")] public ChallengeSettings LuckyChallenges { get; set; } = new ChallengeSettings();
        [JsonProperty("buyInSpins")] public BuyInSettings BuyInSpins { get; set; } = new BuyInSettings();
        [JsonProperty("grenadeWheel")] public List<GrenadeChoice> GrenadeWheel { get; set; } = new List<GrenadeChoice>();
        [JsonProperty("mode")]
        public string Mode { get; set; } = "mixed";

        [JsonProperty("itemCount")]
        public int ItemCount { get; set; } = 1;

        [JsonProperty("chancePercent")]
        public int ChancePercent { get; set; } = 100;

        [JsonProperty("itemPool")]
        public List<PoolItem> ItemPool { get; set; } = new List<PoolItem>();

        [JsonProperty("enableNotification")]
        public bool EnableNotification { get; set; } = true;

        [JsonProperty("debugLogging")]
        public bool DebugLogging { get; set; } = false;

        [JsonProperty("allowClientOverrides")]
        public bool AllowClientOverrides { get; set; } = false;

        [JsonProperty("allowGroundDrop")]
        public bool AllowGroundDrop { get; set; } = true;

        [JsonProperty("scavEnabled")]
        public bool ScavEnabled { get; set; } = true;

        [JsonProperty("pocketMessages")]
        public List<string> PocketMessages { get; set; } = new List<string>();

        [JsonProperty("groundDropMessages")]
        public List<string> GroundDropMessages { get; set; } = new List<string>();

        [JsonProperty("missedRewardMessages")]
        public List<string> MissedRewardMessages { get; set; } = new List<string>();

        [JsonProperty("chanceMissMessages")]
        public List<string> ChanceMissMessages { get; set; } = new List<string>();

        [JsonProperty("ultraRareMessages")]
        public List<string> UltraRareMessages { get; set; } = new List<string>();

        [JsonProperty("ultraRareOddsComparisons")]
        public List<string> UltraRareOddsComparisons { get; set; } = new List<string>();

        [JsonProperty("failureMessages")]
        public List<string> FailureMessages { get; set; } = new List<string>();

        [JsonProperty("multiRollSummaryMessages")]
        public List<string> MultiRollSummaryMessages { get; set; } = new List<string>();

        [JsonProperty("enableGrenadeEvent")]
        public bool EnableGrenadeEvent { get; set; } = false;

        [JsonProperty("grenadeWeight")]
        public int GrenadeWeight { get; set; } = 5;

        [JsonProperty("grenadeSpawnDelaySeconds")]
        public float GrenadeSpawnDelaySeconds { get; set; } = 0.75f;

        [JsonProperty("grenadeTpl")]
        public string GrenadeTpl { get; set; } = "5448be9a4bdc2dfd2f8b456a";

        [JsonProperty("grenadeTimeSpentSeconds")]
        public float GrenadeTimeSpentSeconds { get; set; } = 0f;

        [JsonProperty("grenadeMessages")]
        public List<string> GrenadeMessages { get; set; } = new List<string>();

        public static PocketRouletteConfig CreateDefault()
        {
            using (var stream = typeof(PocketRouletteConfig).Assembly.GetManifestResourceStream("PocketRoulette.DefaultConfig.json"))
            using (var reader = new StreamReader(stream))
            {
#if SERVER_JSON
                return JsonSerializer.Deserialize<PocketRouletteConfig>(reader.ReadToEnd(), new JsonSerializerOptions
                { ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
#else
                return JsonConvert.DeserializeObject<PocketRouletteConfig>(reader.ReadToEnd());
#endif
            }
        }
    }

    public class PoolItem
    {
        [JsonProperty("tpl")]
        public string Tpl { get; set; } = "";

        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [JsonProperty("weight")]
        public int Weight { get; set; } = 1;

        [JsonProperty("rarity")]
        public string Rarity { get; set; } = "common";

        [JsonProperty("width")]
        public int Width { get; set; } = 1;

        [JsonProperty("height")]
        public int Height { get; set; } = 1;

        [JsonProperty("minCount")]
        public int MinCount { get; set; } = 1;

        [JsonProperty("maxCount")]
        public int MaxCount { get; set; } = 1;
    }
}

