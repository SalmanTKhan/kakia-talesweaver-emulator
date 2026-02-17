using Kakia.TW.Shared.Network;
using Kakia.TW.Shared.World;
using Kakia.TW.World.Entities;
using Kakia.TW.World.Scripting;
using System;
using System.Threading.Tasks;
using Yggdrasil.Logging;

namespace Kakia.TW.World.Network
{
	public class PacketHandler : PacketHandler<WorldConnection>
	{
		[PacketHandler(Op.Handshake)]
		public void Handshake(WorldConnection conn, Packet packet)
		{
			Send.Handshake(conn);
			conn.Framer.IsCryptoSet = true;
			conn.Framer.EncryptOutput = true;
		}

		[PacketHandler(Op.InitConnectRequest)]
		public void InitConnectRequest(WorldConnection conn, Packet packet)
		{
			// Client sends this after connecting post-redirect, before ReconnectRequest.
			// Just acknowledge it exists to prevent spam/crash.
			Log.Debug("World: Client sent InitConnectRequest (pre-reconnect handshake).");
		}

		[PacketHandler(Op.Heartbeat)] // 0x24
		public void Heartbeat(WorldConnection conn, Packet packet)
		{
			// Client sends this periodically.
			// Server can acknowledge it (Op.Acknowledge or Pong) or just ignore it 
			// as the TCP layer handles keep-alive.
			// Handling it here stops the "No handler" warning log.
		}

		[PacketHandler(Op.DebugSourceLineRequest)] // 0x7C (Debug Source Line from Client)
		public void DebugSourceLine(WorldConnection conn, Packet packet)
		{
			// Clients sends 0x7C with a byte (01) or string sometimes for debug info.
			// Just consume it to clear logs.
		}

		/// <summary>
		/// Handles reconnection from Lobby Server (Redirect).
		/// </summary>
		[PacketHandler(Op.ReconnectRequest)]
		public void ClientReconnectRequest(WorldConnection conn, Packet packet)
		{
			// Packet Structure: [Op:1] [Seed:4] [HWID_Len:1] [HWID:String] [User_Len:1] [Username:String] ...
			uint seed = packet.GetUInt();
			string hwid = packet.GetString(packet.GetByte());
			string id = packet.GetString(packet.GetByte());
			string nexonId = packet.GetString(packet.GetByte());
			string username = packet.GetString(packet.GetByte());

			Log.Info($"Lobby: Client reconnected. User: {username}, Seed: {seed:X8}");

			// 1. Verify user via DB (gets Account ID etc)
			int accountId = WorldServer.Instance.Database.VerifySession(username);
			if (accountId <= 0)
			{
				Log.Warning($"Lobby: Reconnect failed. Invalid session for '{username}'");
				conn.Close();
				return;
			}

			conn.Username = username;
			conn.Account = WorldServer.Instance.Database.GetAccountById(accountId);
			conn.Seed = seed;

			conn.Framer.Codec.Initialize(seed);
			conn.Framer.IsCryptoSet = true;
			conn.Framer.EncryptOutput = true;

			// 2. Get selected character name from session
			string? selectedCharName = WorldServer.Instance.Database.GetSelectedCharacterName(accountId);
			if (string.IsNullOrEmpty(selectedCharName))
			{
				Log.Error($"World: No character selected for account '{username}'.");
				conn.Close();
				return;
			}

			// 3. Load User Data from Database
			var user = WorldServer.Instance.Database.LoadCharacter(accountId, selectedCharName);

			if (user == null)
			{
				Log.Error($"World: Failed to load character '{selectedCharName}' for '{username}'.");
				conn.Close();
				return;
			}

			// Assign player to connection
			conn.Player = new Player(conn, user);

			// 4. Send World Entry Packets
			Send.Connected(conn); // 0x7E

			// Use the map position from the database (includes MapId, ZoneId, X, Y)
			ushort mapId = user.MapId;
			ushort zoneId = user.ZoneId;

			// Default to Narvik starter area if not set
			if (mapId == 0 || zoneId == 0)
			{
				mapId = 6;
				zoneId = 38656;
				user.X = 305;
				user.Y = 220;
			}

			Log.Info($"World: Spawning '{selectedCharName}' at Map {mapId}-{zoneId} ({user.X},{user.Y})");

			Send.MapChange(conn, mapId, zoneId); // 0x15 Map Packet

			// 6. Add Player to Map Manager (assigns map ObjectId)
			var map = WorldServer.Instance.World.Maps.GetOrCreateMap(mapId, zoneId);
			if (map != null)
			{
				map.Enter(conn.Player);

				// 5. Spawn the user (0x33 subtype 0x00)
				Send.SpawnUser(conn, conn.Player.ObjectId, user, isSelf: true);

				//Send.StatUpdateFull(conn, user);
				//Send.StatUpdateHardcoded(conn);
				//Send.StatUpdateDecoded(conn, user);
				//Send.StatUpdateFull(conn, user);
				Send.StatUpdate(conn, user);
				Send.InitSkills(conn);

				// 7. Send InitObjectId (0x33 subtype 0x01) using map-assigned ObjectId
				Send.InitObjectId(conn, conn.Player.ObjectId);
				Send.CurrentTime(conn);
				Send.EnvironmentalMana(conn);

				// 8. Finished loading
				Send.LoadCompleteAck(conn);
			}
			else
			{
				Log.Warning($"Map {mapId}-{zoneId} not found. Player floating in void.");
			}
		}

