using Kakia.TW.Shared.World;

namespace Kakia.TW.Shared.Data
{
	/// <summary>
	/// Item type classification.
	/// </summary>
	public enum ItemType
	{
		Weapon = 0,
		Armor = 1,
		Accessory = 2,
		Consumable = 3,
		Material = 4,
		Quest = 5,
		Etc = 6
	}

	/// <summary>
	/// Equipment slot positions.
	/// </summary>
	public enum EquipSlot
	{
		None = -1,
		Weapon = 0,
		Shield = 1,
		Head = 2,
		Body = 3,
		Gloves = 4,
		Boots = 5,
		Accessory1 = 6,
		Accessory2 = 7,
		Back = 8,
		Face = 9
	}

	/// <summary>
	/// Character class restriction flags.
	/// </summary>
	[Flags]
	public enum CharacterClass
	{
		None = 0,
		Boris = 1 << 0,
		Mila = 1 << 1,
		Tia = 1 << 2,
		Maximin = 1 << 3,
		Sivelin = 1 << 4,
		Ispin = 1 << 5,
		Nayatrei = 1 << 6,
		Cloe = 1 << 7,
		Josua = 1 << 8,
		Lanziee = 1 << 9,
		Lucian = 1 << 10,
		All = 0x7FF
	}

	/// <summary>
	/// Weapon type classification for damage calculations.
	/// </summary>
	public enum WeaponCategory
	{
		None = 0,
		Sword = 1,      // Stab-based
		Katana = 2,     // Hack-based
		Staff = 3,      // Int-based
		Knuckle = 4,
		Spear = 5,
		Bow = 6,
		Gun = 7
	}

	/// <summary>
	/// Represents a source that drops an item.
	/// </summary>
	public class DropSource
	{
		public int MonsterId { get; set; }
		public float Rate { get; set; }
	}

	/// <summary>
	/// Crafting recipe for an item.
	/// </summary>
	public class CraftingRecipe
	{
		public int NpcId { get; set; }
		public int Gold { get; set; }
		public List<CraftingMaterial> Materials { get; set; } = new();
	}

	/// <summary>
	/// A material required for crafting.
	/// </summary>
	public class CraftingMaterial
	{
		public int ItemId { get; set; }
		public int Amount { get; set; }
	}

	/// <summary>
	/// Defines the static data for an item type (loaded from database).
	/// </summary>
	public class ItemData
	{
		// === Basic ===
		public int Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public ItemType Type { get; set; }
		public EquipSlot Slot { get; set; } = EquipSlot.None;
		public int Price { get; set; }
		public int MaxStack { get; set; } = 1;

		// === Stats (for equipment) ===
		public int MinDamage { get; set; }
		public int MaxDamage { get; set; }
		public int Defense { get; set; }
		public int MagicDefense { get; set; }
		public WeaponCategory WeaponType { get; set; } = WeaponCategory.None;
		public Dictionary<StatType, int> BonusStats { get; set; } = new();

		// === Extended ===
		public string Description { get; set; } = string.Empty;
		public int LevelRequirement { get; set; }
		public CharacterClass ClassRestriction { get; set; } = CharacterClass.All;
		public int VisualModelId { get; set; }
		public int Durability { get; set; } = 100;

		// === Full ===
		public List<DropSource> DropSources { get; set; } = new();
		public int? QuestId { get; set; }
		public CraftingRecipe? Recipe { get; set; }

		/// <summary>
		/// Returns true if this item is equippable.
		/// </summary>
		public bool IsEquipment => Type == ItemType.Weapon || Type == ItemType.Armor || Type == ItemType.Accessory;

		/// <summary>
		/// Returns true if this item can be stacked.
		/// </summary>
		public bool IsStackable => MaxStack > 1;

		/// <summary>
		/// Gets the total bonus for a specific stat type.
		/// </summary>
		public int GetStatBonus(StatType statType)
		{
			return BonusStats.TryGetValue(statType, out var bonus) ? bonus : 0;
		}
	}
}
