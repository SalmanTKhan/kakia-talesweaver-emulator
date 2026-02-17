using Kakia.TW.Shared.Database;
using Kakia.TW.Shared.Database.MySQL;
using Kakia.TW.Shared.World;
using MySqlConnector;
using Yggdrasil.Logging;
using Yggdrasil.Util;

namespace Kakia.TW.Lobby.Database
{
	/// <summary>
	/// Lobby server database interface.
	/// </summary>
	public class LobbyDb : Db
	{
		/// <summary>
		/// Returns lightweight character summaries for Packet 0x50 (LoginResult).
		/// </summary>
		public List<CharacterSummary> GetCharacterSummaries(int accountId)
		{
			var list = new List<CharacterSummary>();
			using var conn = GetConnection();
			using var cmd = new MySqlCommand("SELECT name, level, slot FROM characters WHERE accountId = @id", conn);
			cmd.Parameters.AddWithValue("@id", accountId);

			using var reader = cmd.ExecuteReader();
			while (reader.Read())
			{
				list.Add(new CharacterSummary
				{
					Name = reader.GetString("name"),
					Level = reader.GetInt32("level"),
					ServerId = 1,
					CharacterCount = reader.GetByte("slot")
				});
			}
			return list;
		}

		/// <summary>
		/// Returns full character data for Packet 0x6B (Visual Selection).
		/// </summary>
		public List<WorldCharacter> GetCharacterList(int accountId)
		{
			var list = new List<WorldCharacter>();

			using var conn = GetConnection();
			string sql = @"SELECT characterId, name, char_type, appearance_data, hp, hp_max, mp, sp, map_id, x, y 
                           FROM characters 
                           WHERE accountId = @id";

			using var cmd = new MySqlCommand(sql, conn);
			cmd.Parameters.AddWithValue("@id", accountId);

			using var reader = cmd.ExecuteReader();
			while (reader.Read())
			{
				var c = new WorldCharacter();
				c.UserId = (uint)reader.GetInt32("characterId");
				c.Name = reader.GetString("name");

				// Basic Stats
				c.CurrentHP = (uint)reader.GetInt32("hp");
				c.MaxHP = (uint)reader.GetInt32("hp_max");
				c.CurrentMP = (uint)reader.GetInt32("mp");
				c.CurrentSP = (ulong)reader.GetInt32("sp");

				// Position (Default to 0/0 if null/missing or handle appropriately)
				// Assuming columns exist, otherwise defaults are fine for Lobby
				try
				{
					var x = (ushort)reader.GetInt16("x");
					var y = (ushort)reader.GetInt16("y");
					c.Position = new Position
					{
						X = x,
						Y = y
					};
				}
				catch { /* use defaults */ }

				// Visuals - char_type stores the full ModelId directly
				c.ModelId = (uint)reader.GetInt32("char_type");

				// Appearance (Blob handling)
				if (!reader.IsDBNull(reader.GetOrdinal("appearance_data")))
				{
					try
					{
						byte[] blob = (byte[])reader["appearance_data"];
						c.Appearance = DbSerializer.Deserialize<CharacterAppearance>(blob);
					}
					catch
					{
						// Fallback if binary serialization
						c.Appearance = new CharacterAppearance();
					}
				}
				else
				{
					c.Appearance = new CharacterAppearance();
				}

				// Equipment
				// In a full implementation, we would JOIN the items table here to get equipped items.
				// For now, list remains empty (visuals only via Appearance usually for simple setups, 
				// or Appearance block handles the 'look' of items).

				list.Add(c);
			}

			return list;
		}

		public bool CreateCharacter(int accountId, string name, int charType, CharacterAppearance appearance)
		{
			try
			{
				using var conn = GetConnection();

				// 1. Get next slot
				int slot = 0;
				using (var slotCmd = new MySqlCommand("SELECT COALESCE(MAX(slot) + 1, 0) FROM characters WHERE accountId = @acc", conn))
				{
					slotCmd.Parameters.AddWithValue("@acc", accountId);
					slot = Convert.ToInt32(slotCmd.ExecuteScalar());
				}

				// 2. Insert (Default spawn: Narvik starter area - map 6, zone 38656, pos 305,220)
				string sql = @"INSERT INTO characters
                               (accountId, slot, name, char_type, appearance_data, hp, hp_max, mp, mp_max, sp, sp_max, level, map_id, zone_id, x, y)
                               VALUES
                               (@acc, @slot, @name, @type, @app, 100, 100, 50, 50, 3000, 3000, 1, 6, 38656, 305, 220)";

				using (var cmd = new MySqlCommand(sql, conn))
				{
					cmd.Parameters.AddWithValue("@acc", accountId);
					cmd.Parameters.AddWithValue("@slot", slot);
					cmd.Parameters.AddWithValue("@name", name);
					cmd.Parameters.AddWithValue("@type", charType);
					cmd.Parameters.AddWithValue("@app", DbSerializer.Serialize(appearance));

					cmd.ExecuteNonQuery();
				}

				return true;
			}
			catch (Exception ex)
			{
				Log.Error($"Failed to create character '{name}': {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Checks if a character name is available (not taken).
		/// </summary>
		/// <param name="name">The name to check.</param>
		/// <returns>True if available, False if taken.</returns>
		public bool CheckNameAvailability(string name)
		{
			using var conn = GetConnection();
			using var cmd = new MySqlCommand("SELECT COUNT(*) FROM characters WHERE name = @name", conn);
			cmd.Parameters.AddWithValue("@name", name);

			long count = Convert.ToInt64(cmd.ExecuteScalar());
			return count == 0;
		}

		/// <summary>
		/// Stores the selected character name for world entry handover.
		/// </summary>
		public void SetSelectedCharacter(int accountId, string characterName)
		{
			using var conn = GetConnection();
			using var cmd = new MySqlCommand("UPDATE accounts SET selected_character = @char WHERE accountId = @id", conn);
			cmd.Parameters.AddWithValue("@char", characterName);
			cmd.Parameters.AddWithValue("@id", accountId);
			cmd.ExecuteNonQuery();
		}
	}
}
