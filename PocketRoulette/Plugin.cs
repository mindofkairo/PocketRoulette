using BepInEx;
using BepInEx.Logging;
using System.IO;
using PocketRoulette.Patches;

namespace PocketRoulette
{
    [BepInPlugin("com.kairo.pocketroulette", "PocketRoulette", "1.9.3")]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource LogSource = null!;
        public static Models.PocketRouletteConfig CachedConfig = null!;

        private void Awake()
        {
            LogSource = Logger;
            ClientConfigManager.Bind(Config, Path.GetDirectoryName(Info.Location));
            PlayerMenu.Bind(Config);
            new MenuInputPatch().Enable();
#if POCKET_ROULETTE_TESTING
            TestingPanel.Bind(Config);
#endif
            Config.Save();
            CachedConfig = Models.PocketRouletteConfig.CreateDefault();

            new RaidStartPatch().Enable();
            FikaMaybe.Init();

            // Fetch the authoritative config at raid start. Resolve() also creates
            // optional local content from that response, once the server is ready.
            LogSource.LogInfo("Pocket Roulette loaded; server configuration will be fetched at raid start.");
        }
    }
}

