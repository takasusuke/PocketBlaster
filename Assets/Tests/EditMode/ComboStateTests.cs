using NUnit.Framework;
using PocketBlaster.Gameplay;

namespace PocketBlaster.Tests.EditMode
{
    public class ComboStateTests
    {
        [Test]
        public void StartsAtZeroComboWithMultiplierOne()
        {
            var combo = new ComboState();
            Assert.AreEqual(0, combo.CurrentCombo);
            Assert.AreEqual(0, combo.MaxCombo);
            Assert.AreEqual(1f, combo.Multiplier);
        }

        [Test]
        public void HitsIncreaseCurrentAndMaxCombo()
        {
            var combo = new ComboState();
            combo.RegisterShot(true);
            combo.RegisterShot(true);
            combo.RegisterShot(true);
            Assert.AreEqual(3, combo.CurrentCombo);
            Assert.AreEqual(3, combo.MaxCombo);
        }

        [Test]
        public void MissResetsCurrentComboButKeepsMax()
        {
            var combo = new ComboState();
            combo.RegisterShot(true);
            combo.RegisterShot(true);
            combo.RegisterShot(false);
            Assert.AreEqual(0, combo.CurrentCombo);
            Assert.AreEqual(2, combo.MaxCombo, "MaxComboはミス後もリセットされないこと");
        }

        [TestCase(4, 1f)]
        [TestCase(5, 1.5f)]
        [TestCase(9, 1.5f)]
        [TestCase(10, 2f)]
        [TestCase(19, 2f)]
        [TestCase(20, 3f)]
        [TestCase(50, 3f)]
        public void MultiplierStepsAtComboThresholds(int comboCount, float expectedMultiplier)
        {
            var combo = new ComboState();
            for (var i = 0; i < comboCount; i++) combo.RegisterShot(true);
            Assert.AreEqual(expectedMultiplier, combo.Multiplier);
        }
    }
}
