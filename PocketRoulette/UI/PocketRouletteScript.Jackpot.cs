using System;
using EFT.Communications;
using PocketRoulette.Models;
using UnityEngine;

namespace PocketRoulette
{
    public sealed partial class PocketRouletteScript
    {
        private RollPlan _choicePromptPlan;
        private double _choicePromptUntil;
        private int _choiceDisplayedSecond;
        private JackpotNotification _choiceAnnouncement, _choiceInstructions;

        private void StartJackpotNotifications(RollPlan plan, double remaining)
        {
            StopJackpotNotifications();
            if (remaining <= 0) return;
            _choicePromptPlan = plan;
            _choicePromptUntil = Time.realtimeSinceStartup + remaining;
            _choiceDisplayedSecond = (int)Math.Ceiling(remaining);
            _choiceAnnouncement = new JackpotNotification(RewardNotifications.JackpotAnnouncementText(_config, plan), true);
            _choiceInstructions = new JackpotNotification(JackpotInstructions(plan, _choiceDisplayedSecond), false);
            try
            {
                NotificationManager.DisplayNotification(_choiceAnnouncement);
                NotificationManager.DisplayNotification(_choiceInstructions);
            }
            catch (Exception ex)
            {
                StopJackpotNotifications();
                Plugin.LogSource.LogWarning("[PocketRoulette] Native jackpot notifications could not open: " + ex.Message);
            }
        }

        private static string JackpotInstructions(RollPlan plan, int seconds) =>
            $"[{ClientConfigManager.ShareKey}] SHARE: keep the main prize, give up the bonus; {plan.TeammateCount} teammate(s) get gifts.\n"
            + $"[{ClientConfigManager.KeepKey}] KEEP: take both prizes. {seconds}s remaining; timeout keeps both.";

        private void UpdateJackpotNotifications()
        {
            if (_choicePromptPlan == null) return;
            var remaining = _choicePromptUntil - Time.realtimeSinceStartup;
            if (_choice == null || _choice.Task.IsCompleted || !MenuAvailable || remaining <= 0)
            { StopJackpotNotifications(); return; }
            var second = (int)Math.Ceiling(remaining);
            if (second == _choiceDisplayedSecond) return;
            _choiceDisplayedSecond = second;
            try { _choiceInstructions?.UpdateText(JackpotInstructions(_choicePromptPlan, second)); }
            catch (Exception ex)
            {
                StopJackpotNotifications();
                Plugin.LogSource.LogWarning("[PocketRoulette] Native jackpot notification update failed: " + ex.Message);
            }
        }

        private void StopJackpotNotifications()
        {
            var announcement = _choiceAnnouncement; var instructions = _choiceInstructions;
            _choiceAnnouncement = _choiceInstructions = null;
            _choicePromptPlan = null; _choicePromptUntil = 0;
            foreach (var notification in new[] { announcement, instructions })
            {
                if (notification == null) continue;
                try { notification.Dispose(); }
                catch (Exception ex) { Plugin.LogSource.LogWarning("[PocketRoulette] Native jackpot notification cleanup failed: " + ex.Message); }
            }
        }

        private void OnDisable() => StopJackpotNotifications();
    }
}
