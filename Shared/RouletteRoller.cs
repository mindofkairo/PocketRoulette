#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using PocketRoulette.Models;

namespace PocketRoulette
{
    public sealed class RouletteRoller
    {
        private readonly Random _random;
        public RouletteRoller(Random random) { _random = random; }
        public double NextDouble() => _random.NextDouble();

        public RollPlan Select(PocketRouletteConfig config, List<PoolItem> items, double luck, bool events = true)
        {
            var tiers = AvailableTiers(config.Mode, items).ToDictionary(tier => tier.Key,
                tier => tier.Value * LuckRules.RarityMultiplier(config, tier.Key, luck));
            var choices = new Dictionary<string, double>(tiers);
            if (events && config.EnableGrenadeEvent && config.GrenadeWeight > 0)
                choices["grenade"] = config.GrenadeWeight * LuckRules.GrenadeMultiplier(config, luck);
            if (events && config.EnableGrenadeEvent && config.EnableCursedJackpot
                && items.Any(item => LuckRules.Rank(item.Rarity) >= LuckRules.Rank(config.CursedJackpot.MinimumRarity)))
                choices["cursed"] = config.CursedJackpot.Weight * LuckRules.PositiveMultiplier(config, luck);
            if (events && config.EnableSharedJackpots
                && items.Any(item => LuckRules.Rank(item.Rarity) >= LuckRules.Rank(config.SharedJackpots.WinnerMinimumRarity))
                && items.Any(item => LuckRules.Rank(item.Rarity) >= LuckRules.Rank(config.SharedJackpots.BonusMinimumRarity)))
                choices["shared"] = config.SharedJackpots.Weight * LuckRules.PositiveMultiplier(config, luck);
            var selected = PickWeight(choices);
            if (selected == "grenade" || selected == "cursed" || selected == "shared")
            {
                var plan = CreateEvent(selected, config, items, luck);
                plan.OddsPercent = 100 * choices[selected] / choices.Sum(choice => choice.Value);
                if (selected == "grenade" && config.EnableGrenadeWheel && config.GrenadeWheel.Count > 0)
                {
                    double Weight(GrenadeChoice entry) => entry.Weight * (entry.Dangerous ? LuckRules.GrenadeMultiplier(config, luck) : LuckRules.PositiveMultiplier(config, luck));
                    plan.OddsPercent *= Weight(config.GrenadeWheel.First(entry => entry.Tpl == plan.GrenadeTpl)) / config.GrenadeWheel.Sum(Weight);
                }
                else if (selected == "cursed")
                {
                    var candidates = items.Where(item => LuckRules.Rank(item.Rarity) >= LuckRules.Rank(config.CursedJackpot.MinimumRarity)).ToList();
                    var values = AvailableTiers(config.Mode, candidates).ToDictionary(tier => tier.Key, tier => tier.Value * LuckRules.RarityMultiplier(config, tier.Key, luck));
                    plan.OddsPercent *= values[plan.Reward.Rarity] / values.Sum(value => value.Value)
                        * plan.Reward.Weight / candidates.Where(item => item.Rarity == plan.Reward.Rarity).Sum(item => (double)item.Weight);
                }
                return plan;
            }
            var reward = PickItem(items.Where(item => item.Rarity == selected).ToList());
            var tierTotal = items.Where(item => item.Rarity == selected).Sum(item => (double)item.Weight);
            return new RollPlan { Kind = "item", Reward = reward, Luck = luck,
                OddsPercent = 100 * choices[selected] / choices.Sum(choice => choice.Value) * reward.Weight / tierTotal };
        }

        public RollPlan CreateEvent(string kind, PocketRouletteConfig config, List<PoolItem> items, double luck)
        {
            if (kind == "grenade")
            {
                var grenade = PickGrenade(config, luck);
                return new RollPlan { Kind = kind, Luck = luck, GrenadeTpl = grenade.Tpl,
                    GrenadeName = grenade.Name, GrenadePrewarmSeconds = grenade.PrewarmSeconds };
            }
            if (kind != "cursed" && kind != "shared") throw new ArgumentException("Unknown event.", nameof(kind));
            var minimum = kind == "cursed" ? config.CursedJackpot.MinimumRarity : config.SharedJackpots.WinnerMinimumRarity;
            return new RollPlan { Kind = kind, Luck = luck, Reward = Valuable(config, items, luck, minimum),
                GrenadeTpl = config.CursedJackpot.GrenadeTpl,
                ExplosionDelaySeconds = EventTiming.CursedDelay(config.CursedJackpot.MinExplosionDelaySeconds,
                    config.CursedJackpot.MaxExplosionDelaySeconds, _random.NextDouble(), config.EnableLuck ? luck : 0,
                    config.Luck.CursedDelayBiasPerPoint) };
        }

