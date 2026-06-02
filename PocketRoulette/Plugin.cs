using BepInEx;
using BepInEx.Logging;
using System.IO;
using PocketRoulette.Patches;

namespace PocketRoulette
{
    [BepInPlugin("com.kairo.pocketroulette", "PocketRoulette", "1.2.1")]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource LogSource = null!;
        public static Models.PocketRouletteConfig CachedConfig = null!;

        private void Awake()
        {
            LogSource = Logger;
            ClientConfigManager.Bind(Config, Path.GetDirectoryName(Info.Location));
            Config.Save();
            CachedConfig = LoadStartupConfig();
            ClientConfigManager.EnsureJsonExists(CachedConfig);

            new RaidStartPatch().Enable();
            FikaMaybe.Init();

            LogSource.LogInfo("pocket roulette loaded");
        }

        private Models.PocketRouletteConfig LoadStartupConfig()
        {
            try
            {
                return ConfigLoader.FetchConfig();
            }
            catch
            {
                return Models.PocketRouletteConfig.CreateDefault();
            }
        }
    }
}
