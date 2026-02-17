using Kakia.TW.Shared.Data;
using Kakia.TW.Shared.Network;
using Kakia.TW.Shared.World;
using Kakia.TW.World.Entities;
using Kakia.TW.World.Network;
using Kakia.TW.World.Scripting;
using System;
using System.IO;
using System.Text;
using Yggdrasil.Logging;
using Yggdrasil.Network.Communication;
using Yggdrasil.Util;
using Yggdrasil.Util.Commands;

namespace Kakia.TW.World.Commands
{
	/// <summary>
	/// Chat command handlers for TalesWeaver.
	/// </summary>
	public partial class ChatCommands
	{
		/// <summary>
		/// Creates a new instance of ChatCommands and registers all commands.
		/// </summary>
		public ChatCommands()
		{
			// Help commands
			this.Add("help", "[command]", "Displays help for available commands.", this.HandleHelp);
			this.Add("commands", "", "Alias for help.", this.HandleHelp);

			// Player information
			this.Add("where", "", "Displays your current position.", this.HandleWhere);
			this.Add("pos", "", "Alias for where.", this.HandleWhere);

			// Warping
			this.Add("warp", "<mapId> <zoneId> [x] [y]", "Warps to the specified map and zone.", this.HandleWarp);
			this.Add("goto", "<mapId> <zoneId> [x] [y]", "Alias for warp.", this.HandleWarp);

			// Map management
			this.Add("listmaps", "", "Shows all loaded map IDs.", this.HandleListMaps);

			// Character refresh
			this.Add("refresh", "", "Respawns your character.", this.HandleRefresh);

			// Effects
			this.Add("chareffect", "<effect id>", "Plays a character effect (1=levelup, 2=maxlevel).", this.HandleCharEffect);
			this.Add("effect", "<effect id>", "Alias for chareffect.", this.HandleCharEffect);

			// Stats
			this.Add("levelup", "[levels]", "Increases your level.", this.HandleLevelUp);
			this.Add("stat", "", "Shows your current stats.", this.HandleStat);
			this.Add("setstat", "<stat> <value>", "Sets a stat value (stab/hack/int/def/mr/dex/agi).", this.HandleSetStat);
			this.Add("addstatpoints", "<points>", "Adds stat points.", this.HandleAddStatPoints);

			// Player management
			this.Add("who", "", "Shows the number of players online.", this.HandleWho);

			// Save
			this.Add("save", "", "Saves your character data.", this.HandleSave);

			// Item/Monster Database Commands
			this.Add("iteminfo", "<id|name>", "Shows item database info.", this.HandleItemInfo);
			this.Add("monsterinfo", "<id|name>", "Shows monster database info.", this.HandleMonsterInfo);
			this.Add("searchitem", "<name>", "Searches items by name.", this.HandleSearchItem);
			this.Add("searchmonster", "<name>", "Searches monsters by name.", this.HandleSearchMonster);
			this.Add("spawnmonster", "<id>", "Spawns a monster at your location.", this.HandleSpawnMonster);

			// Dev
			this.Add("test", "", "", this.HandleTest);
			this.Add("testdialog", "", "Test hardcoded dialog packet", this.HandleTestDialog);
			this.Add("testdialogmenu", "", "Test hardcoded dialog packet", this.HandleTestDialogMenu);
			this.Add("testdialogre", "", "Test RE-accurate dialog commands", this.HandleTestDialogRE);
			this.Add("testsysmsg", "<message>", "Test system message (0x05 0x03)", this.HandleTestSysMsg);
			this.Add("testemote", "<emoteId>", "Test emoticon on self (0x05 0x06)", this.HandleTestEmote);
			this.Add("testbubble", "<message>", "Test bubble talk (0x05 0x07)", this.HandleTestBubble);
			this.Add("testwait", "[ms]", "Test dialog wait (0x05 0x05)", this.HandleTestWait);
			this.Add("testnoveltalk", "", "Test novel talk with params (0x05 0x02)", this.HandleTestNovelTalk);
			this.Add("testchoices", "", "Test RE-style menu (0x05 0x04)", this.HandleTestChoices);
			this.Add("weather", "<off|rain|snow> [speed] [direction] [intensity]", "Sends 0x1F 0x04 environmental weather.", this.HandleWeather);
			this.Add("fog", "<on|off>", "Sends 0x1F 0x04 fog toggle.", this.HandleFog);

			this.AddAlias("testdialog", "td");
			this.AddAlias("testdialogmenu", "tdm");
			this.AddAlias("testdialogre", "tdre");
		}

