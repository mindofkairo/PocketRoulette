using System;
using System.Linq;
using System.Text;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Main.Players;
using Fika.Core.Main.Utils;
using Fika.Core.Modding;
using Fika.Core.Modding.Events;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using Fika.Core.Networking.Packets;
using UnityEngine;

namespace PocketRoulette
{
    internal static class FikaBridge
    {
        private static bool _subscribed;
        private static IFikaNetworkManager _registeredManager;

        public static void Initialize()
        {
            if (_subscribed)
                return;

            _subscribed = true;
            FikaEventDispatcher.SubscribeEvent<FikaNetworkManagerCreatedEvent>(OnNetworkManagerCreated);
        }

        public static bool SendGroundItem(Player owner, Item item, Vector3 position, Quaternion rotation)
        {
            if (_registeredManager == null || !Singleton<IFikaNetworkManager>.Instantiated)
                return true;

            if (!(owner is FikaPlayer fikaOwner))
                return true;

            try
            {
                var packet = new GroundPacket
                {
                    OwnerNetId = fikaOwner.NetId,
                    OwnerProfileId = owner.ProfileId,
                    Item = item,
                    Position = position,
                    Rotation = rotation
                };

                Singleton<IFikaNetworkManager>.Instance.SendData(ref packet, DeliveryMethod.ReliableOrdered, true);
                return true;
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogWarning($"[PocketRoulette] Failed to broadcast Fika ground item: {ex.Message}");
                return false;
            }
        }

        public static bool SendPocketItem(Player owner, Item item, ItemAddress address)
        {
            if (_registeredManager == null || !Singleton<IFikaNetworkManager>.Instantiated)
                return true;

            if (!(owner is FikaPlayer fikaOwner) || !(address is GClass3393 gridAddress))
                return true;

            try
            {
                var location = gridAddress.LocationInGrid;
                var packet = new PocketPacket
                {
                    OwnerNetId = fikaOwner.NetId,
                    OwnerProfileId = owner.ProfileId,
                    Item = item,
                    SlotId = gridAddress.Grid.ID,
                    X = location.x,
                    Y = location.y,
                    Rotation = (int)location.r
                };

                Singleton<IFikaNetworkManager>.Instance.SendData(ref packet, DeliveryMethod.ReliableOrdered, true);
                return true;
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogWarning($"[PocketRoulette] Failed to broadcast Fika pocket item: {ex.Message}");
                return false;
            }
        }

        private static void OnNetworkManagerCreated(FikaNetworkManagerCreatedEvent createdEvent)
        {
            if (ReferenceEquals(_registeredManager, createdEvent.Manager))
                return;

            if (FikaBackendUtils.IsServer)
            {
                createdEvent.Manager.RegisterPacket<PocketPacket, NetPeer>(OnServerPocketItem);
                createdEvent.Manager.RegisterPacket<GroundPacket, NetPeer>(OnServerGroundItem);
            }
            else
            {
                createdEvent.Manager.RegisterPacket<PocketPacket>(OnClientPocketItem);
                createdEvent.Manager.RegisterPacket<GroundPacket>(OnClientGroundItem);
            }

            _registeredManager = createdEvent.Manager;
        }

        private static void OnServerPocketItem(PocketPacket packet, NetPeer peer)
        {
            AddPocketItem(packet);

            if (Singleton<IFikaNetworkManager>.Instance is FikaServer server)
            {
                server.SendData(ref packet, DeliveryMethod.ReliableOrdered, peer);
            }
        }

        private static void OnClientPocketItem(PocketPacket packet)
        {
            if (Singleton<GameWorld>.Instantiated && Singleton<GameWorld>.Instance?.MainPlayer?.ProfileId == packet.OwnerProfileId)
                return;

            AddPocketItem(packet);
        }

        private static void OnServerGroundItem(GroundPacket packet, NetPeer peer)
        {
            SpawnGroundItem(packet);

            if (Singleton<IFikaNetworkManager>.Instance is FikaServer server)
            {
                server.SendData(ref packet, DeliveryMethod.ReliableOrdered, peer);
            }
        }

        private static void OnClientGroundItem(GroundPacket packet)
        {
            if (Singleton<GameWorld>.Instantiated && Singleton<GameWorld>.Instance?.MainPlayer?.ProfileId == packet.OwnerProfileId)
                return;

            SpawnGroundItem(packet);
        }

