using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.Communications;
using EFT.InventoryLogic;
using Newtonsoft.Json;
using PocketRoulette.Models;
using SPT.Common.Http;
using UnityEngine;

namespace PocketRoulette
{
    public class PocketRouletteScript : MonoBehaviour
    {
        private const float InventoryReadyDelaySeconds = 3f;
        private const string InjectRoute = "/pocketroulette/inject";
        private readonly System.Random _localRandom = CreateLocalRandom();
        private bool _destroyAfterRoll = true;

        private static readonly string[] UltraRareOddsComparisons =
        {
            "finding a GPU on the floor of Interchange",
            "a Scav being friendly",
            "getting head-eyes'd through a wall... wait, that's common",
            "surviving Labs as a solo",
            "Nikita personally buffing your favorite gun",
            "a raider dropping a keycard",
            "getting extract camped zero times in a day",
            "Fence giving you something useful",
            "winning the lottery (almost)",
            "a peaceful day in Tarkov",
            "Killa doing a backflip",
            "finding the extract without a map",
            "a cultist giving you a hug",
        };

        private void Awake()
        {
            StartCoroutine(RollPocketRoulette());
        }

        private IEnumerator RollPocketRoulette()
        {
            yield return new WaitForSeconds(InventoryReadyDelaySeconds);

            var config = GetConfig();
            if (config?.ItemPool == null || config.ItemPool.Count == 0)
            {
                Plugin.LogSource.LogWarning("[PocketRoulette] No config or empty item pool. Skipping.");
                Destroy(this);
                yield break;
            }

            var gameWorld = Singleton<GameWorld>.Instance;
            if (gameWorld == null)
            {
                Plugin.LogSource.LogError("[PocketRoulette] GameWorld is null in coroutine.");
                Destroy(this);
                yield break;
            }

            var player = gameWorld.MainPlayer;
            if (player == null)
            {
                Plugin.LogSource.LogError("[PocketRoulette] MainPlayer is null in coroutine.");
                Destroy(this);
                yield break;
            }

            try
            {
                var eligibleItems = FilterByMode(config.ItemPool, config.Mode);
                if (eligibleItems.Count == 0)
                {
                    Plugin.LogSource.LogWarning($"[PocketRoulette] No eligible items for mode '{config.Mode}'. Skipping.");
                    Destroy(this);
                    yield break;
                }

                var selectedItem = PickWeightedItem(eligibleItems);

                Plugin.LogSource.LogInfo($"[PocketRoulette] Selected: {selectedItem.Name} (TPL: {selectedItem.Tpl}, Rarity: {selectedItem.Rarity}, Size: {selectedItem.Width}x{selectedItem.Height})");

                TrySpawnReward(player, config, selectedItem, eligibleItems);
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogError($"[PocketRoulette] Error during item injection: {ex}");
            }

            if (_destroyAfterRoll)
                Destroy(this);
        }

        private List<PoolItem> FilterByMode(List<PoolItem> pool, string mode)
        {
            switch (mode?.ToLower())
            {
                case "garbage":
                    return pool.Where(i => i.Rarity == "common").ToList();
                case "useful":
                    return pool.Where(i => i.Rarity != "common").ToList();
                case "jackpot":
                    return pool.Where(i => i.Rarity == "rare" || i.Rarity == "ultrarare").ToList();
                case "mixed":
                default:
                    return pool;
            }
        }

        private PocketRouletteConfig GetConfig()
        {
            try
            {
                Plugin.CachedConfig = ConfigLoader.FetchConfig();
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogWarning($"config broke, using defaults: {ex.Message}");
                Plugin.CachedConfig = PocketRouletteConfig.CreateDefault();
            }

            return Plugin.CachedConfig;
        }

        private PoolItem PickWeightedItem(List<PoolItem> items)
        {
            var totalWeight = items.Sum(i => i.Weight);
            var roll = _localRandom.Next(totalWeight);

            var cumulativeWeight = 0;
            foreach (var poolItem in items)
            {
                cumulativeWeight += poolItem.Weight;
                if (roll < cumulativeWeight)
                    return poolItem;
            }

            return items[items.Count - 1];
        }

