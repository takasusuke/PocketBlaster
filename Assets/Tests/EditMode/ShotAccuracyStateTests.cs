using NUnit.Framework;
using PocketBlaster.Gameplay;

namespace PocketBlaster.Tests.EditMode
{
    public class ShotAccuracyStateTests
    {
        [Test]
        public void StartsAtZeroPercentWithNoShotsFired()
        {
            var accuracy = new ShotAccuracyState();
            Assert.AreEqual(0, accuracy.ShotsFired);
            Assert.AreEqual(0, accuracy.Hits);
            Assert.AreEqual(0f, accuracy.AccuracyPercent, "未発射のゼロ除算を0%扱いにすること");
        }

        [Test]
        public void TracksShotsFiredAndHitsSeparately()
        {
            var accuracy = new ShotAccuracyState();
            accuracy.RegisterShot(true);
            accuracy.RegisterShot(false);
            accuracy.RegisterShot(true);

            Assert.AreEqual(3, accuracy.ShotsFired);
            Assert.AreEqual(2, accuracy.Hits);
        }

        [Test]
        public void ComputesAccuracyPercentFromHitsOverShotsFired()
        {
            var accuracy = new ShotAccuracyState();
            accuracy.RegisterShot(true);
            accuracy.RegisterShot(true);
            accuracy.RegisterShot(false);
            accuracy.RegisterShot(false);

            Assert.AreEqual(50f, accuracy.AccuracyPercent, 0.001f);
        }

        [Test]
        public void AllHitsIsHundredPercent()
        {
            var accuracy = new ShotAccuracyState();
            accuracy.RegisterShot(true);
            accuracy.RegisterShot(true);
            Assert.AreEqual(100f, accuracy.AccuracyPercent, 0.001f);
        }
    }
}
