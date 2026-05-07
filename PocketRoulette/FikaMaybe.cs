using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;

namespace PocketRoulette
{
    internal static class FikaMaybe
    {
        private static Type _bridgeType;
        private static MethodInfo _init;
        private static MethodInfo _sendPocket;
        private static MethodInfo _sendGround;
        private static bool _checked;

        public static void Init()
        {
            try
            {
                Check();
                _init?.Invoke(null, null);
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogDebug($"fika nope: {ex.Message}");
            }
        }

        public static bool Installed()
        {
            Check();
            return _bridgeType != null;
        }

        public static bool SendPocket(Player player, Item item, ItemAddress address)
        {
            return CallBool(_sendPocket, player, item, address);
        }

        public static bool SendGround(Player player, Item item, Vector3 position, Quaternion rotation)
        {
            return CallBool(_sendGround, player, item, position, rotation);
        }

        private static bool CallBool(MethodInfo method, params object[] args)
        {
            try
            {
                if (method == null)
                    return true;

                return (bool)method.Invoke(null, args);
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogDebug($"fika nope: {ex.Message}");
                return true;
            }
        }

        private static void Check()
        {
            if (_checked)
                return;

            _checked = true;

            if (!FikaExists())
                return;

            _bridgeType = Type.GetType("PocketRoulette.FikaBridge, PocketRoulette.Fika", false)
                ?? Type.GetType("PocketRoulette.FikaBridge", false);

            if (_bridgeType == null)
                return;

            _init = _bridgeType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
            _sendPocket = _bridgeType.GetMethod("SendPocketItem", BindingFlags.Public | BindingFlags.Static);
            _sendGround = _bridgeType.GetMethod("SendGroundItem", BindingFlags.Public | BindingFlags.Static);
        }

        private static bool FikaExists()
        {
            if (AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == "Fika.Core"))
                return true;

            if (Chainloader.PluginInfos.Keys.Any(id => id.IndexOf("fika", StringComparison.OrdinalIgnoreCase) >= 0))
                return true;

            try
            {
                var pluginFolder = Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
                var bepInExPluginsFolder = Directory.GetParent(pluginFolder ?? string.Empty)?.FullName;

                return !string.IsNullOrEmpty(bepInExPluginsFolder)
                    && Directory.Exists(bepInExPluginsFolder)
                    && Directory.EnumerateFiles(bepInExPluginsFolder, "Fika.Core.dll", SearchOption.AllDirectories).Any();
            }
            catch
            {
                return false;
            }
        }
    }
}
