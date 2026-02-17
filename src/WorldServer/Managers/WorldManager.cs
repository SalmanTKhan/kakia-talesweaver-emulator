using Kakia.TW.World.Entities;
using Kakia.TW.World.Events;
using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Threading;
using Yggdrasil.Logging;

namespace Kakia.TW.World.Managers
{
	public class WorldManager
	{
		/// <summary>
		/// Returns a reference to a collection of maps in the world.
		/// </summary>
		public MapManager Maps { get; } = new MapManager();

		/// <summary>
		/// The world's heartbeast, which controls timed events.
		/// </summary>
		public Heartbeat Heartbeat { get; } = new Heartbeat();

		public bool TryGetPlayer(uint id, out Player player)
		{
			// Search via MapManager or a global dictionary
			return Maps.TryGetPlayer(id, out player);
		}

		/// <summary>
		/// Tries to find a player by their character name.
		/// </summary>
		public bool TryGetPlayerByName(string name, out Player? player)
		{
			return Maps.TryGetPlayerByName(name, out player);
		}

		/// <summary>
		/// Gets the total number of characters currently online.
		/// </summary>
		public int GetCharacterCount()
		{
			return Maps.GetPlayerCount();
		}

		/// <summary>
		/// Starts the world's heartbeat if it isn't already running.
		/// </summary>
		public void Start()
		{
			this.Heartbeat.Start();
		}
	}
}