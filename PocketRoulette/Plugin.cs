using BepInEx;
using BepInEx.Logging;
using PocketRoulette.Patches;

namespace PocketRoulette
{
    [BepInPlugin("com.kairo.pocketroulette", "PocketRoulette", "1.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource LogSource = null!;
        public static Models.PocketRouletteConfig CachedConfig = null!;

        private void Awake()
        {
            LogSource = Logger;
            CachedConfig = Models.PocketRouletteConfig.CreateDefault();

            new RaidStartPatch().Enable();
            FikaMaybe.Init();

            LogSource.LogInfo("pocket roulette loaded");
        }
    }
}
