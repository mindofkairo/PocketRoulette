using System;
using System.Linq;
using EFT;
using EFT.Ballistics;
using PocketRoulette.Models;
using UnityEngine;

namespace PocketRoulette
{
    public sealed partial class PocketRouletteScript
    {
        private double CurrentChallengeProgress() => _challenge?.Kind == "survive" ? _survived
            : _challenge?.Kind == "walk" ? _walked : _challenge?.Kills.Count ?? 0;

        private void TrackChallengeKill(Player victim, IPlayer killer, DamageInfo damage, EBodyPart bodyPart)
        {
            if (_player == null || _challenge == null || _challenge.Completed || _challenge.Kind != "kills"
                || !ChallengeRules.Enabled(_config) || _token.IsCancellationRequested || !_player.HealthController.IsAlive
                || killer == null || killer.ProfileId != _player.ProfileId || victim == null || victim.ProfileId == _player.ProfileId) return;
            try
            {
                var group = _player.Profile.Info.GroupId;
                if ((!string.IsNullOrEmpty(group) && victim.Profile.Info.GroupId == group)
                    || FikaMaybe.Party(_player).MemberIds.Contains(victim.ProfileId)) return;
                var kill = new ChallengeKillReport
                {
                    VictimId = victim.ProfileId,
                    TargetType = ChallengeVictim.Classify(victim.Side, victim.Profile.Info.Settings?.Role),
                    DistanceMeters = Vector3.Distance(_player.Transform.position, victim.Transform.position)
                };
                if (ChallengeRules.Matches(_challenge, kill) && _challenge.Kills.Count < _challenge.Target
                    && !_challenge.Kills.Any(existing => existing.VictimId == kill.VictimId)) _challenge.Kills.Add(kill);
            }
            catch (Exception ex)
            {
                // A challenge must never interrupt the game's death processing.
                Plugin.LogSource.LogWarning("[PocketRoulette] Could not track challenge kill: " + ex.Message);
            }
        }
    }
}
