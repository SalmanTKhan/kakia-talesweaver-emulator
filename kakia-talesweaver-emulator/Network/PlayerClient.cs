using Kakia.TW.Shared.Network;
using kakia_talesweaver_emulator.DB;
using kakia_talesweaver_emulator.Models;
using kakia_talesweaver_emulator.PacketHandlers;
using kakia_talesweaver_logging;
using kakia_talesweaver_network;
using kakia_talesweaver_packets;
using kakia_talesweaver_packets.Models;
using kakia_talesweaver_packets.Packets;
using kakia_talesweaver_utils;
using kakia_talesweaver_utils.Extensions;
using System;
using System.Net.Sockets;
using System.Reflection.Metadata.Ecma335;
using static System.Collections.Specialized.BitVector32;

namespace kakia_talesweaver_emulator.Network;

public class PlayerClient : IPlayerClient
{
	public TalesServer _server;
	private SocketClient _socketClient;
	private ServerType _serverType;
	private bool _cryptoSet = false;

	private uint _sessionSeed = 0;

	public string AccountId = string.Empty;
	public MapId CurrentMap = new();

	public Character CurrentCharacter = new();
	public PlayerCharacter Character = new();

	public PlayerClient(SocketClient socketClient, ServerType serverType, TalesServer server)
	{
		_socketClient = socketClient;
		_socketClient.PacketReceived += this.PacketRecieved;
		_serverType = serverType;
		_server = server;

		Logger.Log($"Player connected (to server: {serverType}): {_socketClient.GetIP()}", LogLevel.Information);

		_ = Send(new ConnectedPacket().ToBytes(), CancellationToken.None);
		_ = _socketClient.BeginRead();

		if (serverType == ServerType.Login)
		{
			_socketClient.SetCrypto();
			_ = Send(TalesServer.ServerList.ToBytes(), CancellationToken.None);
		}
		else if (serverType == ServerType.World)
		{
			SendMetaData();
		}
	}

	public ServerType GetServerType()
	{
		return _serverType;
	}

	public async Task PacketRecieved(RawPacket packet)
	{
		// --- ADDED LOGGING HERE ---
		PaleLogger.Log(true, packet.Data);
		// Log every single packet unconditionally before processing
		// Logger.Log($"[RECV RAW] ID: 0x{packet.PacketId:X2} | Len: {packet.Data.Length}{Environment.NewLine}{packet.Data.ToFormatedHexString()}", LogLevel.Debug);
		// --------------------------

		if (!_cryptoSet && _serverType == ServerType.Login && packet.Data.Length == 5)
		{
			Logger.Log("Crypto Handshake ACK received.", LogLevel.Debug);
			_cryptoSet = true;
			return;
		}

		PacketHandler handler = PacketHandlers.PacketHandlers.GetHandlerFor((PacketType)packet.PacketId);
		if (handler != null)
		{
			try
			{
				// Redundant log removed/cleaned up to avoid double spam, or kept if you want specific handler info
				Logger.Log($"Handling packetType [{packet.PacketId}] via {handler.GetType().Name}", LogLevel.Debug);

				handler.HandlePacket(this, packet);
				return;
			}
			catch (Exception e)
			{
				Logger.Log(e);
			}
		}
		else
		{
			Logger.Log($"NOT IMPLEMENTED Packet ID: [{packet.PacketId}]", LogLevel.Warning);
			Logger.Log($"[RECV] ID: 0x{packet.PacketId:X2} | Len: {packet.Data.Length}{Environment.NewLine}{packet.Data.ToFormatedHexString()}", LogLevel.Debug);
			// Logger.LogPck(packet.Data); // Redundant thanks to the [RECV RAW] log above
		}
	}

	public async Task<bool> Send(byte[] packet, CancellationToken token)
	{
		PaleLogger.Log(true, packet);
		// --- ADDED LOGGING HERE ---
		ushort pType = 0;
		if (packet.Length >= 2)
			pType = BitConverter.ToUInt16(packet, 0);

		Logger.Log($"[SEND RAW] Type: {((PacketType)pType)} (0x{pType:X4}) | Len: {packet.Length}{Environment.NewLine}{packet.ToFormatedHexString()}", LogLevel.Debug);
		// --------------------------

		await _socketClient!.Send(packet);
		return true;
	}

	public void UpdateCryptoSeed(uint seed)
	{
		_sessionSeed = seed;
		_server.AccountSessions.TryGetValue(seed, out var session);
		if (session != null)
		{
			AccountId = session.AccountId;
			SetCharacter(session.Character?.Name);
			Logger.Log($"Account {AccountId} assigned to session {seed}", LogLevel.Information);
		}

		_socketClient!.SetCrypto(seed);
	}

	public SessionInfo? GetSessionInfo()
	{
		_server.AccountSessions.TryGetValue(_sessionSeed, out var session);
		return session;
	}

	public bool RemoveSessionInfo()
	{
		return _server.AccountSessions.TryRemove(_sessionSeed, out var _);
	}

	public void Broadcast(byte[] packet, bool includeSelf = true, bool sameMap = true)
	{
		_server.Broadcast(this, packet, includeSelf, sameMap, CancellationToken.None);
	}

	public PlayerCharacter GetCharacter()
	{
		return Character;
	}

