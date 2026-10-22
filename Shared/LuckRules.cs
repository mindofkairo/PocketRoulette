#nullable disable
using System;
using PocketRoulette.Models;

namespace PocketRoulette
{
    public static class LuckRules
    {
        public static double Effective(PocketRouletteConfig config, double stored, double emptyBonus) =>
            config.EnableLuck ? Math.Max(0, Math.Min(config.Luck.MaxLuck, stored + emptyBonus)) : 0;

        public static double RarityMultiplier(PocketRouletteConfig config, string rarity, double luck)
        {
            if (!config.EnableLuck) return 1;
            var perPoint = rarity == "ultrarare" ? config.Luck.UltraRareWeightPerPoint
                : rarity == "rare" ? config.Luck.RareWeightPerPoint : 0;
            return 1 + luck * perPoint;
        }
        public static double PositiveMultiplier(PocketRouletteConfig config, double luck) =>
            1 + (config.EnableLuck ? luck * config.Luck.PositiveEventWeightPerPoint : 0);
        public static double GrenadeMultiplier(PocketRouletteConfig config, double luck) =>
            1 / (1 + (config.EnableLuck ? luck * config.Luck.GrenadeReductionPerPoint : 0));
        public static double ChallengeChance(PocketRouletteConfig config, double luck) => Math.Min(100,
            config.LuckyChallenges.AssignmentChancePercent * (1 + (config.EnableLuck ? luck * config.Luck.ChallengeChancePerPoint : 0)));
        public static double Unlucky(PocketRouletteConfig config, double stored) =>
            Math.Min(config.BadLuckProtection.MaxStoredLuck, stored + config.BadLuckProtection.GainPerUnluckyRaid);
        public static double Win(PocketRouletteConfig config, double stored) =>
            Math.Max(0, stored * (1 - config.BadLuckProtection.WinDecayPercent / 100));
        public static double Inactivity(PocketRouletteConfig config, double stored, double days) =>
            Math.Max(0, stored - Math.Max(0, days) * config.BadLuckProtection.InactivityDecayPerDay);
        public static int Rank(string rarity) => rarity == "ultrarare" ? 4 : rarity == "rare" ? 3 : rarity == "uncommon" ? 2 : 1;
    }
}
