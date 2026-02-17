using System.Text.Json;
using TalesWeaverMapConverter;

// Ensure EntityToScriptConverter is accessible

namespace TWConv
{
	class Program
	{
		static void Main(string[] args)
		{
			Run(args);
			Console.ReadLine();
		}

		static int Run(string[] args)
		{
			if (args.Length == 0)
			{
				args = ["C:\\Projects\\GitHub\\kakia-talesweaver-emulator\\src\\TWConv\\", "Output"];
			}

			if (args.Contains("-h") || args.Contains("--help"))
			{
				PrintUsage();
				return 0;
			}

			// Parse common options
			var entityNames = LoadEntityNames(args);

			// --dump: inspect a single binary packet
			if (args[0] == "--dump")
			{
				if (args.Length < 2)
				{
					Console.Error.WriteLine("Usage: --dump <entity.bin>");
					return 1;
				}

				return DumpEntity(args[1]);
			}

			// --single: process one map directory
			if (args[0] == "--single")
			{
				if (args.Length < 3)
				{
					Console.Error.WriteLine("Usage: --single <map_dir> <output_dir> [--names entities.json]");
					return 1;
				}

				return ProcessSingle(args[1], args[2], entityNames);
			}

			// Default: batch convert
			if (args.Length < 2)
			{
				Console.Error.WriteLine("Usage: <maps_root> <output_dir> [--names entities.json]");
				return 1;
			}

			return BatchConvert(args[0], args[1], entityNames);
		}

		// ============================================================================
		// Commands
		// ============================================================================

		static int BatchConvert(string mapsRoot, string outputDir, Dictionary<int, string> entityNames)
		{
			if (!Directory.Exists(mapsRoot))
			{
				Console.Error.WriteLine($"Error: Directory not found: {mapsRoot}");
				return 1;
			}

			Directory.CreateDirectory(outputDir);

			Console.WriteLine(new string('=', 60));
			Console.WriteLine("TalesWeaver Map Data Converter");
			Console.WriteLine(new string('=', 60));
			Console.WriteLine($"Input:  {Path.GetFullPath(mapsRoot)}");
			Console.WriteLine($"Output: {Path.GetFullPath(outputDir)}");
			if (entityNames.Count > 0)
				Console.WriteLine($"Entity names: {entityNames.Count} mappings loaded");
			Console.WriteLine();

			var processor = new MapProcessor(entityNames);
			var generated = processor.ProcessAll(mapsRoot, outputDir);

			Console.WriteLine();
			Console.WriteLine(new string('=', 60));
			Console.WriteLine($"Generated {generated.Count} script files:");
			foreach (var path in generated)
				Console.WriteLine($"  {Path.GetRelativePath(outputDir, path)}");

			return 0;
		}

		static int ProcessSingle(string mapDir, string outputDir, Dictionary<int, string> entityNames)
		{
			if (!Directory.Exists(mapDir))
			{
				Console.Error.WriteLine($"Error: Directory not found: {mapDir}");
				return 1;
			}

			Directory.CreateDirectory(outputDir);

			Console.WriteLine($"Processing: {mapDir}");
			var processor = new MapProcessor(entityNames);
			var generated = processor.ProcessSingleMap(mapDir, outputDir);

			Console.WriteLine();
			Console.WriteLine($"Generated {generated.Count} script files:");
			foreach (var path in generated)
				Console.WriteLine($"  {Path.GetRelativePath(outputDir, path)}");

			return 0;
		}

