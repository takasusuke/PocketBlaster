using NUnit.Framework;
using PocketBlaster.Gameplay;

namespace PocketBlaster.Tests.EditMode
{
    public class StageProgressStateTests
    {
        [Test]
        public void StartsBeforeFirstWave()
        {
            var state = new StageProgressState(new[] { 2, 3 });
            Assert.AreEqual(-1, state.CurrentWaveIndex);
            Assert.IsFalse(state.IsStageCleared);
        }

        [Test]
        public void AdvanceToNextWaveSetsUpRemainingCount()
        {
            var state = new StageProgressState(new[] { 2, 3 });
            Assert.IsTrue(state.AdvanceToNextWave());
            Assert.AreEqual(0, state.CurrentWaveIndex);
            Assert.AreEqual(2, state.RemainingInCurrentWave);
        }

        [Test]
        public void DefeatingAllEnemiesInWaveReportsCleared()
        {
            var state = new StageProgressState(new[] { 2 });
            state.AdvanceToNextWave();
            Assert.IsFalse(state.NotifyEnemyDefeated(), "1体目の撃破ではまだクリアではない");
            Assert.IsTrue(state.NotifyEnemyDefeated(), "2体目(最後)の撃破でクリアになるべき");
        }

        [Test]
        public void AdvancingPastLastWaveMarksStageCleared()
        {
            var state = new StageProgressState(new[] { 1 });
            Assert.IsTrue(state.AdvanceToNextWave());
            Assert.IsFalse(state.AdvanceToNextWave(), "最後のウェーブの次は進めない");
            Assert.IsTrue(state.IsStageCleared);
        }

        [Test]
        public void ExtraDefeatNotificationsAfterWaveClearedAreIgnored()
        {
            var state = new StageProgressState(new[] { 1, 1 });
            state.AdvanceToNextWave();
            Assert.IsTrue(state.NotifyEnemyDefeated());
            // 次のウェーブへまだ進めていない状態で、同じウェーブから遅延して届いた通知を想定
            Assert.IsFalse(state.NotifyEnemyDefeated(), "既に0のウェーブへの通知は無視されるべき");
        }
    }
}
