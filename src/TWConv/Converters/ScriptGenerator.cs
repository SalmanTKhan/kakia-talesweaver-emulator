using System.Text;

namespace TalesWeaverMapConverter;

/// <summary>
/// Generates C# scripts from parsed map data for the Kakia.TW.World.Scripting system.
/// </summary>
public sealed class ScriptGenerator
{
	private readonly Dictionary<int, string> _entityNames;
	private static bool IsValidModelId(uint modelId) => modelId != 0;

	public ScriptGenerator(Dictionary<int, string>? entityNames = null)
	{
		_entityNames = entityNames ?? new();
	}

	/// <summary>Resolve a display name for an entity, falling back to a prefixed ID.</summary>
	public string GetName(int entityId, string prefix = "Entity")
	{
		return _entityNames.TryGetValue(entityId, out var name)
			? name
			: $"{prefix}_{entityId}";
	}

	// ========================================================================
	// Validation helpers
	// ========================================================================

	/// <summary>
	/// Validates that a value fits in a ushort (0–65535).
	/// Throws with context info if it doesn't.
	/// </summary>
	private static void ValidateUShort(long value, string fieldName, string context)
	{
		if (value < ushort.MinValue || value > ushort.MaxValue)
		{
			throw new InvalidDataException(
				$"[{context}] Field '{fieldName}' has value {value} (0x{value:X}) " +
				$"which is outside ushort range (0–65535). " +
				$"This typically indicates an endianness mismatch in the binary data.");
		}
	}

	/// <summary>
	/// Validates that a value fits in a ushort (-32768~32768).
	/// Throws with context info if it doesn't.
	/// </summary>
	private static void ValidateShort(long value, string fieldName, string context)
	{
		if (value < short.MinValue || value > short.MaxValue)
		{
			throw new InvalidDataException(
				$"[{context}] Field '{fieldName}' has value {value} (0x{value:X}) " +
				$"which is outside short range (-32768~32768). " +
				$"This typically indicates an endianness mismatch in the binary data.");
		}
	}

	/// <summary>
	/// Validates that a short value is non-negative (valid for use as ushort coordinate).
	/// Logs a warning but doesn't throw — the value may be intentionally signed.
	/// </summary>
	private static void ValidateCoordinate(short value, string fieldName, string context)
	{
		if (value < 0)
		{
			throw new InvalidDataException(
				$"[{context}] Coordinate '{fieldName}' is negative ({value} / 0x{(ushort)value:X4}). " +
				$"This likely indicates an endianness or parsing issue in the source binary file.");
		}
	}

	/// <summary>
	/// Validates a portal/warp config value fits ushort.
	/// </summary>
	private static void ValidateWarpValue(int value, string fieldName, string context)
	{
		if (value < ushort.MinValue || value > ushort.MaxValue)
		{
			throw new InvalidDataException(
				$"[{context}] Warp field '{fieldName}' has value {value} which is outside ushort range (0–65535). " +
				$"Check the WarpPortal JSON config for invalid values.");
		}
	}

	// ========================================================================
	// Spawn script — monster spawners
	// ========================================================================

