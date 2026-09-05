using NUnit.Framework;
using PocketBlaster.Gameplay;

namespace PocketBlaster.Tests.EditMode
{
    public class TargetHitStateTests
    {
        [Test]
        public void StartsHittable()
        {
            var state = new TargetHitState(0.1f, 0.2f, 1f);
            Assert.IsTrue(state.IsHittable);
            Assert.AreEqual(TargetHitState.Phase.Idle, state.CurrentPhase);
        }

        [Test]
        public void HitTransitionsThroughPhasesAndBackToIdle()
        {
            var state = new TargetHitState(flashDuration: 0.1f, knockDuration: 0.2f, downDuration: 0.5f);

            Assert.IsTrue(state.TryHit());
            Assert.AreEqual(TargetHitState.Phase.Flash, state.CurrentPhase);
            Assert.IsFalse(state.IsHittable);

            state.Tick(0.1f);
            Assert.AreEqual(TargetHitState.Phase.KnockDown, state.CurrentPhase);

            state.Tick(0.2f);
            Assert.AreEqual(TargetHitState.Phase.Down, state.CurrentPhase);

            state.Tick(0.5f);
            Assert.AreEqual(TargetHitState.Phase.RecoverUp, state.CurrentPhase);

            state.Tick(0.2f);
            Assert.AreEqual(TargetHitState.Phase.Idle, state.CurrentPhase);
            Assert.IsTrue(state.IsHittable);
        }

        [Test]
        public void SecondHitIgnoredWhileNotIdle()
        {
            var state = new TargetHitState(0.1f, 0.2f, 0.5f);
            Assert.IsTrue(state.TryHit());
            Assert.IsFalse(state.TryHit(), "既に反応中なら2回目のヒットは無視されるべき");
            Assert.AreEqual(TargetHitState.Phase.Flash, state.CurrentPhase);
        }

        [Test]
        public void PhaseProgressReachesOneAtEndOfKnockDown()
        {
            var state = new TargetHitState(0.1f, 0.2f, 0.5f);
            state.TryHit();
            state.Tick(0.1f); // -> KnockDown開始
            state.Tick(0.2f); // KnockDown終了ちょうど -> Down
            Assert.AreEqual(TargetHitState.Phase.Down, state.CurrentPhase);
            Assert.AreEqual(1f, state.PhaseProgress01, 0.001f);
        }
    }
}