	public void SetCharacter(string? name = null)
	{
		if (string.IsNullOrEmpty(name))
		{
			return;
		}

		CurrentCharacter = DB.JsonDB.GetCharacterList(AccountId).FirstOrDefault(m => m.Name.Equals(name))!;
		GetSessionInfo()?.Character = CurrentCharacter;
		Character.SpawnCharacterPacket = new SpawnCharacterPacket()
		{
			UserId = CurrentCharacter.Id,
			UserName = CurrentCharacter.Name,
			Position = CurrentCharacter.Position,
			ModelId = CurrentCharacter.ModelId,
			GM = (byte)(CurrentCharacter.Id == 2000050 ? 1 : 0),
			CurrentHealth = 180,
			MaxHealth = 180	
		};

		Character.Id = Character.SpawnCharacterPacket.UserId;
		Character.Name = CurrentCharacter.Name;

		/*
		string path = Path.Combine("Accounts", string.IsNullOrEmpty(name) ? AccountId : name);
		Character.SpawnCharacterPacket = SpawnCharacterPacket.FromBytes(
			File.ReadAllText($"{path}.txt")
			.ToByteArray());

		Character.Id = Character.SpawnCharacterPacket.UserId;
		Character.Name = Character.SpawnCharacterPacket.UserName;
		Character.Position = new TsPoint(Character.SpawnCharacterPacket.Movement.XPos, Character.SpawnCharacterPacket.Movement.YPos);
		*/
	}

	public TalesServer GetServer()
	{
		return _server;
	}

