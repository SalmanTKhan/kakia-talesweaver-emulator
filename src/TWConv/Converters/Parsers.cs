using System.Buffers.Binary;
using System.Text.Json;

namespace TalesWeaverMapConverter;

/// <summary>
/// Binary parsers for TalesWeaver map/entity data.
/// All multi-byte fields are read as Big Endian (network byte order).
/// Layouts follow the 010 Editor binary templates (WorldResponse.bt, headers.bt, enums.bt).
/// </summary>
public static class Parsers
{
	// ========================================================================
	// map.bin — Captured MapChangeResponse (0x15) packet
	//
	// Layout (5 bytes, Big Endian):
	//   Byte 0  : Op = 0x15 (MapChangeResponse)
	//   Byte 1-2: MapId  (ushort BE)
	//   Byte 3-4: ZoneId (ushort BE)
	//
	// Fallback: if byte 0 is NOT 0x15, assume raw LE ushort pair (legacy).
	// ========================================================================

	private const byte MapChangeResponseOp = 0x15;

	public static MapHeader ParseMapBin(ReadOnlySpan<byte> data)
	{
		// Captured packet format: [0x15] [mapId BE ushort] [zoneId BE ushort]
		if (data.Length >= 5 && data[0] == MapChangeResponseOp)
		{
			var mapId = BinaryPrimitives.ReadUInt16BigEndian(data[1..]);
			var zoneId = BinaryPrimitives.ReadUInt16BigEndian(data[3..]);
			return new MapHeader(mapId, zoneId);
		}

		// Legacy fallback: raw LE ushort pair (no opcode prefix)
		if (data.Length >= 4)
		{
			var mapId = BinaryPrimitives.ReadUInt16LittleEndian(data);
			var zoneId = BinaryPrimitives.ReadUInt16LittleEndian(data[2..]);
			return new MapHeader(mapId, zoneId);
		}

		throw new InvalidDataException($"map.bin too short: {data.Length} bytes (need ≥ 4)");
	}

	public static MapHeader ParseMapBin(string path)
	{
		var data = File.ReadAllBytes(path);
		return ParseMapBin(data);
	}

	// ========================================================================
	// SpawnPos.txt — CSV: mapId,zoneId,x,y,direction
	// ========================================================================

	public static List<SpawnPosition> ParseSpawnPosTxt(string path)
	{
		var spawns = new List<SpawnPosition>();

		foreach (var raw in File.ReadLines(path))
		{
			var line = raw.Trim();
			if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
				continue;

			var parts = line.Split(',');
			if (parts.Length < 5)
				continue;

			if (int.TryParse(parts[0], out var mapId) &&
				int.TryParse(parts[1], out var zoneId) &&
				int.TryParse(parts[2], out var x) &&
				int.TryParse(parts[3], out var y) &&
				int.TryParse(parts[4], out var direction))
			{
				spawns.Add(new SpawnPosition(mapId, zoneId, x, y, direction));
			}
		}

		return spawns;
	}

	// ========================================================================
	// WarpPortals/*.json
	// ========================================================================

