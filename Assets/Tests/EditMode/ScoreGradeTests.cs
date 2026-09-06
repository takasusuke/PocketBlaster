using NUnit.Framework;
using PocketBlaster.Gameplay;

namespace PocketBlaster.Tests.EditMode
{
    public class ScoreGradeTests
    {
        [Test]
        public void FullScoreIsGradeA()
        {
            Assert.AreEqual('A', ScoreGrade.Compute(100, 100));
        }

        [Test]
        public void ZeroMaxPossibleScoreIsGradeE()
        {
            Assert.AreEqual('E', ScoreGrade.Compute(0, 0));
        }

        [Test]
        public void ThresholdsMapToExpectedGrades()
        {
            Assert.AreEqual('A', ScoreGrade.Compute(90, 100));
            Assert.AreEqual('B', ScoreGrade.Compute(75, 100));
            Assert.AreEqual('C', ScoreGrade.Compute(60, 100));
            Assert.AreEqual('D', ScoreGrade.Compute(40, 100));
            Assert.AreEqual('E', ScoreGrade.Compute(39, 100));
        }

        [Test]
        public void JustBelowThresholdFallsToLowerGrade()
        {
            Assert.AreEqual('B', ScoreGrade.Compute(89, 100));
            Assert.AreEqual('C', ScoreGrade.Compute(74, 100));
            Assert.AreEqual('D', ScoreGrade.Compute(59, 100));
        }
    }
}
