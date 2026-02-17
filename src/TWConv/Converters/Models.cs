namespace TalesWeaverMapConverter;

// ============================================================================
// Enums — mirror inc/enums.bt
// ============================================================================

/// <summary>Packet operation codes (ubyte).</summary>
public enum Op : byte
{
	Handshake = 0x00,
	Acknowledge = 0x02,
	ServerRedirect = 0x03,
	WorldResponse = 0x07,
	UserPositionResponse = 0x0B,
	ChatResponse = 0x0D,
	ChatRequest = 0x0E,
	InitConnectRequest = 0x0F,
	ReconnectRequest = 0x10,
	DirectionUpdateRequest = 0x11,
	AttackRequest = 0x13,
	MapChangeResponse = 0x15,
	DialogResponse = 0x17,
	MetaFilesResponse = 0x18,
	TriggerRequest = 0x1B,
	Heartbeat = 0x24,
	CheckNameRequest = 0x28,
	CharacterInfoUpdateRequest = 0x2A,
	SelectCharacterRequest = 0x2B,
	CreateCharacterRequest = 0x2C,
	SetPoseRequest = 0x32,
	MovementRequest = 0x33,
	UiActionRequest = 0x37,
	LoginSecurityResponse = 0x3C,
	EntityClickRequest = 0x43,
	FriendDialogResponse = 0x44,
	AttackResultResponse = 0x48,
	AttackAck = 0x4A,
	LoginResponse = 0x50,
	ServerListResponse = 0x56,
	CharEffectResponse = 0x5C,
	LoginRequest = 0x66,
	ServerSelectRequest = 0x67,
	HandshakeAck = 0x68,
	CharacterSelectListResponse = 0x6B,
	NpcDialogAnswerRequest = 0x6C,
	EntityClickAck = 0x70,
	CreateCharacterResponse = 0x7C,
	ConnectedResponse = 0x7E,
}

/// <summary>WorldResponse (0x07) sub-actions.</summary>
public enum WorldPacketId : byte
{
	Object = 0x00,
	RemoveObject = 0x01,
	ObjectDie = 0x02,
}

/// <summary>Entity spawn types for WorldResponse.</summary>
public enum SpawnType : byte
{
	Unknown = 0x00,
	Player = 0x01,
	MonsterNpc = 0x02,
	Item = 0x03,
	Portal = 0x04,
	Reactor = 0x05,
	Pet = 0x06,
	MonsterNpc2 = 0x07,
	SummonedCreature = 0x08,
	MonsterNpc3 = 0x09,
}

// ============================================================================
// Parsed data models
// ============================================================================

/// <summary>map.bin header: MapId + ZoneId (2× ushort BE).</summary>
public sealed record MapHeader(ushort MapId, ushort ZoneId);

/// <summary>One row from SpawnPos.txt (CSV: mapId,zoneId,x,y,direction).</summary>
public sealed record SpawnPosition(int MapId, int ZoneId, int X, int Y, int Direction);

/// <summary>Base for all entities parsed from Spawn/*.bin packets.</summary>
public abstract class ParsedEntity
{
	public string FileName { get; init; } = string.Empty;
	public Op Opcode { get; init; }
	public WorldPacketId Action { get; init; }
	public SpawnType Type { get; init; }
	public byte[] RawData { get; init; } = Array.Empty<byte>();
}

/// <summary>
/// MonsterNpc / MonsterNpc2 / MonsterNpc3 (SpawnType 0x02, 0x07, 0x09).
/// Layout: uint objectId, uint modelId, uint unkV3, uint unkV30, Position pos, ubyte direction.
/// </summary>
public sealed class MonsterNpcEntity : ParsedEntity
{
	public uint ObjectId { get; init; }
	public uint ModelId { get; init; }
	public uint UnkV3 { get; init; }
	public uint UnkV30 { get; init; }
	public short PositionX { get; init; }
	public short PositionY { get; init; }
	public byte Direction { get; init; }
}

/// <summary>
/// Player spawn (SpawnType 0x01).
/// Layout: uint objectId, uint unk1, uint modelId, Position pos, ubyte direction, …trailing fields.
/// </summary>
public sealed class PlayerEntity : ParsedEntity
{
	public uint ObjectId { get; init; }
	public uint Unk1 { get; init; }
	public uint ModelId { get; init; }
	public short PositionX { get; init; }
	public short PositionY { get; init; }
	public byte Direction { get; init; }
}

/// <summary>
/// Item drop (SpawnType 0x03).
/// Layout: int itemId, short amount, short durability, int ownerId, Position pos, short droppedAmount.
/// </summary>
public sealed class ItemEntity : ParsedEntity
{
	public int ItemId { get; init; }
	public short Amount { get; init; }
	public short Durability { get; init; }
	public int OwnerId { get; init; }
	public short PositionX { get; init; }
	public short PositionY { get; init; }
	public short DroppedAmount { get; init; }
}

/// <summary>
/// Portal (SpawnType 0x04).
/// Layout: uint portalId, Position pos, ushort destMapId, ushort destPortalId.
/// </summary>
public sealed class PortalEntity : ParsedEntity
{
	public uint PortalId { get; init; }
	public short PositionX { get; init; }
	public short PositionY { get; init; }
	public ushort DestMapId { get; init; }
	public ushort DestPortalId { get; init; }
}

/// <summary>
/// Reactor (SpawnType 0x05) — not in .bt template but present in entity_converter.
/// Fallback layout: uint objectId, uint reactorId, Position pos.
/// </summary>
public sealed class ReactorEntity : ParsedEntity
{
	public uint ObjectId { get; init; }
	public uint ReactorId { get; init; }
	public short PositionX { get; init; }
	public short PositionY { get; init; }
}

/// <summary>
/// Despawn / Die action — just an objectId.
/// </summary>
public sealed class RemoveEntity : ParsedEntity
{
	public uint ObjectId { get; init; }
}

/// <summary>
/// Fallback for unknown or short-format entity packets (SpawnType 0x00, Pet, Summoned, etc.).
/// Only the first uint after the header is extracted.
/// </summary>
public sealed class GenericEntity : ParsedEntity
{
	public uint ObjectId { get; init; }
}

// ============================================================================
// Warp portal JSON config (from WarpPortals/*.json)
//
// Actual JSON structure:
// {
//   "Id": 1764753664,
//   "MapId": 6, "ZoneId": 38656,
//   "MinPoint": {"X": 320, "Y": 212},
//   "MaxPoint": {"X": 331, "Y": 223},
//   "Destination": {
//     "MapId": 6, "ZoneId": 38144,
//     "Position": { "Position": {"X": 94, "Y": 130}, "Direction": 2 }
//   }
// }
// ============================================================================

public sealed class WarpPortalConfig
{
	public long Id { get; set; }
	public int MapId { get; set; }
	public int ZoneId { get; set; }
	public PointConfig? MinPoint { get; set; }
	public PointConfig? MaxPoint { get; set; }
	public WarpDestinationConfig? Destination { get; set; }
}

public sealed class PointConfig
{
	public int X { get; set; }
	public int Y { get; set; }
}

public sealed class WarpDestinationConfig
{
	public int MapId { get; set; }
	public int ZoneId { get; set; }
	public WarpPositionWrapper? Position { get; set; }
}

public sealed class WarpPositionWrapper
{
	public PointConfig? Position { get; set; }
	public int Direction { get; set; }
}