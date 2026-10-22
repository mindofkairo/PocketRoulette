#if POCKET_ROULETTE_TESTING
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PocketRoulette
{
    public sealed partial class TestingPanel
    {
        private const int WindowId = 697731;
        private GUISkin _menuSkin;
        private GUIStyle _shadeStyle;
        private readonly List<Texture2D> _menuTextures = new List<Texture2D>();

        private void MaintainMenuInput()
        {
            MenuInteraction.Maintain(this);
        }
        private void ReleaseMenuInput()
        {
            MenuInteraction.Release(this);
        }
        private void DrawMenuWindow()
        {
            MaintainMenuInput();
            var previousSkin = GUI.skin;
            var previousColor = GUI.color;
            var previousContent = GUI.contentColor;
            var previousBackground = GUI.backgroundColor;
            var previousMatrix = GUI.matrix;
            var previousDepth = GUI.depth;
            var previousEnabled = GUI.enabled;
            try
            {
                EnsureMenuTheme();
                GUI.skin = _menuSkin;
                GUI.color = GUI.contentColor = GUI.backgroundColor = Color.white;
                GUI.matrix = Matrix4x4.identity;
                GUI.depth = -10000;
                GUI.enabled = true;
                GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none, _shadeStyle);
                _window.width = Math.Min(900, Screen.width - 20);
                _window.height = Math.Min(830, Screen.height - 30);
                _window.x = Mathf.Clamp(_window.x, 0, Math.Max(0, Screen.width - _window.width));
                _window.y = Mathf.Clamp(_window.y, 0, Math.Max(0, Screen.height - _window.height));
                _window = GUI.Window(WindowId, _window, Draw, $"Pocket Roulette 1.9.1 | TEST MENU | {_key.Value.ToString().ToUpperInvariant()} / Esc to close");
                GUI.BringWindowToFront(WindowId);
                if (!_renderLogged && Event.current.type == EventType.Repaint)
                {
                    _renderLogged = true;
                    Plugin.LogSource.LogInfo("[PocketRoulette TEST] Menu rendered: Rolls / Events / Luck / Challenges / Buy-in / Odds / Checklist.");
                }
            }
            catch (ExitGUIException) { throw; }
            catch (Exception ex)
            {
                SetVisible(false);
                Plugin.LogSource.LogError("[PocketRoulette TEST] Menu rendering failed; input and cursor released: " + ex);
            }
            finally
            {
                GUI.skin = previousSkin;
                GUI.color = previousColor;
                GUI.contentColor = previousContent;
                GUI.backgroundColor = previousBackground;
                GUI.matrix = previousMatrix;
                GUI.depth = previousDepth;
                GUI.enabled = previousEnabled;
            }
        }
        private void EnsureMenuTheme()
        {
            if (_menuSkin != null) return;
            _menuSkin = Instantiate(GUI.skin);
            _menuSkin.name = "Pocket Roulette Test Menu";
            _menuSkin.hideFlags = HideFlags.HideAndDontSave;
            var panel = Texture(new Color(0.075f, 0.085f, 0.10f, 1));
            var surface = Texture(new Color(0.12f, 0.14f, 0.17f, 1));
            var hover = Texture(new Color(0.24f, 0.27f, 0.30f, 1));
            var selected = Texture(new Color(0.38f, 0.30f, 0.13f, 1));
            foreach (var style in new[] { _menuSkin.label, _menuSkin.button, _menuSkin.toggle, _menuSkin.textField, _menuSkin.box })
            {
                style.fontSize = 14;
                foreach (var state in new[] { style.normal, style.hover, style.active, style.focused,
                             style.onNormal, style.onHover, style.onActive, style.onFocused }) state.textColor = Color.white;
            }
            _menuSkin.window.normal.background = _menuSkin.window.onNormal.background = panel;
            _menuSkin.window.normal.textColor = _menuSkin.window.onNormal.textColor = new Color(1f, 0.82f, 0.43f);
            _menuSkin.window.fontSize = 17;
            _menuSkin.window.padding = new RectOffset(14, 14, 36, 14);
            _menuSkin.label.wordWrap = true;
            _menuSkin.label.padding = new RectOffset(4, 4, 4, 4);
            _menuSkin.box.normal.background = surface;
            _menuSkin.box.wordWrap = true;
            _menuSkin.box.padding = new RectOffset(10, 10, 8, 8);
            _menuSkin.button.normal.background = surface;
            _menuSkin.button.hover.background = hover;
            _menuSkin.button.active.background = selected;
            _menuSkin.button.onNormal.background = _menuSkin.button.onHover.background = selected;
            _menuSkin.button.padding = new RectOffset(10, 10, 7, 7);
            _menuSkin.button.margin = new RectOffset(3, 3, 4, 4);
            _menuSkin.textField.normal.background = surface;
            _menuSkin.textField.focused.background = hover;
            _menuSkin.textField.padding = new RectOffset(7, 7, 6, 6);
            _shadeStyle = new GUIStyle { normal = { background = Texture(new Color(0, 0, 0, 0.55f)) } };
        }
        private Texture2D Texture(Color color)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixels(new[] { color, color, color, color });
            texture.Apply(false, true);
            _menuTextures.Add(texture);
            return texture;
        }
        private void DestroyMenuTheme()
        {
            if (_menuSkin != null) Destroy(_menuSkin);
            foreach (var texture in _menuTextures) if (texture != null) Destroy(texture);
            _menuTextures.Clear();
        }
    }
}
#endif
