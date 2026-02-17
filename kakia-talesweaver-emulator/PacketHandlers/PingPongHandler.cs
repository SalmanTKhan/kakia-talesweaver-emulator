using kakia_talesweaver_emulator.Network;
using kakia_talesweaver_network;
using kakia_talesweaver_packets;
using kakia_talesweaver_utils;
using System.Net.NetworkInformation;

namespace kakia_talesweaver_emulator.PacketHandlers;

[PacketHandlerAttr(PacketType.Ping)]
public class PingPongHandler : PacketHandler
{
	public override void HandlePacket(IPlayerClient client, RawPacket p)
	{
		// This is a response to a ping packet from the server.
		// We don't need to do anything here for now.

		// This is how to read the packet if needed:
		using var pr = p.GetReader();
		DateTime serverTime = DateTime.UnixEpoch.AddSeconds(pr.ReadUInt32BE());
		DateTime clientTime = DateTime.UnixEpoch.AddSeconds(pr.ReadUInt32BE());
	}
}
