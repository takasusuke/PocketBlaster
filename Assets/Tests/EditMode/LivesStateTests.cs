using NUnit.Framework;
using PocketBlaster.Gameplay;

namespace PocketBlaster.Tests.EditMode
{
    public class LivesStateTests
    {
        [Test]
        public void StartsWithGivenLives()
        {
            var lives = new LivesState(3);
            Assert.AreEqual(3, lives.RemainingLives);
            Assert.IsFalse(lives.IsGameOver);
        }

        [Test]
        public void LosingAllLivesTriggersGameOver()
        {
            var lives = new LivesState(2);
            Assert.IsFalse(lives.LoseLife());
            Assert.IsTrue(lives.LoseLife());
            Assert.IsTrue(lives.IsGameOver);
        }

        [Test]
        public void LoseLifeAfterGameOverIsNoOp()
        {
            var lives = new LivesState(1);
            Assert.IsTrue(lives.LoseLife());
            Assert.IsFalse(lives.LoseLife(), "既にゲームオーバーの状態からの重複呼び出しはfalseを返す");
            Assert.AreEqual(0, lives.RemainingLives, "重複呼び出しでマイナスにならないこと");
        }
    }
}
