#if POCKET_ROULETTE_TESTING
using System;
using EFT;
using EFT.InventoryLogic;
using PocketRoulette.Models;
using UnityEngine;

namespace PocketRoulette
{
    public sealed partial class PocketRouletteScript
    {
        private RaidUpdate _testingOriginal;
        private RaidUpdate _testingUpdate;
        private double _testingOriginalSurvived, _testingOriginalWalked;
        private int _testingOriginalQuarter;
        public string TestMessage { get; private set; } = "Enter a sandbox after the normal raid-start rewards finish.";
        public bool InTesting => _testingOriginal != null;
        internal EFT.Player TestPlayer => _player;
        public bool TestPanelAvailable => _player != null && !_token.IsCancellationRequested && _player.HealthController.IsAlive;
        public bool TestReady => _ready && !_halted && _player != null && _player.HealthController.IsAlive;
        public bool TestBusy => _processing || _refreshing || _queue.Count > 0 || _unconfirmed.Count > 0;
        public RaidUpdate TestUpdate => _testingUpdate;
        public Models.PocketRouletteConfig TestConfig => _config;
        public bool TestChoiceOpen => _choice != null;
        public void TestChoose(bool share) => _choice?.TrySetResult(share);
        public bool TestEmptyPockets()
        {
            var pockets = _player?.Profile.Inventory.Equipment.GetSlot(EFT.InventoryLogic.EquipmentSlot.Pockets)?.ContainedItem as EFT.InventoryLogic.CompoundItem;
            return pockets != null && !System.Linq.Enumerable.Any(pockets.GetAllItems(), item => item.Id != pockets.Id);
        }

        public bool TestCommand(TestingRequest request)
        {
            try
            {
                if (!TestReady || TestBusy) throw new InvalidOperationException("Wait for initialization and pending placements/events/confirmations to finish.");
                request.RaidId = _raidId;
                request.CommandId = Guid.NewGuid().ToString("N");
                request.PartyIds = FikaMaybe.Party(_player).MemberIds;
                if (request.Command == "start")
                {
                    if (InTesting) return true;
                    var update = RaidApi.Post("/pocketroulette/testing", request);
                    _testingOriginal = new RaidUpdate { RaidId = _raidId, Config = _config, Challenge = _challenge };
                    _testingOriginalSurvived = _survived; _testingOriginalWalked = _walked; _testingOriginalQuarter = _challengeQuarter;
                    _raidId = update.RaidId; _challenge = null; _survived = _walked = 0; _challengeQuarter = 0;
                    Apply(update);
                }
                else if (request.Command == "stop")
                {
                    if (!InTesting) return true;
                    RaidApi.Post("/pocketroulette/testing", request);
                    _raidId = _testingOriginal.RaidId; _config = _testingOriginal.Config; _challenge = _testingOriginal.Challenge;
                    _survived = _testingOriginalSurvived; _walked = _testingOriginalWalked; _challengeQuarter = _testingOriginalQuarter;
                    _testingOriginal = null; _testingUpdate = null;
                    TestMessage = "Sandbox closed. Normal raid state restored. Spawned items remain real.";
                }
                else
                {
                    if (!InTesting) throw new InvalidOperationException("Enter the sandbox first.");
                    var update = RaidApi.Post("/pocketroulette/testing", request);
                    if (request.Command == "normal" || request.Command == "challenge" || request.Command == "reset" || request.Command == "force" || request.Command == "grenade")
                    { _survived = _walked = 0; _challengeQuarter = 0; }
                    Apply(update);
                    if (request.Command == "progress")
                    {
                        if (_challenge?.Kind == "survive") _survived = update.Testing.ChallengeProgress;
                        else if (_challenge?.Kind == "walk") _walked = update.Testing.ChallengeProgress;
                        ChallengeProgress();
                    }
                    if (request.Command == "normal" && update.ChanceMissed) RewardNotifications.ChanceMiss(_config);
                    if (request.Command == "sample")
                        Plugin.LogSource.LogInfo("[TEST ODDS] " + update.Testing.Message + " "
                            + Newtonsoft.Json.JsonConvert.SerializeObject(update.Testing.Counts));
                    StartQueue();
                }
                Plugin.CachedConfig = _config;
                _lastPosition = _player.Transform.position;
                if (request.Command == "start" || request.Command == "stop") FikaMaybe.NotifyStateChanged();
                if (_testingUpdate?.Testing != null) TestMessage = _testingUpdate.Testing.Message;
                return true;
            }
            catch (Exception ex)
            {
                TestMessage = ex.Message;
                Plugin.LogSource.LogWarning("[PocketRoulette TEST] " + ex.Message);
                return false;
            }
        }
    }
}
#endif