		[PacketHandler(Op.ChatRequest)] // 0x0E
		public void Chat(WorldConnection conn, Packet packet)
		{
			// Packet structure: [SubOp:1] [CharId:4 - optional?] [MsgLen:1] [Msg:N]
			byte subType = packet.GetByte();
			string message = packet.GetString(packet.GetByte());

			if (conn.Player == null) return;

			// Try to execute message as a chat command, don't send if it
			// was handled as one
			if (WorldServer.Instance.ChatCommands.TryExecute(conn.Player, message))
				return;

			Log.Info($"Chat [{conn.Username}]: {message}");

			// Broadcast chat to map
			if (conn.Player.Instance != null)
			{
				Send.ChatBroadcast(conn.Player.Instance, conn.Player.ObjectId, message);
			}
		}

		[PacketHandler(Op.MovementRequest)] // 0x33
		public void MovementRequest(WorldConnection conn, Packet packet)
		{
			if (conn.Player == null) return;

			// Packet: [Flag:1] [Type:1] [X:2] [Y:2] [Dir:1 (Optional)]
			byte flag = packet.GetByte();

			if (flag == 0x00) // InitialRequest — do NOT check portal (player hasn't walked there yet)
			{
				byte moveType = packet.GetByte();
				ushort x = packet.GetUShort();
				ushort y = packet.GetUShort();
				byte dir = packet.Length > 7 ? packet.GetByte() : (byte)conn.Player.Direction;
				conn.Player.Movement.StartMoving(moveType, x, y, dir);
			}
			else if (flag == 0x01) // Continuation — position sync, check portal
			{
				if (packet.Length >= 5)
					conn.Player.Movement.UpdatePosition(packet.GetUShort(), packet.GetUShort());
			}
		}

