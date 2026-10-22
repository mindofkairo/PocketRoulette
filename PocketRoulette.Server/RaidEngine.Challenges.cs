using PocketRoulette.Models;

namespace PocketRoulette.Server;

public sealed partial class RaidEngine
{
    private bool AdvanceChallenge(RaidLedger raid, RaidActionRequest request)
    {
        var challenge = raid.Challenge;
        if (!raid.Alive || !raid.Eligible || !ChallengeRules.Enabled(raid.Config) || challenge == null || challenge.Completed) return false;
        var changed = false;
        double progress;
        if (challenge.Kind == "kills")
        {
            if (request.ChallengeId != challenge.Id) return false;
            var excluded = raid.PartyIds.Concat(raid.CurrentPartyIds).Append(raid.ProfileId).Append(raid.AccountId).ToHashSet();
            challenge.Kills ??= new();
            var known = challenge.Kills.Select(kill => kill.VictimId).ToHashSet();
            foreach (var kill in (request.ChallengeKills ?? new()).Take(100))
            {
                if (challenge.Kills.Count >= challenge.Target) break;
                if (!ChallengeRules.Matches(challenge, kill) || excluded.Contains(kill.VictimId) || !known.Add(kill.VictimId)) continue;
                challenge.Kills.Add(kill);
                changed = true;
            }
            progress = challenge.Kills.Count;
        }
        else
        {
            var elapsed = Math.Max(0, (Now - raid.StartedUtc).TotalSeconds);
            progress = challenge.Kind == "survive" ? request.SurvivedSeconds : request.WalkedMeters;
            if (!double.IsFinite(progress) || progress < 0 ||
                (challenge.Kind == "survive" ? progress > elapsed + 3 : progress > elapsed * 12 + 10)) return false;
        }
        if (progress < challenge.Target) return changed;
        challenge.Completed = true;
        for (var i = 0; i < raid.Config.LuckyChallenges.BonusSpins; i++)
            if (Items(raid).Count > 0) AddSpin(raid, "challenge", false);
        Notice(raid, "challenge", $"Lucky challenge complete! {raid.Config.LuckyChallenges.BonusSpins} bonus spin(s) earned ({ChallengeRules.Description(challenge)}).");
        return true;
    }
}
