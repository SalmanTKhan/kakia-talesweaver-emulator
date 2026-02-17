using Yggdrasil.Logging;
using Yggdrasil.Util.Commands;

namespace Kakia.TW.World.Commands
{
	public class WorldConsoleCommands : ConsoleCommands
	{
		private static readonly DateTime _startTime = DateTime.Now;

		public WorldConsoleCommands()
		{
			Add("who", "", "Shows the current number of online players.", HandlePlayersOnline);
			Add("playersonline", "", "Shows the current number of online players (alias for 'who').", HandlePlayersOnline);
			Add("uptime", "", "Shows how long the server has been running.", HandleUptime);
		}

		private CommandResult HandleUptime(string command, Arguments args)
		{
			var uptime = DateTime.Now - _startTime;
			Log.Info($"Server uptime: {uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s");
			return CommandResult.Okay;
		}

		/// <summary>
		/// Handles the 'who' and 'playersonline' console command.
		/// </summary>
		private CommandResult HandlePlayersOnline(string command, Arguments args)
		{
			var playerCount = WorldServer.Instance.World.GetCharacterCount();
			Log.Info($"There are currently {playerCount} players online.");
			return CommandResult.Okay;
		}
	}
}
