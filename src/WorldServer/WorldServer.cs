using Kakia.TW.Shared;
using Kakia.TW.Shared.Data;
using Kakia.TW.World.Commands;
using Kakia.TW.World.Database;
using Kakia.TW.World.Events;
using Kakia.TW.World.Managers;
using Kakia.TW.World.Network;
using System.IO;
using Yggdrasil.Logging;
using Yggdrasil.Network.TCP;
using Yggdrasil.Util;
using Yggdrasil.Util.Commands;

namespace Kakia.TW.World
{
	/// <summary>
	/// Represents the world server.
	/// </summary>
	public class WorldServer : Server
	{
		private readonly List<TcpConnectionAcceptor<WorldConnection>> _acceptors = new();

		/// <summary>
		/// Global singleton for the world server.
		/// </summary>
		public static readonly WorldServer Instance = new();

		/// <summary>
		/// Returns a reference to the server's packet handlers.
		/// </summary>
		public PacketHandler PacketHandler { get; } = new PacketHandler();

		/// <summary>
		/// Returns reference to the server's database interface.
		/// </summary>
		public WorldDb Database { get; } = new WorldDb();

		/// <summary>
		/// Returns reference to the server's world.
		/// </summary>
		public WorldManager World { get; } = new WorldManager();

		/// <summary>
		/// Returns a reference to the server's event manager.
		/// </summary>
		public ServerEvents ServerEvents { get; } = new();

		/// <summary>
		/// Returns reference to the server's chat command manager.
		/// </summary>
		public ChatCommands ChatCommands { get; } = new();

		/// <summary>
		/// Returns reference to the item database.
		/// </summary>
		public ItemDb ItemDb { get; } = new();

		/// <summary>
		/// Returns reference to the monster database.
		/// </summary>
		public MonsterDb MonsterDb { get; } = new();

		/// <summary>
		/// Starts the server.
		/// </summary>
		/// <param name="args"></param>
		public override void Run(string[] args)
		{
			ConsoleUtil.WriteHeader(nameof(Kakia), "World", ConsoleColor.DarkYellow, ConsoleHeader.Title, ConsoleHeader.Subtitle);
			ConsoleUtil.LoadingTitle();

			this.NavigateToRoot();
			this.LoadConf();
			this.LoadLocalization(this.Conf);
			this.InitDatabase(Database, this.Conf);

			// Load item and monster databases
			this.LoadDatabases();

			// Load NPC/Monster scripts
			this.LoadScripts("world", this.Conf);

			// Load entities from binary spawn files
			this.LoadEntityBinaries();

			this.StartWorld();

			var acceptor = new TcpConnectionAcceptor<WorldConnection>(this.Conf.World.BindIp, this.Conf.World.BindPort);
			acceptor.ConnectionAccepted += OnConnectionAccepted;
			acceptor.Listen();

			_acceptors.Add(acceptor);

			Log.Status("Server ready, listening on {0}.", acceptor.Address);

			ConsoleUtil.RunningTitle();
			new ConsoleCommands().Wait();
		}

		/// <summary>
		/// Loads item and monster databases from JSON files.
		/// </summary>
		private void LoadDatabases()
		{
			var dbPath = Path.Combine("system", "db");

			var itemsPath = Path.Combine(dbPath, "items.json");
			this.ItemDb.Load(itemsPath);

			var monstersPath = Path.Combine(dbPath, "monsters.json");
			this.MonsterDb.Load(monstersPath);
		}

		/// <summary>
		/// Loads entity spawn data from binary files.
		/// </summary>
		private void LoadEntityBinaries()
		{
			// Look for Maps folder in bin/ directory (relative to working dir)
			var mapsPath = Path.Combine("bin", "Maps");
			if (!Directory.Exists(mapsPath))
			{
				// Also check in system/ folder as alternative
				mapsPath = Path.Combine("system", "Maps");
			}

			if (Directory.Exists(mapsPath))
			{
				var loader = new EntityBinLoader(this.World.Maps, mapsPath);
				loader.Load();
			}
			else
			{
				Log.Debug("EntityBinLoader: No Maps directory found, skipping binary entity loading.");
			}
		}

		/// <summary>
		/// Starts the world's update loop, aka the hearbeat.
		/// </summary>
		private void StartWorld()
		{
			Log.Info("Starting world update...");
			this.World.Start();
		}

		/// <summary>
		/// Called when a new connection to the server was established
		/// by a client.
		/// </summary>
		/// <param name="conn"></param>
		private void OnConnectionAccepted(WorldConnection conn)
		{
			Log.Info("New connection accepted from '{0}'.", conn.Address);
		}
	}
}
