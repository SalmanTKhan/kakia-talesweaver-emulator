using Kakia.TW.Shared.World;
using Kakia.TW.World.Managers;
using Kakia.TW.World.Network;
using System;
using System.Threading.Tasks;

namespace Kakia.TW.World.Entities
{
	public delegate Task ReactorFunc(Reactor reactor, WorldConnection conn);

	public class Reactor : Entity
	{
		public uint ReactorId { get; set; }

		// The script to run when interacted with
		public ReactorFunc? Script { get; set; }

		public Reactor(uint reactorId)
		{
			ReactorId = reactorId;
			ModelId = reactorId; // Use ReactorId as ModelId for consistency
		}

		public Reactor()
		{
			ReactorId = 0;
		}

		public override void Update(TimeSpan elapsed) { }
	}
}
