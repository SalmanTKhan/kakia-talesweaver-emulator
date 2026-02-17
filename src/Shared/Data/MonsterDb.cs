using System.Text.Json;
using System.Text.Json.Serialization;
using Yggdrasil.Logging;

namespace Kakia.TW.Shared.Data
{
	/// <summary>
	/// Database of monster definitions loaded from JSON.
	/// </summary>
	public class MonsterDb
	{
		private readonly Dictionary<int, MonsterData> _monsters = new();
		private readonly Dictionary<string, int> _nameIndex = new(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// Gets the number of monsters in the database.
		/// </summary>
		public int Count => _monsters.Count;

		/// <summary>
		/// Loads monster data from a JSON file.
		/// </summary>
		/// <param name="path">Path to monsters.json file.</param>
		public void Load(string path)
		{
			if (!File.Exists(path))
			{
				Log.Warning($"MonsterDb: File not found: {path}");
				return;
			}

			try
			{
				var json = File.ReadAllText(path);
				var options = new JsonSerializerOptions
				{
					PropertyNameCaseInsensitive = true,
					Converters = { new JsonStringEnumConverter() }
				};

				var data = JsonSerializer.Deserialize<MonsterDbFile>(json, options);
				if (data?.Monsters == null)
				{
					Log.Warning("MonsterDb: No monsters found in file.");
					return;
				}

				foreach (var monster in data.Monsters)
				{
					if (_monsters.TryAdd(monster.Id, monster))
					{
						_nameIndex[monster.Name] = monster.Id;
					}
					else
					{
						Log.Warning($"MonsterDb: Duplicate monster ID {monster.Id} ({monster.Name})");
					}
				}

				Log.Info($"MonsterDb: Loaded {_monsters.Count} monsters.");
			}
			catch (Exception ex)
			{
				Log.Error($"MonsterDb: Failed to load {path}: {ex.Message}");
			}
		}

		/// <summary>
		/// Gets a monster by its ID.
		/// </summary>
		public MonsterData? GetById(int id)
		{
			return _monsters.TryGetValue(id, out var monster) ? monster : null;
		}

		/// <summary>
		/// Gets a monster by its name (case-insensitive).
		/// </summary>
		public MonsterData? GetByName(string name)
		{
			if (_nameIndex.TryGetValue(name, out var id))
				return GetById(id);
			return null;
		}

		/// <summary>
		/// Gets all monsters within a level range.
		/// </summary>
		public IEnumerable<MonsterData> GetByLevel(int minLevel, int maxLevel)
		{
			return _monsters.Values.Where(m => m.Level >= minLevel && m.Level <= maxLevel);
		}

		/// <summary>
		/// Gets all monsters that spawn on a specific map.
		/// </summary>
		public IEnumerable<MonsterData> GetByMap(int mapId)
		{
			return _monsters.Values
				.Where(m => m.SpawnLocations.Any(s => s.MapId == mapId));
		}

		/// <summary>
		/// Gets all monsters that spawn in a specific zone.
		/// </summary>
		public IEnumerable<MonsterData> GetByZone(int mapId, int zoneId)
		{
			return _monsters.Values
				.Where(m => m.SpawnLocations.Any(s => s.MapId == mapId && s.ZoneId == zoneId));
		}

		/// <summary>
		/// Searches for monsters by name (partial match, case-insensitive).
		/// </summary>
		public IEnumerable<MonsterData> Search(string query, int maxResults = 20)
		{
			return _monsters.Values
				.Where(m => m.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
				.Take(maxResults);
		}

		/// <summary>
		/// Gets all monsters that drop a specific item.
		/// </summary>
		public IEnumerable<MonsterData> GetByDrop(int itemId)
		{
			return _monsters.Values
				.Where(m => m.DropTable.Any(d => d.ItemId == itemId));
		}

		/// <summary>
		/// Gets all monsters of a specific type.
		/// </summary>
		public IEnumerable<MonsterData> GetByType(MonsterType type)
		{
			return _monsters.Values.Where(m => m.Type == type);
		}

		/// <summary>
		/// Gets all boss monsters.
		/// </summary>
		public IEnumerable<MonsterData> GetBosses()
		{
			return _monsters.Values.Where(m => m.IsBoss);
		}

		/// <summary>
		/// Gets all monsters.
		/// </summary>
		public IEnumerable<MonsterData> GetAll()
		{
			return _monsters.Values;
		}

		/// <summary>
		/// Checks if a monster exists.
		/// </summary>
		public bool Exists(int id)
		{
			return _monsters.ContainsKey(id);
		}
	}

	/// <summary>
	/// Root structure of monsters.json file.
	/// </summary>
	internal class MonsterDbFile
	{
		public List<MonsterData> Monsters { get; set; } = new();
	}
}