	public static Dictionary<long, WarpPortalConfig> ParseWarpConfigs(string warpDir)
	{
		var configs = new Dictionary<long, WarpPortalConfig>();

		if (!Directory.Exists(warpDir))
			return configs;

		foreach (var file in Directory.GetFiles(warpDir, "*.json"))
		{
			try
			{
				var json = File.ReadAllText(file);
				var config = JsonSerializer.Deserialize<WarpPortalConfig>(json,
					new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

				if (config is not null)
					configs[config.Id] = config;
			}
			catch
			{
				// Skip malformed configs
			}
		}

		return configs;
	}

	// ========================================================================
	// Spawn/*.bin — Entity packets
	//
	// Saved file layout:
	//   Byte 0  : Op          (0x07 = WorldResponse)
	//   Byte 1  : WorldPacketId (action: Object / RemoveObject / ObjectDie)
	//   Byte 2  : SpawnType   (only when action == Object)
	//   Byte 3+ : Payload     (varies by SpawnType)
	//
	// IMPORTANT: Entity .bin files are serialized by C# code using native
	// Little Endian byte order. This differs from map.bin which contains
	// raw captured network packets in Big Endian.
	//
	// Payload layouts match WorldResponse.bt (010 Editor default = LE).
	// ========================================================================

	public static ParsedEntity? ParseEntityPacket(ReadOnlySpan<byte> data, string fileName = "")
	{
		if (data.Length < 3)
			return null;

		// --- Header -----------------------------------------------------------
		// Handle optional 0xAA server frame prefix (headers.bt)
		int offset = 0;
		if (data[0] == 0xAA)
			offset += 3; // skip frameHead, frameSeq, frameUnk

		if (data.Length <= offset)
			return null;

		var opcode = (Op)data[offset++];
		if (data.Length <= offset)
			return null;

		var action = (WorldPacketId)data[offset++];

		// --- RemoveObject / ObjectDie: just a uint objectId -------------------
		if (action is WorldPacketId.RemoveObject or WorldPacketId.ObjectDie)
		{
			if (data.Length < offset + 4)
				return null;

			var objId = BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);
			return new RemoveEntity
			{
				FileName = fileName,
				Opcode = opcode,
				Action = action,
				Type = SpawnType.Unknown,
				RawData = data.ToArray(),
				ObjectId = objId,
			};
		}

		// --- Object spawn: read SpawnType then type-specific payload ----------
		if (action != WorldPacketId.Object)
			return null;

		if (data.Length <= offset)
			return null;

		var spawnType = (SpawnType)data[offset++];
		var reader = new PacketReader(data[offset..]);

		var baseProps = (
			FileName: fileName,
			Opcode: opcode,
			Action: action,
			Type: spawnType,
			RawData: data.ToArray()
		);

		switch (spawnType)
		{
			// ---- MonsterNpc / MonsterNpc2 / MonsterNpc3 ----------------------
			// uint objectId, uint modelId, uint unkV3, uint unkV30,
			// Position pos (short x, short y), ubyte direction
			case SpawnType.MonsterNpc:
			case SpawnType.MonsterNpc2:
			case SpawnType.MonsterNpc3:
				if (reader.Remaining < 21) // 4+4+4+4+2+2+1
					return FallbackGeneric(reader, baseProps);

				var objectId = reader.ReadUInt32();
				var modelId = reader.ReadUInt32();
				var unkV3 = reader.ReadUInt32();
				var unkV30 = reader.ReadUInt32();
				var posX = reader.ReadInt16();
				var posY = reader.ReadInt16();
				var direction = reader.ReadByte();

				// Some captured SpawnType 0x02 packets store modelId at payload[12..16] as BE
				// while the normal LE model field is zero.
				if (modelId == 0 && data.Length >= offset + 16)
				{
					var altModelId = BinaryPrimitives.ReadUInt32BigEndian(data[(offset + 12)..]);
					if (altModelId is >= 2_000_000 and <= 3_000_000)
						modelId = altModelId;
				}

				return new MonsterNpcEntity
				{
					FileName = baseProps.FileName,
					Opcode = baseProps.Opcode,
					Action = baseProps.Action,
					Type = baseProps.Type,
					RawData = baseProps.RawData,
					ObjectId = objectId,
					ModelId = modelId,
					UnkV3 = unkV3,
					UnkV30 = unkV30,
					PositionX = posX,
					PositionY = posY,
					Direction = direction,
				};

			// ---- Player (0x01) -----------------------------------------------
			// uint objectId, uint unk1, uint modelId,
			// Position pos (short x, short y), ubyte direction, …trailing
			case SpawnType.Player:
				// Legacy capture files also use SpawnType 0x01 for map-local NPC records:
				// objectId(4) + padding(4) + modelId(4) + posX(2) + posY(2) + direction(1) + unk(1) + 0x0A + ...
				// Detect this shape first so NPCs are generated into NpcScript instead of being skipped as players.
				if (LooksLikeLegacyNpcPayload(data[offset..]))
				{
					return ParseLegacyNpcPayload(data[offset..], baseProps);
				}

				if (reader.Remaining < 17) // 4+4+4+2+2+1
					return FallbackGeneric(reader, baseProps);

				return new PlayerEntity
				{
					FileName = baseProps.FileName,
					Opcode = baseProps.Opcode,
					Action = baseProps.Action,
					Type = baseProps.Type,
					RawData = baseProps.RawData,
					ObjectId = reader.ReadUInt32(),
					Unk1 = reader.ReadUInt32(),
					ModelId = reader.ReadUInt32(),
					PositionX = reader.ReadInt16(),
					PositionY = reader.ReadInt16(),
					Direction = reader.ReadByte(),
				};

			// ---- Item (0x03) -------------------------------------------------
			// int itemId, short amount, short durability, int ownerId,
			// Position pos (short x, short y), short droppedAmount
			case SpawnType.Item:
				if (reader.Remaining < 18) // 4+2+2+4+2+2+2
					return FallbackGeneric(reader, baseProps);

				return new ItemEntity
				{
					FileName = baseProps.FileName,
					Opcode = baseProps.Opcode,
					Action = baseProps.Action,
					Type = baseProps.Type,
					RawData = baseProps.RawData,
					ItemId = reader.ReadInt32(),
					Amount = reader.ReadInt16(),
					Durability = reader.ReadInt16(),
					OwnerId = reader.ReadInt32(),
					PositionX = reader.ReadInt16(),
					PositionY = reader.ReadInt16(),
					DroppedAmount = reader.ReadInt16(),
				};

			// ---- Portal (0x04) -----------------------------------------------
			// uint portalId, Position pos (short x, short y),
			// ushort destMapId, ushort destPortalId
			case SpawnType.Portal:
				if (reader.Remaining < 12) // 4+2+2+2+2
					return FallbackGeneric(reader, baseProps);

				return new PortalEntity
				{
					FileName = baseProps.FileName,
					Opcode = baseProps.Opcode,
					Action = baseProps.Action,
					Type = baseProps.Type,
					RawData = baseProps.RawData,
					PortalId = reader.ReadUInt32(),
					PositionX = reader.ReadInt16(),
					PositionY = reader.ReadInt16(),
					DestMapId = reader.ReadUInt16(),
					DestPortalId = reader.ReadUInt16(),
				};

			// ---- Reactor (0x05) — inferred layout ----------------------------
			// uint objectId, uint reactorId, Position pos
			case SpawnType.Reactor:
				if (reader.Remaining < 12) // 4+4+2+2
					return FallbackGeneric(reader, baseProps);

				return new ReactorEntity
				{
					FileName = baseProps.FileName,
					Opcode = baseProps.Opcode,
					Action = baseProps.Action,
					Type = baseProps.Type,
					RawData = baseProps.RawData,
					ObjectId = reader.ReadUInt32(),
					ReactorId = reader.ReadUInt32(),
					PositionX = reader.ReadInt16(),
					PositionY = reader.ReadInt16(),
				};

			// ---- Unknown (0x00), Pet (0x06), SummonedCreature (0x08), etc. ---
			default:
				return FallbackGeneric(reader, baseProps);
		}
	}

