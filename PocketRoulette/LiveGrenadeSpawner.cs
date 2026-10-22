using System;
using System.Threading;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.AssetsManager;
using EFT.InventoryLogic;
using Systems.Effects;
using UnityEngine;

namespace PocketRoulette
{
    public static class LiveGrenadeSpawner
    {
        public static void RequireTimedFragmentation(ThrowWeap item)
        {
            if (item.GetExplDelay < 0.5f || item.MinTimeToContactExplode > 0)
                throw new InvalidOperationException("Cursed jackpots require an ordinary timed fragmentation grenade, not an impact grenade.");
            GameObject prefab = null;
            try
            {
                prefab = Singleton<ObjectsFactory>.Instance.CreateItem(item, true);
                var settings = prefab?.GetComponent<GrenadePrefab>()?.GrenadeItself;
                if (settings == null || settings is SmokeGrenadeSettings || settings is StunGrenadeSettings)
                    throw new InvalidOperationException("Cursed jackpots require a timed fragmentation grenade; smoke/stun templates are not allowed.");
            }
            finally { if (prefab != null) AssetPoolObject.ReturnToPool(prefab); }
        }

        public static async Task<ThrowWeap> Prepare(string templateId, CancellationToken token, string itemId = null)
        {
            if (!ConfigRules.IsItemId(templateId) || !Singleton<ItemFactory>.Instantiated)
                throw new InvalidOperationException("Invalid grenade template or unavailable item factory.");
            var item = Singleton<ItemFactory>.Instance.CreateItem(itemId ?? MongoID.Generate().ToString(), templateId, null) as ThrowWeap;
            if (item == null) throw new InvalidOperationException($"Template {templateId} is not a throwable grenade.");
            await RaidLifetime.PrepareItem(item, token);
            return item;
        }

        public static async Task<bool> Spawn(Player player, string templateId, float timeSpent, Vector3 position, string itemId, bool allowInactive = false)
        {
            if (!Singleton<GameWorld>.Instantiated) return false;
            var world = Singleton<GameWorld>.Instance;
            var lifetime = RaidLifetime.For(world);
            var token = lifetime.Token;
            if (string.IsNullOrEmpty(itemId) || !lifetime.GrenadeIds.Add(itemId)) return false;
            try
            {
                if (!allowInactive) RaidLifetime.RequirePlayer(world, player, token);
                var item = await Prepare(templateId, token, itemId);
                if (!allowInactive) RaidLifetime.RequirePlayer(world, player, token);
                token.ThrowIfCancellationRequested();
                return SpawnPrepared(world, player, item, timeSpent, position, allowInactive);
            }
            catch (OperationCanceledException) { return false; }
            catch (Exception ex)
            {
                Plugin.LogSource.LogError($"[PocketRoulette] Grenade preparation failed: {ex}");
                return false;
            }
        }

        public static bool SpawnPrepared(GameWorld world, Player player, ThrowWeap item, float timeSpent, Vector3 position, bool allowInactive = false)
        {
            GameObject prefab = null;
            GrenadeSettings settings = null;
            Grenade grenade = null;
            GrenadeEmission emission = null;
            try
            {
                if (!allowInactive) RaidLifetime.RequirePlayer(world, player, RaidLifetime.For(world).Token);
                else RaidLifetime.For(world).Token.ThrowIfCancellationRequested();
                prefab = Singleton<ObjectsFactory>.Instance.CreateItem(item, isAnimated: true);
                var grenadePrefab = prefab != null ? prefab.GetComponent<GrenadePrefab>() : null;
                if (grenadePrefab == null || grenadePrefab.GrenadeItself == null)
                    throw new InvalidOperationException("Grenade prefab has no live grenade settings.");
                settings = UnityEngine.Object.Instantiate(grenadePrefab.GrenadeItself);
                settings.Offset = Vector3.zero;
                var spent = float.IsNaN(timeSpent) || float.IsInfinity(timeSpent) ? 0 : timeSpent;
                // Always leave a minimum fuse, even when a configured prewarm exceeds this template's fuse.
                spent = Mathf.Clamp(spent, 0, Math.Max(0, item.GetExplDelay - 0.5f));
                grenade = world.GrenadeFactory.Create(settings, position, Quaternion.identity, Vector3.zero, item, player.ProfileId, spent);
                if (grenade == null) throw new InvalidOperationException("Grenade factory returned no grenade.");
                if (grenade is SmokeGrenade smoke)
                {
                    // EFT's hands controller performs this separately from the factory.
                    // A physical smoke grenade alone never starts its visible emitter.
                    emission = Singleton<Effects>.Instance.GetEmissionEffect(settings.EmmisionEffect);
                    if (emission == null) throw new InvalidOperationException("Native smoke emitter is unavailable: " + settings.EmmisionEffect);
                    emission.AttachTo(smoke.transform, Vector3.zero);
                    emission.SetFillParams(spent, Math.Max(0.5f, (item.EmitTime > 0 ? item.EmitTime : 30) - spent));
                    emission.StartEmission(spent);
                    smoke.EmissionEnd += emission.StopEmission;
                    smoke.VelocityBelowThreshold += emission.Stall;
                }
                world.RegisterGrenade(grenade);
                Plugin.LogSource.LogInfo($"[PocketRoulette] Created live grenade {item.Id}, fuse {item.GetExplDelay - spent:F1}s, position {position}.");
                return true;
            }
            catch (Exception ex)
            {
                if (emission != null && Singleton<Effects>.Instantiated) Singleton<Effects>.Instance.DisposeEmissionEffect(emission);
                if (grenade != null) UnityEngine.Object.Destroy(grenade.gameObject);
                else if (settings != null) UnityEngine.Object.Destroy(settings.gameObject);
                Plugin.LogSource.LogError($"[PocketRoulette] Grenade creation failed: {ex}");
                return false;
            }
            finally
            {
                if (prefab != null) AssetPoolObject.ReturnToPool(prefab);
            }
        }
    }
}