        private Item CreateItem(string templateId)
        {
            try
            {
                var itemFactory = Singleton<ItemFactoryClass>.Instance;
                if (itemFactory != null)
                {
                    return itemFactory.CreateItem(MongoID.Generate(), templateId, null);
                }

                Plugin.LogSource.LogError("[PocketRoulette] ItemFactory singleton not available.");
                return null;
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogError($"[PocketRoulette] Failed to create item '{templateId}': {ex.Message}");
                return null;
            }
        }

        private void TrySpawnReward(Player player, PocketRouletteConfig config, PoolItem poolItem, List<PoolItem> eligibleItems)
        {
            try
            {
                var rouletteItem = CreateItem(poolItem.Tpl);
                if (rouletteItem == null)
                {
                    Plugin.LogSource.LogError($"[PocketRoulette] Failed to create item for pockets: {poolItem.Tpl}");
                    ShowFailureNotification(config, poolItem);
                    return;
                }

                var inventoryController = player.InventoryController;
                if (inventoryController == null)
                {
                    Plugin.LogSource.LogError("[PocketRoulette] Player InventoryController is null.");
                    return;
                }

                var pocketsSlot = player.Profile.Inventory.Equipment.GetSlot(EquipmentSlot.Pockets);
                if (!(pocketsSlot?.ContainedItem is CompoundItem pocketsItem))
                {
                    Plugin.LogSource.LogWarning("[PocketRoulette] Player has no pockets equipped; trying ground fallback.");
                    TryDropAtFeet(player, rouletteItem, config, poolItem);
                    return;
                }

                var pocketAddress = FindPocketAddress(rouletteItem, pocketsItem);
                if (pocketAddress == null)
                {
                    Plugin.LogSource.LogInfo($"[PocketRoulette] No pocket space found for {poolItem.Name}; trying ground fallback.");
                    TryDropAtFeet(player, rouletteItem, config, poolItem);
                    return;
                }

                var addResult = InteractionsHandlerClass.Add(rouletteItem, pocketAddress, inventoryController, true);
                if (addResult.Failed)
                {
                    Plugin.LogSource.LogWarning($"[PocketRoulette] EFT rejected pocket add for {poolItem.Name}: {addResult.Error}. Trying ground fallback.");
                    TryDropAtFeet(player, rouletteItem, config, poolItem);
                    return;
                }

                if (!SyncPocketItemToServer(rouletteItem, pocketAddress))
                {
                    Plugin.LogSource.LogError($"[PocketRoulette] Server sync failed for {poolItem.Name}; not adding locally.");
                    return;
                }

                if (FikaMaybe.Installed())
                {
                    AddWithFika(player, inventoryController, rouletteItem, pocketAddress, config, poolItem, eligibleItems);
                }
                else
                {
                    AddWithSpt(player, inventoryController, rouletteItem, pocketAddress, config, poolItem, eligibleItems);
                }
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogError($"[PocketRoulette] Error spawning reward: {ex}");
            }
        }

        private void AddWithFika(Player player, InventoryController inventoryController, Item rouletteItem, ItemAddress pocketAddress, PocketRouletteConfig config, PoolItem poolItem, List<PoolItem> eligibleItems)
        {
            if (!FikaMaybe.SendPocket(player, rouletteItem, pocketAddress))
            {
                Plugin.LogSource.LogError($"[PocketRoulette] Fika sync failed for {poolItem.Name}; not adding locally.");
                ShowFailureNotification(config, poolItem);
                return;
            }

            inventoryController.AddAndRaiseEvents(rouletteItem, pocketAddress);
            Plugin.LogSource.LogInfo($"[PocketRoulette] Added {poolItem.Name} to pockets after server sync.");
            _destroyAfterRoll = false;
            StartCoroutine(ShowPocketNotificationThenDestroy(config, poolItem, eligibleItems));
        }

