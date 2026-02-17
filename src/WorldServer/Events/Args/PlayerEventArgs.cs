using Kakia.TW.World.Entities;

namespace Kakia.TW.World.Events.Args
{
	/// <summary>
	/// Arguments for events related to a player character.
	/// </summary>
	/// <remarks>
	/// Creates new instance.
	/// </remarks>
	/// <param name="player"></param>
	public class PlayerEventArgs(Player player) : EventArgs
	{
		/// <summary>
		/// Returns the character associated with the event.
		/// </summary>
		public Player Player { get; } = player;
	}
}
