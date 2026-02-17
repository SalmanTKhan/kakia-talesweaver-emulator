namespace TalesWeaverMapConverter;

/// <summary>
/// Walks the map directory structure, parses all binary files,
/// and delegates to <see cref="ScriptGenerator"/> for output.
/// </summary>
public sealed class MapProcessor
{
	private readonly ScriptGenerator _gen;

	public MapProcessor(Dictionary<int, string>? entityNames = null)
	{
		_gen = new ScriptGenerator(entityNames);
	}

	// ========================================================================
	// Batch: process all Maps/ and NPCs/ under a root directory
	//
	// Expected structure:
	//   {root}/Maps/MapId_{id}/ZoneId_{zoneId}/
	//       map.bin
	//       SpawnPos.txt
	//       Spawn/*.bin
	//       WarpPortals/*.json
	//   {root}/NPCs/*.bin
	// ========================================================================

	public List<string> ProcessAll(string rootDir, string outputDir)
	{
		var (mapDirs, npcFiles) = FindMapsAndNpcs(rootDir);

		if (mapDirs.Count == 0 && npcFiles.Count == 0)
		{
			Console.WriteLine("No Maps or NPCs folders found!");
			Console.WriteLine();
			Console.WriteLine("Expected structure:");
			Console.WriteLine("  Maps/MapId_X/ZoneId_Y/map.bin");
			Console.WriteLine("  Maps/MapId_X/ZoneId_Y/SpawnPos.txt");
			Console.WriteLine("  Maps/MapId_X/ZoneId_Y/Spawn/*.bin");
			Console.WriteLine("  NPCs/*.bin");
			return new();
		}

		Console.WriteLine($"Found {mapDirs.Count} map(s) and {npcFiles.Count} global NPC file(s)");
		Console.WriteLine();

		var generated = new List<string>();

		// --- Global NPCs ---
		var globalNpcs = ParseGlobalNpcs(npcFiles);
		if (globalNpcs.Count > 0)
			Console.WriteLine($"Parsed {globalNpcs.Count} global NPCs");

		// --- Each map zone ---
		foreach (var mapDir in mapDirs.OrderBy(d => d))
		{
			var relPath = Path.GetRelativePath(rootDir, mapDir);
			Console.WriteLine($"\nProcessing: {relPath}");
			generated.AddRange(ProcessSingleMap(mapDir, outputDir));
		}

		// --- Write global NPC script ---
		if (globalNpcs.Count > 0)
		{
			var npcOutDir = Path.Combine(outputDir, "Npcs");
			Directory.CreateDirectory(npcOutDir);

			var script = _gen.GenerateGlobalNpcScript(globalNpcs);
			var path = Path.Combine(npcOutDir, "GlobalNpcs.cs");
			File.WriteAllText(path, script);
			generated.Add(path);
			Console.WriteLine($"\nGenerated global NPC script with {globalNpcs.Count} NPCs");
		}

		return generated;
	}

	// ========================================================================
	// Single-map-directory processing (also used by batch)
	// ========================================================================

