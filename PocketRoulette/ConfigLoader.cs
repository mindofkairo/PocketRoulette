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
                throw new System.InvalidOperationException("Empty response from the Pocket Roulette server config route.");
            }

            var config = JsonConvert.DeserializeObject<Models.PocketRouletteConfig>(json);
            if (config == null)
                throw new System.InvalidOperationException("The Pocket Roulette server returned a null config.");
            ConfigRules.Normalize(config);
            return config;
        }
    }
}