		private CommandResult HandleTest(Player sender, Player target, string message, string commandName, Arguments args)
		{
			// 2203235
			var npc = new Npc("TestNPC", 2200004);
			npc.Position = sender.Position;
			npc.Direction = (Direction)RandomProvider.Get().Next(8);

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

			sender.Instance.AddNpc(npc, true);
			Send.SpawnNpc(sender.Connection, npc);

			return CommandResult.Okay;
		}

		/// <summary>
		/// Test hardcoded dialog packet - exact copy from legacy ClickedEntityHandler
		/// </summary>
		private CommandResult HandleTestDialog(Player sender, Player target, string message, string commandName, Arguments args)
		{
			var conn = sender.Connection;

			var packet = new Packet(Op.FriendDialogResponse);
			packet.PutByte((byte)DialogActionType.Dialog);
			packet.PutByte((byte)0);
			packet.PutByte((byte)0);
			packet.PutByte((byte)1);
			packet.PutByte((byte)0);
			packet.PutBinFromHex("44 05 00 00 01 00");
			conn.Send(packet);

			packet = new Packet(Op.FriendDialogResponse);

			packet.PutByte((byte)DialogActionType.Dialog);
			packet.PutByte((byte)DialogOptionType.HasOptions);

			// Dialog Portrait Model Id
			// 2200000 2201538 (Check where model ids start).

			packet.PutUInt(_modelId++);
			packet.PutInt(0);
			packet.PutString("This is a test message.", false);
			packet.PutByte((byte)0x01); // Unknown

			conn.Send(packet);

			packet = new Packet(Op.FriendDialogResponse);
			packet.PutBinFromHex(@"05 02 00 21 97 C2 00 00 00 00 9A 83 4A 83 8C
				83 93 82 CC 94 AF 82 CC 96 D1 82 AA 82 A0 82 DC
				82 E8 82 C9 82 E0 8B 43 82 C9 82 C8 82 C1 82 C4
				81 42 3C 2F 6E 3E 82 A0 82 EA 82 F0 8C A9 82 C4
				81 41 8E E8 93 FC 82 EA 82 F0 82 B5 82 C4 82 A2
				82 C8 82 A2 82 B5 81 41 8A AE 91 53 82 C9 8C A2
				96 D1 82 B6 82 E1 82 C8 82 A2 82 CC 81 48 82 A0
				82 C8 82 BD 82 E0 94 AF 82 CC 96 D1 82 CC 8E E8
				93 FC 82 EA 82 CD 82 B5 82 C1 82 A9 82 E8 82 C6
				82 B5 82 C4 82 A8 82 A2 82 BD 95 FB 82 AA 97 C7
				82 A2 82 E6 81 42");
			conn.Send(packet);

			packet = new Packet(Op.FriendDialogResponse);
			packet.PutBinFromHex("05 05 03 C0 68 24 03 C0 71 9B");

			conn.Send(packet);

			packet = new Packet(Op.FriendDialogResponse);
			packet.PutBinFromHex("05 01");

			conn.Send(packet);

			return CommandResult.Okay;
		}

		private static uint _modelId = 2200002;
		/// <summary>
		/// Test hardcoded dialog packet - exact copy from legacy ClickedEntityHandler
		/// </summary>
		private CommandResult HandleTestDialogMenu(Player sender, Player target, string message, string commandName, Arguments args)
		{
			var conn = sender.Connection;

			var packet = new Packet(Op.FriendDialogResponse);

			packet.PutByte((byte)DialogActionType.DialogSelectMenu);
			packet.PutByte((byte)DialogOptionType.HasOptions);

			// Dialog Answer ID?
			packet.PutULong(1);

			// Dialog Portrait Model Id
			// 2200000 2201538 (Check where model ids start).

			packet.PutUInt(_modelId++);
			packet.PutByte(0x00);
			packet.PutString("This is a test message.", false);

			var optionCounts = 4;
			// Options
			packet.PutByte((byte)optionCounts); // Option count
			packet.PutByte((byte)0x00); // Unknown

			string[] options = {
				"Let try the next one.",
				"Let me try the previous one.",
				"Turn me back to normal.",
				"Nevermind."
			};

			for (var i = 0; i < optionCounts; i++)
			{
				//byte[] optBytes = System.Text.Encoding.GetEncoding("shift-jis").GetBytes(opt);
				packet.PutString(options[i]);
			}

			conn.Send(packet);
			/**
			// Exact bytes from legacy Outfitter dialog (0x44 0x04 0x02 format)
			// This is the menu dialog with portrait
			using var ms = new MemoryStream();
			using var writer = new BinaryWriter(ms);

			writer.Write((byte)0x44);  // Opcode
			writer.Write((byte)0x04);  // Subtype: Menu dialog
			writer.Write((byte)0x02);  // With options

			// Dialog Answer ID (8 bytes, written as ulong)
			ulong dialogAnswerId = 1;
			writer.Write(dialogAnswerId);

			// Model ID for portrait (4 bytes)
			uint modelId = 2201600; // 0x002197C0
			writer.Write(modelId);

			writer.Write((byte)0x00); // Unknown

			// Message
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
			string msg = "Dress to impress!\nWant to try out a new outfit?\nPerhaps add some flair?";
			byte[] msgBytes = Encoding.GetEncoding("shift-jis").GetBytes(msg);
			writer.Write((byte)msgBytes.Length);
			writer.Write(msgBytes);

			// Options
			writer.Write((byte)0x04); // Option count
			writer.Write((byte)0x00); // Unknown

			string[] options = {
				"Let try the next one.",
				"Let me try the previous one.",
				"Turn me back to normal.",
				"Nevermind."
			};

			foreach (var opt in options)
			{
				byte[] optBytes = System.Text.Encoding.GetEncoding("shift-jis").GetBytes(opt);
				writer.Write((byte)optBytes.Length);
				writer.Write(optBytes);
				writer.Write((byte)0x00); // Null terminator
			}

			// Send raw bytes with proper framing
			byte[] packetBytes = ms.ToArray();
			Msg(sender, $"Sending hardcoded dialog packet ({packet.Length} bytes)");
			Msg(sender, $"Hex: {BitConverter.ToString(packetBytes).Replace("-", " ")}");

			conn.SendRaw(packetBytes);
			**/

			return CommandResult.Okay;
		}

		/// <summary>
		/// Test RE-accurate dialog commands - spawns NPC with new dialog methods.
		/// </summary>
		private CommandResult HandleTestDialogRE(Player sender, Player target, string message, string commandName, Arguments args)
		{
			var npc = new Npc("RE Test NPC", 2200004);
			npc.Position = sender.Position;
			npc.Direction = (Direction)RandomProvider.Get().Next(8);

			npc.Script = async (dialog) =>
			{
				// Test system message
				dialog.SystemMsg("System: Testing RE dialog commands...");

				// Test visual novel with init
				dialog.Message("This is a normal Message().");
				dialog.NovelTalk("This is NovelTalk() with position=0, anim=0, effect=0.", 0, 0, 0);
				dialog.Close();

				// Test RE-style menu
				var choice = await dialog.SelectRE("Which test do you want to run?",
					"Test Emoticon",
					"Test Bubble Talk",
					"Test Wait",
					"Cancel");

				switch (choice)
				{
					case 0: // Emoticon
						dialog.Message("Watch the NPC emote!");
						dialog.Emoticon(1); // Emote ID 1 on NPC
						break;

					case 1: // Bubble
						dialog.Bubble("This is a bubble message, not visual novel!");
						break;

					case 2: // Wait
						dialog.Message("Waiting 2 seconds...");
						dialog.Wait(2000);
						dialog.Message("Done waiting!");
						break;

					case 3: // Cancel
						dialog.Message("Okay, goodbye!");
						break;
				}

				dialog.End();
			};

			sender.Instance.AddNpc(npc, true);
			Send.SpawnNpc(sender.Connection, npc);
			Msg(sender, "Spawned RE Test NPC. Click it to test new dialog commands.");

			return CommandResult.Okay;
		}

		/// <summary>
		/// Test system message (0x05 0x03).
		/// </summary>
		private CommandResult HandleTestSysMsg(Player sender, Player target, string message, string commandName, Arguments args)
		{
			var text = args.Count > 0 ? string.Join(" ", args.GetAll()) : "Test system message!";
			Send.NpcSystemMsg(sender.Connection, text);
			Msg(sender, $"Sent NpcSystemMsg: {text}");
			return CommandResult.Okay;
		}

		/// <summary>
		/// Test emoticon (0x05 0x06).
		/// </summary>
		private CommandResult HandleTestEmote(Player sender, Player target, string message, string commandName, Arguments args)
		{
			short emoteId = 1;
			if (args.Count > 0 && short.TryParse(args.Get(0), out var id))
				emoteId = id;

			Send.NpcEmoticon(sender.Connection, target.ObjectId, emoteId);
			Msg(sender, $"Sent NpcEmoticon: targetId={target.ObjectId}, emoteId={emoteId}");
			return CommandResult.Okay;
		}

		/// <summary>
		/// Test bubble talk (0x05 0x07).
		/// </summary>
		private CommandResult HandleTestBubble(Player sender, Player target, string message, string commandName, Arguments args)
		{
			var text = args.Count > 0 ? string.Join(" ", args.GetAll()) : "Hello! This is a bubble message!";
			Send.NpcBubbleTalk(sender.Connection, target.ObjectId, 0, 0, 0, 0, text);
			Msg(sender, $"Sent NpcBubbleTalk: {text}");
			return CommandResult.Okay;
		}

		/// <summary>
		/// Test dialog wait (0x05 0x05).
		/// </summary>
		private CommandResult HandleTestWait(Player sender, Player target, string message, string commandName, Arguments args)
		{
			int ms = 2000;
			if (args.Count > 0 && int.TryParse(args.Get(0), out var duration))
				ms = duration;

			Send.NpcDialogWait(sender.Connection, ms);
			Msg(sender, $"Sent NpcDialogWait: {ms}ms");
			return CommandResult.Okay;
		}

		/// <summary>
		/// Test novel talk with full parameters (0x05 0x02).
		/// </summary>
		private CommandResult HandleTestNovelTalk(Player sender, Player target, string message, string commandName, Arguments args)
		{
			// Init dialog first
			Send.NpcDialogInit(sender.Connection);

			// Send novel talk with different positions
			Send.NpcNovelTalk(sender.Connection, 2200004, 0, 0, 0, "Position 0, Animation 0, Effect 0");
			Send.NpcNovelTalk(sender.Connection, 2200004, 1, 1, 1, "Position 1, Animation 1, Effect 1");
			Send.NpcNovelTalk(sender.Connection, 2200004, 2, 2, 2, "Position 2, Animation 2, Effect 2");

			// Close dialog
			Send.NpcDialogClose(sender.Connection, 0);

			Msg(sender, "Sent 3 NpcNovelTalk packets with different params.");
			return CommandResult.Okay;
		}

		/// <summary>
		/// Test RE-style menu (0x05 0x04).
		/// </summary>
		private CommandResult HandleTestChoices(Player sender, Player target, string message, string commandName, Arguments args)
		{
			Send.NpcChoices(sender.Connection, 1, 0, 0, "RE-Style Menu Test\nPick an option:", new[] {
				"Option A",
				"Option B",
				"Option C",
				"Cancel"
			});
			Msg(sender, "Sent NpcChoices (0x05 0x04). Check if menu appears.");
			return CommandResult.Okay;
		}

		private CommandResult HandleWeather(Player sender, Player target, string message, string commandName, Arguments args)
		{
			if (sender.Instance == null)
				return CommandResult.Fail;

			if (args.Count < 1)
			{
				Msg(sender, "Usage: >weather <off|rain|snow> [speed] [direction] [intensity]");
				return CommandResult.InvalidArgument;
			}

			var mode = args.Get(0).ToLowerInvariant();
			byte type = mode switch
			{
				"off" => 0,
				"rain" => 2,
				"snow" => 3,
				_ => 0xFF,
			};

			if (type == 0xFF)
			{
				Msg(sender, "Mode must be one of: off, rain, snow");
				return CommandResult.InvalidArgument;
			}

			byte speed = 1;
			byte direction = 1;
			byte intensity = type == 0 ? (byte)0 : (byte)1;

			if (args.Count > 1 && byte.TryParse(args.Get(1), out var parsedSpeed))
				speed = parsedSpeed;
			if (args.Count > 2 && byte.TryParse(args.Get(2), out var parsedDirection))
				direction = parsedDirection;
			if (args.Count > 3 && byte.TryParse(args.Get(3), out var parsedIntensity))
				intensity = parsedIntensity;

			Send.Environment(sender.Instance, type, speed, direction, intensity);
			Msg(sender, $"Environment sent: type={type}, speed={speed}, direction={direction}, intensity={intensity}");

			return CommandResult.Okay;
		}

		private CommandResult HandleFog(Player sender, Player target, string message, string commandName, Arguments args)
		{
			if (sender.Instance == null)
				return CommandResult.Fail;

			if (args.Count < 1)
			{
				Msg(sender, "Usage: >fog <on|off>");
				return CommandResult.InvalidArgument;
			}

			var on = args.Get(0).Equals("on", StringComparison.OrdinalIgnoreCase);
			if (!on && !args.Get(0).Equals("off", StringComparison.OrdinalIgnoreCase))
			{
				Msg(sender, "Usage: >fog <on|off>");
				return CommandResult.InvalidArgument;
			}

			// Matches observed packets:
			// on  = 1F 04 01 01 01 01
			// off = 1F 04 00 00 00 00
			if (on)
				Send.Environment(sender.Instance, 1, 1, 1, 1);
			else
				Send.Environment(sender.Instance, 0, 0, 0, 0);

			Msg(sender, $"Fog {(on ? "enabled" : "disabled")}.");
			return CommandResult.Okay;
		}

		/// <summary>
		/// Sends a server message to the player.
		/// </summary>
		private void Msg(Player player, string message)
		{
			if (player.Connection != null)
				Send.Chat(player.Connection, 0, $"[Server] {message}");
		}

		/// <summary>
		/// Displays available commands or help for a specific command.
		/// </summary>
		private CommandResult HandleHelp(Player sender, Player target, string message, string commandName, Arguments args)
		{
			if (args.Count == 0)
			{
				Msg(sender, "Available commands:");
				foreach (var cmd in _commands.Values.Distinct())
				{
					Msg(sender, $"  >{cmd.Name} {cmd.Usage}");
				}
				return CommandResult.Okay;
			}

			var cmdName = args.Get(0);
			var command = this.GetCommand(cmdName);
			if (command == null)
			{
				Msg(sender, $"Unknown command: {cmdName}");
				return CommandResult.Okay;
			}

			Msg(sender, $">{command.Name} {command.Usage}");
			Msg(sender, $"  {command.Description}");
			return CommandResult.Okay;
		}

		/// <summary>
		/// Displays the player's current position.
		/// </summary>
		private CommandResult HandleWhere(Player sender, Player target, string message, string commandName, Arguments args)
		{
			var mapId = target.Data.MapId;
			var zoneId = target.Data.ZoneId;
			var x = target.Position.X;
			var y = target.Position.Y;
			var dir = target.Direction;

			Msg(sender, $"Map: {mapId}-{zoneId}, Position: ({x}, {y}), Direction: {dir}");
			return CommandResult.Okay;
		}

		/// <summary>
		/// Warps the player to a specified location.
		/// </summary>
		private CommandResult HandleWarp(Player sender, Player target, string message, string commandName, Arguments args)
		{
			if (args.Count < 2)
			{
				Msg(sender, "Usage: >warp <mapId> <zoneId> [x] [y]");
				return CommandResult.InvalidArgument;
			}

			if (!ushort.TryParse(args.Get(0), out var mapId))
			{
				Msg(sender, "Invalid map ID.");
				return CommandResult.InvalidArgument;
			}

			if (!ushort.TryParse(args.Get(1), out var zoneId))
			{
				Msg(sender, "Invalid zone ID.");
				return CommandResult.InvalidArgument;
			}

			// Default coordinates
			ushort x = 100, y = 100;

			if (args.Count >= 3 && ushort.TryParse(args.Get(2), out var parsedX))
				x = parsedX;

			if (args.Count >= 4 && ushort.TryParse(args.Get(3), out var parsedY))
				y = parsedY;

			Log.Info($"Player {target.Data.Name} warping to Map {mapId}-{zoneId} ({x}, {y})");
			target.Warp(mapId, zoneId, x, y);
			Msg(sender, $"Warped to Map {mapId}-{zoneId} at ({x}, {y})");

			return CommandResult.Okay;
		}

		/// <summary>
		/// Shows all loaded map IDs.
		/// </summary>
		private CommandResult HandleListMaps(Player sender, Player target, string message, string commandName, Arguments args)
		{
			var mapList = string.Join(", ", WorldServer.Instance.World.Maps.GetLoadedMapIds());
			Msg(sender, $"Loaded Maps: {mapList}");
			return CommandResult.Okay;
		}

		/// <summary>
		/// Respawns the player's character.
		/// </summary>
		private CommandResult HandleRefresh(Player sender, Player target, string message, string commandName, Arguments args)
		{
			if (target.Connection != null)
			{
				Send.SpawnUser(target.Connection, target.ObjectId, target.Data, isSelf: true);
				Msg(sender, "Character refreshed.");
			}
			return CommandResult.Okay;
		}

		/// <summary>
		/// Plays a character effect.
		/// </summary>
		private CommandResult HandleCharEffect(Player sender, Player target, string message, string commandName, Arguments args)
		{
			if (args.Count < 1 || !byte.TryParse(args.Get(0), out var effectId))
			{
				Msg(sender, "Usage: >chareffect <effect id>");
				Msg(sender, "  1 = Level Up");
				Msg(sender, "  2 = Max Level");
				return CommandResult.InvalidArgument;
			}

			if (target.Connection != null)
			{
				Send.CharEffect(target.Connection, target.ObjectId, effectId);
				Msg(sender, $"Played effect {effectId} on {target.Data.Name}.");
			}
			return CommandResult.Okay;
		}

		/// <summary>
		/// Increases the player's level.
		/// </summary>
		private CommandResult HandleLevelUp(Player sender, Player target, string message, string commandName, Arguments args)
		{
			var levels = 1;
			if (args.Count >= 1 && int.TryParse(args.Get(0), out var parsedLevels))
				levels = parsedLevels;

			var oldLevel = target.Data.Level;
			target.Data.Level = (byte)Math.Min(255, target.Data.Level + levels);

			Msg(sender, $"Level changed from {oldLevel} to {target.Data.Level}.");

			// Send stat update and level up effect
			if (target.Connection != null)
			{
				Send.StatUpdate(target.Connection, target.Data);
				Send.CharEffect(target.Connection, target.ObjectId, 1); // Level up effect
			}

			return CommandResult.Okay;
		}

		/// <summary>
		/// Shows the player's current stats.
		/// </summary>
		private CommandResult HandleStat(Player sender, Player target, string message, string commandName, Arguments args)
		{
			var d = target.Data;
			Msg(sender, $"Level: {d.Level}, HP: {d.CurrentHP}/{d.MaxHP}, MP: {d.CurrentMP}/{d.MaxMP}");
			Msg(sender, $"Stab: {d.StatStab}, Hack: {d.StatHack}, Int: {d.StatInt}");
			Msg(sender, $"Def: {d.StatDef}, MR: {d.StatMR}, Dex: {d.StatDex}, Agi: {d.StatAgi}");
			Msg(sender, $"Stat Points: {d.StatPoints}");
			return CommandResult.Okay;
		}

		/// <summary>
		/// Sets a specific stat value.
		/// </summary>
		private CommandResult HandleSetStat(Player sender, Player target, string message, string commandName, Arguments args)
		{
			if (args.Count < 2)
			{
				Msg(sender, "Usage: >setstat <stat> <value>");
				Msg(sender, "Stats: stab, hack, int, def, mr, dex, agi");
				return CommandResult.InvalidArgument;
			}

			var statName = args.Get(0).ToLowerInvariant();
			if (!int.TryParse(args.Get(1), out var value))
			{
				Msg(sender, "Invalid value.");
				return CommandResult.InvalidArgument;
			}

			var d = target.Data;
			switch (statName)
			{
				case "stab": d.StatStab = (short)value; break;
				case "hack": d.StatHack = (short)value; break;
				case "int": d.StatInt = (short)value; break;
				case "def": d.StatDef = (short)value; break;
				case "mr": d.StatMR = (short)value; break;
				case "dex": d.StatDex = (short)value; break;
				case "agi": d.StatAgi = (short)value; break;
				default:
					Msg(sender, $"Unknown stat: {statName}");
					return CommandResult.InvalidArgument;
			}

			Msg(sender, $"Set {statName} to {value}.");

			if (target.Connection != null)
				Send.StatUpdate(target.Connection, target.Data);

			return CommandResult.Okay;
		}

		/// <summary>
		/// Adds stat points to the player.
		/// </summary>
		private CommandResult HandleAddStatPoints(Player sender, Player target, string message, string commandName, Arguments args)
		{
			if (args.Count < 1 || !int.TryParse(args.Get(0), out var points))
			{
				Msg(sender, "Usage: >addstatpoints <points>");
				return CommandResult.InvalidArgument;
			}

			target.Data.StatPoints += points;
			Msg(sender, $"Added {points} stat points. Total: {target.Data.StatPoints}");

			if (target.Connection != null)
				Send.StatUpdate(target.Connection, target.Data);

			return CommandResult.Okay;
		}

		/// <summary>
		/// Shows the number of players online.
		/// </summary>
		private CommandResult HandleWho(Player sender, Player target, string message, string commandName, Arguments args)
		{
			var count = WorldServer.Instance.World.GetCharacterCount();
			Msg(sender, $"Players online: {count}");
			return CommandResult.Okay;
		}

		/// <summary>
		/// Saves the player's character data.
		/// </summary>
		private CommandResult HandleSave(Player sender, Player target, string message, string commandName, Arguments args)
		{
			target.Save();
			Msg(sender, "Character saved.");
			return CommandResult.Okay;
		}

		/// <summary>
		/// Shows item database information.
		/// </summary>
		private CommandResult HandleItemInfo(Player sender, Player target, string message, string commandName, Arguments args)
		{
			if (args.Count < 1)
			{
				Msg(sender, "Usage: >iteminfo <id|name>");
				return CommandResult.InvalidArgument;
			}

			var query = args.Get(0);
			ItemData? item = null;

			// Try parsing as ID first
			if (int.TryParse(query, out var id))
				item = WorldServer.Instance.ItemDb.GetById(id);

			// Otherwise search by name
			item ??= WorldServer.Instance.ItemDb.GetByName(query);

			if (item == null)
			{
				Msg(sender, $"Item not found: {query}");
				return CommandResult.Okay;
			}

			Msg(sender, $"=== {item.Name} (ID: {item.Id}) ===");
			Msg(sender, $"Type: {item.Type}, Slot: {item.Slot}");
			Msg(sender, $"Price: {item.Price}, Stack: {item.MaxStack}");

			if (item.IsEquipment)
			{
				if (item.MinDamage > 0 || item.MaxDamage > 0)
					Msg(sender, $"Damage: {item.MinDamage}-{item.MaxDamage}");
				if (item.Defense > 0)
					Msg(sender, $"Defense: {item.Defense}");
				if (item.MagicDefense > 0)
					Msg(sender, $"Magic Defense: {item.MagicDefense}");
				if (item.BonusStats.Count > 0)
				{
					var stats = string.Join(", ", item.BonusStats.Select(s => $"{s.Key}+{s.Value}"));
					Msg(sender, $"Bonus: {stats}");
				}
			}

			if (item.LevelRequirement > 0)
				Msg(sender, $"Level Req: {item.LevelRequirement}");

			if (!string.IsNullOrEmpty(item.Description))
				Msg(sender, $"Desc: {item.Description}");

			if (item.DropSources.Count > 0)
			{
				var drops = string.Join(", ", item.DropSources.Select(d =>
				{
					var monster = WorldServer.Instance.MonsterDb.GetById(d.MonsterId);
					return monster != null ? $"{monster.Name} ({d.Rate}%)" : $"#{d.MonsterId} ({d.Rate}%)";
				}));
				Msg(sender, $"Drops from: {drops}");
			}

			return CommandResult.Okay;
		}

		/// <summary>
		/// Shows monster database information.
		/// </summary>
		private CommandResult HandleMonsterInfo(Player sender, Player target, string message, string commandName, Arguments args)
		{
			if (args.Count < 1)
			{
				Msg(sender, "Usage: >monsterinfo <id|name>");
				return CommandResult.InvalidArgument;
			}

			var query = args.Get(0);
			MonsterData? monster = null;

			// Try parsing as ID first
			if (int.TryParse(query, out var id))
				monster = WorldServer.Instance.MonsterDb.GetById(id);

			// Otherwise search by name
			monster ??= WorldServer.Instance.MonsterDb.GetByName(query);

			if (monster == null)
			{
				Msg(sender, $"Monster not found: {query}");
				return CommandResult.Okay;
			}

			Msg(sender, $"=== {monster.Name} (ID: {monster.Id}) ===");
			Msg(sender, $"Level: {monster.Level}, Type: {monster.Type}");
			Msg(sender, $"HP: {monster.MaxHP}, MP: {monster.MaxMP}");
			Msg(sender, $"ATK: {monster.Attack}, DEF: {monster.Defense}");
			Msg(sender, $"MATK: {monster.MagicAttack}, MDEF: {monster.MagicDefense}");
			Msg(sender, $"EXP: {monster.Experience}, Gold: {monster.Gold}");
			Msg(sender, $"Behavior: {monster.Behavior}, Aggro: {monster.AggroRange}");
			Msg(sender, $"Respawn: {monster.RespawnTimeMs / 1000}s");

			if (monster.DropTable.Count > 0)
			{
				Msg(sender, "--- Drop Table ---");
				foreach (var drop in monster.DropTable.Take(5))
				{
					var item = WorldServer.Instance.ItemDb.GetById(drop.ItemId);
					var name = item?.Name ?? $"#{drop.ItemId}";
					Msg(sender, $"  {name}: {drop.DropRate}% (x{drop.MinAmount}-{drop.MaxAmount})");
				}
				if (monster.DropTable.Count > 5)
					Msg(sender, $"  ...and {monster.DropTable.Count - 5} more");
			}

			return CommandResult.Okay;
		}

		/// <summary>
		/// Searches items by name.
		/// </summary>
		private CommandResult HandleSearchItem(Player sender, Player target, string message, string commandName, Arguments args)
		{
			if (args.Count < 1)
			{
				Msg(sender, "Usage: >searchitem <name>");
				return CommandResult.InvalidArgument;
			}

			var query = string.Join(" ", args.GetAll());
			var results = WorldServer.Instance.ItemDb.Search(query, 10).ToList();

			if (results.Count == 0)
			{
				Msg(sender, $"No items found matching: {query}");
				return CommandResult.Okay;
			}

			Msg(sender, $"Found {results.Count} items:");
			foreach (var item in results)
			{
				Msg(sender, $"  [{item.Id}] {item.Name} ({item.Type})");
			}

			return CommandResult.Okay;
		}

		/// <summary>
		/// Searches monsters by name.
		/// </summary>
		private CommandResult HandleSearchMonster(Player sender, Player target, string message, string commandName, Arguments args)
		{
			if (args.Count < 1)
			{
				Msg(sender, "Usage: >searchmonster <name>");
				return CommandResult.InvalidArgument;
			}

			var query = string.Join(" ", args.GetAll());
			var results = WorldServer.Instance.MonsterDb.Search(query, 10).ToList();

			if (results.Count == 0)
			{
				Msg(sender, $"No monsters found matching: {query}");
				return CommandResult.Okay;
			}

			Msg(sender, $"Found {results.Count} monsters:");
			foreach (var monster in results)
			{
				Msg(sender, $"  [{monster.Id}] {monster.Name} (Lv.{monster.Level})");
			}

			return CommandResult.Okay;
		}

		/// <summary>
		/// Spawns a monster at the player's location.
		/// </summary>
		private CommandResult HandleSpawnMonster(Player sender, Player target, string message, string commandName, Arguments args)
		{
			if (args.Count < 1)
			{
				Msg(sender, "Usage: >spawnmonster <id>");
				return CommandResult.InvalidArgument;
			}

			if (!int.TryParse(args.Get(0), out var monsterId))
			{
				Msg(sender, "Invalid monster ID.");
				return CommandResult.InvalidArgument;
			}

			var monsterData = WorldServer.Instance.MonsterDb.GetById(monsterId);
			if (monsterData == null)
			{
				Msg(sender, $"Monster ID {monsterId} not found in database.");
				return CommandResult.Okay;
			}

			if (target.Instance == null)
			{
				Msg(sender, "Player is not on a map.");
				return CommandResult.Fail;
			}

			// Map will assign ObjectId via RegisterEntity
			var monster = new Monster(monsterData.Name, (uint)monsterData.ModelId)
			{
				MaxHP = (uint)monsterData.MaxHP,
				CurrentHP = (uint)monsterData.MaxHP,
				Position = target.Position,
				Direction = target.Direction
			};

			target.Instance.AddMonster(monster);
			Msg(sender, $"Spawned {monsterData.Name} (ObjectId: {monster.ObjectId}) at your location.");

			return CommandResult.Okay;
		}
	}
}
