using System.Linq;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.RA2.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class ChronoResourceDeliveryTest
	{
		[Test]
		public void ChronoDeliveryObservesCurrentDockMovementNotifications()
		{
			Assert.That(typeof(ChronoResourceDelivery).GetInterfaces(),
				Does.Contain(typeof(INotifyDockClientMoving)));
		}
	}
}
