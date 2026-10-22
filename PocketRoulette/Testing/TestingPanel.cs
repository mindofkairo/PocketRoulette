#if POCKET_ROULETTE_TESTING
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx.Configuration;
using EFT;
using EFT.InputSystem;
using PocketRoulette.Models;
using UnityEngine;

namespace PocketRoulette
{
    public sealed partial class TestingPanel : MonoBehaviour
    {
        private static ConfigEntry<KeyboardShortcut> _key;
        private PocketRouletteScript _raid;
        private bool _visible;
        private bool _renderLogged;
        private Rect _window = new Rect(35, 35, 790, 780);
        private Vector2 _scroll;
        private int _tab, _pockets, _placement;
        private bool _features, _armed, _overrideChance, _overrideCount, _overrideGround, _ground, _overrideNotices, _notices = true;
        private string _chance = "100", _count = "1", _luck = "5", _days = "4", _target = "5", _stack = "1", _search = "", _samples = "10000", _seed = "12345";
        private int _mode;
        private bool _overrideCursed, _overrideDecision;
        private string _cursedDelay = "8", _decisionSeconds = "5";
        private string _error = "";
        private readonly HashSet<string> _checked = new HashSet<string>();
        private static readonly string[] Modes = { "server", "mixed", "garbage", "useful", "jackpot", "chaos" };
        private static readonly string[] Checks =
        {
            "Normal chance: misses, successful starts and multi-spin summaries",
            "Common / uncommon / rare / epic-ultrarare: correct item and luck notice",
            "Use, consume, reload and drop spawned items; hands remain usable",
            "Full pockets: missed reward and ground fallback; pick ground loot up",
            "Stack counts clamp to the EFT template maximum",
            "Bad-luck gain, rare-win decay, cap and inactivity decay",
            "Empty-pocket bonus: actual, simulated empty and simulated occupied",
            "Luck disabled and individual feature switches respected",
            "Every grenade: warning, delay, location, visibility and native effects",
            "Cursed prize picked up in time; picked item survives deadline",
            "Cursed prize ignored: original loose prize disappears at deadline",
            "Shared jackpot: share, keep, timeout and automatic solo keep",
            "Shared jackpot: guest winner, disconnect and distinct teammate luck",
            "Survival/travel/kill challenges: assignment, distance/type filters and one-time reward",
            "Buy-in simulation: affordable and insufficient funds; no pity farming",
            "Fika: host/guest/headless see the same loot, choices and hazards",
            "Real scav raid: enabled/disabled roulette and no paid spins",
            "Config hot reload, client overrides allowed/denied and notifications off",
            "Extract with test loot; restart and verify no duplicate IDs/items",
            "Separate real buy-in raid: exact stash debit, UI refresh and saved balance"
        };

        public static void Bind(ConfigFile config)
        {
            _key = config.Bind("Temporary Test Tools", "Open Test Panel", new KeyboardShortcut(KeyCode.End),
                "Test builds only. END opens the menu in raid; gameplay continues while it is open.");
            if (_key.Value.MainKey == KeyCode.F10 && !_key.Value.Modifiers.Any())
            {
                _key.Value = new KeyboardShortcut(KeyCode.End);
                Plugin.LogSource?.LogInfo("[PocketRoulette TEST] Migrated the old F10 menu binding to END.");
            }
        }

