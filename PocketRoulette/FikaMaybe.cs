using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Bootstrap;
using EFT;
using EFT.InventoryLogic;
using PocketRoulette.Models;
using UnityEngine;

namespace PocketRoulette
{
    internal static class FikaMaybe
    {
        private static bool _checked;
        private static bool _installed;
        private static bool _ready;
        private static MethodInfo _pocket;
        private static MethodInfo _ground;
        private static MethodInfo _grenade;
        private static MethodInfo _party;
        private static MethodInfo _stateChanged;

        public static void Init()
        {
            if (_checked) return;
            _checked = true;
            _installed = AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == "Fika.Core")
                || Chainloader.PluginInfos.ContainsKey("com.fika.core")
                || HasFikaAssembly();
            if (!_installed) return;
            try
            {
                var folder = Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
                var bridgePath = Path.Combine(folder, "PocketRoulette.Fika.dll");
                if (!File.Exists(bridgePath)) throw new FileNotFoundException("PocketRoulette.Fika.dll is required when Fika is installed.");
                var assembly = Assembly.LoadFrom(bridgePath);
                if (assembly.GetName().Version != typeof(Plugin).Assembly.GetName().Version)
                    throw new InvalidOperationException("Pocket Roulette client and Fika bridge versions differ. Reinstall the matching pair.");
                var bridge = assembly.GetType("PocketRoulette.FikaBridge", throwOnError: true);
                _pocket = bridge.GetMethod("SendPocketItem");
                _ground = bridge.GetMethod("SendGroundItem");
                _grenade = bridge.GetMethod("RunGrantedEvent");
                _party = bridge.GetMethod("GetPartyInfo");
                _stateChanged = bridge.GetMethod("NotifyStateChanged");
                if (_pocket == null || _ground == null || _grenade == null || _party == null || _stateChanged == null || _grenade.ReturnType != typeof(Task<bool>))
                    throw new MissingMethodException("Fika bridge has an incompatible API.");
                bridge.GetMethod("Initialize").Invoke(null, null);
                _ready = true;
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogError($"[PocketRoulette] Fika integration could not load; raid rolls will be skipped: {ex}");
            }
        }

        public static bool Installed() { Init(); return _installed; }

        public static void NotifyStateChanged()
        {
            if (!_installed || !_ready) return;
            try { _stateChanged.Invoke(null, null); }
            catch (Exception ex) { Plugin.LogSource.LogWarning("[PocketRoulette] Group update signal failed: " + ex.Message); }
        }

        private static bool HasFikaAssembly()
        {
            try
            {
                return Directory.Exists(BepInEx.Paths.PluginPath)
                    && Directory.EnumerateFiles(BepInEx.Paths.PluginPath, "Fika.Core.dll", SearchOption.AllDirectories).Any();
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogError($"[PocketRoulette] Could not inspect Fika installation: {ex.Message}");
                // An unreadable plugin folder cannot establish that solo fallback is safe.
                return true;
            }
        }

        public static void RequireBridge()
        {
            Init();
            if (_installed && !_ready) throw new InvalidOperationException("Fika is installed but its Pocket Roulette bridge is unavailable.");
        }

        public static bool SendPocket(Player player, Item item, ItemAddress address) => Call(_pocket, player, item, address);
        public static bool SendGround(Player player, Item item, Vector3 position, Quaternion rotation) => Call(_ground, player, item, position, rotation);

        public static PartyContext Party(Player player)
        {
            RequireBridge();
            return _installed ? (PartyContext)_party.Invoke(null, new object[] { player }) : new PartyContext();
        }

        public static async Task<bool> SendEvent(Player player, string raidId, RollPlan plan, Vector3 position)
        {
            RequireBridge();
            if (!_installed) return false;
            try { return await (Task<bool>)_grenade.Invoke(null, new object[] { player, raidId, plan.Id, position }); }
            catch (TimeoutException) { throw; }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { Plugin.LogSource.LogError($"[PocketRoulette] Fika grenade request failed: {ex}"); return false; }
        }

        private static bool Call(MethodInfo method, params object[] args)
        {
            RequireBridge();
            if (!_installed) return true;
            try { return method != null && (bool)method.Invoke(null, args); }
            catch (Exception ex) { Plugin.LogSource.LogError($"[PocketRoulette] Fika sync failed: {ex}"); return false; }
        }
    }
}
