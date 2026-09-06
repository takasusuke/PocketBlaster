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

        [Test]
        public void NonRespawningTargetStaysDefeatedAfterDown()
        {
            var state = new TargetHitState(0.1f, 0.2f, 0.3f, respawns: false);
            state.TryHit();
            state.Tick(0.1f); // -> KnockDown
            state.Tick(0.2f); // -> Down
            state.Tick(0.3f); // Down終了 -> respawns:falseなのでDefeated(終端)
            Assert.AreEqual(TargetHitState.Phase.Defeated, state.CurrentPhase);
            Assert.IsFalse(state.IsHittable);

            // それ以降Tickしても状態は変わらない(終端)
            state.Tick(10f);
            Assert.AreEqual(TargetHitState.Phase.Defeated, state.CurrentPhase);
        }

        [Test]
        public void MultiHitTargetFlashesAndReturnsToIdleOnNonFatalHits()
        {
            var state = new TargetHitState(0.1f, 0.2f, 0.3f, respawns: false, hitPoints: 3);

            Assert.IsTrue(state.TryHit());
            Assert.AreEqual(2, state.RemainingHitPoints);
            Assert.AreEqual(TargetHitState.Phase.Flash, state.CurrentPhase);

            state.Tick(0.1f); // Flash終了 -> 致命傷ではないのでIdleへ戻る(KnockDownへ進まない)
            Assert.AreEqual(TargetHitState.Phase.Idle, state.CurrentPhase);
            Assert.IsTrue(state.IsHittable, "まだ体力が残っているので再度狙えるべき");

            Assert.IsTrue(state.TryHit());
            Assert.AreEqual(1, state.RemainingHitPoints);
            state.Tick(0.1f);
            Assert.AreEqual(TargetHitState.Phase.Idle, state.CurrentPhase);
        }

        [Test]
        public void MaxHitPointsStaysConstantWhileRemainingDecreases()
        {
            var state = new TargetHitState(0.1f, 0.2f, 0.3f, respawns: false, hitPoints: 3);
            Assert.AreEqual(3, state.MaxHitPoints);

            state.TryHit();
            state.Tick(0.1f);
            Assert.AreEqual(3, state.MaxHitPoints, "MaxHitPointsは被弾しても変わらない");
            Assert.AreEqual(2, state.RemainingHitPoints);
        }

        [Test]
        public void MultiHitTargetKnocksDownOnFinalHit()
        {
            var state = new TargetHitState(0.1f, 0.2f, 0.3f, respawns: false, hitPoints: 2);
            state.TryHit();
            state.Tick(0.1f); // 1発目: まだ倒れない

            state.TryHit();
            Assert.AreEqual(0, state.RemainingHitPoints);
            state.Tick(0.1f); // 2発目(最後): Flash終了でKnockDownへ進むべき
            Assert.AreEqual(TargetHitState.Phase.KnockDown, state.CurrentPhase);
        }
    }
}
