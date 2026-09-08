using NUnit.Framework;
using OpenRA.Network;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class IosLanServerCompatibilityTest
	{
		[Test]
		public void CurrentActiveModAdvertisementIsCompatibleWithoutExternalRegistration()
		{
			Assert.That(GameServer.IsAdvertisedModCompatible(
				"ra2", "{DEV_VERSION}", "ra2", "{DEV_VERSION}", false), Is.True);
		}

		[Test]
		public void DifferentActiveModAdvertisementStillRequiresExternalRegistration()
		{
			Assert.That(GameServer.IsAdvertisedModCompatible(
				"ra2", "{DEV_VERSION}", "cnc", "release-20250330", false), Is.False);
			Assert.That(GameServer.IsAdvertisedModCompatible(
				"ra2", "{DEV_VERSION}", "cnc", "release-20250330", true), Is.True);
		}
	}
}