        public PoolItem Valuable(PocketRouletteConfig config, List<PoolItem> items, double luck, string minimum)
        {
            var candidates = items.Where(item => LuckRules.Rank(item.Rarity) >= LuckRules.Rank(minimum)).ToList();
            var tiers = AvailableTiers(config.Mode, candidates).ToDictionary(tier => tier.Key,
                tier => tier.Value * LuckRules.RarityMultiplier(config, tier.Key, luck));
            var rarity = PickWeight(tiers);
            return PickItem(candidates.Where(item => item.Rarity == rarity).ToList());
        }

        public GrenadeChoice PickGrenade(PocketRouletteConfig config, double luck)
        {
            if (!config.EnableGrenadeWheel || config.GrenadeWheel.Count == 0)
                return new GrenadeChoice { Tpl = config.GrenadeTpl, Name = "live grenade", PrewarmSeconds = config.GrenadeTimeSpentSeconds };
            var choices = config.GrenadeWheel.ToDictionary(entry => entry.Tpl,
                entry => entry.Weight * (entry.Dangerous ? LuckRules.GrenadeMultiplier(config, luck) : LuckRules.PositiveMultiplier(config, luck)));
            var tpl = PickWeight(choices);
            return config.GrenadeWheel.First(entry => entry.Tpl == tpl);
        }

        private string PickWeight(Dictionary<string, double> choices)
        {
            var total = choices.Sum(choice => Math.Max(0, choice.Value));
            if (total <= 0) throw new InvalidOperationException("No eligible roulette outcomes are available.");
            var roll = _random.NextDouble() * total;
            foreach (var choice in choices)
            {
                if (roll < choice.Value) return choice.Key;
                roll -= Math.Max(0, choice.Value);
            }
            return choices.Last(choice => choice.Value > 0).Key;
        }

        public bool RunsThisRaid(int percent) => _random.NextDouble() * 100 < percent;

        public List<PoolItem> EligibleItems(PocketRouletteConfig config) => config.ItemPool
            .Where(item => RarityWeights(config.Mode).Any(tier => tier.Key == item.Rarity && tier.Value > 0))
            .ToList();

        // A null item represents the live grenade alternative. Tier weights total 100
        // before empty tiers are removed; item weights only compete within their tier.
        public PoolItem Roll(PocketRouletteConfig config, List<PoolItem> items)
        {
            var tiers = AvailableTiers(config.Mode, items);
            var tierTotal = tiers.Sum(tier => tier.Value);
            var grenadeWeight = config.EnableGrenadeEvent ? config.GrenadeWeight : 0;
            if (tierTotal + grenadeWeight <= 0)
                throw new InvalidOperationException("No roulette outcomes are available.");
            var roll = _random.NextDouble() * (tierTotal + grenadeWeight);
            foreach (var tier in tiers)
            {
                if (roll < tier.Value)
                    return PickItem(items.Where(item => item.Rarity == tier.Key).ToList());
                roll -= tier.Value;
            }
            return null;
        }

        public double ItemOddsPercent(PocketRouletteConfig config, List<PoolItem> items, PoolItem selected)
        {
            var tiers = AvailableTiers(config.Mode, items);
            var total = tiers.Sum(tier => tier.Value) + (config.EnableGrenadeEvent ? config.GrenadeWeight : 0);
            var tierWeight = tiers.Where(tier => tier.Key == selected.Rarity).Sum(tier => tier.Value);
            var itemTotal = items.Where(item => item.Rarity == selected.Rarity).Sum(item => (double)item.Weight);
            return total > 0 && itemTotal > 0 ? 100.0 * tierWeight / total * selected.Weight / itemTotal : 0;
        }

        public int StackCount(PoolItem poolItem, int templateMax)
        {
            var max = Math.Min(Math.Max(1, templateMax), Math.Max(1, poolItem.MaxCount));
            var min = Math.Min(max, Math.Max(1, poolItem.MinCount));
            return min + (int)(_random.NextDouble() * ((long)max - min + 1));
        }

        private PoolItem PickItem(List<PoolItem> items)
        {
            var roll = _random.NextDouble() * items.Sum(item => (double)item.Weight);
            foreach (var item in items)
            {
                if (roll < item.Weight) return item;
                roll -= item.Weight;
            }
            return items[items.Count - 1];
        }

        private static List<KeyValuePair<string, int>> AvailableTiers(string mode, List<PoolItem> items) =>
            RarityWeights(mode).Where(tier => tier.Value > 0 && items.Any(item => item.Rarity == tier.Key)).ToList();

        private static Dictionary<string, int> RarityWeights(string mode)
        {
            int[] weights;
            switch (mode)
            {
                case "garbage": weights = new[] { 95, 5, 0, 0 }; break;
                case "useful": weights = new[] { 15, 60, 23, 2 }; break;
                case "jackpot": weights = new[] { 0, 15, 70, 15 }; break;
                case "chaos": weights = new[] { 25, 25, 25, 25 }; break;
                default: weights = new[] { 65, 25, 8, 2 }; break;
            }
            return new Dictionary<string, int>
            {
                { "common", weights[0] }, { "uncommon", weights[1] },
                { "rare", weights[2] }, { "ultrarare", weights[3] }
            };
        }
    }
}
