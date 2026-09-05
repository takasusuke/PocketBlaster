using NUnit.Framework;
using PocketBlaster.Gameplay;

namespace PocketBlaster.Tests.EditMode
{
    public class ScoreStateTests
    {
        [Test]
        public void StartsAtZero()
        {
            var score = new ScoreState();
            Assert.AreEqual(0, score.TotalScore);
        }

        [Test]
        public void AddPointsAccumulates()
        {
            var score = new ScoreState();
            score.AddPoints(100);
            score.AddPoints(250);
            Assert.AreEqual(350, score.TotalScore);
        }

        [Test]
        public void NonPositivePointsAreIgnored()
        {
            var score = new ScoreState();
            score.AddPoints(100);
            score.AddPoints(0);
            score.AddPoints(-50);
            Assert.AreEqual(100, score.TotalScore);
        }
    }
}