        private void Awake()
        {
            _raid = GetComponent<PocketRouletteScript>();
            Plugin.LogSource.LogInfo($"[PocketRoulette TEST] Menu ready. Open with {_key.Value} while in raid.");
        }
        private void Update()
        {
            if (_key != null && _key.Value.IsDown()) SetVisible(!_visible);
            if (_visible && Input.GetKeyDown(KeyCode.Escape)) SetVisible(false);
            if (_visible && (_raid == null || !_raid.TestPanelAvailable)) SetVisible(false);
        }
        private void LateUpdate()
        {
            if (!_visible) return;
            MaintainMenuInput();
        }
        private void SetVisible(bool visible)
        {
            if (visible == _visible) return;
            if (visible)
            {
                if (_raid == null || !_raid.TestPanelAvailable)
                {
                    Plugin.LogSource.LogWarning("[PocketRoulette TEST] The test menu is available while your local player is alive in a raid.");
                    return;
                }
                MenuInteraction.Acquire(this, _raid.TestPlayer, () => SetVisible(false));
                _renderLogged = false;
                _window.x = Mathf.Max(10, (Screen.width - Math.Min(900, Screen.width - 20)) / 2f);
                _window.y = 20;
            }
            else
            {
                ReleaseMenuInput();
            }
            _visible = visible;
            if (visible) MaintainMenuInput();
            Plugin.LogSource.LogInfo(visible ? "[PocketRoulette TEST] Test menu opened." : "[PocketRoulette TEST] Test menu closed.");
        }
        private void OnDestroy()
        {
            SetVisible(false);
            DestroyMenuTheme();
        }
        private void OnDisable() => SetVisible(false);
        public void CloseForHazard() => SetVisible(false);
        private void OnGUI()
        {
            if (!_visible) return;
            DrawMenuWindow();
        }
        private void Draw(int id)
        {
            GUILayout.Label("Test luck/progress are isolated. Loot, explosions and damage are REAL. The raid keeps running.");
            GUILayout.BeginHorizontal();
            GUI.enabled = _raid.TestReady && !_raid.TestBusy;
            if (GUILayout.Button(_raid.InTesting ? "Leave sandbox" : "Enter sandbox", GUILayout.Width(150)))
                Run(_raid.InTesting ? "stop" : "start");
            GUI.enabled = true;
            GUILayout.Label(_raid.InTesting ? "SANDBOX ACTIVE" : "Normal raid active");
            if (GUILayout.Button("Close", GUILayout.Width(65))) SetVisible(false);
            GUILayout.EndHorizontal();
            if (_raid.TestUpdate != null)
            {
                var update = _raid.TestUpdate;
                GUILayout.Label($"Luck: stored {update.StoredLuck:0.##} + empty {update.EmptyPocketLuck:0.##} -> effective {update.Luck:0.##}   |   "
                    + $"Pending: {update.Testing.Pending}   Pity: {update.Testing.PityApplied}   Win decay: {update.Testing.WinApplied}");
                GUILayout.Label($"Eligible shared-jackpot teammates in sandbox: {update.Testing.SharingTeammates}. "
                    + "Each participating player must press END and select Enter sandbox.");
            }
            GUILayout.Label(_raid.TestMessage, GUI.skin.box);
            if (_error.Length > 0) GUILayout.Label(_error, GUI.skin.box);
            if (_raid.TestChoiceOpen)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("SHARE this jackpot (F8)")) _raid.TestChoose(true);
                if (GUILayout.Button("KEEP this jackpot (F9)")) _raid.TestChoose(false);
                GUILayout.EndHorizontal();
            }
            _tab = GUILayout.Toolbar(_tab, new[] { "Rolls", "Events", "Luck", "Challenges", "Buy-in", "Odds", "Checklist" });
            _scroll = GUILayout.BeginScrollView(_scroll);
            if (_tab != 6) Rules();
            GUI.enabled = _raid.TestReady && _raid.InTesting && !_raid.TestBusy;
            switch (_tab)
            {
                case 0: Rolls(); break;
                case 1: Events(); break;
                case 2: Luck(); break;
                case 3: Challenges(); break;
                case 4: Purchases(); break;
                case 5: Odds(); break;
                case 6: GUI.enabled = true; Checklist(); break;
            }
            GUI.enabled = true;
            GUILayout.EndScrollView();
            if (_raid.TestBusy) GUILayout.Label("Waiting for placement, event, choice or server acknowledgement. A running grenade cannot be cancelled.");
            GUI.DragWindow(new Rect(0, 0, _window.width - 5, 22));
        }
        private void Rules()
        {
            GUILayout.Label("Rules for the next command (normal uses server settings; grenades require arming):");
            _features = GUILayout.Toggle(_features, "Enable all optional features in this sandbox");
            _armed = GUILayout.Toggle(_armed, "ARM LIVE GRENADES - real explosions / smoke / flashes / player damage");
            GUILayout.BeginHorizontal();
            _overrideChance = GUILayout.Toggle(_overrideChance, "Override chance %", GUILayout.Width(155));
            _chance = GUILayout.TextField(_chance, GUILayout.Width(55));
            _overrideCount = GUILayout.Toggle(_overrideCount, "Override spin count", GUILayout.Width(165));
            _count = GUILayout.TextField(_count, GUILayout.Width(50));
            GUILayout.EndHorizontal();
            _mode = GUILayout.Toolbar(_mode, Modes);
            GUILayout.Label("Pocket snapshot for empty-pocket luck:");
            _pockets = GUILayout.Toolbar(_pockets, new[] { "Actual pockets NOW", "Pretend empty", "Pretend occupied" });
            GUILayout.Label("Item delivery (no items are moved out of your real pockets):");
            _placement = GUILayout.Toolbar(_placement, new[] { "Actual pockets", "Simulate full", "Force ground", "Force missed" });
            GUILayout.BeginHorizontal();
            _overrideGround = GUILayout.Toggle(_overrideGround, "Override ground fallback");
            _ground = GUILayout.Toggle(_ground, "Fallback enabled");
            _overrideNotices = GUILayout.Toggle(_overrideNotices, "Override notifications");
            _notices = GUILayout.Toggle(_notices, "Show notices");
            GUILayout.EndHorizontal();
        }
        private void Rolls()
        {
            GUILayout.Label("Repeat the raid-start chance, configured spin count, challenge chance and luck rules without reloading the map.");
            if (GUILayout.Button("Run NORMAL raid-start rolls")) Run("normal");
            GUILayout.Label("Guaranteed rarity (bypasses chance and rarity mode; each batch tests normal pity / rare-win decay):");
            GUILayout.BeginHorizontal();
            foreach (var rarity in new[] { "common", "uncommon", "rare", "epic" })
                if (GUILayout.Button(rarity == "epic" ? "Epic / ultrarare" : rarity)) Run("force", rarity);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Requested stack (EFT maximum still applies):", GUILayout.Width(300));
            _stack = GUILayout.TextField(_stack, GUILayout.Width(65));
            GUILayout.EndHorizontal();
            GUILayout.Label("Specific configured item - search name or TPL:");
            _search = GUILayout.TextField(_search);
            var items = _raid.TestConfig?.ItemPool.Where(item => _search.Length == 0
                || item.Name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0 || item.Tpl.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            if (items == null) return;
            foreach (var item in items.Take(15))
                if (GUILayout.Button($"{item.Name}  [{item.Rarity}]")) Run("force", item.Tpl);
            if (items.Count > 15) GUILayout.Label($"{items.Count} matches - narrow the search to see more.");
        }
        private void Events()
        {
            GUILayout.BeginHorizontal();
            _overrideCursed = GUILayout.Toggle(_overrideCursed, "Override cursed deadline (seconds)");
            _cursedDelay = GUILayout.TextField(_cursedDelay, GUILayout.Width(65));
            _overrideDecision = GUILayout.Toggle(_overrideDecision, "Override choice timeout (seconds)");
            _decisionSeconds = GUILayout.TextField(_decisionSeconds, GUILayout.Width(65));
            GUILayout.EndHorizontal();
            GUILayout.Label("Cursed: try picking up the prize and ignoring it. The panel closes before hazards start so you can move.");
            if (GUILayout.Button("Force CURSED JACKPOT")) Run("force", "cursed", hazard: true);
            GUILayout.Label("Fika sharing test: BOTH players must press END -> Enter sandbox. Shared jackpots must be enabled for both. "
                + "Teammates still in normal raid mode are excluded from sandbox gifts. Normal jackpots do not require sandbox mode.");
            if (GUILayout.Button("Force SHARED JACKPOT (share / keep / wait for timeout)")) Run("force", "shared");
            if (GUILayout.Button("Force normal GRENADE WHEEL selection")) Run("force", "grenade", hazard: true);
            if (_raid.TestConfig != null)
                foreach (var choice in _raid.TestConfig.GrenadeWheel)
                    if (GUILayout.Button($"Force {choice.Name}")) Run("grenade", choice.Tpl, hazard: true);
            GUILayout.Label("To test disabled features, leave 'Enable all optional features' off. Grenade commands still require ARM.");
        }
        private void Luck()
        {
            GUILayout.Label("These values affect your sandbox account only. Other players' luck and saved normal luck stay independent.");
            GUILayout.BeginHorizontal();
            _luck = GUILayout.TextField(_luck, GUILayout.Width(80));
            if (GUILayout.Button("Set stored luck")) Run("luck", amount: Number(_luck));
            if (GUILayout.Button("Zero luck")) Run("luck", amount: 0);
            if (GUILayout.Button("Stored luck cap")) Run("luck", amount: _raid.TestConfig?.BadLuckProtection.MaxStoredLuck ?? 10);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            _days = GUILayout.TextField(_days, GUILayout.Width(80));
            if (GUILayout.Button("Simulate inactive days and apply decay")) Run("decay", amount: Number(_days));
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Force unlucky COMMON batch (test pity gain)")) Run("force", "common");
            if (GUILayout.Button("Force RARE batch (test received-win decay)")) Run("force", "rare");
            if (GUILayout.Button("Reset test luck / challenge / buy-in counters")) Run("reset");
            GUILayout.Label("Empty-pocket controls above apply on the next command. Real pockets are never cleared. Reset does not remove spawned loot.");
        }
        private void Challenges()
        {
            var challenge = _raid.TestUpdate?.Challenge;
            if (challenge != null) GUILayout.Label($"Current: {challenge.Kind}, target {challenge.Target:0.##}, complete {challenge.Completed}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Quick: survive 5 seconds")) Run("challenge", "survive", 5);
            if (GUILayout.Button("Quick: walk 10 metres")) Run("challenge", "walk", 10);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            _target = GUILayout.TextField(_target, GUILayout.Width(80));
            if (GUILayout.Button("Assign survival target (seconds)")) Run("challenge", "survive", Number(_target));
            if (GUILayout.Button("Assign walking target (metres)")) Run("challenge", "walk", Number(_target));
            GUILayout.EndHorizontal();
            foreach (var preset in _raid.TestConfig.LuckyChallenges.KillPresets.Where(entry => entry.Enabled))
                if (GUILayout.Button($"Assign: {preset.Name} - {ChallengeRules.Description(ChallengeRules.FromPreset(preset))}"))
                    Run("challenge", preset.Id);
            GUILayout.Label("Close the menu and play normally, or inject sandbox progress below. Injected kills do not test native death events.");
            GUILayout.BeginHorizontal();
            foreach (var percent in new[] { 25, 50, 75, 100 })
                if (GUILayout.Button(percent + "% progress")) Run("progress", amount: percent);
            GUILayout.EndHorizontal();
            GUILayout.Label("NORMAL raid-start rolls test assignment chance. Repeated completion must not give another bonus spin.");
        }
        private void Purchases()
        {
            GUILayout.Label("Simulated wallet: exercises price/cap, insufficient funds, purchase notices and real bonus-roll delivery.");
            GUILayout.Label("Actual stash roubles are never charged by these buttons. Profile-save/debit recovery still needs the real buy-in acceptance test.");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Buy 1 spin - simulated rich wallet")) Run("purchase", "funded", 1);
            if (GUILayout.Button("Buy max spins - simulated rich wallet")) Run("purchase", "funded", _raid.TestConfig?.BuyInSpins.MaxSpinsPerRaid ?? 2);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Attempt purchase - simulated empty wallet")) Run("purchase", "empty", 1);
            GUILayout.Label($"Last purchase: {_raid.TestUpdate?.BoughtSpins ?? 0} spins / {_raid.TestUpdate?.PaidRoubles ?? 0:N0} simulated roubles.");
        }
        private void Odds()
        {
            GUILayout.Label("No-spawn sampling: actual chance and weighted selection code, fixed current luck for every sampled raid. No pity progression, loot or payments.");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Raids (1-10,000):", GUILayout.Width(125)); _samples = GUILayout.TextField(_samples, GUILayout.Width(75));
            GUILayout.Label("Seed:", GUILayout.Width(50)); _seed = GUILayout.TextField(_seed, GUILayout.Width(95));
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Sample normal raid odds; write results to BepInEx log")) Run("sample");
            var counts = _raid.TestUpdate?.Testing.Counts;
            if (counts == null || counts.Count == 0) return;
            foreach (var row in counts)
            {
                var denominator = row.Key == "triggered" || row.Key == "missed" || row.Key == "challenges" ? counts["raids"]
                    : row.Key.StartsWith("wheel:") ? (counts.ContainsKey("grenade") ? counts["grenade"] : 0)
                    : counts["rolls"];
                var rate = row.Key == "raids" || row.Key == "rolls" ? "" : $"  ({100.0 * row.Value / Math.Max(1, denominator):0.##}%)";
                GUILayout.Label($"{row.Key}: {row.Value:N0}{rate}");
            }
            GUILayout.Label("Triggered / missed / challenges use sampled raids. Item/event rates use all generated rolls. Wheel rates use grenade outcomes.");
        }
        private void Checklist()
        {
            GUILayout.Label("Manual acceptance checklist (checks last for this raid; export writes the marked results to your BepInEx log):");
            foreach (var text in Checks)
            {
                if (GUILayout.Toggle(_checked.Contains(text), text)) _checked.Add(text);
                else _checked.Remove(text);
            }
            if (GUILayout.Button("Export checklist and current test state to log"))
                Plugin.LogSource.LogInfo("[TEST CHECKLIST] " + string.Join(" | ", Checks.Select(text => (_checked.Contains(text) ? "PASS: " : "TODO: ") + text))
                    + " State: " + Newtonsoft.Json.JsonConvert.SerializeObject(_raid.TestUpdate));
        }
        private static double Number(string text) => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : double.NaN;
        private void Run(string command, string value = "", double amount = 0, bool hazard = false)
        {
            _error = "";
            if (command == "start" || command == "stop")
            {
                _raid.TestCommand(new TestingRequest { Command = command });
                return;
            }
            int? chance = null, count = null;
            if (_overrideChance)
            {
                if (!int.TryParse(_chance, out var parsed) || parsed < 0 || parsed > 100) { _error = "Chance must be an integer from 0 to 100."; return; }
                chance = parsed;
            }
            if (_overrideCount)
            {
                if (!int.TryParse(_count, out var parsed) || parsed < 1 || parsed > 20) { _error = "Spin count must be 1-20."; return; }
                count = parsed;
            }
            if (command == "force" && value != "cursed" && value != "shared" && value != "grenade") amount = Number(_stack);
            if (double.IsNaN(amount) || double.IsInfinity(amount)) { _error = "Enter a valid number using a decimal point."; return; }
            if (_overrideCursed && (!float.TryParse(_cursedDelay, NumberStyles.Float, CultureInfo.InvariantCulture, out var delay) || float.IsNaN(delay) || delay < 2 || delay > 120))
            { _error = "Cursed deadline must be 2-120 seconds."; return; }
            if (_overrideDecision && (!float.TryParse(_decisionSeconds, NumberStyles.Float, CultureInfo.InvariantCulture, out var decision) || float.IsNaN(decision) || decision < 5 || decision > 120))
            { _error = "Jackpot choice timeout must be 5-120 seconds."; return; }
            var samples = 10000; var seed = 12345;
            if (command == "sample" && (!int.TryParse(_samples, out samples) || !int.TryParse(_seed, out seed)))
            { _error = "Samples and seed must be integers."; return; }
            var request = new TestingRequest { Command = command, Value = value, Amount = amount, Samples = samples, Seed = seed,
                EmptyPockets = _pockets == 1 || _pockets == 0 && _raid.TestEmptyPockets(), Options = new TestingOptions
                {
                    EnableFeatures = _features, ArmGrenades = _armed, Chance = chance, Count = count,
                    Mode = _mode == 0 ? "" : Modes[_mode], Placement = new[] { "actual", "full", "ground", "miss" }[_placement],
                    GroundFallback = _overrideGround ? (bool?)_ground : null, Notifications = _overrideNotices ? (bool?)_notices : null,
                    CursedDelay = _overrideCursed ? (float?)Number(_cursedDelay) : null,
                    DecisionSeconds = _overrideDecision ? (float?)Number(_decisionSeconds) : null
                } };
            if (hazard) SetVisible(false);
            var success = _raid.TestCommand(request);
            if (hazard && !success) SetVisible(true);
        }
    }
}
#endif
