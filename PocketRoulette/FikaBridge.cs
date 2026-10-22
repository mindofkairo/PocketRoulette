using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PocketRoulette.Models;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Main.Players;
using Fika.Core.Main.Utils;
using Fika.Core.Modding;
using Fika.Core.Modding.Events;
using Fika.Core.Networking;
using Fika.Core.Networking.Snapshotting;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using Fika.Core.Networking.Packets;
using UnityEngine;

namespace PocketRoulette
{
    public static class FikaBridge
    {
        private static bool _subscribed;
        private static IFikaNetworkManager _registeredManager;
        private static readonly Dictionary<string, TaskCompletionSource<bool>> PendingEvents = new Dictionary<string, TaskCompletionSource<bool>>();
        private static readonly Dictionary<string, Task<bool>> HostJobs = new Dictionary<string, Task<bool>>();
        private static readonly HashSet<string> StateSignals = new HashSet<string>();

        public static void NotifyStateChanged()
        {
            if (_registeredManager == null) return;
            var packet = new StateChangedPacket { Id = Guid.NewGuid().ToString("N") };
            _registeredManager.SendData(ref packet, DeliveryMethod.ReliableOrdered, FikaBackendUtils.IsServer);
        }
        private static void RefreshLocalState()
        {
            if (Singleton<GameWorld>.Instantiated)
                Singleton<GameWorld>.Instance.GetComponent<PocketRouletteScript>()?.RequestStateRefresh();
        }
        private static void OnServerStateChanged(StateChangedPacket packet, NetPeer peer)
        {
            if (!Guid.TryParse(packet.Id, out _) || !StateSignals.Add(packet.Id)) return;
            RefreshLocalState();
            if (_registeredManager is FikaServer server) server.SendData(ref packet, DeliveryMethod.ReliableOrdered, peer);
        }
        private static void OnClientStateChanged(StateChangedPacket packet)
        {
            if (Guid.TryParse(packet.Id, out _) && StateSignals.Add(packet.Id)) RefreshLocalState();
        }

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
                return false;

            if (!(owner is FikaPlayer fikaOwner))
                return false;

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

        public static PartyContext GetPartyInfo(Player local)
        {
            var context = new PartyContext();
            if (!Singleton<IFikaNetworkManager>.Instantiated || FikaBackendUtils.IsHeadless || FikaBackendUtils.IsSpectator) return context;
            if (!string.IsNullOrEmpty(FikaBackendUtils.GroupId) && !string.IsNullOrEmpty(FikaBackendUtils.RaidCode))
                context.GroupId = FikaBackendUtils.GroupId + ":" + FikaBackendUtils.RaidCode + ":" + local.Location;
            var players = Singleton<IFikaNetworkManager>.Instance.CoopHandler?.Players;
            if (players != null)
                context.MemberIds = players.Values.Where(player => player != null && !player.IsAI && player.gameObject.activeInHierarchy
                    && player.HealthController.IsAlive).Select(player => player.ProfileId).Distinct().ToList();
            return context;
        }

        public static async Task<bool> RunGrantedEvent(Player owner, string raidId, string planId, Vector3 position)
        {
            if (_registeredManager == null || !Singleton<IFikaNetworkManager>.Instantiated || !(owner is FikaPlayer fikaOwner)) return false;
            var data = new FikaEventMessage { OwnerNetId = fikaOwner.NetId, OwnerProfileId = owner.ProfileId,
                RaidId = raidId, PlanId = planId, Stage = "request", X = position.x, Y = position.y, Z = position.z };
            if (FikaBackendUtils.IsServer) return await RunHostOnce(data);
            var completion = new TaskCompletionSource<bool>();
            PendingEvents[planId] = completion;
            try
            {
                var packet = new RouletteEventPacket { Json = JsonConvert.SerializeObject(data) };
                _registeredManager.SendData(ref packet, DeliveryMethod.ReliableOrdered);
                // Maximum configurable cursed deadline + preparation/network allowance.
                await RaidLifetime.WaitFor(completion.Task, RaidLifetime.For(Singleton<GameWorld>.Instance).Token, 165);
                return await completion.Task;
            }
            finally { PendingEvents.Remove(planId); }
        }
        public static bool SendPocketItem(Player owner, Item item, ItemAddress address)
        {
            if (_registeredManager == null || !Singleton<IFikaNetworkManager>.Instantiated)
                return false;

            if (!(owner is FikaPlayer fikaOwner) || !(address is GridItemAddress gridAddress))
                return false;

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
                createdEvent.Manager.RegisterPacket<RouletteEventPacket, NetPeer>(OnServerGrenade);
                createdEvent.Manager.RegisterPacket<StateChangedPacket, NetPeer>(OnServerStateChanged);
            }
            else
            {
                createdEvent.Manager.RegisterPacket<PocketPacket>(OnClientPocketItem);
                createdEvent.Manager.RegisterPacket<GroundPacket>(OnClientGroundItem);
                createdEvent.Manager.RegisterPacket<RouletteEventPacket>(OnClientGrenade);
                createdEvent.Manager.RegisterPacket<StateChangedPacket>(OnClientStateChanged);
            }

