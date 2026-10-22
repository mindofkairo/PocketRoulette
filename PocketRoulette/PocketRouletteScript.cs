using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using PocketRoulette.Models;
using UnityEngine;

namespace PocketRoulette
{
    public sealed partial class PocketRouletteScript : MonoBehaviour
    {
        private readonly RewardInventory _inventory = new RewardInventory();
        private readonly Queue<RollPlan> _queue = new Queue<RollPlan>();
        private readonly HashSet<string> _seenPlans = new HashSet<string>();
        private readonly HashSet<string> _seenNotices = new HashSet<string>();
        private readonly Dictionary<string, string> _unconfirmed = new Dictionary<string, string>();
        private readonly string _raidKey = Guid.NewGuid().ToString("N");
        private GameWorld _world;
        private Player _player;
        private CancellationToken _token;
        private PocketRouletteConfig _config;
        private string _raidId;
        private bool _processing;
        private bool _refreshing;
        private bool _halted;
        private bool _ready;
        private bool _deathReported;
        private readonly RaidSyncSchedule _sync = new RaidSyncSchedule();
        private string _reportedChallenge;
        private double _survived;
        private double _walked;
        private Vector3 _lastPosition;
        private LuckyChallenge _challenge;
        private int _challengeQuarter;
        private string _offerId;
        private TaskCompletionSource<bool> _choice;
        private bool _emptyAtStart;

        private void Awake()
        {
            Player.OnPlayerDeadStatic += TrackChallengeKill;
            var player = GetComponent<GameWorld>()?.MainPlayer;
            var pockets = player?.Profile.Inventory.Equipment.GetSlot(EquipmentSlot.Pockets)?.ContainedItem as CompoundItem;
            _emptyAtStart = pockets != null && !pockets.GetAllItems().Any(item => item.Id != pockets.Id);
            gameObject.AddComponent<PlayerMenu>();
#if POCKET_ROULETTE_TESTING
            gameObject.AddComponent<TestingPanel>();
#endif
        }

        private IEnumerator Start()
        {
            yield return new WaitForSeconds(3);
            _ = Begin();
        }

        private async Task Begin()
        {
            try
            {
                _world = GetComponent<GameWorld>();
                _player = _world?.MainPlayer;
                if (_player == null) return;
                _token = RaidLifetime.For(_world).Token;
                RaidLifetime.RequirePlayer(_world, _player, _token);
                FikaMaybe.RequireBridge();
                var local = ClientConfigManager.Resolve(ConfigLoader.FetchConfig());
                var party = FikaMaybe.Party(_player);
                var begin = new RaidBeginRequest
                {
                    RaidKey = _raidKey, ProfileId = _player.ProfileId, GroupId = party.GroupId, PartyIds = party.MemberIds,
                    EmptyPockets = _emptyAtStart, BuyInCount = ClientConfigManager.BuyInCount, ClientConfig = local
                };
                RaidUpdate update = null;
                for (var attempt = 0; attempt < 3; attempt++)
                {
                    try { update = RaidApi.Begin(begin); break; }
                    catch when (attempt < 2) { await Task.Delay(1000, _token); }
                }
                _raidId = update.RaidId;
                _config = update.Config;
                Plugin.CachedConfig = _config;
                _lastPosition = _player.Transform.position;
                if (update.ChanceMissed) RewardNotifications.ChanceMiss(_config);
                Apply(update);
                if (_config.EnableLuck && _config.EnableEmptyPocketBonus && update.EmptyPocketLuck > 0)
                    RewardNotifications.Notice(_config, $"Empty-pocket bonus: +{update.EmptyPocketLuck:0.##} luck for this raid.");
                // Warm every possible grenade on every peer, not just the selected outcome's owner.
                if (_config.EnableGrenadeEvent)
                {
                    var templates = (_config.EnableGrenadeWheel ? _config.GrenadeWheel.Select(entry => entry.Tpl) : new[] { _config.GrenadeTpl })
                        .Concat(_config.EnableCursedJackpot ? new[] { _config.CursedJackpot.GrenadeTpl } : Array.Empty<string>()).Distinct();
                    foreach (var tpl in templates)
                    {
                        try { await LiveGrenadeSpawner.Prepare(tpl, _token); }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception ex) { Plugin.LogSource.LogWarning($"[PocketRoulette] Grenade warmup failed for {tpl}: {ex.Message}"); }
                    }
                }
                _ready = true;
                StartQueue();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _halted = true;
#if POCKET_ROULETTE_TESTING
                TestMessage = "Raid initialization failed: " + ex.Message;
#endif
                Plugin.LogSource.LogError($"[PocketRoulette] Raid initialization failed: {ex}");
                if (_config != null) RewardNotifications.Notice(_config, "Pocket Roulette could not initialize. Check the client/server logs.");
            }
        }

