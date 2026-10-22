#nullable disable
using System;
using System.Linq;
using PocketRoulette.Models;

namespace PocketRoulette
{
    public static class FeatureRules
    {
        public static void Normalize(PocketRouletteConfig config)
        {
            config.Luck = config.Luck ?? new LuckSettings();
            config.BadLuckProtection = config.BadLuckProtection ?? new BadLuckSettings();
            config.EmptyPocketBonus = config.EmptyPocketBonus ?? new EmptyPocketSettings();
            config.CursedJackpot = config.CursedJackpot ?? new CursedSettings();
            config.SharedJackpots = config.SharedJackpots ?? new SharingSettings();
            config.LuckyChallenges = config.LuckyChallenges ?? new ChallengeSettings();
            config.BuyInSpins = config.BuyInSpins ?? new BuyInSettings();
            config.Luck.MaxLuck = Number(config.Luck.MaxLuck, 0, 100, 20);
            config.Luck.RareWeightPerPoint = Number(config.Luck.RareWeightPerPoint, 0, 10, 0.1);
            config.Luck.UltraRareWeightPerPoint = Number(config.Luck.UltraRareWeightPerPoint, 0, 10, 0.2);
            config.Luck.PositiveEventWeightPerPoint = Number(config.Luck.PositiveEventWeightPerPoint, 0, 10, 0.1);
            config.Luck.CursedDelayBiasPerPoint = Number(config.Luck.CursedDelayBiasPerPoint, 0, 10, 0.1);
            config.Luck.GrenadeReductionPerPoint = Number(config.Luck.GrenadeReductionPerPoint, 0, 10, 0.1);
            config.Luck.ChallengeChancePerPoint = Number(config.Luck.ChallengeChancePerPoint, 0, 10, 0.05);
            var bad = config.BadLuckProtection;
            bad.MaxStoredLuck = Number(bad.MaxStoredLuck, 0, config.Luck.MaxLuck, 10);
            bad.GainPerUnluckyRaid = Number(bad.GainPerUnluckyRaid, 0, 100, 1);
            bad.WinDecayPercent = Number(bad.WinDecayPercent, 0, 100, 50);
            bad.InactivityDecayPerDay = Number(bad.InactivityDecayPerDay, 0, 100, 0.25);
            config.EmptyPocketBonus.LuckPoints = Number(config.EmptyPocketBonus.LuckPoints, 0, config.Luck.MaxLuck, 2);
            var cursed = config.CursedJackpot;
            cursed.Weight = Number(cursed.Weight, 0, 1000, 1);
            cursed.MinExplosionDelaySeconds = (float)Number(cursed.MinExplosionDelaySeconds, 2, 120, 6);
            cursed.MaxExplosionDelaySeconds = (float)Number(cursed.MaxExplosionDelaySeconds, cursed.MinExplosionDelaySeconds, 120, 10);
            cursed.ExplanationSeconds = (float)Number(cursed.ExplanationSeconds, 1, 15, 2);
            cursed.MinimumRarity = Minimum(cursed.MinimumRarity, "rare");
            cursed.GrenadeTpl = ConfigRules.IsItemId(cursed.GrenadeTpl) ? cursed.GrenadeTpl : config.GrenadeTpl;
            var sharing = config.SharedJackpots;
            sharing.Weight = Number(sharing.Weight, 0, 1000, 1);
            sharing.DecisionSeconds = (float)Number(sharing.DecisionSeconds, 5, 120, 20);
            sharing.WinnerMinimumRarity = Minimum(sharing.WinnerMinimumRarity, "ultrarare");
            sharing.BonusMinimumRarity = Minimum(sharing.BonusMinimumRarity, "rare");
            sharing.GiftMinimumRarity = Minimum(sharing.GiftMinimumRarity, "rare");
            sharing.ShareMessages = (sharing.ShareMessages ?? new System.Collections.Generic.List<string>())
                .Where(message => !string.IsNullOrWhiteSpace(message)).ToList();
            sharing.KeepMessages = (sharing.KeepMessages ?? new System.Collections.Generic.List<string>())
                .Where(message => !string.IsNullOrWhiteSpace(message)).ToList();
            var challenge = config.LuckyChallenges;
            challenge.AssignmentChancePercent = Number(challenge.AssignmentChancePercent, 0, 100, 30);
            challenge.SurvivalSeconds = Number(challenge.SurvivalSeconds, 30, 7200, 300);
            challenge.WalkingMeters = Number(challenge.WalkingMeters, 50, 10000, 750);
            challenge.BonusSpins = (int)Number(challenge.BonusSpins, 1, 5, 1);
            challenge.KillPresets = (challenge.KillPresets ?? new System.Collections.Generic.List<KillChallengePreset>())
                .Where(preset => preset != null && !string.IsNullOrWhiteSpace(preset.Id))
                .GroupBy(preset => preset.Id.Trim(), StringComparer.OrdinalIgnoreCase).Select(group => group.First()).Take(50).ToList();
            foreach (var preset in challenge.KillPresets)
            {
                preset.Id = preset.Id.Trim();
                preset.Name = string.IsNullOrWhiteSpace(preset.Name) ? preset.Id : preset.Name.Trim();
                preset.Kills = (int)Number(preset.Kills, 1, 100, 1);
                preset.TargetType = (preset.TargetType ?? "any").Trim().ToLowerInvariant();
                if (!new[] { "any", "scav", "pmc" }.Contains(preset.TargetType)) preset.Enabled = false;
                preset.MinDistanceMeters = Number(preset.MinDistanceMeters, 0, 5000, 0);
                preset.MaxDistanceMeters = Number(preset.MaxDistanceMeters, 0, 5000, 0);
                if (preset.MaxDistanceMeters > 0 && preset.MinDistanceMeters >= preset.MaxDistanceMeters) preset.Enabled = false;
            }
            config.BuyInSpins.PriceRoubles = (int)Number(config.BuyInSpins.PriceRoubles, 1, 10000000, 25000);
            config.BuyInSpins.MaxSpinsPerRaid = (int)Number(config.BuyInSpins.MaxSpinsPerRaid, 0, 10, 2);
            config.GrenadeWheel = (config.GrenadeWheel ?? new System.Collections.Generic.List<GrenadeChoice>())
                .Where(entry => entry != null && ConfigRules.IsItemId(entry.Tpl) && entry.Weight > 0)
                .GroupBy(entry => entry.Tpl).Select(group => group.First()).ToList();
            foreach (var grenade in config.GrenadeWheel)
            {
                grenade.Name = string.IsNullOrWhiteSpace(grenade.Name) ? "live grenade" : grenade.Name.Trim();
                grenade.Weight = Math.Min(1000, grenade.Weight);
                grenade.PrewarmSeconds = (float)Number(grenade.PrewarmSeconds, 0, 30, 0);
            }
        }

        private static string Minimum(string value, string fallback) =>
            new[] { "common", "uncommon", "rare", "ultrarare" }.Contains(value) ? value : fallback;
        private static double Number(double value, double min, double max, double fallback) =>
            Math.Max(min, Math.Min(max, double.IsNaN(value) || double.IsInfinity(value) ? fallback : value));
    }
}