        private void AddWithSpt(Player player, InventoryController inventoryController, Item rouletteItem, ItemAddress pocketAddress, PocketRouletteConfig config, PoolItem poolItem, List<PoolItem> eligibleItems)
        {
            var operation = new GClass3524(
                inventoryController.method_12(),
                Array.Empty<Item>(),
                new Dictionary<Item, ItemAddress> { { rouletteItem, pocketAddress } },
                new Dictionary<Item, ItemAddress>(),
                new Dictionary<GInterface171, GClass1802>(),
                null,
                player);

            inventoryController.vmethod_1(operation, result =>
            {
                if (!result.Succeed)
                {
                    Plugin.LogSource.LogError($"[PocketRoulette] SPT add failed for {poolItem.Name}: {result.Error}");
                    ShowFailureNotification(config, poolItem);
                    return;
                }

                Plugin.LogSource.LogInfo($"[PocketRoulette] Added {poolItem.Name} to pockets.");
                ShowPocketNotification(config, poolItem, eligibleItems);
            });
        }

        private void TryDropAtFeet(Player player, Item rouletteItem, PocketRouletteConfig config, PoolItem poolItem)
        {
            try
            {
                var gameWorld = Singleton<GameWorld>.Instance;
                var playerTransform = player.Transform;
                var dropPosition = playerTransform.position + (playerTransform.forward * 0.75f) + new Vector3(0f, 0.35f, 0f);

                gameWorld.ThrowItem(rouletteItem, player, dropPosition, Quaternion.identity, Vector3.zero, Vector3.zero, true, false, 0f);
                if (!FikaMaybe.SendGround(player, rouletteItem, dropPosition, Quaternion.identity))
                {
                    Plugin.LogSource.LogWarning($"[PocketRoulette] Dropped {poolItem.Name} locally, but Fika ground sync failed.");
                }

                Plugin.LogSource.LogInfo($"[PocketRoulette] Dropped {poolItem.Name} at player's feet.");
                ShowGroundDropNotification(config, poolItem);
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogError($"[PocketRoulette] Failed to drop {poolItem.Name} at player's feet: {ex}");
                ShowFailureNotification(config, poolItem);
            }
        }

        private ItemAddress FindPocketAddress(Item rouletteItem, CompoundItem pocketsItem)
        {
            foreach (var container in pocketsItem.Containers)
            {
                if (container is StashGridClass grid)
                {
                    var address = grid.FindLocationForItem(rouletteItem);
                    if (address != null)
                        return address;
                }
            }

            return null;
        }

        private bool SyncPocketItemToServer(Item rouletteItem, ItemAddress pocketAddress)
        {
            try
            {
                if (!(pocketAddress is GClass3393 gridAddress))
                {
                    Plugin.LogSource.LogError($"[PocketRoulette] Unexpected pocket address type: {pocketAddress.GetType().FullName}");
                    return false;
                }

                var grid = gridAddress.Grid;
                var location = gridAddress.LocationInGrid;
                var requestBody = new
                {
                    id = rouletteItem.Id,
                    tpl = rouletteItem.TemplateId,
                    parentId = grid.ParentItem.Id,
                    slotId = grid.ID,
                    location = new
                    {
                        x = location.x,
                        y = location.y,
                        r = (int)location.r,
                        isSearched = true
                    }
                };

                var response = RequestHandler.PostJson(InjectRoute, JsonConvert.SerializeObject(requestBody));
                if (!string.IsNullOrWhiteSpace(response) && response.IndexOf("\"success\":false", StringComparison.OrdinalIgnoreCase) >= 0)
                    return false;

                Plugin.LogSource.LogInfo($"[PocketRoulette] Server synced {rouletteItem.TemplateId} at {grid.ID} ({location.x},{location.y},{location.r}).");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogError($"[PocketRoulette] Error syncing item with server: {ex}");
                return false;
            }
        }