            _registeredManager = createdEvent.Manager;
            foreach (var pending in PendingEvents.Values) pending.TrySetCanceled();
            PendingEvents.Clear();
            ReplicaWork.Clear();
            HostJobs.Clear();
            StateSignals.Clear();
        }

        private static async void OnServerPocketItem(PocketPacket packet, NetPeer peer)
        {
            var manager = _registeredManager;
            if (!await AddPocketItem(packet) || manager != _registeredManager) return;

            if (Singleton<IFikaNetworkManager>.Instance is FikaServer server)
            {
                server.SendData(ref packet, DeliveryMethod.ReliableOrdered, peer);
            }
        }

        private static readonly Dictionary<string, List<Task<bool>>> ReplicaWork = new Dictionary<string, List<Task<bool>>>();

        private static async void OnServerGrenade(RouletteEventPacket packet, NetPeer peer)
        {
            try
            {
                var data = JsonConvert.DeserializeObject<FikaEventMessage>(packet.Json);
                if (data != null && data.Stage == "request") await RunHostOnce(data);
            }
            catch (Exception ex) { Plugin.LogSource.LogError($"[PocketRoulette] Invalid event request: {ex}"); }
        }

        private static Task<bool> RunHostOnce(FikaEventMessage data)
        {
            if (HostJobs.TryGetValue(data.PlanId, out var existing)) return existing;
            var work = HostEvent(data);
            HostJobs[data.PlanId] = work;
            return work;
        }