	public List<string> ProcessSingleMap(string mapDir, string outputDir)
	{
		var generated = new List<string>();

		// --- map.bin ----------------------------------------------------------
		var mapBinPath = Path.Combine(mapDir, "map.bin");
		if (!File.Exists(mapBinPath))
		{
			Console.WriteLine("  Warning: No map.bin found");
			return generated;
		}

		var header = Parsers.ParseMapBin(mapBinPath);
		Console.WriteLine($"  Map ID: {header.MapId}, Zone ID: {header.ZoneId}");

		// --- SpawnPos.txt -----------------------------------------------------
		var spawnPositions = new List<SpawnPosition>();
		var spawnPosPath = Path.Combine(mapDir, "SpawnPos.txt");
		if (File.Exists(spawnPosPath))
		{
			spawnPositions = Parsers.ParseSpawnPosTxt(spawnPosPath);
			Console.WriteLine($"  Spawn positions: {spawnPositions.Count}");
		}

		// --- Spawn/*.bin entities --------------------------------------------
		var monsters = new List<MonsterNpcEntity>();
		var npcs = new List<MonsterNpcEntity>();
		var portals = new List<PortalEntity>();
		var reactors = new List<ReactorEntity>();
		var items = new List<ItemEntity>();
		var generics = new List<GenericEntity>();
		int skippedPlayers = 0;

		var spawnDir = Path.Combine(mapDir, "Spawn");
		if (Directory.Exists(spawnDir))
		{
			foreach (var file in Directory.GetFiles(spawnDir, "*.bin"))
			{
				try
				{
					var entity = Parsers.ParseEntityPacket(file);
					if (entity is null) continue;

					if (entity is PlayerEntity)
					{
						skippedPlayers++;
						continue;
					}

					ClassifyEntity(entity, monsters, npcs, portals, reactors, items, generics);
				}
				catch (Exception ex)
				{
					Console.WriteLine($"  Warning: Failed to parse {Path.GetFileName(file)}: {ex.Message}");
				}
			}
		}

		Console.WriteLine($"  Monsters: {monsters.Count}, NPCs: {npcs.Count}, Portals: {portals.Count}, " +
						  $"Items: {items.Count}, Reactors: {reactors.Count}, Generic: {generics.Count}" +
						  (skippedPlayers > 0 ? $", Players skipped: {skippedPlayers}" : ""));

		// --- WarpPortals/*.json -----------------------------------------------
		var warpConfigs = Parsers.ParseWarpConfigs(Path.Combine(mapDir, "WarpPortals"));
		if (warpConfigs.Count > 0)
			Console.WriteLine($"  Warp configs: {warpConfigs.Count}");

		// --- Create output directories ----------------------------------------
		var spawnsDir = Path.Combine(outputDir, "Spawns");
		var npcsDir = Path.Combine(outputDir, "Npcs");
		var warpsDir = Path.Combine(outputDir, "Warps");
		Directory.CreateDirectory(spawnsDir);
		Directory.CreateDirectory(npcsDir);
		Directory.CreateDirectory(warpsDir);

		var baseName = $"Map{header.MapId}_Zone{header.ZoneId}";

		// --- Spawn script -----------------------------------------------------
		if (spawnPositions.Count > 0 || monsters.Count > 0 || generics.Count > 0)
		{
			var script = _gen.GenerateSpawnScript(header, spawnPositions, monsters, generics);
			var path = Path.Combine(spawnsDir, $"{baseName}_Spawns.cs");
			File.WriteAllText(path, script);
			generated.Add(path);
		}

		// --- NPC script -------------------------------------------------------
		var npcScript = _gen.GenerateNpcScript(header, npcs);
		if (npcScript is not null)
		{
			var path = Path.Combine(npcsDir, $"{baseName}_Npcs.cs");
			File.WriteAllText(path, npcScript);
			generated.Add(path);
		}

		// --- Warp script ------------------------------------------------------
		var warpScript = _gen.GenerateWarpScript(header, portals, warpConfigs);
		if (warpScript is not null)
		{
			var path = Path.Combine(warpsDir, $"{baseName}_Warps.cs");
			File.WriteAllText(path, warpScript);
			generated.Add(path);
		}

		return generated;
	}

	// ========================================================================
	// Directory scanning
	// ========================================================================