        private void Update()
        {
            UpdateJackpotNotifications();
            if (string.IsNullOrEmpty(_raidId) || _token.IsCancellationRequested || _player == null) return;
            var alive = _player.gameObject.activeInHierarchy && _player.HealthController.IsAlive;
            if (alive)
            {
                _survived += Time.deltaTime;
                var position = _player.Transform.position;
                var delta = position - _lastPosition;
                delta.y = 0;
                if (Time.deltaTime > 0 && delta.magnitude <= Time.deltaTime * 12 + 0.25f) _walked += delta.magnitude;
                _lastPosition = position;
                if (_choice != null)
                {
                    if (ClientConfigManager.SharePressed) _choice.TrySetResult(true);
                    else if (ClientConfigManager.KeepPressed) _choice.TrySetResult(false);
                }
                ChallengeProgress();
            }
            else _choice?.TrySetResult(false);
            if (alive && _challenge != null && ChallengeRules.Enabled(_config) && !_challenge.Completed && _reportedChallenge != _challenge.Id
                && CurrentChallengeProgress() >= _challenge.Target)
            {
                _reportedChallenge = _challenge.Id;
                RequestStateRefresh();
            }
            if (!_refreshing && _sync.TryStart(Time.unscaledTime, _unconfirmed.Count > 0, !alive && !_deathReported))
            {
                if (!alive) _deathReported = true;
                _ = RefreshState(alive);
            }
        }

        public void RequestStateRefresh()
        {
            _sync.Request(Time.unscaledTime);
        }

        private async Task RefreshState(bool alive)
        {
            _refreshing = true;
            try
            {
                foreach (var ack in _unconfirmed.ToList())
                {
                    Apply(RaidApi.Action("complete", new RaidActionRequest { RaidId = _raidId, PlanId = ack.Key, Placement = ack.Value }));
                    _unconfirmed.Remove(ack.Key);
                }
                Apply(RaidApi.Action("update", new RaidActionRequest { RaidId = _raidId, Alive = alive,
                    SurvivedSeconds = _survived, WalkedMeters = _walked, PartyIds = FikaMaybe.Party(_player).MemberIds,
                    ChallengeId = _challenge?.Id ?? "", ChallengeKills = _challenge?.Kills?.ToList() ?? new List<ChallengeKillReport>() }));
                if (alive) StartQueue();
                _sync.Succeeded();
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _sync.Failed(Time.unscaledTime);
                Plugin.LogSource.LogWarning($"[PocketRoulette] Raid update failed; pending grants will not be spawned again: {ex.Message}");
            }
            finally { _refreshing = false; }
        }

        private void Apply(RaidUpdate update)
        {
#if POCKET_ROULETTE_TESTING
            if (update.Testing != null)
            {
                _testingUpdate = update;
                if (update.Challenge == null) _challenge = null;
            }
#endif
            if (update.Config != null) _config = update.Config;
            foreach (var notice in update.Notices)
                if (_seenNotices.Add(notice.Id)) RewardNotifications.Notice(_config, notice.Text);
            foreach (var plan in update.Rolls)
            {
                if (plan.Id == _offerId && plan.DecisionMade.HasValue) _choice?.TrySetResult(plan.DecisionMade.Value);
                if (_seenPlans.Add(plan.Id)) _queue.Enqueue(plan);
            }
            if (update.Challenge != null && ChallengeRules.Enabled(_config))
            {
                if (_challenge == null || _challenge.Id != update.Challenge.Id)
                {
                    _challengeQuarter = 0;
                    _reportedChallenge = null;
                    RewardNotifications.ChallengeAssigned(_config, update.Challenge);
                }
                else if (update.Challenge.Kind == "kills")
                {
                    // Keep not-yet-reported local kills when another action refreshes server state.
                    foreach (var kill in _challenge.Kills)
                        if (!update.Challenge.Kills.Any(existing => existing.VictimId == kill.VictimId)) update.Challenge.Kills.Add(kill);
                }
                _challenge = update.Challenge;
            }
            else _challenge = null;
        }

