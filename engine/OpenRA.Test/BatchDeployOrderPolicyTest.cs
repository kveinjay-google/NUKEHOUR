using NUnit.Framework;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class BatchDeployOrderPolicyTest
	{
		[Test]
		public void MixedStableGroupTargetsUndeployedActors()
		{
			var target = BatchDeployOrderPolicy.GetTargetState(new[]
			{
				DeployState.Deployed,
				DeployState.Undeployed,
				DeployState.Deployed
			});

			Assert.That(target, Is.EqualTo(DeployState.Undeployed));
		}

		[Test]
		public void FullyDeployedGroupTargetsDeployedActors()
		{
			var target = BatchDeployOrderPolicy.GetTargetState(new[]
			{
				DeployState.Deployed,
				DeployState.Deployed
			});

			Assert.That(target, Is.EqualTo(DeployState.Deployed));
		}

		[Test]
		public void TransitionOnlyGroupHasNoTarget()
		{
			var target = BatchDeployOrderPolicy.GetTargetState(new[]
			{
				DeployState.Deploying,
				DeployState.Undeploying
			});

			Assert.That(target, Is.Null);
		}

		[Test]
		public void DeployedGroupWaitsForTransitioningMembers()
		{
			var target = BatchDeployOrderPolicy.GetTargetState(new[]
			{
				DeployState.Deployed,
				DeployState.Deploying
			});

			Assert.That(target, Is.Null);
		}

		[Test]
		public void GrantConditionDeployOptsIntoStateAwareBatching()
		{
			Assert.That(typeof(IStatefulDeployOrder).IsAssignableFrom(typeof(GrantConditionOnDeploy)), Is.True);
		}
	}
}
