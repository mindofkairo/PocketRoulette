#nullable disable
using System;
using System.Collections.Generic;
using PocketRoulette.Models;

namespace PocketRoulette
{
    public static class ChallengeRules
    {
        public static bool Enabled(PocketRouletteConfig config) => config.EnableLuck && config.EnableLuckyChallenges;

        public static List<LuckyChallenge> Candidates(PocketRouletteConfig config)
        {
            var result = new List<LuckyChallenge>();
            if (!Enabled(config)) return result;
            var settings = config.LuckyChallenges;
            if (settings.SurvivalEnabled) result.Add(new LuckyChallenge { Kind = "survive", Target = settings.SurvivalSeconds });
            if (settings.WalkingEnabled) result.Add(new LuckyChallenge { Kind = "walk", Target = settings.WalkingMeters });
            foreach (var preset in settings.KillPresets)
                if (preset.Enabled) result.Add(FromPreset(preset));
            return result;
        }

        public static LuckyChallenge FromPreset(KillChallengePreset preset) => new LuckyChallenge
        {
            Kind = "kills", Name = preset.Name, Target = preset.Kills, TargetType = preset.TargetType,
            MinDistanceMeters = preset.MinDistanceMeters, MaxDistanceMeters = preset.MaxDistanceMeters
        };

        public static bool Matches(LuckyChallenge challenge, ChallengeKillReport kill) =>
            challenge.Kind == "kills" && kill != null && ConfigRules.IsItemId(kill.VictimId)
            && (kill.TargetType == "scav" || kill.TargetType == "pmc")
            && (challenge.TargetType == "any" || kill.TargetType == challenge.TargetType)
            && !double.IsNaN(kill.DistanceMeters) && !double.IsInfinity(kill.DistanceMeters)
            && kill.DistanceMeters >= 0 && kill.DistanceMeters <= 5000
            && (challenge.MinDistanceMeters <= 0 || kill.DistanceMeters > challenge.MinDistanceMeters)
            && (challenge.MaxDistanceMeters <= 0 || kill.DistanceMeters < challenge.MaxDistanceMeters);

        public static string Description(LuckyChallenge challenge)
        {
            if (challenge.Kind == "survive") return $"survive {challenge.Target / 60:0.#} active minutes";
            if (challenge.Kind == "walk") return $"travel {challenge.Target:0} metres";
            var target = challenge.TargetType == "pmc" ? "PMC" : challenge.TargetType == "scav" ? "Scav" : "enemy";
            var text = $"get {challenge.Target:0} {target} {(challenge.Target == 1 ? "kill" : "kills")}";
            if (challenge.MinDistanceMeters > 0) text += $" from over {challenge.MinDistanceMeters:0.#}m";
            if (challenge.MaxDistanceMeters > 0) text += $" {(challenge.MinDistanceMeters > 0 ? "and" : "from")} under {challenge.MaxDistanceMeters:0.#}m";
            return text;
        }
    }
}
