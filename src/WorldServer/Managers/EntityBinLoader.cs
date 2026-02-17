using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Yggdrasil.Logging;

namespace Kakia.TW.World.Managers
{
	/// <summary>
	/// Loads entity spawn data from binary files captured from the client.
	/// Stores raw packet data to send directly to clients (matching legacy behavior).
	/// </summary>
	public class EntityBinLoader
	{
		private readonly MapManager _mapManager;
		private readonly string _mapsPath;

		public EntityBinLoader(MapManager mapManager, string mapsPath)
		{
			_mapManager = mapManager;
			_mapsPath = mapsPath;
		}

		/// <summary>
		/// Loads all entity spawn data from the Maps directory structure.
		/// Expected structure: Maps/MapId_X/ZoneId_Y/Spawn/*.bin
		/// </summary>
		public void Load()
		{
			if (!Directory.Exists(_mapsPath))
			{
				Log.Warning($"EntityBinLoader: Maps directory not found: {_mapsPath}");
				return;
			}

			int totalEntities = 0;
			int totalMaps = 0;

			foreach (var mapFolder in Directory.GetDirectories(_mapsPath, "MapId_*"))
			{
				foreach (var zoneFolder in Directory.GetDirectories(mapFolder, "ZoneId_*"))
				{
					var count = LoadZone(zoneFolder);
					if (count > 0)
					{
						totalEntities += count;
						totalMaps++;
					}
				}
			}

			Log.Info($"EntityBinLoader: Loaded {totalEntities} raw entity packets from {totalMaps} maps");
		}

		private int LoadZone(string zonePath)
		{
			// Read map.bin to get mapId and zoneId
			var mapBinPath = Path.Combine(zonePath, "map.bin");
			if (!File.Exists(mapBinPath))
				return 0;

			var mapData = File.ReadAllBytes(mapBinPath);
			if (mapData.Length < 5)
				return 0;

			// map.bin format: [0]=PacketType(0x15), [1-2]=MapId(BE), [3-4]=ZoneId(BE)
			ushort mapId = (ushort)((mapData[1] << 8) | mapData[2]);
			ushort zoneId = (ushort)((mapData[3] << 8) | mapData[4]);

			var spawnDir = Path.Combine(zonePath, "Spawn");
			if (!Directory.Exists(spawnDir))
				return 0;

			var map = _mapManager.GetOrCreateMap(mapId, zoneId);
			var rawPackets = new List<byte[]>();
			var entityCounts = new Dictionary<byte, int>();

			foreach (var file in Directory.GetFiles(spawnDir, "*.bin"))
			{
				try
				{
					var data = File.ReadAllBytes(file);
					if (data.Length < 3)
						continue;

					// Byte 0: Opcode (0x07 = WorldResponse)
					// Byte 1: ActionType (0x00=Spawn, 0x01=Despawn, 0x02=Death)
					// Byte 2: SpawnType
					if (data[0] != 0x07)
						continue;

					byte spawnType = data[2];

					// Skip items (0x03) and pets (0x06) like legacy does
					// Note: Don't filter by action type - legacy sends all packets
					if (spawnType == 0x03 || spawnType == 0x06)
						continue;

					rawPackets.Add(data);

					// Track counts by type
					if (!entityCounts.ContainsKey(spawnType))
						entityCounts[spawnType] = 0;
					entityCounts[spawnType]++;
				}
				catch (Exception ex)
				{
					Log.Warning($"EntityBinLoader: Failed to load {file}: {ex.Message}");
				}
			}

			if (false)
				if (rawPackets.Count > 0)
				{
					map.SetRawEntityPackets(rawPackets);
					var typeInfo = string.Join(", ", entityCounts.Select(kv => $"Type{kv.Key:X2}={kv.Value}"));
					Log.Debug($"  Map {mapId}-{zoneId}: {rawPackets.Count} entities ({typeInfo})");
				}

			return rawPackets.Count;
		}
	}
}