	public string GenerateSpawnScript(
		MapHeader header,
		List<SpawnPosition> spawnPositions,
		List<MonsterNpcEntity> monsters,
		List<GenericEntity> genericMonsters)
	{
		// Deduplicate spawn positions by (x, y, direction)
		var seen = new HashSet<(int, int, int)>();
		var unique = new List<SpawnPosition>();
		foreach (var sp in spawnPositions)
		{
			if (seen.Add((sp.X, sp.Y, sp.Direction)))
				unique.Add(sp);
		}

		// Build a combined model-ID list from MonsterNpc + Generic entities
		var modelIds = new List<uint>();
		foreach (var m in monsters)
		{
			if (IsValidModelId(m.ModelId))
				modelIds.Add(m.ModelId);
		}

		var className = $"Map{header.MapId}_Zone{header.ZoneId}_Spawns";
		var context = $"Map{header.MapId}/Zone{header.ZoneId} Spawns";
		var sb = new StringBuilder();

		AppendHeader(sb, $"Monster spawns for Map {header.MapId}, Zone {header.ZoneId}");
		sb.AppendLine("using Kakia.TW.World.Scripting;");
		sb.AppendLine();
		sb.AppendLine("namespace Kakia.TW.World.Scripts.Spawns");
		sb.AppendLine("{");
		sb.AppendLine($"    /// <summary>");
		sb.AppendLine($"    /// Monster spawns for Map {header.MapId}, Zone {header.ZoneId}");
		sb.AppendLine($"    /// </summary>");
		sb.AppendLine($"    public class {className} : SpawnScript");
		sb.AppendLine("    {");
		sb.AppendLine("        public override void Load()");
		sb.AppendLine("        {");

		if (unique.Count == 0)
		{
			sb.AppendLine("            // No spawn positions defined");
		}
		else if (modelIds.Count > 0)
		{
			for (int i = 0; i < unique.Count; i++)
			{
				var sp = unique[i];

				// Validate spawn position coordinates
				ValidateShort(sp.X, "SpawnPos.X", $"{context} spawn point {i + 1}");
				ValidateShort(sp.Y, "SpawnPos.Y", $"{context} spawn point {i + 1}");

				uint modelId = modelIds.Count > 0
					? modelIds[i % modelIds.Count]
					: (uint)(1000 + i);

				var name = GetName((int)modelId, "Monster");

				sb.AppendLine($"            // Spawn point {i + 1}");
				sb.AppendLine($"            AddSpawner(");
				sb.AppendLine($"                modelId: {modelId},");
				sb.AppendLine($"                name: \"{name}\",");
				sb.AppendLine($"                mapId: {header.MapId},");
				sb.AppendLine($"                zoneId: {header.ZoneId},");
				sb.AppendLine($"                x: {sp.X},");
				sb.AppendLine($"                y: {sp.Y},");
				sb.AppendLine($"                direction: {sp.Direction},");
				sb.AppendLine($"                respawnTime: 30,");
				sb.AppendLine($"                maxHp: 100");
				sb.AppendLine($"            );");
				sb.AppendLine();
			}
		}
		else
		{
			sb.AppendLine("            // Spawn positions found, but no reliable monster model data.");
			sb.AppendLine("            // Leaving placeholders commented to avoid invalid spawn model IDs.");
			foreach (var pos in unique)
			{
				sb.AppendLine($"            // Position: ({pos.X}, {pos.Y}), Direction: {pos.Direction}");
			}
		}

		sb.AppendLine("        }");
		sb.AppendLine("    }");
		sb.AppendLine("}");
		return sb.ToString();
	}

	// ========================================================================
	// NPC script — map-local NPCs
	// ========================================================================

	public string? GenerateNpcScript(
		MapHeader header,
		List<MonsterNpcEntity> npcs)
	{
		if (npcs.Count == 0)
			return null;

		var className = $"Map{header.MapId}_Zone{header.ZoneId}_Npcs";
		var context = $"Map{header.MapId}/Zone{header.ZoneId} NPCs";
		var sb = new StringBuilder();

		AppendHeader(sb, $"NPCs for Map {header.MapId}, Zone {header.ZoneId}");
		sb.AppendLine("using Kakia.TW.World.Scripting;");
		sb.AppendLine();
		sb.AppendLine("namespace Kakia.TW.World.Scripts.Npcs");
		sb.AppendLine("{");
		sb.AppendLine($"    /// <summary>");
		sb.AppendLine($"    /// NPCs for Map {header.MapId}, Zone {header.ZoneId}");
		sb.AppendLine($"    /// </summary>");
		sb.AppendLine($"    public class {className} : NpcScript");
		sb.AppendLine("    {");
		sb.AppendLine("        public override void Load()");
		sb.AppendLine("        {");

		foreach (var npc in npcs)
		{
			if (!IsValidModelId(npc.ModelId))
				continue;

			// Validate NPC coordinates
			ValidateCoordinate(npc.PositionX, "PositionX",
				$"{context} NPC ObjectId={npc.ObjectId} ModelId={npc.ModelId} File={npc.FileName}");
			ValidateCoordinate(npc.PositionY, "PositionY",
				$"{context} NPC ObjectId={npc.ObjectId} ModelId={npc.ModelId} File={npc.FileName}");

			var name = GetName((int)npc.ModelId, "NPC");
			sb.AppendLine($"            // ObjectID: {npc.ObjectId}, ModelID: {npc.ModelId}");
			sb.AppendLine($"            SpawnNpc(");
			sb.AppendLine($"                modelId: {npc.ModelId},");
			sb.AppendLine($"                name: \"{name}\",");
			sb.AppendLine($"                mapId: {header.MapId},");
			sb.AppendLine($"                zoneId: {header.ZoneId},");
			sb.AppendLine($"                x: {npc.PositionX},");
			sb.AppendLine($"                y: {npc.PositionY},");
			sb.AppendLine($"                direction: {npc.Direction},");
			sb.AppendLine($"                dialogFunc: null  // TODO: Add dialog");
			sb.AppendLine($"            );");
			sb.AppendLine();
		}

		sb.AppendLine("        }");
		sb.AppendLine("    }");
		sb.AppendLine("}");
		return sb.ToString();
	}

