using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.Interactive;
using PocketRoulette.Models;
using UnityEngine;

namespace PocketRoulette
{
    public sealed class PreparedEvent
    {
        public ThrowWeap Grenade;
        public Item Prize;
        public Vector3 Position;
    }

    public static class LiveEventRunner
    {
        public static void WarnParty(string nickname, RollPlan plan)
        {
            MenuInteraction.CloseActive();
            RewardNotifications.PartyHazard(nickname, plan);
        }
        public static async Task<PreparedEvent> PrepareAndExplain(GameWorld world, Player player, RollPlan plan,
            PocketRouletteConfig config, CancellationToken token, bool waitBeforeSpawn = true)
        {
            var prepared = new PreparedEvent
            {
                Grenade = await LiveGrenadeSpawner.Prepare(plan.GrenadeTpl, token, plan.HazardId)
            };
            if (plan.Kind == "cursed")
            {
                LiveGrenadeSpawner.RequireTimedFragmentation(prepared.Grenade);
                prepared.Prize = await PreparePrize(plan, token);
            }
            RaidLifetime.RequirePlayer(world, player, token);
            prepared.Position = player.Transform.position + Vector3.up * 0.3f;
            if (plan.Kind == "cursed") RewardNotifications.CursedExplanation(config, plan);
            else RewardNotifications.GrenadeWarning(config, plan);
            if (waitBeforeSpawn)
                await WaitSeconds(plan.Kind == "cursed" ? config.CursedJackpot.ExplanationSeconds : config.GrenadeSpawnDelaySeconds, token);
            RaidLifetime.RequirePlayer(world, player, token);
            return prepared;
        }

        public static async Task<Item> PreparePrize(RollPlan plan, CancellationToken token)
        {
            var item = Singleton<ItemFactory>.Instance.CreateItem(plan.ItemId, plan.Reward.Tpl, null);
            item.StackObjectsCount = plan.StackCount;
            item.UpdateAttributes();
            await RaidLifetime.PrepareItem(item, token);
            return item;
        }

        public static LootItem Drop(GameWorld world, Player owner, RollPlan plan, Item item, Vector3 position)
        {
            var lifetime = RaidLifetime.For(world);
            if (lifetime.ExpiredEvents.Contains(plan.Id)) return null;
            if (lifetime.EventLoot.TryGetValue(plan.Id, out var existing)) return existing;
            var loot = world.ThrowItem(item, owner, position, Quaternion.identity, Vector3.zero, Vector3.zero, true, false, 0);
            if (loot == null) throw new InvalidOperationException("Cursed prize could not be placed; grenade was cancelled.");
            lifetime.EventLoot[plan.Id] = loot;
            return loot;
        }

        public static void Expire(GameWorld world, string eventId)
        {
            var lifetime = RaidLifetime.For(world);
            lifetime.ExpiredEvents.Add(eventId);
            if (!lifetime.EventLoot.TryGetValue(eventId, out var loot)) return;
            // Pickup removes/kills this exact loose-loot instance. Never search player inventories.
            if (loot != null && world.LootList.Contains(loot)) world.DestroyLoot(loot);
            lifetime.EventLoot.Remove(eventId);
        }

        public static void NotifyActive(PocketRouletteConfig config, RollPlan plan, float remaining) =>
            RewardNotifications.CursedActive(config, plan, remaining);

        public static async Task<string> Solo(GameWorld world, Player player, RollPlan plan, PocketRouletteConfig config,
            PreparedEvent prepared, CancellationToken token)
        {
            var lifetime = RaidLifetime.For(world);
            if (!lifetime.EventIds.Add(plan.Id)) return "failed";
            if (plan.Kind == "grenade")
                return LiveGrenadeSpawner.SpawnPrepared(world, player, prepared.Grenade, plan.GrenadePrewarmSeconds, prepared.Position)
                    ? "grenade" : "failed";
            Drop(world, player, plan, prepared.Prize, prepared.Position);
            NotifyActive(config, plan, plan.ExplosionDelaySeconds);
            var claimed = false;
            var hazardCreated = false;
            var started = Time.time;
            var spawnAt = EventTiming.SpawnAfter(plan.ExplosionDelaySeconds, prepared.Grenade.GetExplDelay);
            try
            {
                while (Time.time - started < plan.ExplosionDelaySeconds)
                {
                    token.ThrowIfCancellationRequested();
                    claimed |= HasPrize(player, plan.ItemId);
                    if (!hazardCreated && Time.time - started >= spawnAt)
                    {
                        var remaining = Math.Max(0.5f, plan.ExplosionDelaySeconds - (Time.time - started));
                        if (!LiveGrenadeSpawner.SpawnPrepared(world, player, prepared.Grenade,
                            EventTiming.Prewarm(remaining, prepared.Grenade.GetExplDelay), prepared.Position, allowInactive: true))
                            throw new InvalidOperationException("Cursed grenade could not be created.");
                        hazardCreated = true;
                    }
                    await Task.Delay(25, token);
                }
                return claimed || HasPrize(player, plan.ItemId) ? "picked" : "expired";
            }
            finally
            {
                if (world != null && !token.IsCancellationRequested) Expire(world, plan.Id);
            }
        }

        public static bool HasPrize(Player player, string itemId) => player != null && player.InventoryController != null
            && player.InventoryController.Inventory.Equipment.GetAllItems().Any(item => item.Id.ToString() == itemId);

        public static async Task WaitSeconds(float seconds, CancellationToken token)
        {
            var until = Time.time + Math.Max(0, seconds);
            while (Time.time < until) { token.ThrowIfCancellationRequested(); await Task.Delay(25, token); }
        }
    }
}
