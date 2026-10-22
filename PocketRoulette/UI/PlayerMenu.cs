using System;
using BepInEx.Configuration;
using PocketRoulette.Models;
using UnityEngine;

namespace PocketRoulette
{
    internal sealed class PlayerMenu : MonoBehaviour
    {
        // Uniform scaling preserves the reference artwork's proportions on different displays.
        private const float Width = 1060, Height = 1164;
        private static ConfigEntry<KeyboardShortcut> _key;
        private static ConfigEntry<float> _scale;
        private PocketRouletteScript _raid;
        private PlayerMenuInfo _info;
        private bool _visible;
        private string _error = "";
        private Vector2 _scroll;
        // Remember the position within the available screen space, including after a scale change.
        private Vector2 _windowPosition = new Vector2(0.5f, 0.5f);
        private LedgerSkin _skin;
        private Texture2D _marker;

        public static void Bind(ConfigFile config)
        {
            _key = config.Bind("Player Menu", "Open Roulette Ledger", new KeyboardShortcut(KeyCode.Home),
                "Open your recent rolls, luck and extra-spin purchases.");
            _scale = config.Bind("Player Menu", "Ledger Scale", 0.55f,
                new ConfigDescription("Ledger size relative to the original layout (default 0.55). Drag its header to move it. It always fits the screen.",
                    new AcceptableValueRange<float>(0.25f, 1.25f)));
        }
        private void Awake() => _raid = GetComponent<PocketRouletteScript>();
        private void Update()
        {
            if (_key != null && _key.Value.IsDown()) SetVisible(!_visible);
            if (_visible && (Input.GetKeyDown(KeyCode.Escape) || !_raid.MenuAvailable)) SetVisible(false);
        }
        private void LateUpdate() { if (_visible) MenuInteraction.Maintain(this); }
        private void SetVisible(bool visible)
        {
            if (_visible == visible) return;
            if (visible)
            {
                if (_raid == null || !_raid.MenuAvailable) return;
                MenuInteraction.Acquire(this, _raid.MenuPlayer, () => SetVisible(false));
                _visible = true; _error = ""; _scroll = Vector2.zero;
                _info = _raid.ReadPlayerMenu();
                return;
            }
            MenuInteraction.Release(this); _visible = false;
        }
        private void OnDisable() => SetVisible(false);
        private void OnDestroy() { SetVisible(false); _skin?.Dispose(); if (_marker != null) Destroy(_marker); }
        private void OnGUI()
        {
            if (!_visible) return;
            MenuInteraction.Maintain(this);
            var color = GUI.color; var matrix = GUI.matrix; var depth = GUI.depth;
            var enabled = GUI.enabled; var background = GUI.backgroundColor; var content = GUI.contentColor;
            try
            {
                if (_skin == null) _skin = new LedgerSkin();
                GUI.matrix = Matrix4x4.identity; GUI.depth = -9999; GUI.enabled = true;
                GUI.color = GUI.backgroundColor = GUI.contentColor = Color.white;
                Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, 0.38f));
                var fits = Mathf.Min((Screen.width - 48f) / Width, (Screen.height - 48f) / Height);
                var scale = Mathf.Min(fits, Mathf.Min(1, fits) * (_scale?.Value ?? 0.55f));
                scale = Mathf.Max(0.1f, scale);
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
                var margin = 12f / scale;
                var travel = new Vector2(Mathf.Max(0, Screen.width / scale - Width - 2 * margin),
                    Mathf.Max(0, Screen.height / scale - Height - 2 * margin));
                var window = new Rect(margin + travel.x * _windowPosition.x,
                    margin + travel.y * _windowPosition.y, Width, Height);
                var moved = GUI.Window(697732, window, Draw, GUIContent.none, GUIStyle.none);
                _windowPosition = new Vector2(travel.x > 0 ? Mathf.Clamp01((moved.x - margin) / travel.x) : 0.5f,
                    travel.y > 0 ? Mathf.Clamp01((moved.y - margin) / travel.y) : 0.5f);
                GUI.BringWindowToFront(697732);
            }
            catch (ExitGUIException) { throw; }
            catch (Exception ex) { SetVisible(false); Plugin.LogSource.LogError("[PocketRoulette] Ledger menu failed: " + ex); }
            finally
            {
                GUI.color = color; GUI.matrix = matrix; GUI.depth = depth; GUI.enabled = enabled;
                GUI.backgroundColor = background; GUI.contentColor = content;
            }
        }

        private void Draw(int id)
        {
            GUI.Box(new Rect(0, 0, Width, Height), GUIContent.none, _skin.Panel);
            Fill(new Rect(2, 4, 6, Height - 8), LedgerSkin.Gold);
            GUI.Label(new Rect(46, 26, 840, 71), "Pocket Roulette", _skin.Title);
            GUI.Label(new Rect(49, 94, 820, 34), _info?.Sandbox == true ? "TEST SANDBOX / SEPARATE HISTORY" : "YOUR RAID LEDGER", _skin.Caption);
            var close = new Rect(950, 33, 69, 65);
            if (GUI.Button(close, _skin.Chevron == null ? "‹" : "", _skin.Quiet)) SetVisible(false);
            Icon(_skin.Chevron, new Rect(973, 53, 22, 26), LedgerSkin.Muted);
            Line(46, 132, 974);
            DrawLuck();
            GUI.Label(new Rect(46, 343, 974, 28), _info?.LuckEnabled == false
                ? "Luck is disabled for this raid." : "Inactivity decay is applied when your next raid starts.", _skin.Centered);
            Line(46, 378, 974);
            GUI.Label(new Rect(46, 391, 974, 47), "Recent rolls", _skin.Heading);
            DrawHistory();
            DrawWallet();
            GUI.DragWindow(new Rect(12, 8, 920, 122));
        }

        private void DrawLuck()
        {
            GUI.Box(new Rect(46, 150, 388, 168), GUIContent.none, _skin.LuckCard);
            GUI.Label(new Rect(78, 169, 290, 35), "CURRENT LUCK", _skin.Caption);
            Icon(_skin.Clover, new Rect(365, 174, 48, 53), LedgerSkin.Gold);
            var luck = _info == null ? "-" : _info.LuckEnabled ? _info.EffectiveLuck.ToString("0.##") : "OFF";
            FitLabel(new Rect(75, 198, 335, 116), luck, _skin.Number, 104);

            GUI.Box(new Rect(454, 150, 566, 168), GUIContent.none, _skin.Card);
            Icon(_skin.Coins, new Rect(486, 194, 63, 64), LedgerSkin.Gold);
            GUI.Label(new Rect(575, 177, 158, 33), "Stored", _skin.Small);
            FitLabel(new Rect(571, 206, 166, 78), _info == null ? "-" : _info.StoredLuck.ToString("0.##"), _skin.Stored, 60);
            Fill(new Rect(744, 181, 1, 108), new Color(0.40f, 0.41f, 0.39f));
            GUI.Label(new Rect(793, 177, 218, 33), "Inactivity decay", _skin.Small);
            Icon(_skin.Decay, new Rect(776, 215, 31, 29), LedgerSkin.Gold);
            FitLabel(new Rect(827, 211, 181, 38), _info == null ? "-" : $"-{_info.InactivityDecayPerDay:0.##}/day", _skin.Body, 27);
            Icon(_skin.Star, new Rect(779, 256, 27, 27), LedgerSkin.Gold);
            FitLabel(new Rect(827, 255, 181, 36), _info == null ? "Rare win  -" : $"Rare win  -{_info.WinDecayPercent:0.##}%", _skin.Small, 22);
            if (_info?.EmptyPocketLuck > 0)
                GUI.Label(new Rect(454, 318, 566, 25), $"Includes +{_info.EmptyPocketLuck:0.##} for empty pockets", _skin.Centered);
        }

        private void DrawHistory()
        {
            var view = new Rect(46, 440, 974, 398);
            if (_info == null || _info.History.Count == 0)
            {
                GUI.Box(view, GUIContent.none, _skin.Card);
                GUI.Label(new Rect(view.x + 30, view.y + 150, view.width - 60, 70),
                    _info == null ? _raid.MenuError : "Your next roll will appear here.", _skin.Centered);
                return;
            }
            var rowWidth = _info.History.Count > 4 ? 956 : 974;
            _scroll = GUI.BeginScrollView(view, _scroll, new Rect(0, 0, rowWidth, _info.History.Count * 102 - 12),
                false, false, GUIStyle.none, _skin.Scrollbar);
            var first = Mathf.Max(0, Mathf.FloorToInt(_scroll.y / 102));
            var last = Mathf.Min(_info.History.Count, Mathf.CeilToInt((_scroll.y + view.height) / 102));
            for (var index = first; index < last; index++) DrawRoll(_info.History[index], index * 102, rowWidth);
            GUI.EndScrollView();
        }

        private void DrawRoll(RollHistoryEntry entry, float y, float width)
        {
            GUI.Box(new Rect(0, y, width, 90), GUIContent.none, _skin.Card);
            var rarity = entry.Rarity == "ultrarare" ? new Color(0.81f, 0.62f, 1)
                : entry.Rarity == "rare" ? new Color(0.43f, 0.70f, 0.92f) : LedgerSkin.Gold;
            var before = GUI.color; GUI.color = rarity;
            GUI.DrawTexture(new Rect(35, y + 35, 18, 18), _marker ?? (_marker = MakeMarker())); GUI.color = before;
            Fill(new Rect(89, y + 17, 2, 56), LedgerSkin.Gold);
            var name = string.IsNullOrWhiteSpace(entry.Name) ? "Reward" : entry.Name;
            if (entry.Count > 1) name += " ×" + entry.Count;
            var nameRect = new Rect(124, y + 11, width - 420, 35);
            FitLabel(nameRect, name, _skin.Body, 27);
            var odds = entry.OddsPercent > 0 ? entry.OddsPercent.ToString("0.##") + "%" : "Odds not recorded";
            FitLabel(new Rect(124, y + 51, width - 420, 30), $"{odds}   ·   Luck {entry.Luck:0.##}   ·   {Origin(entry.Origin)}", _skin.Small, 22);
            var placement = Placement(entry.Placement);
            var isMiss = placement == "MISSED" || placement == "FAILED" || placement == "EXPIRED";
            var delivered = placement == "POCKETS" || placement == "GROUND" || placement == "PICKED UP";
            var badgeRect = new Rect(width - 283, y + 21, 153, 44);
            GUI.Box(badgeRect, GUIContent.none, isMiss ? _skin.Red : delivered ? _skin.Green : _skin.Neutral);
            _skin.Badge.normal.textColor = isMiss ? new Color(1, 0.40f, 0.46f) : delivered ? new Color(0.32f, 0.89f, 0.38f) : LedgerSkin.Gold;
            GUI.Label(badgeRect, placement, _skin.Badge);
            GUI.Label(new Rect(width - 105, y + 22, 77, 43), entry.RolledUtc.ToLocalTime().ToString("HH:mm"), _skin.Time);
        }

        private Texture2D MakeMarker()
        {
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color[1024];
            for (var y = 0; y < 32; y++) for (var x = 0; x < 32; x++)
                pixels[y * 32 + x] = new Color(1, 1, 1, Mathf.Clamp01(15.5f - new Vector2(x - 15.5f, y - 15.5f).magnitude));
            texture.SetPixels(pixels); texture.Apply(false, true); return texture;
        }

        private void DrawWallet()
        {
            GUI.Box(new Rect(46, 872, 974, 118), GUIContent.none, _skin.Stash);
            GUI.DrawTextureWithTexCoords(new Rect(566, 881, 443, 99), _skin.Stripes, new Rect(0, 0, 443f / 96, 99f / 96));
            Roubles(new Rect(754, 881, 117, 105), new Color(1, 0.84f, 0.47f, 0.11f));
            Roubles(new Rect(875, 874, 125, 112), new Color(1, 0.84f, 0.47f, 0.08f));
            Roubles(new Rect(70, 879, 131, 105), Color.white);
            GUI.Label(new Rect(229, 888, 745, 34), "Stash", _skin.Small);
            FitLabel(new Rect(227, 922, 740, 57), _info == null ? "-" : $"₽{_info.StashRoubles:N0}", _skin.Balance, 46);

            GUI.enabled = _info != null && _info.CanBuy && _raid.CanPurchaseNow;
            var button = new Rect(46, 1007, 974, 68);
            var bought = GUI.Button(button, GUIContent.none, _skin.Buy);
            var label = _info == null ? "Extra spin unavailable" : $"Buy an extra spin   |   <color=#F4C563><b>₽{_info.PriceRoubles:N0}</b></color>";
            var labelWidth = _skin.Purchase.CalcSize(new GUIContent(label)).x;
            var start = button.center.x - (labelWidth + 62) / 2;
            Icon(_skin.Spin, new Rect(start, 1025, 34, 34), new Color(1, 0.94f, 0.73f));
            GUI.Label(new Rect(start + 56, 1023, labelWidth + 8, 40), label, _skin.Purchase);
            GUI.enabled = true;
            if (bought)
            {
                if (_raid.BuyMenuSpin(_info.PriceRoubles)) SetVisible(false);
                else { _error = _raid.MenuError; _info = _raid.ReadPlayerMenu(); }
            }
            var reason = _error.Length > 0 ? _error : _raid.MenuError.Length > 0 ? _raid.MenuError : _info?.BuyUnavailableReason ?? "";
            var wrap = _skin.Small.wordWrap; _skin.Small.wordWrap = true;
            GUI.Label(new Rect(49, 1093, 777, 56), reason.Length == 0
                ? "Roubles come from your stash. The menu closes after purchase." : reason, _skin.Small);
            _skin.Small.wordWrap = wrap;
            if (GUI.Button(new Rect(847, 1092, 173, 47), GUIContent.none, _skin.Quiet)) { _error = ""; _info = _raid.ReadPlayerMenu(); }
            Icon(_skin.Spin, new Rect(866, 1101, 29, 29), LedgerSkin.Muted);
            GUI.Label(new Rect(916, 1101, 95, 32), "Refresh", _skin.Small);
        }

        private void Roubles(Rect rect, Color tint)
        {
            if (_skin.Roubles == null) return;
            var uv = _skin.RoublesUV;
            var aspect = _skin.Roubles.width * uv.width / (_skin.Roubles.height * uv.height);
            var width = Mathf.Min(rect.width, rect.height * aspect);
            var height = width / aspect;
            rect = new Rect(rect.center.x - width / 2, rect.center.y - height / 2, width, height);
            var before = GUI.color; GUI.color = tint;
            GUI.DrawTextureWithTexCoords(rect, _skin.Roubles, uv); GUI.color = before;
        }

        private static void FitLabel(Rect rect, string text, GUIStyle style, int size)
        {
            style.fontSize = size;
            while (style.fontSize > 18 && style.CalcSize(new GUIContent(text)).x > rect.width) style.fontSize--;
            GUI.Label(rect, text, style); style.fontSize = size;
        }
        private static void Icon(Texture texture, Rect rect, Color tint)
        {
            if (texture == null) return;
            var before = GUI.color; GUI.color = tint; GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true); GUI.color = before;
        }
        private static void Fill(Rect rect, Color color)
        {
            var before = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = before;
        }
        private static void Line(float x, float y, float width) => Fill(new Rect(x, y, width, 1), new Color(0.19f, 0.23f, 0.25f));
        private static string Placement(string placement) => placement == "pocket" ? "POCKETS" : placement == "ground" ? "GROUND"
            : placement == "picked" ? "PICKED UP" : placement == "pending" ? "PENDING" : placement == "grenade" ? "LIVE EVENT"
            : placement == "failed" ? "FAILED" : placement == "expired" ? "EXPIRED" : placement == "missed" ? "MISSED" : "UNKNOWN";
        private static string Origin(string origin) => origin == "raid" ? "Raid start" : origin == "buy-in" || origin == "recovered-buy-in" ? "Extra spin"
            : origin == "challenge" ? "Challenge" : origin == "shared-gift" ? "Shared gift" : origin == "test-forced" ? "Forced test" : "Jackpot";
    }
}
