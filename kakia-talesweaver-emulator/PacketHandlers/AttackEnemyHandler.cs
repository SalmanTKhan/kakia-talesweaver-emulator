using kakia_talesweaver_emulator.Network;
using kakia_talesweaver_network;
using kakia_talesweaver_packets;
using kakia_talesweaver_utils;
using kakia_talesweaver_utils.Extensions;

namespace kakia_talesweaver_emulator.PacketHandlers;

[PacketHandlerAttr(PacketType.Attack)]
public class AttackEnemyHandler : PacketHandler
{
	public override void HandlePacket(IPlayerClient client, RawPacket p)
	{
		using var pr = p.GetReader();
		pr.Skip(6);
		uint id = pr.ReadUInt32BE();

		using PacketWriter pw = new();
		pw.Write((byte)0x3F);
		pw.Write(id);
		pw.Write(@"3F 00 2D D4 9E 00 01 2D 0C 84 72 01 00 2D D4 9E 
03 E0 02 B4 25 4C 32 50 00 00 00 00 00 00 00 64 
00".ToByteArray());

		client.Send(pw.ToArray(), CancellationToken.None).Wait();


		//client.Send("0C 28 FF C5 6E 03 C7 03 02".ToByteArray(), CancellationToken.None).Wait();

		/*
		client.Send("4A 00 00 00 00 00 00".ToByteArray(), CancellationToken.None).Wait();
		client.Send("17 00".ToByteArray(), CancellationToken.None).Wait();

		using PacketWriter pw = new();
		pw.Write((byte)0x48);
		pw.Write((byte)0);
		pw.Write(client.GetCharacter().Id);
		pw.Write((byte)0xFF);

		client.Send(pw.ToArray(), CancellationToken.None).Wait();
		client.Send("6C 03 E7".ToByteArray(), CancellationToken.None).Wait();
		*/
	}
}