		// 0x43 - EntityClickRequest
		[PacketHandler(Op.EntityClickRequest)]
		public void ClickedEntityRequest(WorldConnection conn, Packet packet)
		{
			uint objectId = packet.GetUInt();

			if (conn.Player?.Instance == null) return;

			// Send click acknowledgment
			Send.EntityClickAck(conn, objectId);

			// Unified entity lookup by ObjectId
			if (!conn.Player.Instance.TryGetEntity(objectId, out var entity))
			{
				Log.Debug($"Player {conn.Username} clicked unknown entity (ObjectId: {objectId})");
				return;
			}

			switch (entity)
			{
				case Npc npc:
					Log.Debug($"Player {conn.Username} clicked NPC '{npc.Name}' (ObjectId: {objectId})");

					// Send pre-dialog packet sequence (from legacy ClickedEntityHandler)
					Send.EntityFocus(conn, objectId);
					Send.InteractionConfirm(conn);
					Send.InteractionTimer(conn);
					Send.EntityClickAck(conn, objectId); // Sent again per legacy
					Send.EntityInteraction(conn, objectId);

					// Start Dialog
					var dialog = new Dialog(conn, npc);
					conn.CurrentDialog = dialog;

					if (npc.Script == null)
					{
						// Assign a test dialog script
						npc.Script = async (dialog) =>
						{
							// Visual novel messages - sent all at once, client handles pacing
							dialog.Message("Hello there, adventurer!");
							dialog.Message("Welcome to the Kakia TalesWeaver Private Server.\nI'm here to test the dialog system.");
							dialog.Close(); // Close visual novel before showing menu

							// In-game select menu - requires await for user input
							var choice = await dialog.Select("What would you like to do?",
								"Tell me about this server",
								"Show me your dance moves",
								"Give me some gold",
								"Goodbye");

							switch (choice)
							{
								case 0: // Tell me about this server
									dialog.Message("This server is being built from scratch!");
									dialog.Message("The dialog system uses async/await\nfor smooth conversation flow.");
									dialog.Message("Pretty cool, right?");
									break;

								case 1: // Dance moves
									dialog.Message("*does a little dance*");
									dialog.Message("Ta-da! Not bad for an NPC, huh?");
									break;

								case 2: // Gold
									dialog.Message("Ha! You wish!");
									dialog.Message("Maybe in a future update...");
									break;

								case 3: // Goodbye
									dialog.Message("Safe travels, adventurer!");
									break;
							}

							dialog.End(); // Close visual novel and end dialog session
						};
					}

					// Run script async (fire and forget from handler perspective)
					_ = Task.Run(async () =>
					{
						try
						{
							await npc.Script(dialog);
						}
						catch (Exception ex)
						{
							Log.Error($"NPC Script error: {ex.Message}");
							dialog.Close();
						}
					});
					break;

				case Monster monster:
					Log.Debug($"Player {conn.Username} clicked Monster '{monster.Name}' (ObjectId: {objectId})");
					// TODO: Handle monster targeting/combat
					break;

				case Player otherPlayer:
					Log.Debug($"Player {conn.Username} clicked Player '{otherPlayer.Data.Name}' (ObjectId: {objectId})");
					// TODO: Handle player interaction
					break;

				default:
					Log.Debug($"Player {conn.Username} clicked entity type {entity.GetType().Name} (ObjectId: {objectId})");
					break;
			}
		}

		[PacketHandler(Op.NpcDialogAnswerRequest)]
		public void NpcDialogAnswerRequest(WorldConnection conn, Packet packet)
		{
			if (conn.CurrentDialog == null) return;

			// Packet structure from legacy:
			// [flag1:1] [flag2:1] [dialogId:8 BE] [padding:2?] [selectedOption:1]
			byte flag1 = packet.GetByte();
			byte flag2 = packet.GetByte();

			// flag2 == 5 means the dialog window was closed
			if (flag2 == 5)
			{
				Log.Debug("Dialog closed by player");
				conn.CurrentDialog.Close();
				return;
			}

			// For "Next" button presses (simple message dialogs)
			if (flag2 == 0 || flag1 == 0)
			{
				conn.CurrentDialog.Resume("next");
				return;
			}

			// For menu selections, read the dialog ID and selected option
			if (packet.Length >= 8)
			{
				// Dialog ID (8 bytes, Big Endian) - used to track which dialog we're responding to
				ulong dialogId = packet.GetULongBE();

				// Skip 2 bytes padding and read option index
				if (packet.Length >= 2)
				{
					packet.GetUShort(); // padding
					byte selectedOption = packet.GetByte();

					Log.Debug($"Dialog answer: DialogId={dialogId}, Option={selectedOption}");
					conn.CurrentDialog.Resume(selectedOption.ToString());
				}
				else
				{
					conn.CurrentDialog.Resume("0");
				}
			}
			else
			{
				// Fallback for simple Next responses
				conn.CurrentDialog.Resume("next");
			}
		}

		[PacketHandler(Op.StatIncreaseRequest)] // 0x0A
		public void StatIncrease(WorldConnection conn, Packet packet)
		{
			// Packet structure: [StatType:1]
			// StatType: 1=Stab, 2=Hack, 3=Int, 4=Def, 5=MR, 6=Dex, 7=Agi
			var statType = (StatType)packet.GetByte();

			if (conn.Player == null) return;

			var data = conn.Player.Data;

			// Check if player has stat points available
			if (data.StatPoints <= 0)
			{
				Log.Debug($"Player {conn.Username} tried to increase stat but has no stat points");
				return;
			}

			// Increase the appropriate stat and get the new value
			int newValue;
			switch (statType)
			{
				case StatType.Stab: newValue = ++data.StatStab; break;
				case StatType.Hack: newValue = ++data.StatHack; break;
				case StatType.Int: newValue = ++data.StatInt; break;
				case StatType.Def: newValue = ++data.StatDef; break;
				case StatType.MR: newValue = ++data.StatMR; break;
				case StatType.Dex: newValue = ++data.StatDex; break;
				case StatType.Agi: newValue = ++data.StatAgi; break;
				default:
					Log.Warning($"Unknown stat type: {statType}");
					return;
			}

			data.StatPoints--;

			Log.Debug($"Player {conn.Username} increased stat {statType} to {newValue}, remaining points: {data.StatPoints}");

			// Send stat update packet to client
			Send.StatUpdate(conn, conn.Player.Data);
		}