		static int DumpEntity(string filePath)
		{
			if (!File.Exists(filePath))
			{
				Console.Error.WriteLine($"Error: File not found: {filePath}");
				return 1;
			}

			var data = File.ReadAllBytes(filePath);
			Console.WriteLine($"File: {filePath}");
			Console.WriteLine($"Size: {data.Length} bytes");
			Console.WriteLine($"Hex:  {BitConverter.ToString(data)}");
			Console.WriteLine();

			var entity = Parsers.ParseEntityPacket(data, Path.GetFileName(filePath));
			if (entity is null)
			{
				Console.WriteLine("Could not parse entity packet.");
				return 1;
			}

			Console.WriteLine($"Opcode:    0x{(byte)entity.Opcode:X2} ({entity.Opcode})");
			Console.WriteLine($"Action:    0x{(byte)entity.Action:X2} ({entity.Action})");
			Console.WriteLine($"SpawnType: 0x{(byte)entity.Type:X2} ({entity.Type})");

			switch (entity)
			{
				case MonsterNpcEntity m:
					Console.WriteLine($"ObjectID:  {m.ObjectId}");
					Console.WriteLine($"ModelID:   {m.ModelId}");
					Console.WriteLine($"UnkV3:     0x{m.UnkV3:X8}");
					Console.WriteLine($"UnkV30:    0x{m.UnkV30:X8}");
					Console.WriteLine($"Position:  ({m.PositionX}, {m.PositionY})");
					Console.WriteLine($"Direction: {m.Direction}");
					break;

				case PlayerEntity p:
					Console.WriteLine($"ObjectID:  {p.ObjectId}");
					Console.WriteLine($"Unk1:      0x{p.Unk1:X8}");
					Console.WriteLine($"ModelID:   {p.ModelId}");
					Console.WriteLine($"Position:  ({p.PositionX}, {p.PositionY})");
					Console.WriteLine($"Direction: {p.Direction}");
					break;

				case ItemEntity item:
					Console.WriteLine($"ItemID:    {item.ItemId}");
					Console.WriteLine($"Amount:    {item.Amount}");
					Console.WriteLine($"Durability:{item.Durability}");
					Console.WriteLine($"OwnerID:   {item.OwnerId}");
					Console.WriteLine($"Position:  ({item.PositionX}, {item.PositionY})");
					Console.WriteLine($"Dropped:   {item.DroppedAmount}");
					break;

				case PortalEntity portal:
					Console.WriteLine($"PortalID:  {portal.PortalId}");
					Console.WriteLine($"Position:  ({portal.PositionX}, {portal.PositionY})");
					Console.WriteLine($"DestMap:   {portal.DestMapId}");
					Console.WriteLine($"DestPortal:{portal.DestPortalId}");
					break;

				case ReactorEntity r:
					Console.WriteLine($"ObjectID:  {r.ObjectId}");
					Console.WriteLine($"ReactorID: {r.ReactorId}");
					Console.WriteLine($"Position:  ({r.PositionX}, {r.PositionY})");
					break;

				case RemoveEntity rem:
					Console.WriteLine($"ObjectID:  {rem.ObjectId}");
					break;

				case GenericEntity g:
					Console.WriteLine($"ObjectID:  {g.ObjectId}");
					break;
			}

			return 0;
		}

		// ============================================================================
		// Helpers
		// ============================================================================

		static Dictionary<int, string> LoadEntityNames(string[] args)
		{
			var idx = Array.IndexOf(args, "--names");
			if (idx < 0) idx = Array.IndexOf(args, "--config");

			if (idx >= 0 && idx + 1 < args.Length && File.Exists(args[idx + 1]))
			{
				try
				{
					var json = File.ReadAllText(args[idx + 1]);
					var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
					if (raw is not null)
					{
						var result = new Dictionary<int, string>();
						foreach (var (k, v) in raw)
						{
							if (int.TryParse(k, out var id))
								result[id] = v;
						}
						return result;
					}
				}
				catch (Exception ex)
				{
					Console.Error.WriteLine($"Warning: Failed to load entity names: {ex.Message}");
				}
			}

			return new();
		}

		static void PrintUsage()
		{
			Console.WriteLine("TalesWeaver Map Data Converter");
			Console.WriteLine();
			Console.WriteLine("Usage:");
			Console.WriteLine("  TalesWeaverMapConverter <maps_root> <output_dir> [--names entities.json]");
			Console.WriteLine("  TalesWeaverMapConverter --single <map_dir> <output_dir> [--names entities.json]");
			Console.WriteLine("  TalesWeaverMapConverter --dump <entity.bin>");
			Console.WriteLine();
			Console.WriteLine("Commands:");
			Console.WriteLine("  (default)   Batch-convert all maps under a root directory");
			Console.WriteLine("  --single    Convert a single map directory");
			Console.WriteLine("  --dump      Inspect a single entity binary file (Big Endian)");
			Console.WriteLine();
			Console.WriteLine("Options:");
			Console.WriteLine("  --names FILE    JSON file mapping entity IDs to display names");
			Console.WriteLine("  --config FILE   Alias for --names");
			Console.WriteLine("  -h, --help      Show this help message");
			Console.WriteLine();
			Console.WriteLine("Expected directory structure:");
			Console.WriteLine("  Maps/MapId_X/ZoneId_Y/");
			Console.WriteLine("      map.bin           # Map header (required)");
			Console.WriteLine("      SpawnPos.txt      # Spawn positions (optional)");
			Console.WriteLine("      Spawn/*.bin       # Entity spawn packets (optional)");
			Console.WriteLine("      WarpPortals/*.json# Warp portal configs (optional)");
			Console.WriteLine("  NPCs/*.bin            # Global NPC packets (optional)");
			Console.WriteLine();
			Console.WriteLine("All binary data is read as Big Endian (network byte order).");
		}
	}
}