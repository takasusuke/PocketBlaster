using NUnit.Framework;
using PocketBlaster.Gameplay;

namespace PocketBlaster.Tests.EditMode
{
    public class PlayerHealthStateTests
    {
        [Test]
        public void StartsAtMaxHealth()
        {
            var health = new PlayerHealthState(100);
            Assert.AreEqual(100, health.CurrentHealth);
            Assert.IsFalse(health.IsDead);
        }

        [Test]
        public void TakeDamageReducesHealth()
        {
            var health = new PlayerHealthState(100);
            Assert.IsFalse(health.TakeDamage(30));
            Assert.AreEqual(70, health.CurrentHealth);
        }

        [Test]
        public void TakeDamageReturnsTrueWhenLethal()
        {
            var health = new PlayerHealthState(50);
            Assert.IsFalse(health.TakeDamage(30));
            Assert.IsTrue(health.TakeDamage(30));
            Assert.IsTrue(health.IsDead);
            Assert.AreEqual(0, health.CurrentHealth, "マイナスにならないこと");
        }

        [Test]
        public void DamageAfterDeathIsNoOp()
        {
            var health = new PlayerHealthState(10);
            health.TakeDamage(10);
            Assert.IsFalse(health.TakeDamage(10), "既に死亡している場合はfalseを返す");
            Assert.AreEqual(0, health.CurrentHealth);
        }

        [Test]
        public void HealIsCappedAtMaxHealth()
        {
            var health = new PlayerHealthState(100);
            health.TakeDamage(20);
            health.Heal(50);
            Assert.AreEqual(100, health.CurrentHealth, "最大値を超えて回復しないこと");
        }

        [Test]
        public void NonPositiveAmountsAreIgnored()
        {
            var health = new PlayerHealthState(100);
            Assert.IsFalse(health.TakeDamage(0));
            Assert.IsFalse(health.TakeDamage(-5));
            Assert.AreEqual(100, health.CurrentHealth);
            health.Heal(0);
            health.Heal(-5);
            Assert.AreEqual(100, health.CurrentHealth);
        }
    }
}
