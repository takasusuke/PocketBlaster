using NUnit.Framework;
using PocketBlaster.Aim;

namespace PocketBlaster.Tests.EditMode
{
    public class AmmoStateTests
    {
        [Test]
        public void StartsFull()
        {
            var ammo = new AmmoState(6);
            Assert.AreEqual(6, ammo.CurrentAmmo);
            Assert.IsTrue(ammo.CanShoot);
        }

        [Test]
        public void ShootDecrementsAmmo()
        {
            var ammo = new AmmoState(2);
            Assert.IsTrue(ammo.Shoot());
            Assert.AreEqual(1, ammo.CurrentAmmo);
            Assert.IsTrue(ammo.Shoot());
            Assert.AreEqual(0, ammo.CurrentAmmo);
        }

        [Test]
        public void ShootFailsWhenEmpty()
        {
            var ammo = new AmmoState(1);
            Assert.IsTrue(ammo.Shoot());
            Assert.IsFalse(ammo.CanShoot);
            Assert.IsFalse(ammo.Shoot(), "弾切れ後の発射はfalseを返し、残弾もマイナスにならないこと");
            Assert.AreEqual(0, ammo.CurrentAmmo);
        }

        [Test]
        public void ReloadRefillsToMagazineSize()
        {
            var ammo = new AmmoState(3);
            ammo.Shoot();
            ammo.Shoot();
            ammo.Reload();
            Assert.AreEqual(3, ammo.CurrentAmmo);
            Assert.IsTrue(ammo.CanShoot);
        }

        [Test]
        public void IncreaseMagazineSizeGrowsBothMagazineAndCurrentAmmo()
        {
            var ammo = new AmmoState(3);
            ammo.Shoot();
            ammo.IncreaseMagazineSize(2);
            Assert.AreEqual(5, ammo.MagazineSize);
            Assert.AreEqual(4, ammo.CurrentAmmo, "取得した分だけ即座に使える弾も増えること");
        }

        [Test]
        public void IncreaseMagazineSizeIgnoresNonPositiveAmounts()
        {
            var ammo = new AmmoState(3);
            ammo.IncreaseMagazineSize(0);
            ammo.IncreaseMagazineSize(-2);
            Assert.AreEqual(3, ammo.MagazineSize);
            Assert.AreEqual(3, ammo.CurrentAmmo);
        }
    }
}
