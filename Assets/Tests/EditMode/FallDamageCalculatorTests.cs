using NUnit.Framework;
using PocketBlaster.Gameplay;

namespace PocketBlaster.Tests.EditMode
{
    public class FallDamageCalculatorTests
    {
        [Test]
        public void FallBelowSafeHeightDealsNoDamage()
        {
            Assert.AreEqual(0, FallDamageCalculator.ComputeDamage(dropHeight: 1f, safeHeight: 1.5f, damagePerMeterBeyondSafeHeight: 20f));
        }

        [Test]
        public void FallExactlyAtSafeHeightDealsNoDamage()
        {
            Assert.AreEqual(0, FallDamageCalculator.ComputeDamage(dropHeight: 1.5f, safeHeight: 1.5f, damagePerMeterBeyondSafeHeight: 20f));
        }

        [Test]
        public void FallBeyondSafeHeightScalesWithExcess()
        {
            // 超過1mぶんだけダメージが乗る
            Assert.AreEqual(20, FallDamageCalculator.ComputeDamage(dropHeight: 2.5f, safeHeight: 1.5f, damagePerMeterBeyondSafeHeight: 20f));
        }

        [Test]
        public void RoundsToNearestInt()
        {
            // 超過0.68m×20 = 13.6 → 14に丸める
            Assert.AreEqual(14, FallDamageCalculator.ComputeDamage(dropHeight: 2.18f, safeHeight: 1.5f, damagePerMeterBeyondSafeHeight: 20f));
        }
    }
}
