using NUnit.Framework;
using PocketBlaster.Gameplay;

namespace PocketBlaster.Tests.EditMode
{
    public class ObstacleCrossingTests
    {
        [Test]
        public void NoOverlapIsAlwaysClear()
        {
            Assert.AreEqual(ObstacleCrossingResult.Clear, ObstacleCrossing.Evaluate(false, obstacleHeight: 10f, stepUpHeight: 0.5f));
        }

        [Test]
        public void LowObstacleAllowsStepUp()
        {
            Assert.AreEqual(ObstacleCrossingResult.StepUp, ObstacleCrossing.Evaluate(true, obstacleHeight: 0.4f, stepUpHeight: 0.6f));
        }

        [Test]
        public void ExactlyStepUpHeightIsStillClimbable()
        {
            Assert.AreEqual(ObstacleCrossingResult.StepUp, ObstacleCrossing.Evaluate(true, obstacleHeight: 0.6f, stepUpHeight: 0.6f));
        }

        [Test]
        public void TallObstacleBlocks()
        {
            Assert.AreEqual(ObstacleCrossingResult.Blocked, ObstacleCrossing.Evaluate(true, obstacleHeight: 1.5f, stepUpHeight: 0.6f));
        }
    }
}
