using Kakia.TW.Shared.World;
using Kakia.TW.World.Entities;
using Kakia.TW.World.Managers;
using System;
using System.Collections.Generic;
using System.Text;
using Yggdrasil.Logging;

namespace Kakia.TW.World.Scripting
{
	public static partial class Shortcuts
	{
		public static void AddSpawner(string mapKey, string mobName, uint modelId, int count, ushort x, ushort y, int respawnMs)
		{
			// MapKey parsing logic (string -> ID)
			// For now assuming mapKey is "1_1" style or we look up ID
			var map = WorldServer.Instance.World.Maps.GetMap(1, 1);

			if (map != null)
			{
				for (int i = 0; i < count; i++)
				{
					// Scatter logic could go here (x + rand, y + rand)
					var spawner = new Spawner(map, mobName, modelId, x, y, respawnMs);
					map.AddSpawner(spawner);
				}
			}
		}

		public static void AddWarp(string mapKey, ushort x, ushort y, ushort destMapId, ushort destX, ushort destY)
		{
			var map = WorldServer.Instance.World.Maps.GetMap(1, 1);
			if (map != null)
			{
				// Map will assign ObjectId via RegisterEntity
				var warp = new Warp()
				{
					Position = new Position(x, y),
					DestMapId = destMapId,
					DestZoneId = 1, // Default zone usually
					DestX = destX,
					DestY = destY
				};
				map.AddWarp(warp);
			}
		}

		/// <summary>
		/// Adds a warp portal defined by a trigger area (minX,minY)-(maxX,maxY).
		/// The warp entity is placed at the center of the area.
		/// </summary>
		public static Warp AddWarp(ushort mapId, ushort zoneId, ushort minX, ushort minY, ushort maxX, ushort maxY, ushort destMapId, ushort destZoneId, ushort destX, ushort destY, byte destDirection = 0)
		{
			var map = WorldServer.Instance.World.Maps.GetOrCreateMap(mapId, zoneId);
			if (map == null)
			{
				Log.Warning($"NpcScript: Cannot add warp - Map {mapId}-{zoneId} not found.");
				return null;
			}

			// Place the warp entity at the center of the trigger area
			var cx = (ushort)((minX + maxX) / 2);
			var cy = (ushort)((minY + maxY) / 2);

			var warp = new Warp()
			{
				Position = new Position(cx, cy),
				DestMapId = destMapId,
				DestZoneId = destZoneId,
				DestX = destX,
				DestY = destY,
				// Area bounds for trigger detection
				MinX = minX,
				MinY = minY,
				MaxX = maxX,
				MaxY = maxY,
				DestDirection = destDirection,
			};

			map.AddWarp(warp);

			Log.Debug($"Added area Warp (ObjectId: {warp.ObjectId}) at {mapId}-{zoneId} area ({minX},{minY})-({maxX},{maxY}) -> {destMapId}-{destZoneId} ({destX},{destY}) dir:{destDirection}");

			return warp;
		}
	}
}