	// ========================================================================
	// Global NPC script — NPCs from the top-level NPCs/ folder
	// ========================================================================

	public string GenerateGlobalNpcScript(List<MonsterNpcEntity> npcs)
	{
		var sb = new StringBuilder();

		AppendHeader(sb, "Global NPCs — assign mapId/zoneId based on where they should spawn");
		sb.AppendLine("using Kakia.TW.World.Scripting;");
		sb.AppendLine();
		sb.AppendLine("namespace Kakia.TW.World.Scripts.Npcs");
		sb.AppendLine("{");
		sb.AppendLine("    public class GlobalNpcs : NpcScript");
		sb.AppendLine("    {");
		sb.AppendLine("        public override void Load()");
		sb.AppendLine("        {");

		foreach (var npc in npcs)
		{
			if (!IsValidModelId(npc.ModelId))
				continue;

			// Validate global NPC coordinates
			ValidateCoordinate(npc.PositionX, "PositionX",
				$"GlobalNPC ObjectId={npc.ObjectId} ModelId={npc.ModelId} File={npc.FileName}");
			ValidateCoordinate(npc.PositionY, "PositionY",
				$"GlobalNPC ObjectId={npc.ObjectId} ModelId={npc.ModelId} File={npc.FileName}");

			var name = GetName((int)npc.ModelId, "NPC");
			sb.AppendLine($"            // NPC ModelID: {npc.ModelId}, ObjectID: {npc.ObjectId}");
			sb.AppendLine($"            SpawnNpc(");
			sb.AppendLine($"                modelId: {npc.ModelId},");
			sb.AppendLine($"                name: \"{name}\",");
			sb.AppendLine($"                mapId: 0,     // TODO: Set correct map");
			sb.AppendLine($"                zoneId: 0,    // TODO: Set correct zone");
			sb.AppendLine($"                x: {npc.PositionX},");
			sb.AppendLine($"                y: {npc.PositionY},");
			sb.AppendLine($"                direction: {npc.Direction},");
			sb.AppendLine($"                dialogFunc: null  // TODO: Add dialog");
			sb.AppendLine($"            );");
			sb.AppendLine();
		}

		sb.AppendLine("        }");
		sb.AppendLine("    }");
		sb.AppendLine("}");
		return sb.ToString();
	}

	// ========================================================================
	// Warp script — portals
	// ========================================================================

	public string? GenerateWarpScript(
		MapHeader header,
		List<PortalEntity> portals,
		Dictionary<long, WarpPortalConfig> warpConfigs)
	{
		if (portals.Count == 0 && warpConfigs.Count == 0)
			return null;

		var className = $"Map{header.MapId}_Zone{header.ZoneId}_Warps";
		var context = $"Map{header.MapId}/Zone{header.ZoneId} Warps";
		var sb = new StringBuilder();

		AppendHeader(sb, $"Warp portals for Map {header.MapId}, Zone {header.ZoneId}");
		sb.AppendLine("using Kakia.TW.World.Scripting;");
		sb.AppendLine("using static Kakia.TW.World.Scripting.Shortcuts;");
		sb.AppendLine();
		sb.AppendLine("namespace Kakia.TW.World.Scripts.Warps");
		sb.AppendLine("{");
		sb.AppendLine($"    /// <summary>");
		sb.AppendLine($"    /// Warp portals for Map {header.MapId}, Zone {header.ZoneId}");
		sb.AppendLine($"    /// </summary>");
		sb.AppendLine($"    public class {className} : NpcScript");
		sb.AppendLine("    {");
		sb.AppendLine("        public override void Load()");
		sb.AppendLine("        {");

		var processedIds = new HashSet<long>();

		// Portals from binary packets, with optional JSON override
		foreach (var portal in portals)
		{
			long portalId = portal.PortalId;
			processedIds.Add(portalId);

			if (warpConfigs.TryGetValue(portalId, out var config))
			{
				// JSON config available — use area-based AddWarp
				EmitWarpFromConfig(sb, config, context);
			}
			else
			{
				// Binary-only portal — validate and emit point-based SpawnWarp
				ValidateCoordinate(portal.PositionX, "PositionX",
					$"{context} Portal {portalId} File={portal.FileName}");
				ValidateCoordinate(portal.PositionY, "PositionY",
					$"{context} Portal {portalId} File={portal.FileName}");

				sb.AppendLine($"            // Portal {portalId} -> Map {portal.DestMapId}, Portal {portal.DestPortalId}");
				sb.AppendLine($"            SpawnWarp(");
				sb.AppendLine($"                mapId: {header.MapId},");
				sb.AppendLine($"                zoneId: {header.ZoneId},");
				sb.AppendLine($"                x: {portal.PositionX},");
				sb.AppendLine($"                y: {portal.PositionY},");
				sb.AppendLine($"                destMapId: {portal.DestMapId},");
				sb.AppendLine($"                destZoneId: 1,");
				sb.AppendLine($"                destX: 100,");
				sb.AppendLine($"                destY: 100");
				sb.AppendLine($"            );");
			}

			sb.AppendLine();
		}

		// JSON-only portals not matched to any binary entity
		foreach (var (warpId, config) in warpConfigs)
		{
			if (processedIds.Contains(warpId))
				continue;

			EmitWarpFromConfig(sb, config, context);
			sb.AppendLine();
		}

		sb.AppendLine("        }");
		sb.AppendLine("    }");
		sb.AppendLine("}");
		return sb.ToString();
	}

