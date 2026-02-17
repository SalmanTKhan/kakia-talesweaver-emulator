using Kakia.TW.Shared.Network;
using Kakia.TW.World.Entities;
using Kakia.TW.World.Scripting;

namespace Kakia.TW.World.Network
{
	/// <summary>
	/// Represents a connection to a client.
	/// </summary>
	public class WorldConnection : Connection
	{
		public Player Player { get; set; }

		/// <summary>
		/// Current Dialog
		/// </summary>
		public Dialog? CurrentDialog { get; set; }

		/// <summary>
		/// Called when a packet was send by the client.
		/// </summary>
		/// <param name="packet"></param>
		protected override void OnPacketReceived(Packet packet)
		{
			WorldServer.Instance.PacketHandler.Handle(this, packet);
		}
	}
}