        private void StartQueue()
        {
            if (_ready && !_processing && !_halted && _unconfirmed.Count == 0 && _queue.Count > 0) _ = ProcessQueue();
        }

        private async Task ProcessQueue()
        {
            _processing = true;
            var summarize = _queue.Count > 1;
            var results = new List<RewardResult>();
            var luckUsed = new List<double>();
            try
            {
                while (_queue.Count > 0 && !_halted && _unconfirmed.Count == 0)
                {
                    RaidLifetime.RequirePlayer(_world, _player, _token);
                    var plan = _queue.Dequeue();
                    var placement = "failed";
                    RewardResult summaryResult = null;
                    try
                    {
                        if (plan.Kind == "shared") { await ChooseJackpot(plan); continue; }
                        if (plan.Kind == "grenade" || plan.Kind == "cursed")
                        {
                            MenuInteraction.CloseActive();
                            // Revalidate against server feature switches before starting any hazard.
                            var authorized = RaidApi.Event(_raidId, plan.Id);
                            var cooperative = FikaMaybe.Installed();
                            // In Fika the host waits once, after warning the whole party.
                            var prepared = await LiveEventRunner.PrepareAndExplain(_world, _player, plan, authorized.Config, _token,
                                waitBeforeSpawn: !cooperative);
                            if (cooperative)
                            {
                                var eventTask = FikaMaybe.SendEvent(_player, _raidId, plan, prepared.Position);
                                var claimed = false;
                                while (!eventTask.IsCompleted)
                                {
                                    claimed |= plan.Kind == "cursed" && LiveEventRunner.HasPrize(_player, plan.ItemId);
                                    await Task.Delay(25, _token);
                                }
                                placement = !await eventTask ? "failed" : plan.Kind == "grenade" ? "grenade"
                                    : claimed || LiveEventRunner.HasPrize(_player, plan.ItemId) ? "picked" : "expired";
                            }
                            else placement = await LiveEventRunner.Solo(_world, _player, plan, authorized.Config, prepared, _token);
                            if (placement == "expired") RewardNotifications.Notice(_config, "Cursed jackpot missed: its unclaimed prize has vanished.");
                            else if (placement == "failed") RewardNotifications.Notice(_config, "The live event failed. Check the logs.");
                            summaryResult = new RewardResult(plan.Reward, plan.StackCount, placement == "grenade" ? RewardPlacement.Grenade
                                : placement == "picked" ? RewardPlacement.Ground : placement == "expired" ? RewardPlacement.Missed : RewardPlacement.Failed);
                        }
                        else
                        {
                            var result = await _inventory.Place(_player, _config, plan, _raidId, _token);
                            placement = result.Placement.ToString().ToLowerInvariant();
                            summaryResult = result;
                            if (!summarize) RewardNotifications.Single(_config, result, plan);
                        }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (TimeoutException ex)
                    {
                        _halted = true;
#if POCKET_ROULETTE_TESTING
                        TestMessage = "Placement timed out. Test reset cannot safely retry an uncertain native transaction. See the logs.";
#endif
                        Plugin.LogSource.LogError($"[PocketRoulette] Placement timed out; later rolls stopped: {ex}");
                        RewardNotifications.Notice(_config, "Pocket Roulette stopped because a placement timed out. Pending items will not be retried.");
                    }
                    catch (Exception ex)
                    {
                        Plugin.LogSource.LogError($"[PocketRoulette] Grant {plan.Id} failed: {ex}");
                        if (plan.Kind == "shared")
                        {
                            // Opening/deciding is idempotent; retry the offer after the next successful poll.
                            _seenPlans.Remove(plan.Id);
                            _sync.Request(Time.unscaledTime, 3);
                            break;
                        }
                        RewardNotifications.Single(_config, new RewardResult(plan.Reward, plan.StackCount, RewardPlacement.Failed), plan);
                    }
                    if (summarize)
                    {
                        results.Add(summaryResult ?? new RewardResult(plan.Reward, plan.StackCount, RewardPlacement.Failed));
                        luckUsed.Add(plan.Luck);
                    }
                    _unconfirmed[plan.Id] = placement;
                    try
                    {
                        Apply(RaidApi.Action("complete", new RaidActionRequest { RaidId = _raidId, PlanId = plan.Id, Placement = placement }));
                        _unconfirmed.Remove(plan.Id);
                    }
                    catch (Exception ex) { Plugin.LogSource.LogWarning($"[PocketRoulette] Delivery confirmation pending: {ex.Message}"); }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Plugin.LogSource.LogError($"[PocketRoulette] Roll queue stopped: {ex}"); }
            finally
            {
                if (summarize && results.Count > 0 && !_token.IsCancellationRequested)
                {
                    var luck = _config.EnableLuck && _config.Luck.ShowAtRoll ? " Luck used: "
                        + string.Join(", ", luckUsed.GroupBy(value => value).Select(group => $"{group.Key:0.##} ({group.Count()} spin(s))")) + "." : "";
                    RewardNotifications.Summary(_config, results, luck);
                }
                _processing = false;
            }
        }

        private async Task ChooseJackpot(RollPlan plan)
        {
            var requestTime = System.Diagnostics.Stopwatch.StartNew();
            var update = RaidApi.Action("offer", new RaidActionRequest { RaidId = _raidId, PlanId = plan.Id,
                PartyIds = FikaMaybe.Party(_player).MemberIds });
            Apply(update);
            FikaMaybe.NotifyStateChanged();
            plan = update.Rolls.FirstOrDefault(entry => entry.Id == plan.Id) ?? plan;
            if (plan.DecisionMade.HasValue || plan.TeammateCount == 0) return;
            _offerId = plan.Id;
            _choice = new TaskCompletionSource<bool>();
            try
            {
                var remaining = Math.Max(0, plan.DecisionSeconds - requestTime.Elapsed.TotalSeconds);
                StartJackpotNotifications(plan, remaining);
                var timeout = Task.Delay(TimeSpan.FromSeconds(remaining), _token);
                var chosen = await Task.WhenAny(_choice.Task, timeout);
                _token.ThrowIfCancellationRequested();
                var share = chosen == _choice.Task && await _choice.Task;
                StopJackpotNotifications();
                Apply(RaidApi.Action("decide", new RaidActionRequest { RaidId = _raidId, PlanId = plan.Id, Share = share,
                    PartyIds = FikaMaybe.Party(_player).MemberIds }));
                FikaMaybe.NotifyStateChanged();
            }
            finally { StopJackpotNotifications(); _choice = null; _offerId = null; }
        }

        private void ChallengeProgress()
        {
            if (_challenge == null || _challenge.Completed || !ChallengeRules.Enabled(_config)) return;
            var progress = CurrentChallengeProgress();
            if (_challenge.Kind == "kills")
            {
                var kills = (int)progress;
                if (kills > _challengeQuarter && kills < _challenge.Target && _config.LuckyChallenges.NotifyProgress)
                    RewardNotifications.Notice(_config, $"Lucky challenge: {_challenge.Name} - {kills}/{_challenge.Target:0} kills.");
                _challengeQuarter = kills;
                return;
            }
            var quarter = Math.Min(3, (int)(progress / _challenge.Target * 4));
            if (quarter > _challengeQuarter)
            {
                _challengeQuarter = quarter;
                if (_config.LuckyChallenges.NotifyProgress) RewardNotifications.Notice(_config, $"Lucky challenge: {quarter * 25}% complete.");
            }
        }

        private void OnDestroy()
        {
            Player.OnPlayerDeadStatic -= TrackChallengeKill;
            StopJackpotNotifications();
#if POCKET_ROULETTE_TESTING
            if (_testingOriginal != null)
            {
                var normalId = _testingOriginal.RaidId;
                _ = Task.Run(() => { try { RaidApi.Action("end", new RaidActionRequest { RaidId = normalId }); } catch { } });
            }
#endif
            var id = _raidId;
            if (string.IsNullOrEmpty(id)) return;
            // Only the HTTP request leaves the Unity thread; no Unity objects are used here.
            _ = Task.Run(() =>
            {
                try { RaidApi.Action("end", new RaidActionRequest { RaidId = id }); }
                catch { }
            });
        }
    }
}
