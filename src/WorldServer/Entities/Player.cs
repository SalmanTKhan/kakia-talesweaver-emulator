using Kakia.TW.Shared.Network;
using Kakia.TW.Shared.World;
using Kakia.TW.World;
using Kakia.TW.World.Entities;
using Kakia.TW.World.Entities.Components;
using Kakia.TW.World.Managers;
using Kakia.TW.World.Network;
using Yggdrasil.Geometry.Shapes;

namespace Kakia.TW.World.Entities
{
	public class Player : Entity
	{
		public WorldConnection Connection { get; }
		public WorldCharacter Data { get; }
		public MovementController Movement { get; }

		public Player(WorldConnection conn, WorldCharacter data)
		{
			Connection = conn;
			Data = data;
			Position = new Position(data.X, data.Y);
			Direction = data.Direction;
			Movement = new MovementController(this);
		}

		public override void Update(TimeSpan elapsed)
		{
			// Regen HP/MP, handle buffs
		}

		public void Warp(ushort mapId, ushort zoneId, ushort x, ushort y)
		{
			// 1. Remove from current map
			Instance?.Leave(this);

			// 2. Get new map
			var newMap = WorldServer.Instance.World.Maps.GetMap(mapId, zoneId);
			if (newMap == null) return;

			// 3. Teleport visual effect before entering new map
			Send.CharEffect(this.Connection, this.ObjectId, CharEffect.TeleportEffect2); // TeleportEffect2

			// 4. Update position data
			Data.MapId = mapId;
			Data.ZoneId = zoneId;
			Data.X = x;
			Data.Y = y;
			Position = new Position(x, y);

			// 5. Enter new map (sends MapChange + spawns all map entities)
			newMap.Enter(this);

			// 6. Spawn the user (0x33 subtype 0x00)
			Send.SpawnUser(this.Connection, this.ObjectId, this.Data, isSelf: true);

			Send.StatUpdate(this.Connection, this.Data);
			Send.InitSkills(this.Connection);

			// 7. Send InitObjectId (0x33 subtype 0x01) using map-assigned ObjectId
			Send.InitObjectId(this.Connection, this.ObjectId);
			Send.CurrentTime(this.Connection);
			Send.EnvironmentalMana(this.Connection);

			// 8. Finished loading
			Send.LoadCompleteAck(this.Connection);

			// 9. Save location to DB
			WorldServer.Instance.Database.SaveCharacterPosition(this.Data);
		}

		public void Save()
		{
			WorldServer.Instance.Database.SaveCharacterFull(this.Data);
		}
	}
}
