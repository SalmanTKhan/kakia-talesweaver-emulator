using Kakia.TW.World.Entities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Yggdrasil.Logging;

namespace Kakia.TW.World.Managers
{
	public class MapManager
	{
		private readonly ConcurrentDictionary<int, Map> _maps = new();

		public void Load()
		{
			// TODO: Load from MapsData.json
			// Mocking a map for now (Map 1, Zone 1)
			var map1 = new Map(1, 1);
			_maps.TryAdd(GetHash(1, 1), map1);
			Log.Info("Loaded Map 1-1");
		}

		public Map GetMap(ushort mapId, ushort zoneId)
		{
			if (_maps.TryGetValue(GetHash(mapId, zoneId), out var map))
				return map;

			// Create dynamically or return Limbo
			return null;
		}

		public void Update(TimeSpan elapsed)
		{
			foreach (var map in _maps.Values)
			{
				map.Update(elapsed);
			}
		}

		public bool TryGetPlayer(uint id, out Player? player)
		{
			player = default;
			foreach (var map in _maps.Values)
			{
				if (map.TryGetPlayer(id, out player))
					break;
			}
			return player != null;
		}

		/// <summary>
		/// Tries to find a player by their character name.
		/// </summary>
		public bool TryGetPlayerByName(string name, out Player? player)
		{
			player = default;
			foreach (var map in _maps.Values)
			{
				if (map.TryGetPlayerByName(name, out player))
					break;
			}
			return player != null;
		}

		/// <summary>
		/// Gets the total number of players across all maps.
		/// </summary>
		public int GetPlayerCount()
		{
			var count = 0;
			foreach (var map in _maps.Values)
			{
				count += map.GetPlayerCount();
			}
			return count;
		}

		/// <summary>
		/// Gets or creates a map instance.
		/// </summary>
		public Map GetOrCreateMap(ushort mapId, ushort zoneId)
		{
			var hash = GetHash(mapId, zoneId);
			if (_maps.TryGetValue(hash, out var existing))
				return existing;

			var newMap = new Map(mapId, zoneId);
			if (_maps.TryAdd(hash, newMap))
			{
				Log.Info($"Created Map {mapId}-{zoneId}");
				return newMap;
			}

			// Another thread created it first
			return _maps[hash];
		}

		/// <summary>
		/// Gets a list of all loaded map identifiers.
		/// </summary>
		public IEnumerable<string> GetLoadedMapIds()
		{
			foreach (var map in _maps.Values)
			{
				yield return $"{map.MapId}-{map.ZoneId}";
			}
		}

		private int GetHash(ushort map, ushort zone) => map << 16 | zone;
	}
}
