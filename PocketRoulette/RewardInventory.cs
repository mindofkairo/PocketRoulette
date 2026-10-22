using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.InventoryLogic.Operations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PocketRoulette.Models;
using SPT.Common.Http;
using UnityEngine;

namespace PocketRoulette
{
    internal sealed class RewardInventory
    {
        public async Task<RewardResult> Place(Player player, PocketRouletteConfig config, RollPlan plan,
            string raidId, CancellationToken token)
        {
            var reward = plan.Reward;
            var world = Singleton<GameWorld>.Instance;
            RaidLifetime.RequirePlayer(world, player, token);
            if (System.Linq.Enumerable.Any(player.InventoryController.Inventory.Equipment.GetAllItems(), existing => existing.Id.ToString() == plan.ItemId))
                return new RewardResult(reward, plan.StackCount, RewardPlacement.Pocket);
            var item = Singleton<ItemFactory>.Instance.CreateItem(plan.ItemId, reward.Tpl, null);
            item.StackObjectsCount = Math.Max(1, Math.Min(plan.StackCount, item.StackMaxSize));
            item.UpdateAttributes();
            await RaidLifetime.PrepareItem(item, token);
            RaidLifetime.RequirePlayer(world, player, token);

            var controller = player.InventoryController ?? throw new InvalidOperationException("Inventory is not ready.");
            ItemAddress address = null;
            var pockets = player.Profile.Inventory.Equipment.GetSlot(EquipmentSlot.Pockets)?.ContainedItem as CompoundItem;
#if POCKET_ROULETTE_TESTING
            if (plan.TestPlacement != "actual") pockets = null;
#endif
            if (pockets != null)
            {
                foreach (var grid in pockets.Grids)
                {
                    address = grid.FindLocationForItem(item);
                    if (address != null) break;
                }
            }
            if (address != null && ItemManipulator.Add(item, address, controller, true).Failed)
                address = null;

            if (address == null)
            {
                var allowGround = config.AllowGroundDrop;
#if POCKET_ROULETTE_TESTING
                if (plan.TestPlacement == "ground") allowGround = true;
                if (plan.TestPlacement == "miss") allowGround = false;
#endif
                if (!allowGround)
                    return new RewardResult(reward, item.StackObjectsCount, RewardPlacement.Missed);
                Authorize("/pocketroulette/register-ground", player, item, raidId, plan.Id);
                var position = player.Transform.position + Vector3.up * 0.35f;
                var dropped = world.ThrowItem(item, player, position, Quaternion.identity,
                    Vector3.zero, Vector3.zero, true, false, 0f);
                if (dropped == null) throw new InvalidOperationException("EFT could not create the ground item.");
                RaidLifetime.RequirePlayer(world, player, token);
                if (!FikaMaybe.SendGround(player, item, position, Quaternion.identity))
                    throw new InvalidOperationException("Fika ground sync failed.");
                return new RewardResult(reward, item.StackObjectsCount, RewardPlacement.Ground);
            }

            Authorize("/pocketroulette/inject", player, item, raidId, plan.Id);
            if (FikaMaybe.Installed())
            {
                FikaMaybe.RequireBridge();
                if (!FikaMaybe.SendPocket(player, item, address))
                    throw new InvalidOperationException("Fika pocket sync failed.");
                controller.AddAndRaiseEvents(item, address);
            }
            else
            {
                var completion = new TaskCompletionSource<bool>();
                var operation = new ChangeItemsOperation(controller.GetAndIncrementNextOperationId(), Array.Empty<Item>(),
                    new Dictionary<Item, ItemAddress> { { item, address } }, new Dictionary<Item, ItemAddress>(),
                    new Dictionary<IItemInfoContainer, ItemInfo>(), null, player);
                controller.Execute(operation, result =>
                {
                    if (result.Succeed) completion.TrySetResult(true);
                    else completion.TrySetException(new InvalidOperationException(result.Error.ToString()));
                });
                // A timeout stops this raid's remaining rolls, since this operation may still complete.
                await RaidLifetime.WaitFor(completion.Task, token);
            }
            RaidLifetime.RequirePlayer(world, player, token);
            return new RewardResult(reward, item.StackObjectsCount, RewardPlacement.Pocket);
        }

        private static void Authorize(string route, Player player, Item item, string raidId, string planId)
        {
            var json = RequestHandler.PostJson(route, JsonConvert.SerializeObject(new
            {
                id = item.Id.ToString(), tpl = item.TemplateId.ToString(),
                profileId = player.ProfileId, stackCount = item.StackObjectsCount, raidId, planId
            }));
            var response = string.IsNullOrWhiteSpace(json) ? null : JObject.Parse(json);
            if (response?["success"]?.Value<bool>() != true)
                throw new InvalidOperationException($"Server rejected reward: {response?["error"]?.Value<string>() ?? "empty response"}.");
        }
    }
}