        private static async Task<bool> HostEvent(FikaEventMessage data)
        {
            var manager = _registeredManager;
            var world = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null;
            var success = false;
            try
            {
                if (world == null || manager == null || !IsFinite(new Vector3(data.X, data.Y, data.Z))) return false;
                var authorized = RaidApi.Event(data.RaidId, data.PlanId);
                if (authorized.OwnerProfileId != data.OwnerProfileId) throw new InvalidOperationException("Event owner does not match its server grant.");
                var plan = authorized.Rolls.Single();
                var owner = FindPlayer(data.OwnerNetId, data.OwnerProfileId);
                var lifetime = RaidLifetime.For(world);
                var token = lifetime.Token;
                RaidLifetime.RequirePlayer(world, owner, token);
                if (!lifetime.EventIds.Add(plan.Id)) return false;
                data.Plan = plan;
                data.OwnerNickname = authorized.OwnerNickname;
                Broadcast("warning", true);
                WarnLocal(world, data);
                await LiveEventRunner.WaitSeconds(plan.Kind == "cursed" ? authorized.Config.CursedJackpot.ExplanationSeconds
                    : authorized.Config.GrenadeSpawnDelaySeconds, token);
                var grenade = await LiveGrenadeSpawner.Prepare(plan.GrenadeTpl, token, plan.HazardId);
                if (plan.Kind == "cursed") LiveGrenadeSpawner.RequireTimedFragmentation(grenade);
                RaidLifetime.RequirePlayer(world, owner, token);
                var position = new Vector3(data.X, data.Y, data.Z);
                if (plan.Kind == "grenade")
                {
                    success = LiveGrenadeSpawner.SpawnPrepared(world, owner, grenade, plan.GrenadePrewarmSeconds, position);
                    if (success) { data.Prewarm = plan.GrenadePrewarmSeconds; Broadcast("grenade", true); }
                }
                else
                {
                    var prize = await LiveEventRunner.PreparePrize(plan, token);
                    RaidLifetime.RequirePlayer(world, owner, token);
                    LiveEventRunner.Drop(world, owner, plan, prize, position);
                    data.Deadline = NetworkTimeSync.NetworkTime + plan.ExplosionDelaySeconds;
                    Broadcast("start", true);
                    if (owner.IsYourPlayer) LiveEventRunner.NotifyActive(authorized.Config, plan, plan.ExplosionDelaySeconds);
                    var spawned = false;
                    try
                    {
                        while (NetworkTimeSync.NetworkTime < data.Deadline)
                        {
                            token.ThrowIfCancellationRequested();
                            var remaining = (float)(data.Deadline - NetworkTimeSync.NetworkTime);
                            if (!spawned && remaining <= grenade.GetExplDelay)
                            {
                                data.Prewarm = EventTiming.Prewarm(Math.Max(0.5f, remaining), grenade.GetExplDelay);
                                if (!LiveGrenadeSpawner.SpawnPrepared(world, owner, grenade, data.Prewarm, position, allowInactive: true))
                                    throw new InvalidOperationException("Cursed grenade creation failed.");
                                spawned = true;
                                Broadcast("grenade", true);
                            }
                            await Task.Delay(25, token);
                        }
                        success = spawned;
                    }
                    finally
                    {
                        if (world != null && !token.IsCancellationRequested)
                        { LiveEventRunner.Expire(world, plan.Id); Broadcast("expire", true); }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Plugin.LogSource.LogError($"[PocketRoulette] Host event failed: {ex}"); }
            finally
            {
                try { if (world != null) Broadcast("complete", success); }
                catch (Exception ex) { Plugin.LogSource.LogError($"[PocketRoulette] Event result broadcast failed: {ex.Message}"); }
            }
            return success;

            void Broadcast(string stage, bool result)
            {
                if (manager != _registeredManager || world == null || !Singleton<GameWorld>.Instantiated || Singleton<GameWorld>.Instance != world) return;
                data.Stage = stage;
                data.Success = result;
                var packet = new RouletteEventPacket { Json = JsonConvert.SerializeObject(data) };
                manager.SendData(ref packet, DeliveryMethod.ReliableOrdered, true);
            }
        }

        private static bool IsFinite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        private static async void OnClientGrenade(RouletteEventPacket packet)
        {
            try
            {
                var data = JsonConvert.DeserializeObject<FikaEventMessage>(packet.Json);
                if (data == null || string.IsNullOrEmpty(data.PlanId) || !Singleton<GameWorld>.Instantiated) return;
                var world = Singleton<GameWorld>.Instance;
                if (data.Stage == "warning") WarnLocal(world, data);
                else if (data.Stage == "start" || data.Stage == "grenade")
                {
                    if (!ReplicaWork.TryGetValue(data.PlanId, out var work)) ReplicaWork[data.PlanId] = work = new List<Task<bool>>();
                    work.Add(ApplyReplica(world, data));
                }
                else if (data.Stage == "expire") LiveEventRunner.Expire(world, data.PlanId);
                else if (data.Stage == "complete")
                {
                    var success = data.Success;
                    if (ReplicaWork.TryGetValue(data.PlanId, out var work))
                    { success &= (await Task.WhenAll(work)).All(result => result); ReplicaWork.Remove(data.PlanId); }
                    if (PendingEvents.TryGetValue(data.PlanId, out var pending)) pending.TrySetResult(success);
                }
            }
            catch (Exception ex) { Plugin.LogSource.LogError($"[PocketRoulette] Event replica failed: {ex}"); }
        }

        private static void WarnLocal(GameWorld world, FikaEventMessage data)
        {
            if (FikaBackendUtils.IsHeadless) return;
            if (data.Plan == null || world.MainPlayer == null || world.MainPlayer.ProfileId == data.OwnerProfileId) return;
            if (RaidLifetime.For(world).HazardWarnings.Add(data.PlanId))
                LiveEventRunner.WarnParty(data.OwnerNickname, data.Plan);
        }

        private static async Task<bool> ApplyReplica(GameWorld world, FikaEventMessage data)
        {
            try
            {
                if (!data.Success || data.Plan == null) return false;
                var owner = FindPlayer(data.OwnerNetId, data.OwnerProfileId);
                if (owner == null) return false;
                var position = new Vector3(data.X, data.Y, data.Z);
                var token = RaidLifetime.For(world).Token;
                if (data.Stage == "grenade")
                    return await LiveGrenadeSpawner.Spawn(owner, data.Plan.GrenadeTpl, data.Prewarm, position, data.Plan.HazardId,
                        allowInactive: data.Plan.Kind == "cursed");
                var prize = await LiveEventRunner.PreparePrize(data.Plan, token);
                var remaining = (float)(data.Deadline - NetworkTimeSync.NetworkTime);
                if (remaining <= 0 || RaidLifetime.For(world).ExpiredEvents.Contains(data.PlanId)) return false;
                var dropped = LiveEventRunner.Drop(world, owner, data.Plan, prize, position);
                if (dropped != null && owner.IsYourPlayer)
                    LiveEventRunner.NotifyActive(Plugin.CachedConfig ?? Models.PocketRouletteConfig.CreateDefault(), data.Plan, remaining);
                return dropped != null;
            }
            catch (Exception ex) { Plugin.LogSource.LogError($"[PocketRoulette] Event replica preparation failed: {ex}"); return false; }
        }
        private static async void OnClientPocketItem(PocketPacket packet)
        {
            if (Singleton<GameWorld>.Instantiated && Singleton<GameWorld>.Instance?.MainPlayer?.ProfileId == packet.OwnerProfileId)
                return;

            await AddPocketItem(packet);
        }

        private static async void OnServerGroundItem(GroundPacket packet, NetPeer peer)
        {
            var manager = _registeredManager;
            if (!await SpawnGroundItem(packet) || manager != _registeredManager) return;

            if (Singleton<IFikaNetworkManager>.Instance is FikaServer server)
            {
                server.SendData(ref packet, DeliveryMethod.ReliableOrdered, peer);
            }
        }

        private static async void OnClientGroundItem(GroundPacket packet)
        {
            if (Singleton<GameWorld>.Instantiated && Singleton<GameWorld>.Instance?.MainPlayer?.ProfileId == packet.OwnerProfileId)
                return;

            await SpawnGroundItem(packet);
        }

        private static async Task<bool> SpawnGroundItem(GroundPacket packet)
        {
            try
            {
                var gameWorld = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null;
                if (gameWorld == null || packet.Item == null)
                    return false;

                var token = RaidLifetime.For(gameWorld).Token;
                await RaidLifetime.PrepareItem(packet.Item, token);
                token.ThrowIfCancellationRequested();
                // Use the same physics path as the owner, rather than fixing the replica in mid-air.
                return gameWorld.ThrowItem(packet.Item, FindPlayer(packet.OwnerNetId, packet.OwnerProfileId),
                    packet.Position, packet.Rotation, Vector3.zero, Vector3.zero, true, false, 0f) != null;
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogWarning($"[PocketRoulette] Failed to sync Fika ground item: {ex.Message}");
                return false;
            }
        }

        private static async Task<bool> AddPocketItem(PocketPacket packet)
        {
            try
            {
                var player = FindPlayer(packet.OwnerNetId, packet.OwnerProfileId);
                if (player?.InventoryController == null || packet.Item == null)
                    return false;

                var gameWorld = Singleton<GameWorld>.Instance;
                var token = RaidLifetime.For(gameWorld).Token;
                await RaidLifetime.PrepareItem(packet.Item, token);
                RaidLifetime.RequirePlayer(gameWorld, player, token);

                if (player.InventoryController.Inventory.Equipment.GetAllItems().Any(existing => existing.Id == packet.Item.Id))
                    return true;

                var pocketsSlot = player.Profile.Inventory.Equipment.GetSlot(EquipmentSlot.Pockets);
                if (!(pocketsSlot?.ContainedItem is CompoundItem pocketsItem))
                    return false;

                foreach (var grid in pocketsItem.Grids)
                {
                    if (grid.ID == packet.SlotId)
                    {
                        var location = new LocationInGrid(packet.X, packet.Y, (ItemRotation)packet.Rotation);
                        var address = grid.CreateItemAddress(location);
                        if (ItemManipulator.Add(packet.Item, address, player.InventoryController, true).Failed)
                            return false;
                        player.InventoryController.AddAndRaiseEvents(packet.Item, address);
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogWarning($"[PocketRoulette] Failed to sync Fika pocket item: {ex.Message}");
                return false;
            }
        }

        private static Player FindPlayer(int netId, string profileId)
        {
            if (!Singleton<IFikaNetworkManager>.Instantiated)
                return null;

            var networkManager = Singleton<IFikaNetworkManager>.Instance;
            if (networkManager.CoopHandler?.Players != null && networkManager.CoopHandler.Players.TryGetValue(netId, out var player)
                && player.ProfileId == profileId)
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

    internal struct RouletteEventPacket : INetSerializable
    {
        public string Json;
        public void Serialize(NetDataWriter writer) => writer.PutBytesWithLength(Encoding.UTF8.GetBytes(Json ?? string.Empty));
        public void Deserialize(NetDataReader reader) => Json = Encoding.UTF8.GetString(reader.GetBytesWithLength());
    }
    internal struct StateChangedPacket : INetSerializable
    {
        public string Id;
        public void Serialize(NetDataWriter writer) => writer.PutBytesWithLength(Encoding.UTF8.GetBytes(Id ?? ""));
        public void Deserialize(NetDataReader reader) => Id = Encoding.UTF8.GetString(reader.GetBytesWithLength());
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

