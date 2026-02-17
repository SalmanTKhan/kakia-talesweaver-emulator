using Kakia.TW.Shared.World;
using Kakia.TW.World.Network;
using Yggdrasil.Logging;

namespace Kakia.TW.World.Entities.Components
{
	public class MovementController
	{
		private readonly Player _player;

		public bool IsMoving { get; private set; }
		public Position Destination { get; private set; }

		public MovementController(Player player)
		{
			_player = player;
		}

		/// <summary>
		/// flag 0x00 (InitialRequest): record destination + broadcast move.
		/// Does NOT check portal — player hasn't walked there yet.
		/// </summary>
		public void StartMoving(byte moveType, ushort destX, ushort destY, byte direction)
		{
			var prevX = _player.Position.X;
			var prevY = _player.Position.Y;

			IsMoving = true;
			Destination = new Position(destX, destY);
			_player.Position = new Position(destX, destY);
			_player.Direction = (Direction)direction;
			_player.Data.X = destX;
			_player.Data.Y = destY;
			_player.Data.Direction = (Direction)direction;

			if (_player.Instance != null)
				Send.MoveObject(_player.Instance, _player.ObjectId, moveType,
								prevX, prevY, destX, destY, direction);
		}

		/// <summary>
		/// flag 0x01 (Continuation): step update while walking — checks portal.
		/// </summary>
		public void UpdatePosition(ushort x, ushort y)
		{
			_player.Position = new Position(x, y);
			_player.Data.X = x;
			_player.Data.Y = y;
			CheckPortalCollision();
		}

		/// <summary>
		/// flag 0x02 or implicit stop — checks portal.
		/// </summary>
		public void StopMoving(ushort x, ushort y)
		{
			IsMoving = false;
			_player.Position = new Position(x, y);
			_player.Data.X = x;
			_player.Data.Y = y;
			CheckPortalCollision();
		}

		private void CheckPortalCollision()
		{
			if (_player.Instance == null) return;

			var portal = _player.Instance.FindPortalAt(_player.Position.X, _player.Position.Y);
			if (portal != null)
			{
				IsMoving = false;
				Log.Info($"Player {_player.Data.Name} touched portal -> Map {portal.DestMapId}-{portal.DestZoneId}");
				_player.Warp(portal.DestMapId, portal.DestZoneId, portal.DestX, portal.DestY);
			}
		}
	}
}
