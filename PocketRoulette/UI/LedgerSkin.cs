using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PocketRoulette
{
    // All artwork is cached once per menu. Nine-slice corners stay round at every window size.
    internal sealed class LedgerSkin : IDisposable
    {
        internal static readonly Color White = new Color(0.96f, 0.96f, 0.95f);
        internal static readonly Color Muted = new Color(0.65f, 0.70f, 0.75f);
        internal static readonly Color Gold = new Color(0.94f, 0.74f, 0.30f);
        internal readonly GUIStyle Panel, LuckCard, Card, Stash, Buy, Quiet, Red, Green, Neutral;
        internal readonly GUIStyle Title, Caption, Heading, Body, Small, Centered, Number, Stored, Balance, Badge, Time, Purchase;
        internal readonly GUIStyle Scrollbar;
        internal readonly Texture2D Roubles, Clover, Coins, Decay, Star, Spin, Chevron, Stripes;
        internal readonly Rect RoublesUV;
        private readonly List<Texture2D> _textures = new List<Texture2D>();

        internal LedgerSkin()
        {
            var edge = new Color(0.19f, 0.23f, 0.25f);
            var dark = new Color(0.035f, 0.047f, 0.051f, 0.97f);
            Panel = Box(dark, new Color(0.33f, 0.39f, 0.42f), 6);
            Card = Box(new Color(0.035f, 0.047f, 0.051f, 0.72f), edge);
            LuckCard = Box(new Color(0.12f, 0.10f, 0.041f, 0.88f), Gold);
            Stash = Box(new Color(0.10f, 0.08f, 0.031f, 0.85f), Gold);
            Buy = Button(new Color(0.48f, 0.39f, 0.17f), Gold, new Color(0.61f, 0.49f, 0.23f));
            Quiet = Button(new Color(0.11f, 0.14f, 0.16f), edge, new Color(0.18f, 0.22f, 0.24f));
            Red = Box(new Color(0.24f, 0.025f, 0.055f, 0.9f), new Color(0.90f, 0.08f, 0.20f));
            Green = Box(new Color(0.025f, 0.15f, 0.047f, 0.9f), new Color(0.08f, 0.61f, 0.18f));
            Neutral = Box(new Color(0.14f, 0.13f, 0.075f, 0.9f), new Color(0.52f, 0.45f, 0.23f));
            Title = Text(54, White, true); Caption = Text(25, Gold);
            Heading = Text(34, White, true); Body = Text(27, White, true);
            Small = Text(22, Muted); Centered = Text(22, Muted); Centered.alignment = TextAnchor.MiddleCenter;
            Number = Text(104, new Color(1, 0.97f, 0.87f), true);
            Stored = Text(60, White, true); Balance = Text(46, new Color(1, 0.80f, 0.39f), true);
            Badge = Text(20, White, true); Badge.alignment = TextAnchor.MiddleCenter;
            Time = Text(22, Muted); Time.alignment = TextAnchor.MiddleRight;
            Purchase = Text(27, White); Purchase.richText = true;
            Roubles = Load("roubles", true); RoublesUV = VisibleBounds(Roubles);
            Clover = Load("clover"); Coins = Load("coins"); Decay = Load("decay");
            Star = Load("star"); Spin = Load("spin"); Chevron = Load("chevron");
            Stripes = new Texture2D(96, 96, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color[96 * 96];
            for (var y = 0; y < 96; y++) for (var x = 0; x < 96; x++)
                pixels[y * 96 + x] = (x - y + 96) % 48 < 20 ? new Color(1, 0.79f, 0.32f, 0.035f) : Color.clear;
            Stripes.SetPixels(pixels); Stripes.Apply(false, true); _textures.Add(Stripes);
            Scrollbar = new GUIStyle(GUI.skin.verticalScrollbar) { fixedWidth = 10 };
            Scrollbar.normal.background = Rounded(new Color(0.10f, 0.12f, 0.13f), Color.clear, 5);
        }

        internal static GUIStyle Text(int size, Color color, bool bold = false) => new GUIStyle(GUI.skin.label)
        {
            fontSize = size, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
            normal = { textColor = color }, padding = new RectOffset(), clipping = TextClipping.Clip
        };

        private GUIStyle Box(Color fill, Color border, int radius = 12) => new GUIStyle
        {
            normal = { background = Rounded(fill, border, radius) }, border = new RectOffset(16, 16, 16, 16),
            padding = new RectOffset(), margin = new RectOffset()
        };

        private GUIStyle Button(Color fill, Color border, Color hover)
        {
            var style = Box(fill, border);
            style.hover.background = Rounded(hover, border, 12);
            style.active.background = Rounded(Color.Lerp(fill, Color.black, 0.25f), border, 12);
            style.focused.background = style.hover.background;
            style.fontSize = 27; style.normal.textColor = White; style.hover.textColor = Color.white;
            style.active.textColor = White; style.focused.textColor = White; style.alignment = TextAnchor.MiddleCenter;
            return style;
        }

        private Texture2D Rounded(Color fill, Color border, int radius)
        {
            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
            {
                var qx = Mathf.Abs(x + 0.5f - size / 2f) - (size / 2f - radius);
                var qy = Mathf.Abs(y + 0.5f - size / 2f) - (size / 2f - radius);
                var distance = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - radius;
                var value = Color.Lerp(fill, border, Mathf.Clamp01(distance + 1.5f));
                // Subtle light at the upper edge; kept out of transparent pixels.
                if (distance < -1.5f) value = Color.Lerp(value, Color.white, 0.025f * y / size);
                value.a *= Mathf.Clamp01(0.5f - distance); pixels[y * size + x] = value;
            }
            texture.SetPixels(pixels); texture.Apply(false, true); _textures.Add(texture); return texture;
        }

        private Texture2D Load(string name, bool readable = false)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var path = Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? "", "assets", name + ".png");
                if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path), !readable)) throw new IOException("Invalid PNG");
                _textures.Add(texture); return texture;
            }
            catch (Exception ex)
            {
                UnityEngine.Object.Destroy(texture);
                Plugin.LogSource.LogWarning($"[PocketRoulette] Ledger icon '{name}' unavailable: {ex.Message}");
                return null;
            }
        }

        private static Rect VisibleBounds(Texture2D texture)
        {
            if (texture == null) return new Rect(0, 0, 1, 1);
            // Ignore the supplied image's transparent padding without altering its file.
            var pixels = texture.GetPixels32();
            var left = texture.width; var bottom = texture.height; var right = -1; var top = -1;
            for (var y = 0; y < texture.height; y++) for (var x = 0; x < texture.width; x++)
            {
                if (pixels[y * texture.width + x].a < 8) continue;
                left = Math.Min(left, x); right = Math.Max(right, x);
                bottom = Math.Min(bottom, y); top = Math.Max(top, y);
            }
            texture.Apply(false, true);
            return right < left ? new Rect(0, 0, 1, 1)
                : new Rect((float)left / texture.width, (float)bottom / texture.height,
                    (float)(right - left + 1) / texture.width, (float)(top - bottom + 1) / texture.height);
        }

        public void Dispose() { foreach (var texture in _textures) if (texture != null) UnityEngine.Object.Destroy(texture); _textures.Clear(); }
    }
}