        private void ShowPocketNotification(PocketRouletteConfig config, PoolItem selectedItem, List<PoolItem> eligibleItems)
        {
            if (!config.EnableNotification) return;

            try
            {
                string message;
                Color color;

                if (selectedItem.Rarity == "ultrarare" && config.UltraRareMessages.Count > 0)
                {
                    var totalWeight = eligibleItems.Sum(i => i.Weight);
                    var oddsPercent = (float)selectedItem.Weight / totalWeight * 100f;
                    var oddsText = oddsPercent < 1f ? $"{oddsPercent:F2}%" : $"{oddsPercent:F1}%";
                    var comparison = PickRandom(UltraRareOddsComparisons);

                    message = PickRandom(config.UltraRareMessages);
                    message = message.Replace("{item}", selectedItem.Name)
                                    .Replace("{odds}", oddsText)
                                    .Replace("{comparison}", comparison);
                    color = new Color(1f, 0.84f, 0f);
                }
                else if (config.PocketMessages.Count > 0)
                {
                    message = PickRandom(config.PocketMessages);
                    message = message.Replace("{item}", selectedItem.Name);
                    color = GetRarityColor(selectedItem.Rarity);
                }
                else
                {
                    return;
                }

                NotificationManagerClass.DisplayMessageNotification(
                    message,
                    ENotificationDurationType.Long,
                    ENotificationIconType.Default,
                    color
                );
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogWarning($"[PocketRoulette] Failed to show notification: {ex.Message}");
            }
        }

        private IEnumerator ShowPocketNotificationThenDestroy(PocketRouletteConfig config, PoolItem selectedItem, List<PoolItem> eligibleItems)
        {
            yield return new WaitForSeconds(0.5f);
            ShowPocketNotification(config, selectedItem, eligibleItems);
            Destroy(this);
        }

        private void ShowGroundDropNotification(PocketRouletteConfig config, PoolItem poolItem)
        {
            if (!config.EnableNotification) return;

            var message = config.GroundDropMessages.Count > 0
                ? PickRandom(config.GroundDropMessages)
                : "Your pockets are full. A {item} appears at your feet.";
            var text = message.Replace("{item}", poolItem.Name).Replace("{location}", "at your feet");

            if (text.IndexOf(poolItem.Name, StringComparison.OrdinalIgnoreCase) < 0)
                text = $"{text} ({poolItem.Name})";

            if (text.IndexOf("feet", StringComparison.OrdinalIgnoreCase) < 0 && text.IndexOf("ground", StringComparison.OrdinalIgnoreCase) < 0)
                text = $"{text} It landed at your feet.";

            NotificationManagerClass.DisplayMessageNotification(
                text,
                ENotificationDurationType.Long,
                ENotificationIconType.Default,
                GetRarityColor(poolItem.Rarity)
            );
        }

        private void ShowFailureNotification(PocketRouletteConfig config, PoolItem poolItem)
        {
            if (!config.EnableNotification) return;

            NotificationManagerClass.DisplayMessageNotification(
                $"Pocket Roulette failed to spawn {poolItem.Name}.",
                ENotificationDurationType.Long,
                ENotificationIconType.Default,
                Color.red
            );
        }

        private Color GetRarityColor(string rarity)
        {
            switch (rarity)
            {
                case "common":
                    return new Color(0.7f, 0.7f, 0.7f);
                case "uncommon":
                    return new Color(0.3f, 0.85f, 0.3f);
                case "rare":
                    return new Color(0.4f, 0.6f, 1f);
                case "ultrarare":
                    return new Color(1f, 0.84f, 0f);
                default:
                    return Color.white;
            }
        }

        private T PickRandom<T>(IReadOnlyList<T> values)
        {
            return values[_localRandom.Next(values.Count)];
        }

        private static System.Random CreateLocalRandom()
        {
            return new System.Random(BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0));
        }
    }
}
