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
        private const string RegisterGroundRoute = "/pocketroulette/register-ground";
        private const int MinItemCount = 1;
        private const int MaxItemCount = 20;
        private const float SptAddTimeoutSeconds = 5f;
        private readonly System.Random _localRandom = CreateLocalRandom();
        private bool _destroyAfterRoll = true;
        private bool _waitingForSptAdd;
        private RewardResult _waitingForSptResult;

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

            if (!config.ScavEnabled && IsPlayerScav(player))
            {
                Plugin.LogSource.LogDebug("[PocketRoulette] Player scav raid detected and scavEnabled is false. Skipping.");
                Destroy(this);
                yield break;
            }

            var eligibleItems = FilterByMode(config.ItemPool, config.Mode);
            if (eligibleItems.Count == 0)
            {
                Plugin.LogSource.LogWarning($"[PocketRoulette] No eligible items for mode '{config.Mode}'. Skipping.");
                Destroy(this);
                yield break;
            }

            var itemCount = GetItemCount(config);
            var results = new List<RewardResult>();
            var showIndividualNotifications = itemCount == 1;
            var chancePercent = GetChancePercent(config);

            if (chancePercent <= 0 || _localRandom.Next(100) >= chancePercent)
            {
                Plugin.LogSource.LogInfo($"[PocketRoulette] Chance roll missed ({chancePercent}%). No reward this raid.");
                ShowChanceMissNotification(config);
                Destroy(this);
                yield break;
            }

            for (var rollNumber = 1; rollNumber <= itemCount; rollNumber++)
            {
                try
                {
                    var selectedItem = PickRewardItem(eligibleItems, config.Mode);

                    Plugin.LogSource.LogInfo($"[PocketRoulette] Roll {rollNumber}/{itemCount}: {selectedItem.Name} (TPL: {selectedItem.Tpl}, Rarity: {selectedItem.Rarity}, Size: {selectedItem.Width}x{selectedItem.Height})");

                    results.Add(TrySpawnReward(player, config, selectedItem, eligibleItems, showIndividualNotifications));
                }
                catch (Exception ex)
                {
                    Plugin.LogSource.LogError($"[PocketRoulette] Error during roll {rollNumber}/{itemCount}: {ex}");
                }

                var waitStarted = Time.time;
                while (_waitingForSptAdd && Time.time - waitStarted < SptAddTimeoutSeconds)
                    yield return null;

                if (_waitingForSptAdd)
                {
                    Plugin.LogSource.LogError("[PocketRoulette] SPT add callback timed out. Moving on so the raid start script can finish.");
                    _waitingForSptResult?.MarkFailed();
                    _waitingForSptResult = null;
                    _waitingForSptAdd = false;
                }

                yield return null;
            }

            if (!showIndividualNotifications)
            {
                _destroyAfterRoll = false;
                StartCoroutine(ShowMultiRollNotificationThenDestroy(config, results));
            }

            if (_destroyAfterRoll)
                Destroy(this);
        }

        private List<PoolItem> FilterByMode(List<PoolItem> pool, string mode)
        {
            switch (mode?.ToLower())
            {
                case "garbage":
                    return pool.Where(i => i.Rarity == "common" || i.Rarity == "uncommon").ToList();
                case "useful":
                    return pool.Where(i => i.Rarity == "common" || i.Rarity == "uncommon" || i.Rarity == "rare" || i.Rarity == "ultrarare").ToList();
                case "jackpot":
                    return pool.Where(i => i.Rarity == "uncommon" || i.Rarity == "rare" || i.Rarity == "ultrarare").ToList();
                case "chaos":
                case "mixed":
                default:
                    return pool;
            }
        }

        private PocketRouletteConfig GetConfig()
        {
            try
            {
                var serverConfig = ConfigLoader.FetchConfig();
                Plugin.CachedConfig = ClientConfigManager.Resolve(serverConfig);
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogWarning($"config broke, using defaults: {ex.Message}");
                Plugin.CachedConfig = PocketRouletteConfig.CreateDefault();
            }

            return Plugin.CachedConfig;
        }

        private int GetItemCount(PocketRouletteConfig config)
        {
            if (config.ItemCount < MinItemCount)
                return MinItemCount;

            if (config.ItemCount > MaxItemCount)
            {
                Plugin.LogSource.LogWarning($"[PocketRoulette] itemCount {config.ItemCount} is too high. Clamping to {MaxItemCount}.");
                return MaxItemCount;
            }

            return config.ItemCount;
        }

        private int GetChancePercent(PocketRouletteConfig config)
        {
            if (config.ChancePercent < 0)
                return 0;

            if (config.ChancePercent > 100)
                return 100;

            return config.ChancePercent;
        }

        private bool IsPlayerScav(Player player)
        {
            try
            {
                var side = player.Profile?.Info?.Side.ToString();
                return !string.IsNullOrEmpty(side)
                    && side.IndexOf("savage", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogWarning($"[PocketRoulette] Could not determine player side: {ex.Message}");
                return false;
            }
        }

        private PoolItem PickRewardItem(List<PoolItem> items, string mode)
        {
            var rarity = PickWeightedRarity(items, mode);
            var rarityItems = items.Where(item => string.Equals(item.Rarity, rarity, StringComparison.OrdinalIgnoreCase)).ToList();

            return rarityItems.Count > 0
                ? PickWeightedItem(rarityItems)
                : PickWeightedItem(items);
        }

        private string PickWeightedRarity(List<PoolItem> items, string mode)
        {
            var rarityWeights = GetAvailableRarityWeights(items, mode);

            if (rarityWeights.Count == 0)
                return items[0].Rarity;

            var totalWeight = rarityWeights.Sum(entry => entry.Value);
            var roll = _localRandom.Next(totalWeight);
            var cumulativeWeight = 0;

            foreach (var entry in rarityWeights)
            {
                cumulativeWeight += entry.Value;
                if (roll < cumulativeWeight)
                    return entry.Key;
            }

            return rarityWeights[rarityWeights.Count - 1].Key;
        }

        private List<KeyValuePair<string, int>> GetAvailableRarityWeights(List<PoolItem> items, string mode)
        {
            return GetRarityWeights(mode)
                .Where(entry => entry.Value > 0 && items.Any(item => string.Equals(item.Rarity, entry.Key, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        private Dictionary<string, int> GetRarityWeights(string mode)
        {
            switch (mode?.ToLower())
            {
                case "garbage":
                    return new Dictionary<string, int>
                    {
                        { "common", 95 },
                        { "uncommon", 5 },
                        { "rare", 0 },
                        { "ultrarare", 0 }
                    };
                case "useful":
                    return new Dictionary<string, int>
                    {
                        { "common", 15 },
                        { "uncommon", 60 },
                        { "rare", 23 },
                        { "ultrarare", 2 }
                    };
                case "jackpot":
                    return new Dictionary<string, int>
                    {
                        { "common", 0 },
                        { "uncommon", 15 },
                        { "rare", 70 },
                        { "ultrarare", 15 }
                    };
                case "chaos":
                    return new Dictionary<string, int>
                    {
                        { "common", 25 },
                        { "uncommon", 25 },
                        { "rare", 25 },
                        { "ultrarare", 25 }
                    };
                case "mixed":
                default:
                    return new Dictionary<string, int>
                    {
                        { "common", 65 },
                        { "uncommon", 25 },
                        { "rare", 8 },
                        { "ultrarare", 2 }
                    };
            }
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

        private RewardResult TrySpawnReward(Player player, PocketRouletteConfig config, PoolItem poolItem, List<PoolItem> eligibleItems, bool showIndividualNotification)
        {
            try
            {
                var rouletteItem = CreateItem(poolItem.Tpl);
                if (rouletteItem == null)
                {
                    Plugin.LogSource.LogError($"[PocketRoulette] Failed to create item for pockets: {poolItem.Tpl}");
                    if (showIndividualNotification)
                        ShowFailureNotification(config, poolItem);

                    return RewardResult.Failed(poolItem, 1);
                }

                var stackCount = RollStackCount(poolItem, rouletteItem);
                ApplyStackCount(rouletteItem, stackCount);

                var inventoryController = player.InventoryController;
                if (inventoryController == null)
                {
                    Plugin.LogSource.LogError("[PocketRoulette] Player InventoryController is null.");
                    if (showIndividualNotification)
                        ShowFailureNotification(config, poolItem);

                    return RewardResult.Failed(poolItem, stackCount);
                }

                var pocketsSlot = player.Profile.Inventory.Equipment.GetSlot(EquipmentSlot.Pockets);
                if (!(pocketsSlot?.ContainedItem is CompoundItem pocketsItem))
                {
                    Plugin.LogSource.LogWarning("[PocketRoulette] Player has no pockets equipped.");
                    return HandleNoPocketSpace(player, rouletteItem, config, poolItem, stackCount, showIndividualNotification);
                }

                var pocketAddress = FindPocketAddress(rouletteItem, pocketsItem);
                if (pocketAddress == null)
                {
                    Plugin.LogSource.LogInfo($"[PocketRoulette] No pocket space found for {poolItem.Name}.");
                    return HandleNoPocketSpace(player, rouletteItem, config, poolItem, stackCount, showIndividualNotification);
                }

                var addResult = InteractionsHandlerClass.Add(rouletteItem, pocketAddress, inventoryController, true);
                if (addResult.Failed)
                {
                    Plugin.LogSource.LogWarning($"[PocketRoulette] EFT rejected pocket add for {poolItem.Name}: {addResult.Error}.");
                    return HandleNoPocketSpace(player, rouletteItem, config, poolItem, stackCount, showIndividualNotification);
                }

                if (!SyncPocketItemToServer(rouletteItem, pocketAddress))
                {
                    Plugin.LogSource.LogError($"[PocketRoulette] Server registration failed for {poolItem.Name}; not adding locally.");
                    if (showIndividualNotification)
                        ShowFailureNotification(config, poolItem);

                    return RewardResult.Failed(poolItem, stackCount);
                }

                if (FikaMaybe.Installed())
                {
                    if (!AddWithFika(player, inventoryController, rouletteItem, pocketAddress, config, poolItem, eligibleItems, showIndividualNotification))
                        return RewardResult.Failed(poolItem, stackCount);
                }
                else
                {
                    var result = RewardResult.Pocket(poolItem, stackCount);
                    AddWithSpt(player, inventoryController, rouletteItem, pocketAddress, config, poolItem, eligibleItems, showIndividualNotification, result);
                    return result;
                }

                return RewardResult.Pocket(poolItem, stackCount);
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogError($"[PocketRoulette] Error spawning reward: {ex}");
                if (showIndividualNotification)
                    ShowFailureNotification(config, poolItem);

                return RewardResult.Failed(poolItem, 1);
            }
        }

        private RewardResult HandleNoPocketSpace(Player player, Item rouletteItem, PocketRouletteConfig config, PoolItem poolItem, int stackCount, bool showIndividualNotification)
        {
            if (config.AllowGroundDrop)
            {
                return TryDropAtFeet(player, rouletteItem, config, poolItem, showIndividualNotification)
                    ? RewardResult.Ground(poolItem, stackCount)
                    : RewardResult.Failed(poolItem, stackCount);
            }

            Plugin.LogSource.LogInfo($"[PocketRoulette] Ground fallback disabled. {poolItem.Name} was skipped.");
            if (showIndividualNotification)
                ShowMissedRewardNotification(config, poolItem, stackCount);

            return RewardResult.Missed(poolItem, stackCount);
        }

        private bool AddWithFika(Player player, InventoryController inventoryController, Item rouletteItem, ItemAddress pocketAddress, PocketRouletteConfig config, PoolItem poolItem, List<PoolItem> eligibleItems, bool showIndividualNotification)
        {
            if (!FikaMaybe.SendPocket(player, rouletteItem, pocketAddress))
            {
                Plugin.LogSource.LogError($"[PocketRoulette] Fika sync failed for {poolItem.Name}; not adding locally.");
                if (showIndividualNotification)
                    ShowFailureNotification(config, poolItem);
                return false;
            }

            inventoryController.AddAndRaiseEvents(rouletteItem, pocketAddress);
            Plugin.LogSource.LogInfo($"[PocketRoulette] Added {poolItem.Name} to pockets after server sync.");
            if (showIndividualNotification)
            {
                _destroyAfterRoll = false;
                StartCoroutine(ShowPocketNotificationThenDestroy(config, poolItem, eligibleItems, rouletteItem.StackObjectsCount));
            }

            return true;
        }

        private void AddWithSpt(Player player, InventoryController inventoryController, Item rouletteItem, ItemAddress pocketAddress, PocketRouletteConfig config, PoolItem poolItem, List<PoolItem> eligibleItems, bool showIndividualNotification, RewardResult rewardResult)
        {
            _waitingForSptAdd = true;
            _waitingForSptResult = rewardResult;

            var operation = new GClass3524(
                inventoryController.method_12(),
                Array.Empty<Item>(),
                new Dictionary<Item, ItemAddress> { { rouletteItem, pocketAddress } },
                new Dictionary<Item, ItemAddress>(),
                new Dictionary<GInterface171, GClass1802>(),
                null,
                player);

            try
            {
                inventoryController.vmethod_1(operation, operationResult =>
                {
                    if (!operationResult.Succeed)
                    {
                        Plugin.LogSource.LogError($"[PocketRoulette] SPT add failed for {poolItem.Name}: {operationResult.Error}");
                        if (showIndividualNotification)
                            ShowFailureNotification(config, poolItem);
                        rewardResult.MarkFailed();
                        _waitingForSptResult = null;
                        _waitingForSptAdd = false;
                        return;
                    }

                    Plugin.LogSource.LogInfo($"[PocketRoulette] Added {poolItem.Name} to pockets.");
                    if (showIndividualNotification)
                        ShowPocketNotification(config, poolItem, eligibleItems, rouletteItem.StackObjectsCount);

                    _waitingForSptResult = null;
                    _waitingForSptAdd = false;
                });
            }
            catch
            {
                _waitingForSptResult = null;
                _waitingForSptAdd = false;
                throw;
            }
        }

        private bool TryDropAtFeet(Player player, Item rouletteItem, PocketRouletteConfig config, PoolItem poolItem, bool showIndividualNotification)
        {
            try
            {
                var gameWorld = Singleton<GameWorld>.Instance;
                var playerTransform = player.Transform;
                var dropPosition = playerTransform.position + (playerTransform.forward * 0.75f) + new Vector3(0f, 0.35f, 0f);
                var dropRotation = Quaternion.identity;

                if (!SyncGroundItemToServer(rouletteItem, dropPosition, dropRotation))
                {
                    Plugin.LogSource.LogError($"[PocketRoulette] Server ground registration failed for {poolItem.Name}; not dropping locally.");
                    if (showIndividualNotification)
                        ShowFailureNotification(config, poolItem);

                    return false;
                }

                gameWorld.ThrowItem(rouletteItem, player, dropPosition, dropRotation, Vector3.zero, Vector3.zero, true, false, 0f);
                if (!FikaMaybe.SendGround(player, rouletteItem, dropPosition, dropRotation))
                {
                    Plugin.LogSource.LogWarning($"[PocketRoulette] Dropped {poolItem.Name} locally, but Fika ground sync failed.");
                }

                Plugin.LogSource.LogInfo($"[PocketRoulette] Dropped {poolItem.Name} at player's feet.");
                if (showIndividualNotification)
                    ShowGroundDropNotification(config, poolItem, rouletteItem.StackObjectsCount);

                return true;
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogError($"[PocketRoulette] Failed to drop {poolItem.Name} at player's feet: {ex}");
                if (showIndividualNotification)
                    ShowFailureNotification(config, poolItem);

                return false;
            }
        }

        private int RollStackCount(PoolItem poolItem, Item item)
        {
            var minCount = Math.Max(1, poolItem.MinCount);
            var maxCount = Math.Max(minCount, poolItem.MaxCount);
            var templateMax = Math.Max(1, item.StackMaxSize);
            var clampedMax = Math.Min(maxCount, templateMax);

            if (clampedMax <= minCount)
                return Math.Min(minCount, clampedMax);

            return _localRandom.Next(minCount, clampedMax + 1);
        }

        private void ApplyStackCount(Item item, int stackCount)
        {
            item.StackObjectsCount = Math.Max(1, stackCount);
            item.UpdateAttributes();
        }

        private bool SyncGroundItemToServer(Item rouletteItem, Vector3 position, Quaternion rotation)
        {
            try
            {
                var requestBody = new
                {
                    id = rouletteItem.Id,
                    tpl = rouletteItem.TemplateId,
                    stackCount = rouletteItem.StackObjectsCount,
                    position = new
                    {
                        x = position.x,
                        y = position.y,
                        z = position.z
                    },
                    rotation = new
                    {
                        x = rotation.x,
                        y = rotation.y,
                        z = rotation.z,
                        w = rotation.w
                    }
                };

                var response = RequestHandler.PostJson(RegisterGroundRoute, JsonConvert.SerializeObject(requestBody));
                if (!string.IsNullOrWhiteSpace(response) && response.IndexOf("\"success\":false", StringComparison.OrdinalIgnoreCase) >= 0)
                    return false;

                Plugin.LogSource.LogInfo($"[PocketRoulette] Server registered ground item {rouletteItem.TemplateId} ({rouletteItem.Id}).");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogError($"[PocketRoulette] Error registering ground item with server: {ex}");
                return false;
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
                    stackCount = rouletteItem.StackObjectsCount,
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

                Plugin.LogSource.LogInfo($"[PocketRoulette] Server registered {rouletteItem.TemplateId} at {grid.ID} ({location.x},{location.y},{location.r}).");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogError($"[PocketRoulette] Error syncing item with server: {ex}");
                return false;
            }
        }

        private void ShowPocketNotification(PocketRouletteConfig config, PoolItem selectedItem, List<PoolItem> eligibleItems, int stackCount)
        {
            if (!config.EnableNotification) return;

            try
            {
                string message;
                Color color;

                if (selectedItem.Rarity == "ultrarare" && config.UltraRareMessages.Count > 0)
                {
                    var oddsPercent = GetItemOddsPercent(selectedItem, eligibleItems, config.Mode);
                    var oddsText = oddsPercent < 1f ? $"{oddsPercent:F2}%" : $"{oddsPercent:F1}%";
                    var comparison = config.UltraRareOddsComparisons.Count > 0
                        ? PickRandom(config.UltraRareOddsComparisons)
                        : "finding a GPU on the floor of Interchange";

                    message = PickRandom(config.UltraRareMessages);
                    message = message.Replace("{item}", FormatRewardName(selectedItem, stackCount))
                                    .Replace("{odds}", oddsText)
                                    .Replace("{comparison}", comparison);
                    color = new Color(1f, 0.84f, 0f);
                }
                else if (config.PocketMessages.Count > 0)
                {
                    message = PickRandom(config.PocketMessages);
                    message = message.Replace("{item}", FormatRewardName(selectedItem, stackCount));
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

        private IEnumerator ShowPocketNotificationThenDestroy(PocketRouletteConfig config, PoolItem selectedItem, List<PoolItem> eligibleItems, int stackCount)
        {
            yield return new WaitForSeconds(0.5f);
            ShowPocketNotification(config, selectedItem, eligibleItems, stackCount);
            Destroy(this);
        }

        private IEnumerator ShowMultiRollNotificationThenDestroy(PocketRouletteConfig config, List<RewardResult> results)
        {
            yield return new WaitForSeconds(0.5f);
            ShowMultiRollNotification(config, results);
            Destroy(this);
        }

        private void ShowMultiRollNotification(PocketRouletteConfig config, List<RewardResult> results)
        {
            if (!config.EnableNotification || results.Count == 0)
                return;

            try
            {
                var pocketItems = results.Where(result => result.Placement == RewardPlacement.Pocket).ToList();
                var groundItems = results.Where(result => result.Placement == RewardPlacement.Ground).ToList();
                var missedItems = results.Where(result => result.Placement == RewardPlacement.Missed).ToList();
                var failedItems = results.Where(result => result.Placement == RewardPlacement.Failed).ToList();

                var highlights = PickHighlights(results);
                var template = config.MultiRollSummaryMessages.Count > 0
                    ? PickRandom(config.MultiRollSummaryMessages)
                    : "Pocket Roulette rolled {total} rewards: {pocketCount} in pockets{groundPart}{missedPart}{failedPart}.{bestPart}";

                var message = template
                    .Replace("{total}", results.Count.ToString())
                    .Replace("{pocketCount}", pocketItems.Count.ToString())
                    .Replace("{groundCount}", groundItems.Count.ToString())
                    .Replace("{missedCount}", missedItems.Count.ToString())
                    .Replace("{failedCount}", failedItems.Count.ToString())
                    .Replace("{groundPart}", groundItems.Count > 0 ? $", {groundItems.Count} at your feet" : "")
                    .Replace("{missedPart}", missedItems.Count > 0 ? $", {missedItems.Count} missed" : "")
                    .Replace("{failedPart}", failedItems.Count > 0 ? $", {failedItems.Count} failed" : "")
                    .Replace("{bestFinds}", highlights.Count > 0 ? string.Join(", ", highlights) : "")
                    .Replace("{bestPart}", highlights.Count > 0 ? $" Best finds: {string.Join(", ", highlights)}." : "");

                NotificationManagerClass.DisplayMessageNotification(
                    message,
                    ENotificationDurationType.Long,
                    ENotificationIconType.Default,
                    GetBestRarityColor(results)
                );
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogWarning($"[PocketRoulette] Failed to show multi-roll notification: {ex.Message}");
            }
        }

        private List<string> PickHighlights(List<RewardResult> results)
        {
            return results
                .Where(IsReceivedReward)
                .OrderByDescending(result => RarityRank(result.Item.Rarity))
                .ThenBy(result => result.Item.Name)
                .Take(3)
                .Select(FormatRewardName)
                .ToList();
        }

        private Color GetBestRarityColor(List<RewardResult> results)
        {
            var best = results
                .Where(IsReceivedReward)
                .OrderByDescending(result => RarityRank(result.Item.Rarity))
                .FirstOrDefault();

            return best != null ? GetRarityColor(best.Item.Rarity) : Color.red;
        }

        private bool IsReceivedReward(RewardResult result)
        {
            return result.Placement == RewardPlacement.Pocket || result.Placement == RewardPlacement.Ground;
        }

        private void ShowGroundDropNotification(PocketRouletteConfig config, PoolItem poolItem, int stackCount)
        {
            if (!config.EnableNotification) return;

            var message = config.GroundDropMessages.Count > 0
                ? PickRandom(config.GroundDropMessages)
                : "Your pockets are full. A {item} appears at your feet.";
            var text = message.Replace("{item}", FormatRewardName(poolItem, stackCount)).Replace("{location}", "at your feet");

            if (!MessageMentionsItem(text, poolItem))
                text = $"{text} ({FormatRewardName(poolItem, stackCount)})";

            if (text.IndexOf("feet", StringComparison.OrdinalIgnoreCase) < 0 && text.IndexOf("ground", StringComparison.OrdinalIgnoreCase) < 0)
                text = $"{text} It landed at your feet.";

            NotificationManagerClass.DisplayMessageNotification(
                text,
                ENotificationDurationType.Long,
                ENotificationIconType.Default,
                GetRarityColor(poolItem.Rarity)
            );
        }

        private void ShowMissedRewardNotification(PocketRouletteConfig config, PoolItem poolItem, int stackCount)
        {
            if (!config.EnableNotification) return;

            var message = config.MissedRewardMessages.Count > 0
                ? PickRandom(config.MissedRewardMessages)
                : "Your pockets were full. You missed out on {item}.";
            var text = message.Replace("{item}", FormatRewardName(poolItem, stackCount));

            if (!MessageMentionsItem(text, poolItem))
                text = $"{text} ({FormatRewardName(poolItem, stackCount)})";

            NotificationManagerClass.DisplayMessageNotification(
                text,
                ENotificationDurationType.Long,
                ENotificationIconType.Default,
                Color.red
            );
        }

        private void ShowChanceMissNotification(PocketRouletteConfig config)
        {
            if (!config.EnableNotification || config.ChancePercent >= 100)
                return;

            var message = config.ChanceMissMessages.Count > 0
                ? PickRandom(config.ChanceMissMessages)
                : "Pocket Roulette spun the wheel, but luck was not on your side.";

            NotificationManagerClass.DisplayMessageNotification(
                message,
                ENotificationDurationType.Long,
                ENotificationIconType.Default,
                Color.gray
            );
        }

        private void ShowFailureNotification(PocketRouletteConfig config, PoolItem poolItem)
        {
            if (!config.EnableNotification) return;

            var message = config.FailureMessages.Count > 0
                ? PickRandom(config.FailureMessages)
                : "Pocket Roulette failed to spawn {item}.";

            NotificationManagerClass.DisplayMessageNotification(
                message.Replace("{item}", FormatRewardName(poolItem, 1)),
                ENotificationDurationType.Long,
                ENotificationIconType.Default,
                Color.red
            );
        }

        private string FormatRewardName(PoolItem poolItem, int stackCount)
        {
            var name = stackCount > 1
                ? StripSingleCountPrefix(poolItem.Name)
                : poolItem.Name;

            return stackCount > 1
                ? $"{name} x{stackCount}"
                : name;
        }

        private string FormatRewardName(RewardResult result)
        {
            var name = result.StackCount > 1
                ? StripSingleCountPrefix(result.Item.Name)
                : result.Item.Name;

            return result.StackCount > 1
                ? $"{name} x{result.StackCount}"
                : name;
        }

        private string StripSingleCountPrefix(string name)
        {
            if (name.StartsWith("1x ", StringComparison.OrdinalIgnoreCase))
                return name.Substring(3);

            if (name.StartsWith("1 ", StringComparison.OrdinalIgnoreCase))
                return name.Substring(2);

            return name;
        }

        private bool MessageMentionsItem(string text, PoolItem poolItem)
        {
            var itemName = poolItem.Name;
            var displayName = StripSingleCountPrefix(poolItem.Name);

            return text.IndexOf(itemName, StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf(displayName, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private float GetItemOddsPercent(PoolItem selectedItem, List<PoolItem> eligibleItems, string mode)
        {
            var rarityWeights = GetAvailableRarityWeights(eligibleItems, mode);
            var totalRarityWeight = rarityWeights.Sum(entry => entry.Value);
            if (totalRarityWeight <= 0)
                return 0f;

            var selectedRarityWeight = rarityWeights
                .Where(entry => string.Equals(entry.Key, selectedItem.Rarity, StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry.Value)
                .FirstOrDefault();
            if (selectedRarityWeight <= 0)
                return 0f;

            var sameRarityItems = eligibleItems
                .Where(item => string.Equals(item.Rarity, selectedItem.Rarity, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var totalItemWeight = sameRarityItems.Sum(item => Math.Max(0, item.Weight));
            if (totalItemWeight <= 0)
                return 0f;

            var rarityChance = (float)selectedRarityWeight / totalRarityWeight;
            var itemChance = (float)Math.Max(0, selectedItem.Weight) / totalItemWeight;
            return rarityChance * itemChance * 100f;
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

        private int RarityRank(string rarity)
        {
            switch (rarity)
            {
                case "ultrarare":
                    return 4;
                case "rare":
                    return 3;
                case "uncommon":
                    return 2;
                case "common":
                    return 1;
                default:
                    return 0;
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

        private enum RewardPlacement
        {
            Pocket,
            Ground,
            Missed,
            Failed
        }

        private class RewardResult
        {
            public PoolItem Item { get; private set; }
            public RewardPlacement Placement { get; private set; }
            public int StackCount { get; private set; }

            private RewardResult(PoolItem item, RewardPlacement placement, int stackCount)
            {
                Item = item;
                Placement = placement;
                StackCount = Math.Max(1, stackCount);
            }

            public static RewardResult Pocket(PoolItem item, int stackCount)
            {
                return new RewardResult(item, RewardPlacement.Pocket, stackCount);
            }

            public static RewardResult Ground(PoolItem item, int stackCount)
            {
                return new RewardResult(item, RewardPlacement.Ground, stackCount);
            }

            public static RewardResult Failed(PoolItem item, int stackCount)
            {
                return new RewardResult(item, RewardPlacement.Failed, stackCount);
            }

            public static RewardResult Missed(PoolItem item, int stackCount)
            {
                return new RewardResult(item, RewardPlacement.Missed, stackCount);
            }

            public void MarkFailed()
            {
                Placement = RewardPlacement.Failed;
            }
        }
    }
}
