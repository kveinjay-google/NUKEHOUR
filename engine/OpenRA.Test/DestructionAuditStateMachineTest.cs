using NUnit.Framework;
using OpenRA.Mods.RA2.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class DestructionAuditStateMachineTest
	{
		[Test]
		public void SuccessfulCasePassesThroughEveryRiskBoundary()
		{
			var state = new DestructionAuditStateMachine(settleTicks: 2, timeoutTicks: 20);

			Assert.That(state.Phase, Is.EqualTo(DestructionAuditPhase.Prepare));
			state.Spawned();
			Assert.That(state.Phase, Is.EqualTo(DestructionAuditPhase.SettleBeforeKill));
			state.Tick();
			state.Tick();
			Assert.That(state.Phase, Is.EqualTo(DestructionAuditPhase.Kill));
			state.KillReturned();
			Assert.That(state.Phase, Is.EqualTo(DestructionAuditPhase.SettleEffects));
			state.Tick();
			state.Tick();
			Assert.That(state.Phase, Is.EqualTo(DestructionAuditPhase.Cleanup));
			state.CleanedUp();
			Assert.That(state.Phase, Is.EqualTo(DestructionAuditPhase.Passed));
		}

		[Test]
		public void PreKillAndEffectSettlingUseIndependentBudgets()
		{
			var state = new DestructionAuditStateMachine(
				preKillSettleTicks: 1, effectSettleTicks: 3, timeoutTicks: 20);

			state.Spawned();
			state.Tick();
			Assert.That(state.Phase, Is.EqualTo(DestructionAuditPhase.Kill));

			state.KillReturned();
			state.Tick();
			state.Tick();
			Assert.That(state.Phase, Is.EqualTo(DestructionAuditPhase.SettleEffects));
			state.Tick();
			Assert.That(state.Phase, Is.EqualTo(DestructionAuditPhase.Cleanup));
		}

		[Test]
		public void CaseThatCannotSpawnIsRecordedAsSkipped()
		{
			var state = new DestructionAuditStateMachine(1, 20);

			state.Skip("no compatible terrain");

			Assert.That(state.Phase, Is.EqualTo(DestructionAuditPhase.Skipped));
			Assert.That(state.Detail, Is.EqualTo("no compatible terrain"));
		}

		[Test]
		public void TimeoutRetainsTheExactPhase()
		{
			var state = new DestructionAuditStateMachine(20, 2);
			state.Spawned();

			state.Tick();
			state.Tick();
			state.Tick();

			Assert.That(state.Phase, Is.EqualTo(DestructionAuditPhase.Failed));
			StringAssert.Contains("SettleBeforeKill", state.Detail);
		}
	}
}
