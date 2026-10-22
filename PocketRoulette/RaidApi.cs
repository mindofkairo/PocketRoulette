using System;
using Newtonsoft.Json;
using PocketRoulette.Models;
using SPT.Common.Http;

namespace PocketRoulette
{
    public static class RaidApi
    {
        public static RaidUpdate Begin(RaidBeginRequest request) => Post("/pocketroulette/raid/begin", request);
        public static RaidUpdate Action(string action, RaidActionRequest request) => Post("/pocketroulette/raid/" + action, request);
        public static RaidUpdate Event(string raidId, string planId) => Post("/pocketroulette/event/authorize",
            new RaidActionRequest { RaidId = raidId, PlanId = planId });
        public static RaidUpdate Post(string route, object body)
        {
            var json = RequestHandler.PostJson(route, JsonConvert.SerializeObject(body));
            var update = string.IsNullOrWhiteSpace(json) ? null : JsonConvert.DeserializeObject<RaidUpdate>(json);
            if (update == null || !update.Success) throw new InvalidOperationException(update?.Error ?? "Empty Pocket Roulette server response.");
            return update;
        }
    }
}
