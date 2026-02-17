using Kakia.TW.Shared.World;
using Kakia.TW.World.Entities;
using Kakia.TW.World.Managers;
using Yggdrasil.Logging;
using Yggdrasil.Scripting;

// Note: Removed static _npcIdCounter - Map now assigns ObjectIds

namespace Kakia.TW.World.Scripting
{
	/// <summary>
	/// Base class for NPC scripts that define NPC spawns and dialog behavior.
	/// </summary>
	public abstract class NpcScript : IScript, IDisposable
	{
		private readonly List<Npc> _spawnedNpcs = new();
		private readonly List<Reactor> _spawnedReactors = new();

		/// <summary>
		/// Initializes the script.
		/// </summary>
		public bool Init()
		{
			Load();
			return true;
		}

		/// <summary>
		/// Called when the script is being removed before a reload.
		/// </summary>
		public virtual void Dispose()
		{
			// Remove all NPCs spawned by this script
			foreach (var npc in _spawnedNpcs)
			{
				npc.Instance?.RemoveNpc(npc.ObjectId);
			}
			_spawnedNpcs.Clear();

			// Remove all reactors spawned by this script
			foreach (var reactor in _spawnedReactors)
			{
				reactor.Instance?.RemoveReactor(reactor.ObjectId);
			}
			_spawnedReactors.Clear();
		}

		/// <summary>
		/// Called when the script is being initialized. Override to spawn NPCs.
		/// </summary>
		public abstract void Load();

		/// <summary>
		/// Spawns an NPC at the specified location.
		/// </summary>
		/// <param name="modelId">The NPC's visual model ID.</param>
		/// <param name="name">The NPC's display name.</param>
		/// <param name="mapId">Map ID to spawn on.</param>
		/// <param name="zoneId">Zone ID to spawn on.</param>
		/// <param name="x">X coordinate.</param>
		/// <param name="y">Y coordinate.</param>
		/// <param name="direction">Facing direction (0-7).</param>
		/// <param name="dialogFunc">Optional dialog function when clicked.</param>
		/// <returns>The spawned NPC entity.</returns>
		protected Npc SpawnNpc(uint modelId, string name, ushort mapId, ushort zoneId, ushort x, ushort y, byte direction = 0, DialogFunc? dialogFunc = null)
		{
			var map = WorldServer.Instance.World.Maps.GetOrCreateMap(mapId, zoneId);
			if (map == null)
			{
				Log.Warning($"NpcScript: Cannot spawn NPC '{name}' - Map {mapId}-{zoneId} not found.");
				return null;
			}

			// Map will assign ObjectId via RegisterEntity
			var npc = new Npc(name, modelId)
			{
				Position = new Position(x, y),
				Direction = (Direction)direction,
				Script = dialogFunc
			};

			map.AddNpc(npc);
			_spawnedNpcs.Add(npc);

			Log.Debug($"Spawned NPC '{name}' (ObjectId: {npc.ObjectId}, Model: {modelId}) at {mapId}-{zoneId} ({x},{y})");

			return npc;
		}

		/// <summary>
		/// Spawns a warp portal at the specified location.
		/// </summary>
		protected Warp SpawnWarp(ushort mapId, ushort zoneId, ushort x, ushort y, ushort destMapId, ushort destZoneId, ushort destX, ushort destY)
		{
			var map = WorldServer.Instance.World.Maps.GetOrCreateMap(mapId, zoneId);
			if (map == null)
			{
				Log.Warning($"NpcScript: Cannot spawn warp - Map {mapId}-{zoneId} not found.");
				return null;
			}

			// Map will assign ObjectId via RegisterEntity
			var warp = new Warp()
			{
				Position = new Position(x, y),
				DestMapId = destMapId,
				DestZoneId = destZoneId,
				DestX = destX,
				DestY = destY
			};

			map.AddWarp(warp);

			Log.Debug($"Spawned Warp (ObjectId: {warp.ObjectId}) at {mapId}-{zoneId} ({x},{y}) -> {destMapId}-{destZoneId} ({destX},{destY})");

			return warp;
		}

		/// <summary>
		/// Spawns a reactor (interactive object) at the specified location.
		/// </summary>
		/// <param name="reactorId">The reactor's model/type ID.</param>
		/// <param name="mapId">Map ID to spawn on.</param>
		/// <param name="zoneId">Zone ID to spawn on.</param>
		/// <param name="x">X coordinate.</param>
		/// <param name="y">Y coordinate.</param>
		/// <param name="script">Optional script function when interacted with.</param>
		/// <returns>The spawned reactor entity.</returns>
		protected Reactor SpawnReactor(uint reactorId, ushort mapId, ushort zoneId, ushort x, ushort y, ReactorFunc? script = null)
		{
			var map = WorldServer.Instance.World.Maps.GetOrCreateMap(mapId, zoneId);
			if (map == null)
			{
				Log.Warning($"NpcScript: Cannot spawn reactor - Map {mapId}-{zoneId} not found.");
				return null;
			}

			var reactor = new Reactor(reactorId)
			{
				Position = new Position(x, y),
				Script = script
			};

			map.AddReactor(reactor);
			_spawnedReactors.Add(reactor);

			Log.Debug($"Spawned Reactor (ObjectId: {reactor.ObjectId}, ReactorId: {reactorId}) at {mapId}-{zoneId} ({x},{y})");

			return reactor;
		}
	}
}
