using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.InventoryLogic.Operations;
using EFT.Interactive;
using UnityEngine;

namespace PocketRoulette
{
    public sealed class RaidLifetime : MonoBehaviour
    {
        private readonly CancellationTokenSource _stop = new CancellationTokenSource();
        public CancellationToken Token => _stop.Token;
        public readonly HashSet<string> GrenadeIds = new HashSet<string>();
        public readonly HashSet<string> EventIds = new HashSet<string>();
        public readonly HashSet<string> ExpiredEvents = new HashSet<string>();
        public readonly HashSet<string> HazardWarnings = new HashSet<string>();
        public readonly Dictionary<string, LootItem> EventLoot = new Dictionary<string, LootItem>();

        public static RaidLifetime For(GameWorld world) =>
            world.GetComponent<RaidLifetime>() ?? world.gameObject.AddComponent<RaidLifetime>();

        public static void RequirePlayer(GameWorld world, Player player, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (world == null || !Singleton<GameWorld>.Instantiated || Singleton<GameWorld>.Instance != world
                || player == null || !player.gameObject.activeInHierarchy || player.HealthController == null || !player.HealthController.IsAlive)
                throw new OperationCanceledException("The raid or player is no longer active.");
        }

        public static async Task PrepareItem(Item item, CancellationToken token)
        {
            await WaitFor(ChangeItemsOperation.LoadBundles(item), token);
        }

        public static async Task WaitFor(Task work, CancellationToken token, int seconds = 15)
        {
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                var delay = Task.Delay(TimeSpan.FromSeconds(seconds), timeout.Token);
                if (await Task.WhenAny(work, delay) != work)
                {
                    // Observe late failures without allowing late placement or notification.
                    _ = work.ContinueWith(task => { var ignored = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                    token.ThrowIfCancellationRequested();
                    throw new TimeoutException("Pocket Roulette operation timed out; remaining rolls were stopped.");
                }
                timeout.Cancel();
                await work;
                token.ThrowIfCancellationRequested();
            }
        }

        private void OnDestroy()
        {
            _stop.Cancel();
            _stop.Dispose();
        }
    }
}
