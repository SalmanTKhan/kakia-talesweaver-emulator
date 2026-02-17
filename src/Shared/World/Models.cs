namespace Kakia.TW.Shared.World
{
	/// <summary>
	/// Stat types for equipment bonuses and item stats.
	/// </summary>
	public enum StatType : short
	{
		Stab = 1,           // 突き - Thrust/Stab
		Hack = 2,           // 斬り - Slash/Hack
		Int = 3,            // 魔法攻撃 - Magic Attack
		Def = 4,            // 物理防御 - Physical Defense
		MR = 5,             // 魔法防御 - Magic Defense
		Dex = 6,            // 命中補正 - Hit Correction
		Agi = 7,            // 敏捷補正 - Agility Correction
		Evasion = 8,
	}

	/// <summary>
	/// Simple 2D position with X, Y coordinates.
	/// </summary>
	public struct Position
	{
		public ushort X;
		public ushort Y;

		public Position(ushort x, ushort y)
		{
			X = x;
			Y = y;
		}

		// Arithmetic operators
		public static Position operator +(Position a, Position b) => new((ushort)(a.X + b.X), (ushort)(a.Y + b.Y));
		public static Position operator -(Position a, Position b) => new((ushort)(a.X - b.X), (ushort)(a.Y - b.Y));

		// Distance calculations
		public readonly int DistanceSquared(Position other)
		{
			int dx = X - other.X;
			int dy = Y - other.Y;
			return dx * dx + dy * dy;
		}

		public readonly double Distance(Position other) => Math.Sqrt(DistanceSquared(other));

		public readonly int ManhattanDistance(Position other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);

		// Linear interpolation
		public readonly Position Lerp(Position target, float t)
		{
			return new Position(
				(ushort)(X + (target.X - X) * t),
				(ushort)(Y + (target.Y - Y) * t)
			);
		}

		// Direction between two positions (8-direction)
		public readonly Direction DirectionTo(Position target)
		{
			int dx = target.X - X;
			int dy = target.Y - Y;

			// Handle zero movement
			if (dx == 0 && dy == 0) return Direction.South;

			// Calculate angle and map to 8 directions
			double angle = Math.Atan2(dy, dx) * (180.0 / Math.PI);
			angle = (angle + 360) % 360; // Normalize to 0-360

			// Map angle to 8 directions (each direction covers 45 degrees)
			return angle switch
			{
				< 22.5 or >= 337.5 => Direction.East,
				< 67.5 => Direction.SouthEast,
				< 112.5 => Direction.South,
				< 157.5 => Direction.SouthWest,
				< 202.5 => Direction.West,
				< 247.5 => Direction.NorthWest,
				< 292.5 => Direction.North,
				_ => Direction.NorthEast
			};
		}

		// In range check
		public readonly bool InRange(Position other, int range) => DistanceSquared(other) <= range * range;

		public override readonly string ToString() => $"({X}, {Y})";
	}

	/// <summary>
	/// TalesWeaver 8-direction facing.
	/// </summary>
	public enum Direction : byte
	{
		North = 0,
		NorthEast = 1,
		East = 2,
		SouthEast = 3,
		South = 4,
		SouthWest = 5,
		West = 6,
		NorthWest = 7
	}

	public class CharacterSummary
	{
		public byte ServerId { get; set; }
		public string Name { get; set; } = string.Empty;
		public int Level { get; set; } // Unk1 in old code?
		public int Time1 { get; set; } = 0x661da586;
		public int Time2 { get; set; } = 0x66d38b6c;
		public byte CharacterCount { get; set; } // Usually implies slot
	}

	public class GameItem
	{
		public int ItemId { get; set; }
		public short Amount { get; set; }
		public short Durability { get; set; }
		public int Slot { get; set; } = -1; // 0-21 for equipment
		public uint VisualId { get; set; }

		// Equipment specific
		public byte Refine { get; set; }
		public byte DataFlags { get; set; }
		public List<ItemStat> Stats { get; set; } = new();

		// Advanced Magic properties (Legacy ItemMagicProperty)
		public List<ItemMagicProperty> MagicProperties { get; set; } = new();

		public bool IsEquipment => ItemId > 2000;

		/// <summary>
		/// Gets the bonus value for a specific stat type from this item.
		/// </summary>
		public int GetStatBonus(StatType statType)
		{
			return Stats
				.Where(s => s.Type == (short)statType)
				.Sum(s => s.Value);
		}
	}

	public struct ItemStat
	{
		public short Type;
		public int Value;
		public ItemStat(short t, int v) { Type = t; Value = v; }
		public ItemStat(StatType type, int value) { Type = (short)type; Value = value; }
	}

	public class ItemMagicProperty
	{
		public byte PropertyId { get; set; }
		public byte PropertyType { get; set; }
		public uint Value1 { get; set; }
		public uint Value2 { get; set; } // Union-like usage in legacy
		public ushort[] StatValues { get; set; } = Array.Empty<ushort>();
		public uint[] StatParams { get; set; } = Array.Empty<uint>();
		// Simplification: Keeping raw data structures for complex skill props if needed
	}

	public class GuildTeamInfo
	{
		public bool HasGuildInfo { get; set; }
		public string GuildName { get; set; } = string.Empty;
		public uint GuildId { get; set; }
		public string TeamName { get; set; } = string.Empty;
		public ushort MarkId { get; set; } // UnknownShort1
		public ushort MarkColor { get; set; } // UnknownShort2
	}

	public class CharacterAppearance
	{
		// 9 Slots for visuals
		public uint[] VisualIds { get; set; } = new uint[9];
		// In legacy, VisualId2 existed, usually 0
		public uint[] VisualIds2 { get; set; } = new uint[9];
		public byte[] Colors { get; set; } = new byte[9];
		public byte[] Types { get; set; } = new byte[9]; // 1 = Visual, 0 = Color

		// 4 Properties (Rank, etc)
		public uint[] Props { get; set; } = new uint[4];
		public byte[] PropTypes { get; set; } = new byte[4]; // 1 = Int, 0 = Byte
	}

	public class WorldCharacter
	{
		public uint UserId { get; set; }
		public string Name { get; set; } = string.Empty;

		// Position - flat fields for DB storage
		public ushort MapId { get; set; }
		public ushort ZoneId { get; set; }
		public ushort X { get; set; }
		public ushort Y { get; set; }
		public Direction Direction { get; set; }

		/// <summary>
		/// Computed Position from X/Y for convenience. Setting this updates X and Y.
		/// </summary>
		public Position Position
		{
			get => new(X, Y);
			set { X = value.X; Y = value.Y; }
		}

		public bool IsGM { get; set; } = true;
		public uint LastLoginTime { get; set; }
		public uint CreationTime { get; set; }

		// Progression
		public int Level { get; set; } = 1;
		public long CurrentExp { get; set; } = 205;
		public long NextExp { get; set; } = 100;
		public long LimitExp { get; set; } = 250;

		// Vitals
		public uint CurrentHP { get; set; } = 300;
		public uint MaxHP { get; set; } = 300;
		public uint CurrentMP { get; set; } = 75;
		public uint MaxMP { get; set; } = 75;
		public ulong CurrentSP { get; set; } = 1300;
		public ulong MaxSP { get; set; } = 1300;

		// ===== Base Stats (Character's own stats) =====
		public short StatStab { get; set; } = 2;
		public short StatHack { get; set; } = 4;
		public short StatInt { get; set; } = 1;
		public short StatDef { get; set; } = 3;
		public short StatMR { get; set; } = 3;
		public short StatDex { get; set; } = 3;
		public short StatAgi { get; set; } = 3;

		// Available stat points to distribute
		public int StatPoints { get; set; } = 9999;

		// Rune progression
		public short RuneLevel { get; set; } = 1;
		public int RuneExp { get; set; } = 5000;
		public int RuneMaxExp { get; set; } = 10000;

		// ===== Equipment Bonus Stats (Computed from equipped items) =====
		/// <summary>
		/// Calculates the total bonus for a stat type from all equipped items.
		/// </summary>
		private short GetEquipmentBonus(StatType statType)
		{
			if (Equipment == null || Equipment.Count == 0)
				return 0;

			return (short)Equipment.Sum(item => item.GetStatBonus(statType));
		}

		/// <summary>STAB bonus from equipment (突き)</summary>
		public short EquipStabBonus => GetEquipmentBonus(StatType.Stab);

		/// <summary>HACK bonus from equipment (斬り)</summary>
		public short EquipHackBonus => GetEquipmentBonus(StatType.Hack);

		/// <summary>INT bonus from equipment (魔法攻撃)</summary>
		public short EquipIntBonus => GetEquipmentBonus(StatType.Int);

		/// <summary>DEF bonus from equipment (物理防御)</summary>
		public short EquipDefBonus => GetEquipmentBonus(StatType.Def);

		/// <summary>MR bonus from equipment (魔法防御)</summary>
		public short EquipMRBonus => GetEquipmentBonus(StatType.MR);

		/// <summary>DEX bonus from equipment (命中補正)</summary>
		public short EquipDexBonus => GetEquipmentBonus(StatType.Dex);

		/// <summary>AGI bonus from equipment (敏捷補正)</summary>
		public short EquipAgiBonus => GetEquipmentBonus(StatType.Agi);

		/// <summary>Evasion bonus from equipment (回避補正)</summary>
		public short EquipEvasionBonus => GetEquipmentBonus(StatType.Evasion);

		// ===== Total Stats (Base + Equipment) =====
		public int TotalStab => StatStab + EquipStabBonus;
		public int TotalHack => StatHack + EquipHackBonus;
		public int TotalInt => StatInt + EquipIntBonus;
		public int TotalDef => StatDef + EquipDefBonus;
		public int TotalMR => StatMR + EquipMRBonus;
		public int TotalDex => StatDex + EquipDexBonus;
		public int TotalAgi => StatAgi + EquipAgiBonus;

		// Movement (calculated from AGI)
		public byte WalkSpeed => StatsCalculator.IsInitialized
			? StatsCalculator.CalculateWalkSpeed(this)
			: (byte)22;
		public byte RunSpeed => StatsCalculator.IsInitialized
			? StatsCalculator.CalculateRunSpeed(this)
			: (byte)27;

		// Calculated Combat Stats (computed from base stats + equipment)
		public int MinAttack => StatsCalculator.IsInitialized
			? StatsCalculator.CalculateMinPhysicalAttack(this)
			: 1;
		public int MaxAttack => StatsCalculator.IsInitialized
			? StatsCalculator.CalculateMaxPhysicalAttack(this)
			: 1;
		public int MinAttack2 => StatsCalculator.IsInitialized
			? StatsCalculator.CalculateMinPhysicalAttack(this, WeaponType.Katana)
			: 1;
		public int MaxAttack2 => StatsCalculator.IsInitialized
			? StatsCalculator.CalculateMaxPhysicalAttack(this, WeaponType.Katana)
			: 1;
		public int MagicAttack => StatsCalculator.IsInitialized
			? StatsCalculator.CalculateMagicAttack(this)
			: 1;
		public int PhysicalDefense => StatsCalculator.IsInitialized
			? StatsCalculator.CalculatePhysicalDefense(this)
			: 0;
		public int MagicalDefense => StatsCalculator.IsInitialized
			? StatsCalculator.CalculateMagicalDefense(this)
			: 0;
		public byte HitRate => StatsCalculator.IsInitialized
			? StatsCalculator.CalculateHitRate(this)
			: (byte)50;
		public byte PhysicalEvasion => StatsCalculator.IsInitialized
			? StatsCalculator.CalculatePhysicalEvasion(this)
			: (byte)5;
		public byte MagicalEvasion => StatsCalculator.IsInitialized
			? StatsCalculator.CalculateMagicalEvasion(this)
			: (byte)5;

		// Attributes (Elemental resistances/bonuses)
		public short NoneAttribute { get; set; } = 5;
		public short FireAttribute { get; set; } = 5;
		public short WaterAttribute { get; set; } = 5;
		public short WindAttribute { get; set; } = 5;
		public short EarthAttribute { get; set; } = 10;
		public short LightningAttribute { get; set; } = 5;
		public short HolyAttribute { get; set; } = 5;
		public short DarkAttribute { get; set; } = 10;

		// Visuals
		public uint ModelId { get; set; }
		public ushort Outfit { get; set; }
		public uint TitleId { get; set; }

		public GuildTeamInfo GuildInfo { get; set; } = new();
		public List<GameItem> Equipment { get; set; } = new();
		public CharacterAppearance Appearance { get; set; } = new();
	}
}
