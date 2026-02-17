using System.Text.Json;
using System.Text.Json.Serialization;
using Yggdrasil.Logging;

namespace Kakia.TW.Shared.Data
{
	/// <summary>
	/// Database of item definitions loaded from JSON.
	/// </summary>
	public class ItemDb
	{
		private readonly Dictionary<int, ItemData> _items = new();
		private readonly Dictionary<string, int> _nameIndex = new(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// Gets the number of items in the database.
		/// </summary>
		public int Count => _items.Count;

		/// <summary>
		/// Loads item data from a JSON file.
		/// </summary>
		/// <param name="path">Path to items.json file.</param>
		public void Load(string path)
		{
			if (!File.Exists(path))
			{
				Log.Warning($"ItemDb: File not found: {path}");
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

				var data = JsonSerializer.Deserialize<ItemDbFile>(json, options);
				if (data?.Items == null)
				{
					Log.Warning("ItemDb: No items found in file.");
					return;
				}

				foreach (var item in data.Items)
				{
					if (_items.TryAdd(item.Id, item))
					{
						_nameIndex[item.Name] = item.Id;
					}
					else
					{
						Log.Warning($"ItemDb: Duplicate item ID {item.Id} ({item.Name})");
					}
				}

				Log.Info($"ItemDb: Loaded {_items.Count} items.");
			}
			catch (Exception ex)
			{
				Log.Error($"ItemDb: Failed to load {path}: {ex.Message}");
			}
		}

		/// <summary>
		/// Gets an item by its ID.
		/// </summary>
		public ItemData? GetById(int id)
		{
			return _items.TryGetValue(id, out var item) ? item : null;
		}

		/// <summary>
		/// Gets an item by its name (case-insensitive).
		/// </summary>
		public ItemData? GetByName(string name)
		{
			if (_nameIndex.TryGetValue(name, out var id))
				return GetById(id);
			return null;
		}

		/// <summary>
		/// Gets all items of a specific type.
		/// </summary>
		public IEnumerable<ItemData> GetByType(ItemType type)
		{
			return _items.Values.Where(i => i.Type == type);
		}

		/// <summary>
		/// Gets all items that can be equipped in a specific slot.
		/// </summary>
		public IEnumerable<ItemData> GetBySlot(EquipSlot slot)
		{
			return _items.Values.Where(i => i.Slot == slot);
		}

		/// <summary>
		/// Searches for items by name (partial match, case-insensitive).
		/// </summary>
		public IEnumerable<ItemData> Search(string query, int maxResults = 20)
		{
			return _items.Values
				.Where(i => i.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
				.Take(maxResults);
		}

		/// <summary>
		/// Gets all items that are dropped by a specific monster.
		/// </summary>
		public IEnumerable<ItemData> GetDropsFromMonster(int monsterId)
		{
			return _items.Values
				.Where(i => i.DropSources.Any(d => d.MonsterId == monsterId));
		}

		/// <summary>
		/// Gets all items.
		/// </summary>
		public IEnumerable<ItemData> GetAll()
		{
			return _items.Values;
		}

		/// <summary>
		/// Checks if an item exists.
		/// </summary>
		public bool Exists(int id)
		{
			return _items.ContainsKey(id);
		}
	}

	/// <summary>
	/// Root structure of items.json file.
	/// </summary>
	internal class ItemDbFile
	{
		public List<ItemData> Items { get; set; } = new();
	}
}