		[PacketHandler(Op.DirectionUpdateRequest)] // 0x11
		public void UpdateDirection(WorldConnection conn, Packet packet)
		{
			byte direction = packet.GetByte();

			if (conn.Player == null) return;

			conn.Player.Direction = (Direction)direction;
			conn.Player.Data.Direction = (Direction)direction;

			// Broadcast to other players on the map
			if (conn.Player.Instance != null)
			{
				Send.DirectionUpdate(conn.Player.Instance, conn.Player.ObjectId, direction, conn.Player);
			}
		}

		[PacketHandler(Op.AttackRequest)] // 0x13
		public void Attack(WorldConnection conn, Packet packet)
		{
			if (conn.Player == null) return;

			// Basic attack acknowledgment
			// Legacy: 4A 00 00 00 00 00 00, 17 00, then attack result

			Send.AttackAck(conn);
			Send.AttackResult(conn, conn.Player.ObjectId);
		}

		[PacketHandler(Op.AttackStart)] // 0xB4
		public void AttackStart(WorldConnection conn, Packet packet)
		{
			if (conn.Player == null) return;

			uint targetId = 0;
			if (packet.Length >= 10)
			{
				packet.GetBytes(6);
				targetId = packet.GetUInt();
			}
			else if (packet.Length >= 5)
			{
				packet.GetByte();
				targetId = packet.GetUInt();
			}

			Log.Debug($"AttackStartRequest: targetId={targetId}");

			if (targetId != 0)
			{
				Send.AttackTarget(conn, targetId);
				return;
			}

			Send.AttackAck(conn);
			Send.AttackResult(conn, conn.Player.ObjectId);
		}

		[PacketHandler(Op.TargetEntityRequest)] // 0x59
		public void TargetEntity(WorldConnection conn, Packet packet)
		{
			if (conn.Player == null) return;
			if (packet.Length < 5) return;

			byte subType = packet.GetByte();
			uint entityId = packet.GetUInt();
			Log.Debug($"TargetEntityRequest: sub=0x{subType:X2}, entityId={entityId}");

			Send.TargetEntity(conn, entityId);
		}

		[PacketHandler(Op.SetPoseRequest)] // 0x32
		public void SetPose(WorldConnection conn, Packet packet)
		{
			byte pose = packet.GetByte(); // 0 = stand, 1 = sit

			if (conn.Player == null) return;

			Log.Debug($"Player {conn.Username} changed pose to {(pose == 0 ? "stand" : "sit")}");

			// Broadcast pose change to all players including self
			if (conn.Player.Instance != null)
			{
				Send.PoseUpdate(conn.Player.Instance, conn.Player.ObjectId, pose);
			}
		}

		[PacketHandler(Op.CharacterInfoUpdateRequest)] // 0x2A
		public void UpdateCharacterInfo(WorldConnection conn, Packet packet)
		{
			// This is sent when character is leaving the map/logging out
			if (conn.Player == null) return;

			Log.Info($"Saving character data for {conn.Username}");

			// Save character to database
			conn.Player.Save();

			// Broadcast despawn to other players
			if (conn.Player.Instance != null)
			{
				Send.EntityDespawn(conn.Player.Instance, conn.Player.ObjectId, conn.Player);
			}
		}

		// Stub Handlers for logs to prevent warnings
		[PacketHandler(
			Op.Unknown05Response,
			Op.TriggerRequest,
			Op.Unknown21Request,
			Op.UiActionRequest,
			Op.Unknown2ERequest,
			Op.Unknown39Request,
			Op.Unknown3DRequest,
			Op.FriendDialogResponse,
			Op.Unknown45Request,
			Op.Unknown51Request,
			Op.Unknown55Request,
			Op.Unknown5FRequest,
			Op.Unknown60Request,
			Op.Unknown63Request,
			Op.Unknown6ARequest,
			Op.Unknown77Request)]
		public void IgnoredPackets(WorldConnection conn, Packet packet)
		{
			// These packets are currently ignored to prevent console spam
		}
	}
}
