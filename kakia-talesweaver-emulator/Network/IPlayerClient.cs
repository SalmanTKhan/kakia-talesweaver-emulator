using kakia_talesweaver_emulator.Models;
using kakia_talesweaver_packets.Models;

namespace kakia_talesweaver_emulator.Network;

public interface IPlayerClient
{
	Task<bool> Send(byte[] packet, CancellationToken token);
	void Broadcast(byte[] packet, bool includeSelf = true, bool sameMap = true);
	void UpdateCryptoSeed(uint seed);
	ServerType GetServerType();
	PlayerCharacter GetCharacter();
	void SetCharacter(string? name = null);
	public SessionInfo? GetSessionInfo();
	public bool RemoveSessionInfo();
	TalesServer GetServer();
	void LoadMap(MapInfo map, bool sendEffect, ObjectPos? spawnPos = null, CancellationToken ct = default);

	public bool InMap(MapId? map);
	public MapId? GetCurrentMap();
}
