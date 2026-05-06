using SPT.Common.Http;
using Newtonsoft.Json;

namespace PocketRoulette
{
    public static class ConfigLoader
    {
        private const string ConfigRoute = "/pocketroulette/config";

        public static Models.PocketRouletteConfig FetchConfig()
        {
            var json = RequestHandler.GetJson(ConfigRoute);

            if (string.IsNullOrWhiteSpace(json))
            {
                Plugin.LogSource.LogWarning("[PocketRoulette] Empty response from server config route.");
                return Models.PocketRouletteConfig.CreateDefault();
            }

            var config = JsonConvert.DeserializeObject<Models.PocketRouletteConfig>(json);
            return config ?? Models.PocketRouletteConfig.CreateDefault();
        }
    }
}
