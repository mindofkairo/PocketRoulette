using System;
using System.Collections.Generic;
using EFT;
using EFT.InputSystem;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PocketRoulette
{
    internal static class MenuInteraction
    {
        private static Component _owner;
        private static Player _player;
        private static Action _close;
        private static CursorLockMode _previousLock;
        private static bool _previousVisible;
        private static readonly Dictionary<EventSystem, bool> BlockedUi = new Dictionary<EventSystem, bool>();

        public static void Acquire(Component owner, Player player, Action close)
        {
            CloseActive();
            _owner = owner; _player = player; _close = close;
            _previousLock = Cursor.lockState; _previousVisible = Cursor.visible;
            Maintain(owner);
        }
        public static void CloseActive() => _close?.Invoke();
        public static bool BlocksPlayerInput(InputNode node) => _owner != null && node is PlayerOwner owner
            && ReferenceEquals(owner.Player, _player);
        public static void Maintain(Component owner)
        {
            if (!ReferenceEquals(owner, _owner)) return;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            for (var remaining = 8; remaining > 0; remaining--)
            {
                var current = EventSystem.current;
                if (current == null || !current.enabled) break;
                if (!BlockedUi.ContainsKey(current)) BlockedUi.Add(current, current.enabled);
                current.enabled = false;
            }
        }
        public static void Release(Component owner)
        {
            if (!ReferenceEquals(owner, _owner)) return;
            foreach (var system in BlockedUi) if (system.Key != null) system.Key.enabled = system.Value;
            BlockedUi.Clear();
            Cursor.lockState = _previousLock; Cursor.visible = _previousVisible;
            _owner = null; _player = null; _close = null;
        }
    }
}
