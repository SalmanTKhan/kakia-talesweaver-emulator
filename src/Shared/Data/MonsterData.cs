using Kakia.TW.Shared.World;

namespace Kakia.TW.Shared.Data
{
	/// <summary>
	/// Monster AI behavior type.
	/// </summary>
	public enum AiBehavior
	{
		Passive = 0,    // Doesn't attack unless provoked
		Aggressive = 1, // Attacks on sight
		Defensive = 2,  // Attacks when nearby allies are attacked
		Boss = 3        // Special boss behavior
	}

	/// <summary>
	/// Monster type classification.
	/// </summary>
	public enum MonsterType
	{
		Normal = 0,
		Elite = 1,
		Boss = 2,
		WorldBoss = 3
	}

	/// <summary>
	/// A location where a monster can spawn.
	/// </summary>
	public class SpawnLocation
	{
		public int MapId { get; set; }
		public int ZoneId { get; set; }
		public int X { get; set; }
		public int Y { get; set; }
		public int Radius { get; set; } = 0; // Random spawn radius
	}

	/// <summary>
	/// An entry in a monster's drop table.
	/// </summary>
	public class DropEntry
	{
		public int ItemId { get; set; }
		public float DropRate { get; set; }  // 0.0 - 100.0 (percentage)
		public int MinAmount { get; set; } = 1;
		public int MaxAmount { get; set; } = 1;
	}

	/// <summary>
	/// A skill that a monster can use.
	/// </summary>
	public class MonsterSkill
	{
		public int SkillId { get; set; }
		public int Level { get; set; } = 1;
		public float UseChance { get; set; } = 50.0f; // Chance to use when available
		public int CooldownMs { get; set; } = 5000;
	}

	/// <summary>
	/// Defines the static data for a monster type (loaded from database).
	/// </summary>
	public class MonsterData
	{
		// === Basic ===
		public int Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public int Level { get; set; } = 1;
		public int MaxHP { get; set; } = 100;
		public int MaxMP { get; set; } = 0;
		public int ModelId { get; set; }
		public MonsterType Type { get; set; } = MonsterType.Normal;

		// === Stats ===
		public int Attack { get; set; }
		public int Defense { get; set; }
		public int MagicAttack { get; set; }
		public int MagicDefense { get; set; }
		public int HitRate { get; set; } = 50;
		public int Evasion { get; set; } = 5;
		public Dictionary<StatType, int> Stats { get; set; } = new();

		// === Extended ===
		public List<DropEntry> DropTable { get; set; } = new();
		public int Experience { get; set; }
		public int Gold { get; set; }
		public int RespawnTimeMs { get; set; } = 30000; // 30 seconds default
		public int AggroRange { get; set; } = 0; // 0 = passive

		// === Movement ===
		public int WalkSpeed { get; set; } = 20;
		public int RunSpeed { get; set; } = 25;
		public int ChaseRange { get; set; } = 100; // How far to chase before giving up

		// === Full ===
		public List<MonsterSkill> Skills { get; set; } = new();
		public AiBehavior Behavior { get; set; } = AiBehavior.Passive;
		public List<SpawnLocation> SpawnLocations { get; set; } = new();

		// === Elemental ===
		public int FireResist { get; set; }
		public int WaterResist { get; set; }
		public int WindResist { get; set; }
		public int EarthResist { get; set; }
		public int LightResist { get; set; }
		public int DarkResist { get; set; }

		/// <summary>
		/// Returns true if this monster is aggressive.
		/// </summary>
		public bool IsAggressive => Behavior == AiBehavior.Aggressive || Behavior == AiBehavior.Boss;

		/// <summary>
		/// Returns true if this monster is a boss type.
		/// </summary>
		public bool IsBoss => Type == MonsterType.Boss || Type == MonsterType.WorldBoss;

		/// <summary>
		/// Gets the value of a specific stat.
		/// </summary>
		public int GetStat(StatType statType)
		{
			return Stats.TryGetValue(statType, out var value) ? value : 0;
		}

		/// <summary>
		/// Rolls the drop table and returns items that dropped.
		/// </summary>
		public List<(int ItemId, int Amount)> RollDrops(Random? rng = null)
		{
			rng ??= Random.Shared;
			var drops = new List<(int ItemId, int Amount)>();

			foreach (var entry in DropTable)
			{
				var roll = rng.NextDouble() * 100.0;
				if (roll <= entry.DropRate)
				{
					var amount = rng.Next(entry.MinAmount, entry.MaxAmount + 1);
					drops.Add((entry.ItemId, amount));
				}
			}

			return drops;
		}
	}
}