        private static void SpawnGroundItem(GroundPacket packet)
        {
            try
            {
                var gameWorld = Singleton<GameWorld>.Instance;
                if (gameWorld == null || packet.Item == null)
                    return;

                gameWorld.SetupItem(packet.Item, null, packet.Position, packet.Rotation);
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogWarning($"[PocketRoulette] Failed to sync Fika ground item: {ex.Message}");
            }
        }

        private static void AddPocketItem(PocketPacket packet)
        {
            try
            {
                var player = FindPlayer(packet.OwnerNetId, packet.OwnerProfileId);
                if (player?.InventoryController == null || packet.Item == null)
                    return;

                if (player.InventoryController.Inventory.Equipment.GetAllItems().Any(existing => existing.Id == packet.Item.Id))
                    return;

                var pocketsSlot = player.Profile.Inventory.Equipment.GetSlot(EquipmentSlot.Pockets);
                if (!(pocketsSlot?.ContainedItem is CompoundItem pocketsItem))
                    return;

                foreach (var container in pocketsItem.Containers)
                {
                    if (container is StashGridClass grid && grid.ID == packet.SlotId)
                    {
                        var location = new LocationInGrid(packet.X, packet.Y, (ItemRotation)packet.Rotation);
                        player.InventoryController.AddAndRaiseEvents(packet.Item, grid.CreateItemAddress(location));
                        Plugin.LogSource.LogInfo($"[PocketRoulette] Synced {packet.Item.TemplateId} into {player.Profile.Info.Nickname}'s pockets.");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogWarning($"[PocketRoulette] Failed to sync Fika pocket item: {ex.Message}");
            }
        }

        private static Player FindPlayer(int netId, string profileId)
        {
            if (!Singleton<IFikaNetworkManager>.Instantiated)
                return null;

            var networkManager = Singleton<IFikaNetworkManager>.Instance;
            if (networkManager.CoopHandler?.Players != null && networkManager.CoopHandler.Players.TryGetValue(netId, out var player))
                return player;

            return Singleton<GameWorld>.Instantiated
                ? Singleton<GameWorld>.Instance.AllPlayersEverExisted?.FirstOrDefault(player => player.ProfileId == profileId)
                : null;
        }
    }

    internal struct PocketPacket : INetSerializable
    {
        public int OwnerNetId;
        public string OwnerProfileId;
        public Item Item;
        public string SlotId;
        public int X;
        public int Y;
        public int Rotation;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(OwnerNetId);
            writer.PutBytesWithLength(Encoding.UTF8.GetBytes(OwnerProfileId ?? string.Empty));
            writer.PutItem(Item);
            writer.PutBytesWithLength(Encoding.UTF8.GetBytes(SlotId ?? string.Empty));
            writer.Put(X);
            writer.Put(Y);
            writer.Put(Rotation);
        }

        public void Deserialize(NetDataReader reader)
        {
            OwnerNetId = reader.GetInt();
            OwnerProfileId = Encoding.UTF8.GetString(reader.GetBytesWithLength());
            Item = reader.GetItem();
            SlotId = Encoding.UTF8.GetString(reader.GetBytesWithLength());
            X = reader.GetInt();
            Y = reader.GetInt();
            Rotation = reader.GetInt();
        }
    }

    internal struct GroundPacket : INetSerializable
    {
        public int OwnerNetId;
        public string OwnerProfileId;
        public Item Item;
        public Vector3 Position;
        public Quaternion Rotation;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(OwnerNetId);
            writer.PutBytesWithLength(Encoding.UTF8.GetBytes(OwnerProfileId ?? string.Empty));
            writer.PutItem(Item);
            writer.Put(Position.x);
            writer.Put(Position.y);
            writer.Put(Position.z);
            writer.Put(Rotation.x);
            writer.Put(Rotation.y);
            writer.Put(Rotation.z);
            writer.Put(Rotation.w);
        }

        public void Deserialize(NetDataReader reader)
        {
            OwnerNetId = reader.GetInt();
            OwnerProfileId = Encoding.UTF8.GetString(reader.GetBytesWithLength());
            Item = reader.GetItem();
            Position = new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
            Rotation = new Quaternion(reader.GetFloat(), reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        }
    }
}
