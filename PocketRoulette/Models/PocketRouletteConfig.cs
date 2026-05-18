using System.Collections.Generic;
using Newtonsoft.Json;

namespace PocketRoulette.Models
{
    public class PocketRouletteConfig
    {
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
        public bool AllowGroundDrop { get; set; } = false;

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

        public static PocketRouletteConfig CreateDefault()
        {
            return new PocketRouletteConfig
            {
                Mode = "mixed",
                ItemCount = 1,
                ChancePercent = 100,
                EnableNotification = true,
                DebugLogging = false,
                AllowClientOverrides = false,
                AllowGroundDrop = false,
                ScavEnabled = true,
                PocketMessages = new List<string>
                {
                    "The Pocket Gods smile upon you. Search your pockets...",
                    "Something just materialized in your pockets. Don't question it, just check."
                },
                GroundDropMessages = new List<string>
                {
                    "Your pockets overfloweth! Something tumbled to the ground at your feet."
                },
                MissedRewardMessages = new List<string>
                {
                    "Your pockets were full. You missed out on {item}."
                },
                ChanceMissMessages = new List<string>
                {
                    "Pocket Roulette spun the wheel, but luck was not on your side."
                },
                UltraRareMessages = new List<string>
                {
                    "JACKPOT! Something incredible appeared! (Odds: ~{odds} - rarer than {comparison})"
                },
                UltraRareOddsComparisons = new List<string>
                {
                    "finding a GPU on the floor of Interchange",
                    "a Scav being friendly",
                    "surviving Labs as a solo"
                },
                FailureMessages = new List<string>
                {
                    "Pocket Roulette failed to spawn {item}."
                },
                MultiRollSummaryMessages = new List<string>
                {
                    "Pocket Roulette rolled {total} rewards: {pocketCount} in pockets{groundPart}{missedPart}{failedPart}.{bestPart}"
                },
                ItemPool = new List<PoolItem>
                {
                    new PoolItem { Tpl = "5448ff904bdc2d6f028b456e", Name = "Army Crackers", Weight = 30, Rarity = "common", Width = 1, Height = 1 },
                    new PoolItem { Tpl = "57347b8b24597737dd42e192", Name = "Classic Matches", Weight = 28, Rarity = "common", Width = 1, Height = 1 },
                    new PoolItem { Tpl = "5c13cef886f774072e618e82", Name = "Toilet Paper", Weight = 28, Rarity = "common", Width = 1, Height = 1 },
                    new PoolItem { Tpl = "544fb25a4bdc2dfb738b4567", Name = "Aseptic Bandage", Weight = 14, Rarity = "uncommon", Width = 1, Height = 1 },
                    new PoolItem { Tpl = "5755356824597772cb798962", Name = "AI-2 Medkit", Weight = 14, Rarity = "uncommon", Width = 1, Height = 1 },
                }
            };
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