	public static ParsedEntity? ParseEntityPacket(string path)
	{
		var data = File.ReadAllBytes(path);
		return ParseEntityPacket(data, Path.GetFileName(path));
	}

	// ========================================================================
	// Fallback: extract just the first uint as an object/entity ID
	// ========================================================================

	private static GenericEntity? FallbackGeneric(
		PacketReader reader,
		(string FileName, Op Opcode, WorldPacketId Action, SpawnType Type, byte[] RawData) b)
	{
		if (reader.Remaining < 4)
			return null;

		return new GenericEntity
		{
			FileName = b.FileName,
			Opcode = b.Opcode,
			Action = b.Action,
			Type = b.Type,
			RawData = b.RawData,
			ObjectId = reader.ReadUInt32(),
		};
	}

	private static bool LooksLikeLegacyNpcPayload(ReadOnlySpan<byte> payload)
	{
		// Need: objectId(4) + padding(4) + modelId(4) + x(2) + y(2) + dir(1) + unk(1) + 0x0A + ...
		if (payload.Length < 19)
			return false;

		// SpawnNpc packets have 4 zero-padding bytes after objectId, followed by 0x0A marker.
		return payload[4] == 0 &&
			   payload[5] == 0 &&
			   payload[6] == 0 &&
			   payload[7] == 0 &&
			   payload[18] == 0x0A;
	}

	private static MonsterNpcEntity ParseLegacyNpcPayload(
		ReadOnlySpan<byte> payload,
		(string FileName, Op Opcode, WorldPacketId Action, SpawnType Type, byte[] RawData) b)
	{
		return new MonsterNpcEntity
		{
			FileName = b.FileName,
			Opcode = b.Opcode,
			Action = b.Action,
			Type = b.Type,
			RawData = b.RawData,
			ObjectId = BinaryPrimitives.ReadUInt32LittleEndian(payload),
			ModelId = BinaryPrimitives.ReadUInt32BigEndian(payload[8..]),
			UnkV3 = 0,
			UnkV30 = 0,
			PositionX = BinaryPrimitives.ReadInt16BigEndian(payload[12..]),
			PositionY = BinaryPrimitives.ReadInt16BigEndian(payload[14..]),
			Direction = payload[16],
		};
	}
}