	/// <summary>
	/// Locate MapId_X/ZoneId_Y/ directories and NPC .bin files.
	/// Supports multiple layouts:
	///   1. {root}/Maps/MapId_X/ZoneId_Y/map.bin   (full structure)
	///   2. {root}/MapId_X/ZoneId_Y/map.bin         (root IS the Maps folder)
	///   3. {root}/ZoneId_Y/map.bin                  (root is a single MapId_X)
	///   4. {root}/map.bin                           (root is a single zone)
	///   5. {root}/child/map.bin                     (flat children with map.bin)
	/// </summary>
	private static (List<string> MapDirs, List<string> NpcFiles) FindMapsAndNpcs(string rootDir)
	{
		var mapDirs = new List<string>();
		var npcFiles = new List<string>();

		// Try to scan for MapId_*/ZoneId_*/ in candidate roots
		var candidateRoots = new List<string>();

		// (1) {root}/Maps/
		var mapsSubDir = Path.Combine(rootDir, "Maps");
		if (Directory.Exists(mapsSubDir))
			candidateRoots.Add(mapsSubDir);

		// (2) root itself (user passed the Maps/ folder directly)
		candidateRoots.Add(rootDir);

		foreach (var candidate in candidateRoots)
		{
			foreach (var mapFolder in Directory.GetDirectories(candidate, "MapId_*"))
			{
				foreach (var zoneFolder in Directory.GetDirectories(mapFolder, "ZoneId_*"))
				{
					if (File.Exists(Path.Combine(zoneFolder, "map.bin")))
						mapDirs.Add(zoneFolder);
				}
			}

			if (mapDirs.Count > 0)
				break; // Found maps in this candidate, no need to check others
		}

		// (3) root is a single MapId_X → look for ZoneId_*/map.bin directly
		if (mapDirs.Count == 0)
		{
			foreach (var zoneFolder in Directory.GetDirectories(rootDir, "ZoneId_*"))
			{
				if (File.Exists(Path.Combine(zoneFolder, "map.bin")))
					mapDirs.Add(zoneFolder);
			}
		}

		// (4) root is a single zone directory containing map.bin
		if (mapDirs.Count == 0 && File.Exists(Path.Combine(rootDir, "map.bin")))
		{
			mapDirs.Add(rootDir);
		}

		// (5) flat children with map.bin
		if (mapDirs.Count == 0)
		{
			foreach (var sub in Directory.GetDirectories(rootDir))
			{
				if (File.Exists(Path.Combine(sub, "map.bin")))
					mapDirs.Add(sub);
			}
		}

		// NPCs/ — check both {root}/NPCs/ and {root}/../NPCs/ (sibling)
		foreach (var npcsCandidate in new[]
		{
			Path.Combine(rootDir, "NPCs"),
			Path.Combine(rootDir, "..", "NPCs"),
		})
		{
			var resolved = Path.GetFullPath(npcsCandidate);
			if (Directory.Exists(resolved))
			{
				npcFiles.AddRange(Directory.GetFiles(resolved, "*.bin"));
				break;
			}
		}

		return (mapDirs, npcFiles);
	}

	// ========================================================================
	// Entity classification
	// ========================================================================

	/// <summary>
	/// Route a parsed entity into the appropriate typed list.
	/// Player spawns are skipped — they are captured packet data, not server-authored content.
	/// </summary>
	private static void ClassifyEntity(
		ParsedEntity entity,
		List<MonsterNpcEntity> monsters,
		List<MonsterNpcEntity> npcs,
		List<PortalEntity> portals,
		List<ReactorEntity> reactors,
		List<ItemEntity> items,
		List<GenericEntity> generics)
	{
		switch (entity)
		{
			case MonsterNpcEntity m:
				// Legacy NPC payloads are parsed through SpawnType.Player;
				// regular MonsterNpc payloads are treated as monster spawns.
				if (m.Type == SpawnType.Player)
					npcs.Add(m);
				else
					monsters.Add(m);
				break;

			case PlayerEntity:
				// Skip — player spawns are recorded session data, not map definitions
				break;

			case PortalEntity portal:
				portals.Add(portal);
				break;

			case ReactorEntity r:
				reactors.Add(r);
				break;

			case ItemEntity item:
				items.Add(item);
				break;

			case GenericEntity g:
				generics.Add(g);
				break;

				// RemoveEntity is informational only — not used in script generation
		}
	}

	// ========================================================================
	// Global NPC parsing
	// ========================================================================

	private List<MonsterNpcEntity> ParseGlobalNpcs(List<string> npcFiles)
	{
		var result = new List<MonsterNpcEntity>();

		foreach (var path in npcFiles)
		{
			try
			{
				var entity = Parsers.ParseEntityPacket(path);
				if (entity is MonsterNpcEntity npc)
					result.Add(npc);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"  Warning: Failed to parse global NPC {Path.GetFileName(path)}: {ex.Message}");
			}
		}

		return result;
	}
}