	public void LoadMap(MapInfo map, bool sendEffect, ObjectPos? spawnPos = null, CancellationToken ct = default)
	{
		if (sendEffect)
		{
			Broadcast(new SendCharEffectPacket()
			{
				ObjectId = Character.Id,
				Effect = CharEffect.TeleportEffect2
			}.ToBytes(), includeSelf: true);
			Task.Delay(1500, ct).Wait(ct);
		}

		
		if (CurrentMap.Id != 0 && CurrentMap.Zone != 0)
		{
			EntitySpawnPacket packet = new()
			{
				SubOpcode = ActionType.Despawn,
				SpawnKind = SpawnType.Player,
				RemovePayload = new EntitySpawnPacket.RemoveObjectPayload()
				{
					ObjectID = (int)Character.Id
				}
			};

			Broadcast(packet.ToBytes(), false);
		}
		

		CurrentMap = new MapId(map.MapId, map.ZoneId);

		//Send("15 00 05 50 00 00 00 04".ToByteArray(), ct).Wait(ct);
		Send(map.GetMapPacket().ToBytes(), ct).Wait(ct);

		if (spawnPos != null)
		{
			Character.Position = spawnPos.Position.Copy();
			Character.Direction = spawnPos.Direction;

			Character.SpawnCharacterPacket?.Position.Position = spawnPos.Position.Copy();
			Character.SpawnCharacterPacket?.Position.Direction = spawnPos.Direction;
		}
		else
		{
			Character.Position = map.SpawnPoints[0].Position.Copy();
			Character.Direction = map.SpawnPoints[0].Direction;

			Character.SpawnCharacterPacket?.Position.Position = map.SpawnPoints[0].Position.Copy();
			Character.SpawnCharacterPacket?.Position.Direction = map.SpawnPoints[0].Direction;
		}



		CurrentCharacter.MapId = CurrentMap.Id;
		CurrentCharacter.ZoneId = CurrentMap.Zone;
		JsonDB.SaveCharacter(AccountId, CurrentCharacter);


		Send(Character.SpawnCharacterPacket!.ToBytes(), ct).Wait(ct);
		Send(new InitObjectIdPacket(Character.Id).ToBytes(), ct).Wait(ct);

		
		
		// Send current time as unix timestamp
		uint unixTimestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		using (var pw = new PacketWriter())
		{ 
			pw.Write((byte)0x66);
			pw.Write(unixTimestamp);
			Send(pw.ToArray(), ct).Wait(ct);
		}


		// Send Gear and stats etc

		// buffs
		Send(@"2C 03 C6 85 96 00 05 03 C6 85 D5 2B 00 2D E1 94 
00 01 00 00 03 C6 85 D6 2B 00 2D E1 95 00 01 00 
00 03 C6 85 D7 2B 00 2D E1 96 00 01 00 00 03 C6 
85 D8 2B 00 2D E3 D6 00 01 00 00 03 C6 85 D9 2B 
00 2D E7 4A 00 01 00 00 00".ToByteArray(), CancellationToken.None).Wait();

		// Stats
		Send(@"08 00 00 FD FF 00 1E 84 8D 00 00 00 00 00 00 01 
00 02 01 00 00 00 00 00 00 00 00 01 27 00 00 00 
00 00 00 01 27 00 00 00 00 00 00 00 49 00 00 00 
00 00 00 00 49 00 00 00 00 00 00 04 C5 00 00 00 
00 00 00 04 C5 00 00 00 00 00 00 00 00 00 00 00 
00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
00 00 00 00 00 00 02 00 02 00 00 00 00 00 00 00 
CD 00 00 00 00 00 00 00 64 00 00 00 00 00 00 00 
FA 00 01 00 00 00 00 00 00 27 10 00 00 00 00 00 
00 01 27 00 00 00 00 00 00 00 00 49 00 00 00 00 
00 00 04 C5 00 00 00 00 00 00 00 CD 00 00 00 00 
00 00 00 00 00 00 00 00 00 00 00 00 00 02 00 04 
00 01 00 03 00 03 00 03 00 03 00 02 00 04 00 01 
00 03 00 03 00 03 00 03 00 01 00 00 00 00 00 00 
00 00 00 00 00 00 00 00 00 00 00 00 05 00 19 00 
02 00 08 00 00 00 02 00 01 00 01 00 00 00 00 16 
1B 00 00 02 6A 00 00 04 D6 00 00 02 67 00 00 04 
D0 00 00 00 39 00 00 00 15 59 12 12 00 05 00 05 
00 05 00 05 00 0A 00 05 00 05 00 0A 00 00 00 00 
00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
00 00 00 00 00 00 00 00 00 00 00 00 00 00 02 BA 
7D EF 30 00 00 00 00 00 00 98 96 80 00 00 00 00 
00 00 00 00 00".ToByteArray(), CancellationToken.None).Wait();

		// Gear
		Send(@"37 00 0F A6 E4 03 C6 85 B1 0A 81 F5 83 5B 83 62 
83 77 83 8B 00 00 00 00 00 00 00 00 00 00 00 00 
00 00 00 00 00 00 08 01 00 00 01 00 00 00 01 01 
00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
00 08 00 0A 00 0D 00 04 00 18 00 00 00 00 00 02 
00 01 00 01 00 01 00 01 04 00 5F FF 00 00 00 00 
00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
00 00 00 00 00 00 01 00 00 00 00 00 00 00 00 00 
00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
00 00 00 00 00 00 00 00 FF 00".ToByteArray(), CancellationToken.None).Wait();



		// Set skills
		Send(@"16 00 00 25 00 2D E8 A7 00 00 00 00 00 00 00 00 
00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
00 00 00 00 00 2D E8 A7 00 00 00 00 00 00 00 00 
00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
00 00 00 00 00 00 00 00".ToByteArray(), CancellationToken.None).Wait();

		// Enable skill bar
		Send(@"51 02 00 00 15 01 00 00 2D C6 C0 01 01 00 2D C6 
C0 01 02 00 2D C6 C0 01 03 00 2D C6 C0 01 04 00 
2D C6 C0 01 05 00 2D C6 C0 01 06 00 2D C6 C0 02 
00 00 2D C6 C0 02 01 00 2D C6 C0 02 02 00 2D C6 
C0 02 03 00 2D C6 C0 02 04 00 2D C6 C0 02 05 00 
2D C6 C0 02 06 00 2D C6 C0 03 00 00 2D C6 C0 03 
01 00 2D C6 C0 03 02 00 2D C6 C0 03 03 00 2D C6 
C0 03 04 00 2D C6 C0 03 05 00 2D C6 C0 03 06 00 
2D C6 C0".ToByteArray(), CancellationToken.None).Wait();

		Send(@"51 02 01 01 00".ToByteArray(), CancellationToken.None).Wait();

		// Environmental mana 999
		Send(@"6C 03 E7".ToByteArray(), CancellationToken.None).Wait();

		Send(@"51 02 01 01 00".ToByteArray(), CancellationToken.None).Wait();

		Send(@"4D 00 00 01 01 00 2D CF 94 00 01 00 01 01 00 00
00 00 00 00 00 00 00 00 00 00 00 ".ToByteArray(), CancellationToken.None).Wait();


		Logger.Log($"[LoadMap] Sending {map.Entities.Count} entity groups for map {map.MapId}-{map.ZoneId}", LogLevel.Information);
		int entityCount = 0;
		foreach (var subList in map.Entities)
		{
			foreach (var entity in subList.Value)
			{
				// Do not include enemies
				//if (entity[2] == 0x02)
				//	continue;

				// Do not include items
				if (entity[2] == 0x03)
					continue;

				// Do not include pets
				if (entity[2] == 0x06)
					continue;

				// Log entity details
				byte spawnType = entity[2];
				uint objectId = entity.Length >= 7 ? BitConverter.ToUInt32(entity, 3) : 0;
				string typeName = spawnType switch
				{
					0x00 => "Monster",
					0x01 => "ExtendedNPC",
					0x02 => "ZoneNPC",
					0x04 => "Portal",
					0x05 => "Reactor",
					_ => $"Unknown({spawnType:X2})"
				};
				Logger.Log($"[LoadMap] Sending {typeName} ObjectId={objectId} Len={entity.Length}", LogLevel.Debug);
				entityCount++;

				Send(entity, ct).Wait(ct);
			}
		}
		Logger.Log($"[LoadMap] Sent {entityCount} entities total", LogLevel.Information);

		foreach (var player in _server.ConnectedPlayers.Values)
		{
			if (player == this || string.IsNullOrEmpty(player.GetCharacter().Name))
				continue;
			if (!player.InMap(this.CurrentMap))
				continue;

			var otherCharacter = player.GetCharacter();
			if (otherCharacter.SpawnCharacterPacket is null)
				continue;

			Send(otherCharacter
				.SpawnCharacterPacket
				.ToBytes(SetAsOther: true), ct).Wait(ct);

			player.Send(Character.SpawnCharacterPacket!.ToBytes(SetAsOther: true), ct).Wait(ct);
		}
		
	}

	public bool InMap(MapId? map)
	{
		return CurrentMap == map;
	}

	public MapId? GetCurrentMap()
	{
		return CurrentMap;
	}

	public void SendMetaData()
	{
		#region meta packet
		string packet = @"18 00 01 00 D4 14 43 61 72 64 69 66 66 5F 57 65 
61 70 6F 6E 31 2E 6D 65 74 61 41 D9 12 FF 10 43 
61 6E 74 61 5F 4D 61 67 69 63 2E 6D 65 74 61 AB 
26 E4 C9 14 43 6C 75 62 5F 50 6F 69 6E 74 5F 53 
68 6F 70 2E 6D 65 74 61 E2 BD 09 FB 17 53 68 61 
64 6F 77 54 6F 77 65 72 5F 57 61 69 74 52 31 2E 
6D 65 74 61 15 93 BF 68 0B 42 65 61 75 74 79 2E 
6D 65 74 61 9E 8E 09 BE 0D 4E 61 72 5F 42 61 72 
31 2E 6D 65 74 61 40 F9 57 26 1C 51 75 69 63 6B 
53 6C 6F 74 52 65 73 74 72 69 63 74 65 64 44 42 
49 44 2E 6D 65 74 61 62 0F A7 32 12 4B 65 6C 74 
69 63 61 5F 41 72 6D 6F 72 2E 6D 65 74 61 41 D9 
12 FF 1F 41 76 61 74 61 72 4D 61 74 63 68 5F 53 
48 4F 57 48 45 41 44 5F 45 76 65 6E 74 2E 6D 65 
74 61 14 7A B4 92 0F 4C 6F 77 53 70 65 63 4D 61 
70 2E 6D 65 74 61 15 A2 8A A8 10 45 6C 74 69 76 
6F 5F 49 74 65 6D 2E 6D 65 74 61 B5 78 8A 3E 17 
53 68 61 64 6F 77 54 6F 77 65 72 5F 57 61 69 74 
52 36 2E 6D 65 74 61 D9 B9 F4 7B 1B 52 65 73 74 
72 69 63 74 65 64 49 74 65 6D 5F 45 6E 63 68 61 
6E 74 2E 6D 65 74 61 69 FC 68 03 19 41 76 61 74 
61 72 4D 61 74 63 68 5F 53 48 4F 57 46 4F 4F 54 
2E 6D 65 74 61 8B 04 FE C9 19 41 76 61 74 61 72 
4D 61 74 63 68 5F 53 48 4F 57 46 41 43 45 2E 6D 
65 74 61 F9 99 0B 60 20 4D 61 67 69 63 52 6F 75 
6C 65 74 74 65 52 65 73 74 72 69 63 74 65 64 49 
74 65 6D 2E 6D 65 74 61 48 3D 8B 39 0D 4C 61 69 
5F 49 74 65 6D 2E 6D 65 74 61 57 11 30 98 14 43 
61 72 64 69 66 66 5F 46 6F 72 74 75 6E 65 2E 6D 
65 74 61 61 C4 7A 53 15 52 75 6D 6F 6C 69 5F 44 
72 75 67 53 74 6F 72 65 2E 6D 65 74 61 79 FD 3B 
71 19 52 65 6D 6F 74 65 51 75 65 73 74 41 6C 65 
72 74 4C 69 73 74 2E 6D 65 74 61 B8 CA 03 33 0F 
4F 72 6C 69 65 5F 4D 69 6C 6C 2E 6D 65 74 61 8F 
B2 3C BA 14 41 62 61 6E 64 6F 6E 65 64 5F 53 74 
6F 72 65 2E 6D 65 74 61 FB 60 DD 23 0E 4D 6F 6F 
6E 5F 5A 65 72 6F 2E 6D 65 74 61 C3 03 F5 BF 17 
4D 69 6E 69 47 61 6D 65 5F 46 6C 61 67 53 74 6F 
72 65 2E 6D 65 74 61 70 75 27 8B 1D 52 65 73 74 
72 69 63 74 65 64 49 74 65 6D 5F 45 6C 65 6D 65 
6E 74 61 6C 2E 6D 65 74 61 36 56 78 F7 21 41 76 
61 74 61 72 4D 61 74 63 68 5F 53 48 4F 57 45 46 
46 45 43 54 5F 45 76 65 6E 74 2E 6D 65 74 61 F4 
F2 F1 78 0F 48 65 72 6F 5F 53 68 6F 70 43 2E 6D 
65 74 61 1E DD 12 66 10 53 69 6F 63 61 6E 5F 49 
74 65 6D 2E 6D 65 74 61 78 01 BF B5 0F 53 61 6E 
73 72 75 5F 49 6E 6E 2E 6D 65 74 61 3C B0 7B 04 
0E 47 6F 6C 64 5F 49 74 65 6D 2E 6D 65 74 61 9F 
39 14 06 12 4F 72 6C 69 65 5F 4B 61 74 72 69 6E 
61 2E 6D 65 74 61 A7 F1 65 76 11 50 72 6F 70 6F 
73 65 5F 49 74 65 6D 2E 6D 65 74 61 5F 7C 7F 84 
16 52 75 6D 6F 6C 69 5F 53 70 69 72 69 74 53 68 
6F 70 2E 6D 65 74 61 B0 B7 35 91 14 48 69 64 65 
4D 6F 6E 73 74 65 72 43 61 72 64 2E 6D 65 74 61 
6E 15 86 6D 12 54 57 5F 50 6C 69 6E 67 5F 52 61 
72 65 2E 6D 65 74 61 72 A6 00 4A 11 53 61 6E 73 
72 75 5F 51 75 65 73 74 2E 6D 65 74 61 5C B9 83 
E3 14 46 6C 65 61 4D 61 72 6B 65 74 53 74 6F 72 
65 2E 6D 65 74 61 D8 BF 8A 81 11 54 6F 77 65 72 
34 38 5F 49 74 65 6D 2E 6D 65 74 61 42 05 67 28 
10 41 64 73 65 6C 6C 5F 49 74 65 6D 2E 6D 65 74 
61 17 36 D7 7F 12 4C 69 6D 6F 6E 61 64 65 5F 49 
74 65 6D 2E 6D 65 74 61 CA C5 7B 02 0E 4E 61 72 
5F 4E 61 73 74 65 2E 6D 65 74 61 3C 5A F1 3E 0F 
47 72 61 76 65 5F 52 75 64 69 2E 6D 65 74 61 92 
36 67 FD 15 53 69 65 6E 6E 61 52 65 73 65 74 5F 
53 68 6F 70 2E 6D 65 74 61 70 06 03 88 14 52 75 
6E 65 5F 46 65 72 74 69 6C 69 7A 65 72 2E 6D 65 
74 61 FD 70 97 5F 11 4B 65 6C 74 69 63 61 5F 49 
74 65 6D 2E 6D 65 74 61 63 10 F6 A6 16 53 69 6C 
76 65 72 53 6B 75 6C 6C 5F 32 31 35 4C 76 2E 6D 
65 74 61 CF 03 02 CF 16 53 69 6C 76 65 72 53 6B 
75 6C 6C 5F 31 36 35 4C 76 2E 6D 65 74 61 CD 12 
72 68 10 53 68 61 64 6F 77 5F 49 74 65 6D 2E 6D 
65 74 61 8D 74 9A 5F 0F 43 6C 61 64 5F 4D 61 67 
69 63 2E 6D 65 74 61 3B 09 72 C0 17 53 68 61 64 
6F 77 54 6F 77 65 72 5F 57 61 69 74 52 32 2E 6D 
65 74 61 DC 4A 6D 98 16 52 65 76 6F 6C 75 74 69 
6F 6E 5F 73 75 70 70 6C 79 2E 6D 65 74 61 EE FA 
83 52 0D 4E 61 72 5F 42 61 72 32 2E 6D 65 74 61 
8A 15 5A CB 0D 43 6C 61 64 5F 49 6E 6E 2E 6D 65 
74 61 E6 CB A2 9B 17 4E 61 72 5F 70 6F 69 6E 74 
57 70 5F 61 73 74 69 6E 65 2E 6D 65 74 61 DA 2E 
17 E3 10 53 61 6B 75 72 61 5F 49 74 65 6D 2E 6D 
65 74 61 04 B6 41 CE 14 46 61 69 72 79 50 69 74 
74 61 5F 53 68 6F 70 2E 6D 65 74 61 E3 8E 2D EB 
14 54 69 74 6C 65 47 72 6F 75 70 42 6F 6E 75 73 
2E 6D 65 74 61 0F 5F 17 E7 19 41 76 61 74 61 72 
4D 61 74 63 68 5F 53 48 4F 57 48 45 41 44 2E 6D 
65 74 61 E0 18 04 4C 17 53 68 61 64 6F 77 54 6F 
77 65 72 5F 57 61 69 74 52 37 2E 6D 65 74 61 8A 
10 C9 A0 11 43 6C 61 64 5F 43 72 79 73 74 61 6C 
2E 6D 65 74 61 F8 02 76 EF 18 4F 6C 64 45 71 75 
69 70 6D 65 6E 74 41 62 69 6C 69 74 79 2E 6D 65 
74 61 A4 B3 AE 76 13 43 6C 75 62 5F 41 67 69 74 
5F 53 68 6F 70 2E 6D 65 74 61 EA EB 0D B0 13 54 
57 5F 43 6C 61 64 5F 4D 6F 72 67 61 6E 2E 6D 65 
74 61 05 3A AC EB 0E 42 65 61 75 74 79 4C 69 70 
2E 6D 65 74 61 0F E5 8A EB 11 4F 72 6C 69 65 5F 
4D 61 72 63 69 61 2E 6D 65 74 61 4A 69 22 91 0F 
52 75 6E 65 5F 56 69 73 69 74 2E 6D 65 74 61 A9 
F6 B7 FC 11 41 64 73 65 6C 6C 5F 4D 61 67 69 63 
2E 6D 65 74 61 DA 22 4A 33 13 4F 75 74 50 6F 73 
74 5F 57 65 61 70 6F 6E 2E 6D 65 74 61 41 D9 12 
FF 20 41 76 61 74 61 72 4D 61 74 63 68 5F 53 48 
4F 57 46 4F 4F 54 5F 4E 6F 41 74 74 72 2E 6D 65 
74 61 44 64 FE CF 11 4F 75 74 50 6F 73 74 5F 43 
6F 6F 6B 2E 6D 65 74 61 0D 1F 8E 8E 0F 4B 61 75 
6C 5F 4D 61 67 69 63 2E 6D 65 74 61 36 70 AB 4F 
0F 54 69 74 6C 65 45 76 65 6E 74 2E 6D 65 74 61 
15 9F 57 3E 10 73 69 6C 76 65 72 73 6B 75 6C 6C 
2E 6D 65 74 61 A2 34 B9 08 14 46 61 69 72 79 50 
69 74 74 61 52 65 73 65 74 2E 6D 65 74 61 F9 68 
64 27 0E 42 65 61 75 74 79 45 79 65 2E 6D 65 74 
61 07 43 92 E5 0E 43 6C 61 64 5F 49 74 65 6D 2E 
6D 65 74 61 C4 2C 3F C1 19 54 57 5F 48 61 63 6B 
69 6E 67 5F 54 6F 6F 6C 5F 4C 69 73 74 2E 6D 65 
74 61 19 E2 C5 E6 17 4E 61 72 5F 70 6F 69 6E 74 
57 70 5F 79 75 72 69 67 65 2E 6D 65 74 61 DF E2 
C8 06 0B 4E 50 43 47 65 6E 49 6E 66 6F 30 74 8F 
44 83 0B 4E 50 43 47 65 6E 49 6E 66 6F 31 7B 3C 
BB 9A 0F 42 65 61 75 74 79 53 6B 69 6E 2E 6D 65 
74 61 5E A3 9F 39 15 4F 70 65 6E 4D 61 72 6B 65 
74 53 65 72 76 65 72 2E 6D 65 74 61 71 B9 8D 39 
0C 4C 61 69 5F 49 6E 6E 2E 6D 65 74 61 88 46 D6 
73 11 45 6C 74 69 76 6F 5F 51 75 65 73 74 2E 6D 
65 74 61 AF 6F DC 8E 11 43 61 6E 74 61 5F 57 65 
61 70 6F 6E 2E 6D 65 74 61 41 D9 12 FF 1F 41 76 
61 74 61 72 4D 61 74 63 68 5F 53 48 4F 57 46 41 
43 45 5F 45 76 65 6E 74 2E 6D 65 74 61 03 C2 CE 
81 14 45 76 65 6E 74 55 49 5F 43 6F 6E 74 72 6F 
6C 2E 6D 65 74 61 97 EA 72 C8 10 43 61 72 64 69 
66 66 5F 42 61 72 2E 6D 65 74 61 40 F9 57 26 10 
43 6C 61 64 5F 57 65 61 70 6F 6E 2E 6D 65 74 61 
41 D9 12 FF 09 6E 75 6C 6C 2E 6D 65 74 61 41 D9 
12 FF 17 53 68 61 64 6F 77 54 6F 77 65 72 5F 57 
61 69 74 52 33 2E 6D 65 74 61 64 36 02 FF 1D 52 
65 73 74 72 69 63 74 65 64 49 74 65 6D 5F 52 65 
69 6E 66 6F 72 63 65 2E 6D 65 74 61 86 9E B2 8E 
08 62 67 6D 2E 6D 65 74 61 41 D9 12 FF 18 45 6C 
73 6F 50 6F 69 6E 74 49 74 65 6D 73 5F 53 68 6F 
70 2E 6D 65 74 61 AA 0A A1 4B 14 4E 6F 72 6D 61 
6C 53 68 6F 70 5F 49 74 65 6D 2E 6D 65 74 61 91 
CF DB B6 11 47 75 69 64 65 4D 65 73 73 61 67 65 
2E 6D 65 74 61 5B DC 4C F7 17 53 68 61 64 6F 77 
54 6F 77 65 72 5F 57 61 69 74 52 38 2E 6D 65 74 
61 74 DE 46 56 1B 52 65 73 74 72 69 63 74 65 64 
49 74 65 6D 5F 41 62 69 6C 69 74 79 2E 6D 65 74 
61 41 D9 12 FF 12 43 61 73 68 5F 53 65 65 64 53 
68 6F 70 2E 6D 65 74 61 47 36 5D 7C 12 6D 75 74 
74 65 72 69 6E 67 74 65 73 74 2E 6D 65 74 61 3D 
E7 A8 16 0D 4E 61 72 5F 49 74 65 6D 2E 6D 65 74 
61 AC A4 A8 C6 11 53 68 6F 77 47 61 6D 65 49 63 
6F 6E 2E 6D 65 74 61 63 0B 8D B5 11 53 61 6E 73 
72 75 5F 4D 61 67 69 63 2E 6D 65 74 61 AB 09 42 
6B 14 41 76 61 74 61 72 53 65 74 45 66 66 65 63 
74 2E 6D 65 74 61 6B 68 03 D8 10 4F 72 6C 69 65 
5F 53 74 65 76 65 2E 6D 65 74 61 80 DC 86 77 1B 
41 76 61 74 61 72 4D 61 74 63 68 5F 53 48 4F 57 
45 46 46 45 43 54 2E 6D 65 74 61 0A 0B 11 5D 17 
54 57 5F 54 61 6C 65 73 50 6F 69 6E 74 5F 53 68 
6F 70 2E 6D 65 74 61 AA 70 7F E2 15 53 69 6C 76 
65 72 53 6B 75 6C 6C 5F 49 74 65 6D 2E 6D 65 74 
61 A4 DA CE 93 14 46 6C 65 61 4D 61 72 6B 65 74 
5F 43 6C 61 64 2E 6D 65 74 61 90 62 9F 12 13 53 
69 6D 65 72 6F 6E 6F 5F 53 74 6F 72 65 2E 6D 65 
74 61 EF E9 70 34 11 45 6C 74 69 76 6F 5F 73 6E 
61 63 6B 2E 6D 65 74 61 FC 01 E1 C0 10 54 69 74 
6C 65 4E 6F 72 6D 61 6C 2E 6D 65 74 61 B0 3C 9B 
47 11 4F 75 74 50 6F 73 74 5F 49 74 65 6D 2E 6D 
65 74 61 DA 1B 9C 7A 0D 43 6C 61 64 5F 53 75 61 
2E 6D 65 74 61 66 46 8B 84 16 4D 61 6E 75 66 61 
63 74 75 72 65 73 5F 43 6F 6F 6B 2E 6D 65 74 61 
B9 CA B3 EA 15 42 6C 75 65 63 6F 72 61 6C 5F 57 
65 61 70 6F 6E 2E 6D 65 74 61 41 D9 12 FF 11 43 
61 72 64 69 66 66 5F 49 74 65 6D 2E 6D 65 74 61 
C4 23 A8 B0 0F 45 6C 74 69 76 6F 5F 49 6E 6E 2E 
6D 65 74 61 18 0C 84 E6 0E 43 6C 61 64 5F 52 75 
64 69 2E 6D 65 74 61 0D 66 3D 63 0E 4B 61 75 6C 
5F 49 74 65 6D 2E 6D 65 74 61 20 47 A1 9E 16 54 
61 6C 65 73 50 6F 69 6E 74 5F 42 65 61 75 74 79 
2E 6D 65 74 61 AC FE C8 4F 12 52 75 6D 6F 6C 69 
5F 57 65 61 70 6F 6E 2E 6D 65 74 61 41 D9 12 FF 
0B 43 31 30 5F 4D 41 2E 6D 65 74 61 A6 A3 8D 34 
14 54 57 5F 46 6F 72 74 5F 41 72 74 69 73 61 6E 
2E 6D 65 74 61 B1 9E A5 CA 0F 41 63 74 69 6F 6E 
49 6E 66 6F 2E 6D 65 74 61 41 D9 12 FF 10 43 61 
6E 74 61 5F 44 72 69 6E 6B 2E 6D 65 74 61 7C 0A 
04 7E 12 53 61 6B 75 72 61 5F 57 65 61 70 6F 6E 
2E 6D 65 74 61 41 D9 12 FF 0C 44 79 65 53 68 6F 
70 2E 6D 65 74 61 DD CC 61 2E 11 4F 72 6C 69 65 
5F 4A 75 6C 69 65 6E 2E 6D 65 74 61 BF 19 45 DE 
13 4B 65 6C 74 69 63 61 5F 57 65 61 70 6F 6E 2E 
6D 65 74 61 41 D9 12 FF 13 50 72 61 62 68 61 5F 
4F 75 74 70 6F 73 74 2E 6D 65 74 61 13 3B 97 8F 
18 45 6C 65 6D 65 6E 74 61 6C 54 6F 77 6E 5F 53 
74 6F 72 65 2E 6D 65 74 61 27 65 55 7B 0C 52 65 
77 61 72 64 73 2E 6D 65 74 61 68 B0 C1 B1 11 54 
6F 77 65 72 31 36 5F 49 74 65 6D 2E 6D 65 74 61 
B0 D6 B6 E2 13 42 6C 75 65 63 6F 72 61 6C 5F 49 
74 65 6D 2E 6D 65 74 61 32 B7 10 D1 17 53 68 61 
64 6F 77 54 6F 77 65 72 5F 57 61 69 74 52 34 2E 
6D 65 74 61 04 6C 53 D4 0F 4F 72 6C 69 65 5F 53 
65 73 61 2E 6D 65 74 61 A6 D0 EC D2 0F 4E 61 72 
5F 57 65 61 70 6F 6E 2E 6D 65 74 61 41 D9 12 FF 
14 52 75 6D 6F 6C 69 5F 46 69 73 68 53 68 6F 70 
2E 6D 65 74 61 70 75 4B CB 0C 4E 61 72 5F 49 6E 
6E 2E 6D 65 74 61 C8 01 89 78 1B 52 65 73 74 72 
69 63 74 65 64 49 74 65 6D 5F 49 6E 68 65 72 69 
74 2E 6D 65 74 61 74 34 E8 E7 17 53 68 61 64 6F 
77 54 6F 77 65 72 5F 57 61 69 74 52 39 2E 6D 65 
74 61 E5 AE C1 C9 10 43 61 6E 74 61 5F 51 75 65 
73 74 2E 6D 65 74 61 99 B6 D7 98 13 4E 65 6F 54 
65 63 69 74 68 5F 53 68 6F 70 2E 6D 65 74 61 3D 
B4 61 2D 0F 4D 6F 6E 73 74 65 72 47 65 6E 49 6E 
66 6F 30 F4 E8 4E F1 10 53 61 6E 73 72 75 5F 46 
69 73 68 2E 6D 65 74 61 9B 7F 42 05 0B 45 76 65 
6E 74 73 2E 6D 65 74 61 A2 46 81 F8 0F 4D 6F 6E 
73 74 65 72 47 65 6E 49 6E 66 6F 31 94 D5 CB FD 
0F 43 61 6E 74 61 5F 43 6F 6F 6B 2E 6D 65 74 61 
CE AC 84 6A 0F 48 65 72 6F 5F 53 68 6F 70 41 2E 
6D 65 74 61 90 FF 9E 45 10 4F 72 6C 69 65 5F 45 
6C 69 73 61 2E 6D 65 74 61 79 E9 FB D1 14 56 69 
73 75 61 6C 45 76 65 6E 74 4C 69 73 74 2E 6D 65 
74 61 D9 0C 48 C7 11 45 6C 74 69 76 6F 5F 4D 61 
67 69 63 2E 6D 65 74 61 26 A2 98 6E 11 52 65 6D 
6F 74 65 5F 53 74 6F 72 65 2E 6D 65 74 61 CE 1D 
26 E9 10 49 73 6C 61 6E 64 5F 46 69 73 68 2E 6D 
65 74 61 3F F5 86 D6 1C 4D 69 6E 69 47 61 6D 65 
5F 32 30 74 68 5F 46 6C 61 67 53 74 6F 72 65 2E 
6D 65 74 61 69 09 FB D6 10 4B 6F 62 6F 6C 74 5F 
49 74 65 6D 2E 6D 65 74 61 60 6E 85 CA 11 53 69 
6F 63 61 6E 5F 51 75 65 73 74 2E 6D 65 74 61 59 
A6 69 E4 0E 4D 65 6E 74 5F 49 74 65 6D 2E 6D 65 
74 61 E5 A2 7D 8B 14 4E 65 77 59 65 61 72 5F 50 
65 6E 64 61 6E 74 2E 6D 65 74 61 2B 3B 1B DC 12 
53 61 6E 73 72 75 5F 57 65 61 70 6F 6E 2E 6D 65 
74 61 23 20 69 2C 0F 52 75 6E 65 5F 53 65 65 64 
73 2E 6D 65 74 61 37 BD 6B 72 10 52 75 6D 6F 6C 
69 5F 49 74 65 6D 2E 6D 65 74 61 FE B6 77 0B 12 
54 57 5F 50 6C 69 6E 67 5F 49 74 65 6D 2E 6D 65 
74 61 A9 C5 A4 56 0F 42 65 61 75 74 79 48 61 69 
72 2E 6D 65 74 61 87 5A 51 CB 13 41 74 74 72 69 
62 75 74 65 5F 49 74 65 6D 2E 6D 65 74 61 58 D1 
43 98 13 43 61 72 64 69 66 66 5F 57 65 61 70 6F 
6E 2E 6D 65 74 61 41 D9 12 FF 0F 43 61 6E 74 61 
5F 46 69 73 68 2E 6D 65 74 61 0B 0F CF BA 19 41 
76 61 74 61 72 4D 61 74 63 68 5F 53 48 4F 57 42 
41 43 4B 2E 6D 65 74 61 58 0D 8F 01 10 53 61 6E 
73 72 75 5F 49 74 65 6D 2E 6D 65 74 61 5C 10 2A 
B5 0F 4D 6F 6F 6E 5F 51 75 65 73 74 2E 6D 65 74 
61 C9 25 7F 7E 11 49 73 6C 61 6E 64 5F 46 69 73 
68 32 2E 6D 65 74 61 FF 20 71 0C 12 51 75 65 73 
74 45 78 65 63 75 74 6F 72 2E 6D 65 74 61 68 8E 
6A 19 12 42 6C 75 65 63 6F 72 61 6C 5F 42 61 72 
2E 6D 65 74 61 49 21 C8 8C 0D 43 6C 61 64 5F 45 
67 67 2E 6D 65 74 61 64 4F 0C D4 12 41 64 73 65 
6C 6C 5F 57 65 61 70 6F 6E 2E 6D 65 74 61 41 D9 
12 FF 14 4C 69 6D 6F 6E 61 64 65 5F 57 65 61 70 
6F 6E 2E 6D 65 74 61 41 D9 12 FF 0E 58 6D 61 73 
5F 54 69 6D 65 2E 6D 65 74 61 BC 85 BB E4 12 46 
61 69 72 79 50 69 74 74 61 53 75 62 2E 6D 65 74 
61 0E A4 38 29 16 47 69 75 6C 69 6F 44 65 73 73 
65 72 74 53 68 6F 70 2E 6D 65 74 61 D8 DA 5F E6 
17 53 68 61 64 6F 77 54 6F 77 65 72 5F 57 61 69 
74 52 35 2E 6D 65 74 61 F4 C1 95 50 20 52 65 73 
74 72 69 63 74 65 64 49 74 65 6D 5F 52 61 6E 64 
6F 6D 4F 70 74 69 6F 6E 2E 6D 65 74 61 98 E4 AB 
A0 0E 4E 61 72 5F 4D 61 67 69 63 2E 6D 65 74 61 
52 89 4D 5B 17 54 77 69 73 74 65 64 56 69 6C 6C 
61 67 65 53 68 6F 70 2E 6D 65 74 61 88 69 A2 BF 
13 42 6C 61 63 6B 4F 6C 69 76 65 53 68 6F 70 2E 
6D 65 74 61 32 D3 AF 99 12 4F 75 74 50 6F 73 74 
5F 51 75 65 73 74 2E 6D 65 74 61 B5 A7 AC 2B 10 
41 67 67 72 6F 54 61 62 6C 65 73 2E 6D 65 74 61 
FE 7A 8B C5 11 54 57 5F 4E 61 72 5F 4E 61 73 74 
65 2E 6D 65 74 61 2B B2 D6 56 15 54 57 5F 46 6F 
72 74 32 5F 41 72 74 69 73 61 6E 2E 6D 65 74 61 
AE CB C6 02 14 53 74 61 74 52 65 76 65 61 6C 49 
74 65 6D 73 2E 6D 65 74 61 3B 27 76 F6 12 45 6C 
74 69 76 6F 5F 57 65 61 70 6F 6E 2E 6D 65 74 61 
41 D9 12 FF 0F 43 61 6E 74 61 5F 49 74 65 6D 2E 
6D 65 74 61 07 64 62 CB 0F 48 65 72 6F 5F 53 68 
6F 70 42 2E 6D 65 74 61 53 BB 5C 3C 0F 43 6C 61 
64 5F 44 61 69 73 79 2E 6D 65 74 61 FC 6E 2F 76 
1F 41 76 61 74 61 72 4D 61 74 63 68 5F 53 48 4F 
57 42 41 43 4B 5F 45 76 65 6E 74 2E 6D 65 74 61 
85 FF 58 D3 0E 4C 61 69 5F 4D 61 67 69 63 2E 6D 
65 74 61 07 A6 86 6B 16 53 69 6C 76 65 72 53 6B 
75 6C 6C 5F 31 30 35 4C 76 2E 6D 65 74 61 B0 DE 
5F 95 12 4C 69 6D 6F 6E 61 64 65 5F 43 61 66 65 
2E 6D 65 74 61 AA 31 38 EC 0E 4D 6F 6F 6E 5F 49 
74 65 6D 2E 6D 65 74 61 6E AF BA 49 1C 47 61 6D 
65 49 63 6F 6E 52 65 73 74 72 69 63 74 65 64 46 
69 65 6C 64 2E 6D 65 74 61 2F B6 10 A5 13 54 57 
5F 41 64 73 65 6C 6C 5F 49 74 65 6D 2E 6D 65 74 
61 D6 D0 F5 5B 14 48 61 6D 6F 6E 69 63 5F 44 65 
66 6C 61 74 65 2E 6D 65 74 61 14 35 4E F2 12 54 
75 74 6F 72 69 61 6C 5F 53 68 6F 70 2E 6D 65 74 
61 CA 60 64 6D 16 43 61 72 64 69 66 66 5F 44 72 
75 67 53 74 6F 72 65 2E 6D 65 74 61 9B E4 DC 21 
08 75 72 6C 2E 6D 65 74 61 BB 2D F2 6B 11 4B 65 
6C 74 69 63 61 5F 43 61 66 65 2E 6D 65 74 61 51 
04 8E 17 15 4B 65 6C 74 69 63 61 5F 42 6F 75 74 
69 71 75 65 2E 6D 65 74 61 CF D0 64 80 10 4D 6F 
6F 6E 5F 57 65 61 70 6F 6E 2E 6D 65 74 61 41 D9 
12 FF 0F 4C 61 69 5F 57 65 61 70 6F 6E 2E 6D 65 
74 61 41 D9 12 FF 15 42 6C 75 65 63 6F 72 61 6C 
5F 54 69 63 6B 65 74 2E 6D 65 74 61 C2 39 96 41 
12 53 61 6E 73 72 75 5F 46 6C 6F 77 65 72 2E 6D 
65 74 61 91 65 1B 0A 10 4B 61 75 6C 5F 57 65 61 
70 6F 6E 2E 6D 65 74 61 41 D9 12 FF";
		#endregion

		Send(packet.ToByteArray(), CancellationToken.None).Wait();
	}


}
