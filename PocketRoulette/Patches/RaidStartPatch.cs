using System.Reflection;
using Comfort.Common;
using EFT;
using SPT.Reflection.Patching;
using UnityEngine;

namespace PocketRoulette.Patches
{
    internal class RaidStartPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(GameWorld).GetMethod(nameof(GameWorld.OnGameStarted));
        }

        [PatchPostfix]
        private static void PatchPostfix()
        {
            try
            {
                if (Application.isBatchMode)
                {
                    Plugin.LogSource.LogInfo("[PocketRoulette] Headless mode detected, skipping.");
                    return;
                }

                if (!Singleton<GameWorld>.Instantiated)
                {
                    Plugin.LogSource.LogError("[PocketRoulette] GameWorld not instantiated.");
                    return;
                }

                var gameWorld = Singleton<GameWorld>.Instance;
                var player = gameWorld.MainPlayer;

                if (player == null)
                {
                    Plugin.LogSource.LogError("[PocketRoulette] MainPlayer is null.");
                    return;
                }

                if (player.Location?.ToLower() == "hideout")
                {
                    Plugin.LogSource.LogDebug("[PocketRoulette] In hideout, skipping.");
                    return;
                }

                gameWorld.gameObject.AddComponent<PocketRouletteScript>();

                Plugin.LogSource.LogInfo("[PocketRoulette] Raid started - Pocket Roulette is rolling the dice!");
            }
            catch (System.Exception ex)
            {
                Plugin.LogSource.LogError($"[PocketRoulette] Error in RaidStartPatch: {ex.Message}");
            }
        }
    }
}