	/// <summary>Emit an AddWarp call from a WarpPortalConfig JSON entry.</summary>
	private static void EmitWarpFromConfig(StringBuilder sb, WarpPortalConfig config, string context)
	{
		var min = config.MinPoint;
		var max = config.MaxPoint;
		var dest = config.Destination;
		var destPos = dest?.Position?.Position;
		var destDir = dest?.Position?.Direction ?? 0;

		// Validate all values fit in ushort
		var warpContext = $"{context} WarpConfig Id={config.Id}";
		ValidateWarpValue(config.MapId, "MapId", warpContext);
		ValidateWarpValue(config.ZoneId, "ZoneId", warpContext);
		ValidateWarpValue(min?.X ?? 0, "MinPoint.X", warpContext);
		ValidateWarpValue(min?.Y ?? 0, "MinPoint.Y", warpContext);
		ValidateWarpValue(max?.X ?? 0, "MaxPoint.X", warpContext);
		ValidateWarpValue(max?.Y ?? 0, "MaxPoint.Y", warpContext);
		ValidateWarpValue(dest?.MapId ?? 0, "Destination.MapId", warpContext);
		ValidateWarpValue(dest?.ZoneId ?? 0, "Destination.ZoneId", warpContext);
		ValidateWarpValue(destPos?.X ?? 0, "Destination.Position.X", warpContext);
		ValidateWarpValue(destPos?.Y ?? 0, "Destination.Position.Y", warpContext);

		sb.AppendLine($"            // Portal {config.Id} (configured)");
		sb.AppendLine($"            // Area: ({min?.X},{min?.Y})-({max?.X},{max?.Y})");
		sb.AppendLine($"            AddWarp(");
		sb.AppendLine($"                mapId: {config.MapId},");
		sb.AppendLine($"                zoneId: {config.ZoneId},");
		sb.AppendLine($"                minX: {min?.X ?? 0},");
		sb.AppendLine($"                minY: {min?.Y ?? 0},");
		sb.AppendLine($"                maxX: {max?.X ?? 0},");
		sb.AppendLine($"                maxY: {max?.Y ?? 0},");
		sb.AppendLine($"                destMapId: {dest?.MapId ?? 0},");
		sb.AppendLine($"                destZoneId: {dest?.ZoneId ?? 0},");
		sb.AppendLine($"                destX: {destPos?.X ?? 0},");
		sb.AppendLine($"                destY: {destPos?.Y ?? 0},");
		sb.AppendLine($"                destDirection: {destDir}");
		sb.AppendLine($"            );");
	}

	// ========================================================================
	// Helpers
	// ========================================================================

	private static void AppendHeader(StringBuilder sb, string description)
	{
		sb.AppendLine($"// Auto-generated by TalesWeaver Map Converter");
		sb.AppendLine($"// Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
		sb.AppendLine($"// {description}");
		sb.AppendLine();
	}
}
