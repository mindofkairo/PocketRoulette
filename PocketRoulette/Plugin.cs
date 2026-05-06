using BepInEx;
using BepInEx.Logging;
using PocketRoulette.Patches;

namespace PocketRoulette
{
    [BepInPlugin("com.kairo.pocketroulette", "PocketRoulette", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource LogSource = null!;
        public static Models.PocketRouletteConfig CachedConfig = null!;

        private void Awake()
        {
            LogSource = Logger;

            try
            {
                CachedConfig = ConfigLoader.FetchConfig();
            }
            catch (System.Exception ex)
            {
                LogSource.LogWarning($"config broke, using defaults: {ex.Message}");
                CachedConfig = Models.PocketRouletteConfig.CreateDefault();
            }

            new RaidStartPatch().Enable();
            FikaBridge.Initialize();

            LogSource.LogInfo("pocket roulette loaded");
        }
    }
}
