using System.Text.Json.Serialization;

namespace PocketRoulette.Server.Models;

public class PocketRouletteConfig
{
    [JsonPropertyName("mode")]
    [JsonPropertyOrder(1)]
    public string Mode { get; set; } = "mixed";

    [JsonPropertyName("itemCount")]
    [JsonPropertyOrder(2)]
    public int ItemCount { get; set; } = 1;

    [JsonPropertyName("chancePercent")]
    [JsonPropertyOrder(3)]
    public int ChancePercent { get; set; } = 100;

    [JsonPropertyName("enableNotification")]
    [JsonPropertyOrder(4)]
    public bool EnableNotification { get; set; } = true;

    [JsonPropertyName("debugLogging")]
    [JsonPropertyOrder(5)]
    public bool DebugLogging { get; set; } = false;

    [JsonPropertyName("allowGroundDrop")]
    [JsonPropertyOrder(6)]
    public bool AllowGroundDrop { get; set; } = false;

    [JsonPropertyName("scavEnabled")]
    [JsonPropertyOrder(7)]
    public bool ScavEnabled { get; set; } = true;

    [JsonPropertyName("pocketMessages")]
    [JsonPropertyOrder(8)]
    public List<string> PocketMessages { get; set; } = [];

    [JsonPropertyName("groundDropMessages")]
    [JsonPropertyOrder(9)]
    public List<string> GroundDropMessages { get; set; } = [];

    [JsonPropertyName("missedRewardMessages")]
    [JsonPropertyOrder(10)]
    public List<string> MissedRewardMessages { get; set; } = [];

    [JsonPropertyName("chanceMissMessages")]
    [JsonPropertyOrder(11)]
    public List<string> ChanceMissMessages { get; set; } = [];

    [JsonPropertyName("ultraRareMessages")]
    [JsonPropertyOrder(12)]
    public List<string> UltraRareMessages { get; set; } = [];

    [JsonPropertyName("ultraRareOddsComparisons")]
    [JsonPropertyOrder(13)]
    public List<string> UltraRareOddsComparisons { get; set; } = [];

    [JsonPropertyName("failureMessages")]
    [JsonPropertyOrder(14)]
    public List<string> FailureMessages { get; set; } = [];

    [JsonPropertyName("multiRollSummaryMessages")]
    [JsonPropertyOrder(15)]
    public List<string> MultiRollSummaryMessages { get; set; } = [];

    [JsonPropertyName("itemPool")]
    [JsonPropertyOrder(100)]
    public List<PoolItem> ItemPool { get; set; } = [];

    public static PocketRouletteConfig CreateDefault()
    {
        return new PocketRouletteConfig
        {
            Mode = "mixed",
            ItemCount = 1,
            ChancePercent = 100,
            EnableNotification = true,
            DebugLogging = false,
            AllowGroundDrop = true,
            ScavEnabled = true,
            PocketMessages =
            [
                "The Pocket Gods smile upon you. Search your pockets...",
                "Something just materialized in your pockets. Don't question it, just check.",
                "Surprise! The loot fairy left you a little something. Check your pockets.",
                "Your pockets are feeling heavier today...",
            ],
            GroundDropMessages =
            [
                "Your pockets overfloweth! Something tumbled to the ground at your feet.",
                "No room in your pockets! An item clattered to the floor nearby.",
                "Something fell out of your full pockets. Check the ground!",
            ],
            MissedRewardMessages =
            [
                "Your pockets were full. You missed out on {item}.",
                "No pocket space. {item} vanished before you could grab it.",
                "Pocket Roulette rolled {item}, but your pockets were full.",
            ],
            ChanceMissMessages =
            [
                "Pocket Roulette spun the wheel, but luck was not on your side.",
                "The pocket gods looked away this raid.",
                "Nothing appeared in your pockets this time.",
            ],
            UltraRareMessages =
            [
                "JACKPOT! Something incredible appeared! (Odds: ~{odds} - rarer than {comparison})",
                "You lucky rat! You struck gold! (Odds: ~{odds} - {comparison})",
            ],
            UltraRareOddsComparisons =
            [
                "finding a GPU on the floor of Interchange",
                "a Scav being friendly",
                "surviving Labs as a solo",
                "a raider dropping a keycard",
                "a peaceful day in Tarkov",
            ],
            FailureMessages =
            [
                "Pocket Roulette failed to spawn {item}.",
            ],
            MultiRollSummaryMessages =
            [
                "Pocket Roulette rolled {total} rewards: {pocketCount} in pockets{groundPart}{missedPart}{failedPart}.{bestPart}",
            ],
            ItemPool = GetDefaultItemPool()
        };
    }

