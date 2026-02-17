using Kakia.TW.Shared.Network;
using Kakia.TW.Shared.World;
using Kakia.TW.World.Entities;
using Kakia.TW.World.Network;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Kakia.TW.World.Managers
{
	public class Map : IUpdateable
	{
		public ushort MapId { get; }
		public ushort ZoneId { get; }

		// Thread-safe collections for entities by ObjectId
		private readonly ConcurrentDictionary<uint, Player> _players = new();
		private readonly ConcurrentDictionary<uint, ItemEntity> _items = new();
		private readonly ConcurrentDictionary<uint, Npc> _npcs = new();
		private readonly ConcurrentDictionary<uint, Monster> _monsters = new();
		private readonly ConcurrentDictionary<uint, Warp> _warps = new();
		private readonly ConcurrentDictionary<uint, Reactor> _reactors = new();
		private readonly List<Spawner> _spawners = new();

		// Object ID counter - starts at 0, increments for each entity
		private uint _nextObjectId = 0;

		// Unified entity lookup by ObjectId (all entity types)
		private readonly ConcurrentDictionary<uint, Entity> _entities = new();

		// Raw entity packets loaded from .bin files (sent directly to clients)
		private List<byte[]> _rawEntityPackets = new();

		public Map(ushort mapId, ushort zoneId)
		{
			MapId = mapId;
			ZoneId = zoneId;
		}

		/// <summary>
		/// Generates the next object ID for this map. Thread-safe.
		/// </summary>
		private uint GetNextObjectId()
		{
			return Interlocked.Increment(ref _nextObjectId);
		}

		/// <summary>
		/// Assigns an object ID and registers entity in unified lookup.
		/// </summary>
		private void RegisterEntity(Entity entity)
		{
			entity.ObjectId = GetNextObjectId();
			entity.Instance = this;
			_entities.TryAdd(entity.ObjectId, entity);
		}

		/// <summary>
		/// Removes entity from unified lookup.
		/// </summary>
		private void UnregisterEntity(Entity entity)
		{
			_entities.TryRemove(entity.ObjectId, out _);
			entity.Instance = null;
		}

		/// <summary>
		/// Unified entity lookup by ObjectId.
		/// </summary>
		public bool TryGetEntity(uint objectId, out Entity? entity)
		{
			return _entities.TryGetValue(objectId, out entity);
		}

		/// <summary>
		/// Typed entity lookup by ObjectId.
		/// </summary>
		public bool TryGetEntity<T>(uint objectId, out T? entity) where T : Entity
		{
			if (_entities.TryGetValue(objectId, out var e) && e is T typed)
			{
				entity = typed;
				return true;
			}
			entity = default;
			return false;
		}

		/// <summary>
		/// Removes any entity by ObjectId and broadcasts despawn.
		/// </summary>
		public bool RemoveEntity(uint objectId)
		{
			if (!_entities.TryRemove(objectId, out var entity))
				return false;

			// Remove from type-specific collection
			switch (entity)
			{
				case Player p: _players.TryRemove(p.ObjectId, out _); break;
				case Npc n: _npcs.TryRemove(n.ObjectId, out _); break;
				case Monster m: _monsters.TryRemove(m.ObjectId, out _); break;
				case Warp w: _warps.TryRemove(w.ObjectId, out _); break;
				case Reactor r: _reactors.TryRemove(r.ObjectId, out _); break;
				case ItemEntity i: _items.TryRemove(i.ObjectId, out _); break;
			}

			entity.Instance = null;

			// Broadcast despawn to all players
			foreach (var player in _players.Values)
			{
				Send.EntityRemove(player.Connection, objectId);
			}

			return true;
		}

		/// <summary>
		/// Sets raw entity packets to be sent to players when they enter the map.
		/// These are the original captured packets from the client.
		/// </summary>
		public void SetRawEntityPackets(List<byte[]> packets)
		{
			_rawEntityPackets = packets;
		}

		public void Update(TimeSpan elapsed)
		{
			// 1. Update Players
			foreach (var p in _players.Values)
			{
				p.Update(elapsed);
				CheckWarpCollision(p); // Check if player walked into a portal
			}

			// 2. Update Monsters
			foreach (var m in _monsters.Values) m.Update(elapsed);

			// 3. Update Spawners
			foreach (var s in _spawners) s.Update(elapsed);
		}

		public void Enter(Entity entity)
		{
			// Assign ObjectId via RegisterEntity
			RegisterEntity(entity);

			if (entity is Player p)
			{
				if (_players.TryAdd(p.ObjectId, p))
				{
					Send.MapChange(p.Connection, MapId, ZoneId);

					// Spawn existing entities for the player using ObjectId
					foreach (var m in _monsters.Values) Send.SpawnNpc(p.Connection, m.ObjectId, m.ModelId, m.Position, m.Direction);
					foreach (var w in _warps.Values) Send.SpawnPortal(p.Connection, w);

					// Spawn this player for others
					Broadcast(p, () =>
					{
						var pkt = new Packet((Op)0x33);
						// ... (Reuse spawn logic from Send.SpawnUser)
						return pkt;
					}, includeSelf: false);
				}
			}
			else if (entity is Monster m)
			{
				if (_monsters.TryAdd(m.ObjectId, m))
				{
					Broadcast(m, () =>
					{
						var pkt = new Packet(Op.WorldResponse);
						// Using Send.EntitySpawnNpc logic inline or via helper
						pkt.PutByte((byte)WorldPacketId.Spawn); // 0 Spawn
						pkt.PutByte(0x02); // Type 2: Monster/NPC
						pkt.PutUInt(m.ObjectId);
						pkt.PutUInt(m.ModelId);
						pkt.PutUInt(0); // Unk
						pkt.PutUInt(0); // Unk
						pkt.PutUShort(m.Position.X);
						pkt.PutUShort(m.Position.Y);
						pkt.PutByte((byte)m.Direction);
						return pkt;
					}, includeSelf: false);
				}
			}
			else if (entity is Warp w)
			{
				_warps.TryAdd(w.ObjectId, w);
				// Warps are usually static, so we might not broadcast spawn if added during load,
				// but if added dynamically:
				Broadcast(w, () =>
				{
					var pkt = new Packet(Op.WorldResponse);
					pkt.PutByte((byte)WorldPacketId.Spawn);
					pkt.PutByte(0x04); // Type 4: Portal
					pkt.PutUInt(w.ObjectId);
					pkt.PutUShort(w.Position.X);
					pkt.PutUShort(w.Position.Y);
					pkt.PutUShort(w.DestMapId);
					pkt.PutUShort(1); // Dest Portal ID (visual only usually)
					return pkt;
				}, includeSelf: false);
			}
		}

		public void Enter(Player player)
		{
			// Assign ObjectId via RegisterEntity
			RegisterEntity(player);

			if (_players.TryAdd(player.ObjectId, player))
			{
				// 1. Send Map Change Packet to Player
				Send.MapChange(player.Connection, MapId, ZoneId);

				// 2. Notify Player about existing entities
				foreach (var existingPlayer in _players.Values)
				{
					if (existingPlayer.ObjectId == player.ObjectId) continue;
					// Send existing player to new player
					Send.SpawnUser(player.Connection, existingPlayer.ObjectId, existingPlayer.Data, false);
					// Send new player to existing player
					Send.SpawnUser(existingPlayer.Connection, player.ObjectId, player.Data, false);
				}

				foreach (var item in _items.Values)
				{
					Send.SpawnItem(player.Connection, item.ItemData, item.Position, item.OwnerId);
				}

				// Send existing NPCs to new player using ObjectId
				foreach (var npc in _npcs.Values)
				{
					Send.SpawnNpc(player.Connection, npc.ObjectId, npc.ModelId, npc.Position, npc.Direction);
				}

				// Send existing monsters to new player using ObjectId
				foreach (var monster in _monsters.Values)
				{
					Send.SpawnMonster(player.Connection, monster);
				}

				// Send existing warps to new player
				foreach (var warp in _warps.Values)
				{
					Send.SpawnPortal(player.Connection, warp);
				}

				// Send existing reactors to new player
				foreach (var reactor in _reactors.Values)
				{
					Send.SpawnReactor(player.Connection, reactor.ObjectId, reactor.ReactorId, reactor.Position);
				}

				// Send raw entity packets (portals, NPCs from .bin files)
				foreach (var rawPacket in _rawEntityPackets)
				{
					player.Connection.SendRaw(rawPacket);
				}
			}
		}

		public void Leave(Player player)
		{
			if (_players.TryRemove(player.ObjectId, out _))
			{
				// Broadcast removal to others using ObjectId
				Broadcast(player, () =>
				{
					var p = new Packet(Op.WorldResponse);
					p.PutByte((byte)WorldPacketId.Despawn);
					p.PutUInt(player.ObjectId);
					return p;
				}, includeSelf: false);

				UnregisterEntity(player);
			}
		}

		public void Leave(Entity entity)
		{
			if (entity is Monster m)
			{
				_monsters.TryRemove(m.ObjectId, out _);
				UnregisterEntity(m);
				// Death packet handled in Monster.Die
			}
			else if (entity is Player p)
			{
				_players.TryRemove(p.ObjectId, out _);
				UnregisterEntity(p);
				// Broadcast vanish
			}
		}

		public void AddSpawner(Spawner spawner)
		{
			_spawners.Add(spawner);
		}

		public void Broadcast(Entity source, Func<Packet> packetBuilder, bool includeSelf = false)
		{
			var packet = packetBuilder();
			// In a real scenario, use QuadTree or Grid for range checks
			foreach (var p in _players.Values)
			{
				if (!includeSelf && p.ObjectId == source.ObjectId) continue;

				// Simple distance check (e.g. 20 tiles)
				if (IsInRange(source.Position, p.Position))
				{
					p.Connection.Send(packet);
				}
			}
		}

		private void CheckWarpCollision(Player p)
		{
			foreach (var w in _warps.Values)
			{
				// Simple distance check (Portal is usually 1x1 or 3x3)
				int dist = Math.Abs(p.Position.X - w.Position.X) +
						   Math.Abs(p.Position.Y - w.Position.Y);

				if (dist <= 1)
				{
					// Trigger Warp
					p.Warp(w.DestMapId, w.DestZoneId, w.DestX, w.DestY);
					break;
				}
			}
		}

		private bool IsInRange(Position a, Position b)
		{
			var dx = a.X - b.X;
			var dy = a.Y - b.Y;
			// 20*20 = 400
			return (dx * dx + dy * dy) <= 400;
		}

		public bool TryGetNpc(uint objectId, out Npc? npc)
		{
			return _npcs.TryGetValue(objectId, out npc);
		}

		/// <summary>
		/// Gets all players currently on this map.
		/// </summary>
		public IEnumerable<Player> GetPlayers()
		{
			return _players.Values;
		}

		/// <summary>
		/// Gets a player by ObjectId if they are on this map.
		/// </summary>
		public bool TryGetPlayer(uint objectId, out Player? player)
		{
			return _players.TryGetValue(objectId, out player);
		}

		/// <summary>
		/// Tries to find a player by their character name.
		/// </summary>
		public bool TryGetPlayerByName(string name, out Player? player)
		{
			player = _players.Values.FirstOrDefault(p =>
				p.Data.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
			return player != null;
		}

		/// <summary>
		/// Gets the number of players currently on this map.
		/// </summary>
		public int GetPlayerCount()
		{
			return _players.Count;
		}

		/// <summary>
		/// Finds a portal at the given coordinates.
		/// </summary>
		public Warp? FindPortalAt(ushort x, ushort y)
		{
			foreach (var warp in _warps.Values)
			{
				if (warp.HasArea)
				{
					if (x >= warp.MinX && x <= warp.MaxX &&
						y >= warp.MinY && y <= warp.MaxY)
						return warp;
					continue;
				}

				// Point warp: keep small tolerance around portal position.
				int dx = Math.Abs(x - warp.Position.X);
				int dy = Math.Abs(y - warp.Position.Y);
				if (dx <= 2 && dy <= 2)
				{
					return warp;
				}
			}
			return null;
		}

		/// <summary>
		/// Adds a warp portal to the map.
		/// </summary>
		public void AddWarp(Warp warp)
		{
			RegisterEntity(warp);
			_warps.TryAdd(warp.ObjectId, warp);
		}

		/// <summary>
		/// Adds an NPC to the map and broadcasts spawn to all players.
		/// </summary>
		public void AddNpc(Npc npc, bool silently = false)
		{
			RegisterEntity(npc);

			if (_npcs.TryAdd(npc.ObjectId, npc))
			{
				// Broadcast NPC spawn to all players on the map using ObjectId
				foreach (var player in _players.Values)
				{
					if (!silently)
						Send.SpawnNpc(player.Connection, npc.ObjectId, npc.ModelId, npc.Position, npc.Direction);
				}
			}
		}

		/// <summary>
		/// Removes an NPC from the map by ObjectId and broadcasts despawn to all players.
		/// </summary>
		public void RemoveNpc(uint objectId)
		{
			if (_npcs.TryRemove(objectId, out var npc))
			{
				// Broadcast NPC removal to all players using ObjectId
				foreach (var player in _players.Values)
				{
					Send.EntityRemove(player.Connection, npc.ObjectId);
				}

				UnregisterEntity(npc);
			}
		}

		/// <summary>
		/// Adds a monster to the map and broadcasts spawn to all players.
		/// </summary>
		public void AddMonster(Monster monster)
		{
			RegisterEntity(monster);

			if (_monsters.TryAdd(monster.ObjectId, monster))
			{
				// Broadcast monster spawn to all players on the map using ObjectId
				foreach (var player in _players.Values)
				{
					Send.SpawnMonster(player.Connection, monster.ObjectId, monster.ModelId, monster.Position, monster.Direction);
				}
			}
		}

		/// <summary>
		/// Removes a monster from the map by ObjectId and broadcasts despawn to all players.
		/// </summary>
		public void RemoveMonster(uint objectId)
		{
			if (_monsters.TryRemove(objectId, out var monster))
			{
				// Broadcast monster removal to all players using ObjectId
				foreach (var player in _players.Values)
				{
					Send.EntityRemove(player.Connection, monster.ObjectId);
				}

				UnregisterEntity(monster);
			}
		}

		/// <summary>
		/// Gets a monster by ObjectId if it exists on this map.
		/// </summary>
		public bool TryGetMonster(uint objectId, out Monster? monster)
		{
			return _monsters.TryGetValue(objectId, out monster);
		}

		/// <summary>
		/// Adds a reactor to the map and broadcasts spawn to all players.
		/// </summary>
		public void AddReactor(Reactor reactor)
		{
			RegisterEntity(reactor);

			if (_reactors.TryAdd(reactor.ObjectId, reactor))
			{
				// Broadcast reactor spawn to all players on the map
				foreach (var player in _players.Values)
				{
					Send.SpawnReactor(player.Connection, reactor.ObjectId, reactor.ReactorId, reactor.Position);
				}
			}
		}

		/// <summary>
		/// Removes a reactor from the map by ObjectId and broadcasts despawn to all players.
		/// </summary>
		public void RemoveReactor(uint objectId)
		{
			if (_reactors.TryRemove(objectId, out var reactor))
			{
				// Broadcast reactor removal to all players
				foreach (var player in _players.Values)
				{
					Send.EntityRemove(player.Connection, reactor.ObjectId);
				}

				UnregisterEntity(reactor);
			}
		}

		/// <summary>
		/// Gets a reactor by ObjectId if it exists on this map.
		/// </summary>
		public bool TryGetReactor(uint objectId, out Reactor? reactor)
		{
			return _reactors.TryGetValue(objectId, out reactor);
		}
	}
}
