using kakia_talesweaver_emulator.Network;
using kakia_talesweaver_network;
using kakia_talesweaver_packets;
using kakia_talesweaver_utils;
using kakia_talesweaver_utils.Extensions;

namespace kakia_talesweaver_emulator.PacketHandlers;

[PacketHandlerAttr(PacketType.TargetEntity)]
public class TargetEntityHandler : PacketHandler
{
	public override void HandlePacket(IPlayerClient client, RawPacket p)
	{
		using PacketReader reader = new(p.Data);
		reader.Skip(1); // Packet ID
		byte subId = reader.ReadByte();
		uint entityId = reader.ReadUInt32BE();

		using (PacketWriter writer = new())
		{
			writer.Write((byte)0x11);			
			writer.Write(entityId);
			writer.Write((byte)0x02);
			client.Send(writer.ToArray(), CancellationToken.None).Wait();
		}

		/* client packet??
		using (PacketWriter writer = new())
		{
			writer.Write((byte)0x59);
			writer.Write(subId);
			writer.Write(entityId);

			client.Send(writer.ToArray(), CancellationToken.None).Wait();
		}
		*/

		/*
		using (PacketWriter writer = new())
		{
			writer.Write((byte)0x4A);
			writer.Write(subId);
			writer.Write(entityId);
			writer.Write(client.GetCharacter().Id);
			writer.Write((byte)0);
			writer.Write((byte)0x2C);
			writer.Write((byte)0);

			client.Send(writer.ToArray(), CancellationToken.None).Wait();
		}

		using (PacketWriter writer = new())
		{
			writer.Write((byte)0x4D);
			writer.Write(subId);
			writer.Write((byte)1);
			writer.Write((byte)1);
			writer.Write(entityId);
			writer.Write((byte)0);
			writer.Write((byte)1);
			writer.Write((byte)0);
			writer.Write((byte)1);
			writer.Write((byte)1);
			writer.Write(new byte[13]);

			client.Send(writer.ToArray(), CancellationToken.None).Wait();
		}

		client.Send(@"14 01 00 00".ToByteArray(), CancellationToken.None).Wait();
		*/
	}
}