    private static List<PoolItem> GetDefaultItemPool()
    {
        return
        [
            new("5448ff904bdc2d6f028b456e", "Army Crackers", 30, "common", 1, 1),
            new("57347b8b24597737dd42e192", "Classic Matches", 28, "common", 1, 1),
            new("5c13cef886f774072e618e82", "Toilet Paper", 28, "common", 1, 1),
            new("573476d324597737da2adc13", "Malboro Cigarettes", 26, "common", 1, 1),
            new("5734770f24597738025ee254", "Strike Cigarettes", 26, "common", 1, 1),
            new("56742c284bdc2d98058b456d", "Crickent Lighter", 25, "common", 1, 1),
            new("56742c2e4bdc2d95058b456d", "Zibbo Lighter", 22, "common", 1, 1),
            new("5bc9c29cd4351e003562b8a3", "Can of Sprats", 25, "common", 1, 1),
            new("57347c5b245977448d35f6e1", "Bolts", 24, "common", 1, 1),
            new("59e35ef086f7741777737012", "Pack of Screws", 24, "common", 1, 1),
            new("5734795124597738002c6176", "Insulating Tape", 22, "common", 1, 1),
            new("57347c1124597737fb1379e3", "Duct Tape", 22, "common", 1, 1),
            new("5e2af29386f7746d4159f077", "KEKTAPE Duct Tape", 18, "common", 1, 1),
            new("5c06779c86f77426e00dd782", "Bundle of Wires", 22, "common", 1, 1),
            new("5672cb124bdc2d1a0f8b4568", "AA Battery", 22, "common", 1, 1),
            new("5672cb304bdc2dc2088b456a", "D Size Battery", 20, "common", 1, 1),
            new("590a3c0a86f774385a33c450", "Spark Plug", 18, "common", 1, 1),
            new("5d1b392c86f77425243e98fe", "Light Bulb", 18, "common", 1, 1),
            new("5e2af37686f774755a234b65", "SurvL Lighter", 16, "common", 1, 1),
            new("5e2af2bc86f7746d3f3c33fc", "Hunting Matches", 16, "common", 1, 1),
            new("59e3577886f774176a362503", "Pack of Sugar", 20, "common", 1, 1),
            new("59e35abd86f7741778269d82", "Sodium Bicarbonate", 18, "common", 1, 1),
            new("5734773724597737fd047c14", "Condensed Milk", 18, "common", 1, 1),
            new("57505f6224597709a92585a9", "Alyonka Chocolate Bar", 20, "common", 1, 1),
            new("544fb6cc4bdc2d34748b456e", "Slickers Chocolate Bar", 20, "common", 1, 1),
            new("5751487e245977207e26a315", "Emelya Rye Croutons", 18, "common", 1, 1),
            new("57347d7224597744596b4e72", "Small Tushonka", 18, "common", 1, 1),
            new("575062b524597720a31c09a1", "Can of Ice Green Tea", 16, "common", 1, 1),
            new("5751435d24597720a27126d1", "Can of Max Energy", 16, "common", 1, 1),
            new("5751496424597720a27126da", "Can of Hot Rod", 16, "common", 1, 1),
            new("60098b1705871270cd5352a1", "Emergency Water Ration", 15, "common", 1, 1),
            new("693bfb50d5c25889e701d444", "Nuts Can", 15, "common", 1, 1),

            new("56d59d3ad2720bdb418b4577", "1x 9x19mm Pst gzh", 20, "common", 1, 1, 1, 45),
            new("56dff3afd2720bba668b4567", "1x 5.45x39mm PS gs", 18, "common", 1, 1, 1, 45),
            new("560d5e524bdc2d25448b4571", "1x 12/70 7mm Buckshot", 15, "common", 1, 1, 1, 45),

            new("5449016a4bdc2d6f028b456f", "1 Rouble", 25, "common", 1, 1, 1, 20000),

            new("544fb25a4bdc2dfb738b4567", "Aseptic Bandage", 14, "uncommon", 1, 1),
            new("5751a25924597722c463c472", "Army Bandage", 14, "uncommon", 1, 1),
            new("544fb3364bdc2d34748b456a", "Immobilizing Splint", 13, "uncommon", 1, 1),
            new("5af0454c86f7746bf20992e8", "Aluminum Splint", 12, "uncommon", 1, 1),
            new("5e831507ea0a7c419c2f9bd9", "Esmarch Tourniquet", 12, "uncommon", 1, 1),
            new("544fb37f4bdc2dee738b4567", "Analgin Painkillers", 12, "uncommon", 1, 1),
            new("5755356824597772cb798962", "AI-2 Medkit", 14, "uncommon", 1, 1),
            new("590c695186f7741e566b64a2", "Augmentin Antibiotics", 10, "uncommon", 1, 1),
            new("5d1b3a5d86f774252167ba22", "Pile of Meds", 8, "uncommon", 1, 1),
            new("5751a89d24597722aa0e8db0", "Golden Star Balm", 8, "uncommon", 1, 1),
            new("590a391c86f774385a33c404", "Magnet", 10, "uncommon", 1, 1),
            new("61bf83814088ec1a363d7097", "Sewing Kit", 8, "uncommon", 1, 1),
            new("5c06782b86f77426df5407d2", "Capacitors", 8, "uncommon", 1, 1),

            new("5696686a4bdc2da3298b456a", "1 Dollar", 10, "uncommon", 1, 1, 1, 100),
            new("569668774bdc2da2298b4568", "1 Euro", 9, "uncommon", 1, 1, 1, 100),

            new("590c661e86f7741e566b646a", "Car First Aid Kit", 8, "uncommon", 2, 1),
            new("5d02778e86f774203e7dedbe", "CMS Surgical Kit", 7, "uncommon", 2, 1),
            new("57347da92459774491567cf5", "Large Tushonka", 6, "uncommon", 1, 2),
            new("544fb45d4bdc2dee738b4568", "Salewa First Aid Kit", 6, "uncommon", 1, 2),
            new("5448fee04bdc2dbc018b4567", "Bottle of Water (0.6L)", 8, "uncommon", 1, 2),
            new("544fb62a4bdc2dfb738b4568", "Pineapple Juice", 7, "uncommon", 1, 2),
            new("590c5d4b86f77784e1b9c45", "Iskra Ration Pack", 6, "uncommon", 1, 2),
            new("5c0fa877d174af02a012e1cf", "Aquamari Water Bottle", 5, "uncommon", 1, 2),

            new("590c678286f77426c9660122", "IFAK First Aid Kit", 4, "rare", 1, 1),
            new("60098ad7c2240c0fe85c570a", "AFAK First Aid Kit", 3, "rare", 1, 1),
            new("5755383e24597772cb798966", "Vaseline", 3, "rare", 1, 1),
            new("5af0548586f7743a532b7e99", "Ibuprofen", 3, "rare", 1, 1),
            new("544fb3f34bdc2d03748b456a", "Morphine Injector", 3, "rare", 1, 1),
            new("5c0e530286f7747fa1419862", "Propital Injector", 3, "rare", 1, 1),
            new("5c0e531d86f7747fa23f4d42", "SJ6 Stimulant", 2, "rare", 1, 1),
            new("5c0e531286f7747fa54205c2", "SJ1 Stimulant", 2, "rare", 1, 1),
            new("5c0e533786f7747fa23f4d47", "Zagustin Injector", 2, "rare", 1, 1),
            new("590c392f86f77444754deb29", "SSD Drive", 3, "rare", 1, 1),
            new("590c621186f774138d11ea29", "Secure Flash Drive", 3, "rare", 1, 1),
            new("5e2aedd986f7746d404f3aa4", "GreenBat Battery", 2, "rare", 1, 1),
            new("619cbf476b8a1b37a54eebf8", "Military Corrugated Tube", 3, "rare", 1, 1),
            new("5d0375ff86f774186372f685", "Military Cable", 2, "rare", 2, 1),
            new("5c05308086f7746b2101e90b", "Virtex Processor", 2, "rare", 1, 1),
            new("5c12688486f77426843c7d32", "Paracord", 2, "rare", 2, 1),

            new("573478bc24597738002c6175", "Horse Figurine", 2, "rare", 1, 2),
            new("5e54f62086f774219b0f1937", "Raven Figurine", 2, "rare", 1, 2),
            new("590c645c86f77412b01304d9", "Diary", 2, "rare", 1, 2),

            new("57347ca924597744596b4e71", "Graphics Card", 1, "ultrarare", 2, 1),
            new("59faff1d86f7746c51718c9c", "Physical Bitcoin", 1, "ultrarare", 1, 1),
            new("5c0530ee86f774697952d952", "LEDX Skin Transilluminator", 1, "ultrarare", 1, 1),
            new("5c12613b86f7743bbe2c3f76", "Intelligence Folder", 1, "ultrarare", 2, 1),
            new("5c12620d86f7743f8b198b72", "Tetriz Portable Game Console", 1, "ultrarare", 1, 2),
            new("5d03775b86f774203e7e0c4b", "Phased Array Element (AESA)", 1, "ultrarare", 2, 2),
            new("59e3658a86f7741776641ac4", "Cat Figurine", 1, "ultrarare", 1, 3),
            new("5bc9bc53d4351e00367fbcee", "Golden Rooster Figurine", 1, "ultrarare", 2, 2),

            new("5c1d0c5f86f7744bb2683cf0", "Labs Blue Keycard", 1, "ultrarare", 1, 1),
            new("5c1d0d6d86f7744bb2683e1f", "Labs Yellow Keycard", 1, "ultrarare", 1, 1),
            new("5c1d0dc586f7744baf2e7b79", "Labs Green Keycard", 1, "ultrarare", 1, 1),
            new("5c1e495a86f7743109743dfb", "Labs Violet Keycard", 1, "ultrarare", 1, 1),

            new("590c657e86f77412b013051d", "Grizzly Medical Kit", 1, "ultrarare", 2, 2),
        ];
    }
}

public class PoolItem
{
    [JsonPropertyName("tpl")]
    public string Tpl { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("weight")]
    public int Weight { get; set; } = 1;

    [JsonPropertyName("rarity")]
    public string Rarity { get; set; } = "common";

    [JsonPropertyName("width")]
    public int Width { get; set; } = 1;

    [JsonPropertyName("height")]
    public int Height { get; set; } = 1;

    [JsonPropertyName("minCount")]
    public int MinCount { get; set; } = 1;

    [JsonPropertyName("maxCount")]
    public int MaxCount { get; set; } = 1;

    public PoolItem() { }

    public PoolItem(string tpl, string name, int weight, string rarity, int width, int height)
        : this(tpl, name, weight, rarity, width, height, 1, 1)
    {
    }

    public PoolItem(string tpl, string name, int weight, string rarity, int width, int height, int minCount, int maxCount)
    {
        Tpl = tpl;
        Name = name;
        Weight = weight;
        Rarity = rarity;
        Width = width;
        Height = height;
        MinCount = minCount;
        MaxCount = maxCount;
    }
}
