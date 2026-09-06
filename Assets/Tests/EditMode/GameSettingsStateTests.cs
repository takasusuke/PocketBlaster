using NUnit.Framework;
using PocketBlaster.Meta;

namespace PocketBlaster.Tests.EditMode
{
    public class GameSettingsStateTests
    {
        [Test]
        public void CreateDefaultIsCasualWithFullVolume()
        {
            var settings = GameSettingsState.CreateDefault();
            Assert.IsFalse(settings.IsArcadeMode);
            Assert.AreEqual(1f, settings.SfxVolume);
        }

        [Test]
        public void SetModeTogglesArcadeFlag()
        {
            var settings = GameSettingsState.CreateDefault();
            settings.SetMode(true);
            Assert.IsTrue(settings.IsArcadeMode);
            settings.SetMode(false);
            Assert.IsFalse(settings.IsArcadeMode);
        }

        [Test]
        public void SfxVolumeIsClampedToUnitRange()
        {
            var settings = GameSettingsState.CreateDefault();
            settings.SetSfxVolume(-0.5f);
            Assert.AreEqual(0f, settings.SfxVolume);
            settings.SetSfxVolume(1.5f);
            Assert.AreEqual(1f, settings.SfxVolume);
            settings.SetSfxVolume(0.4f);
            Assert.AreEqual(0.4f, settings.SfxVolume);
        }

        [Test]
        public void VerticalSensitivityIsClampedToDefinedRange()
        {
            var settings = GameSettingsState.CreateDefault();
            settings.SetVerticalSensitivity(0f);
            Assert.AreEqual(GameSettingsState.MinSensitivity, settings.VerticalSensitivity);
            settings.SetVerticalSensitivity(999f);
            Assert.AreEqual(GameSettingsState.MaxSensitivity, settings.VerticalSensitivity);
            settings.SetVerticalSensitivity(10f);
            Assert.AreEqual(10f, settings.VerticalSensitivity);
        }

        [Test]
        public void HorizontalSensitivityIsClampedToDefinedRangeIndependentlyOfVertical()
        {
            var settings = GameSettingsState.CreateDefault();
            settings.SetVerticalSensitivity(20f);
            settings.SetHorizontalSensitivity(0f);
            Assert.AreEqual(GameSettingsState.MinSensitivity, settings.HorizontalSensitivity);
            Assert.AreEqual(20f, settings.VerticalSensitivity, "左右を変更しても上下の値は影響を受けないこと");
        }

        [Test]
        public void LookSensitivityIsClampedToDefinedRangeIndependentlyOfAimSensitivity()
        {
            var settings = GameSettingsState.CreateDefault();
            settings.SetVerticalSensitivity(20f);
            settings.SetLookSensitivity(0f);
            Assert.AreEqual(GameSettingsState.MinLookSensitivity, settings.LookSensitivity);
            settings.SetLookSensitivity(9999f);
            Assert.AreEqual(GameSettingsState.MaxLookSensitivity, settings.LookSensitivity);
            Assert.AreEqual(20f, settings.VerticalSensitivity, "視界回転の感度を変更しても構える感度は影響を受けないこと");
        }

        [Test]
        public void ConstructorAlsoClampsInvalidValues()
        {
            var settings = new GameSettingsState(true, sfxVolume: 2f, verticalSensitivity: 1f, horizontalSensitivity: 999f, lookSensitivity: 1f);
            Assert.IsTrue(settings.IsArcadeMode);
            Assert.AreEqual(1f, settings.SfxVolume);
            Assert.AreEqual(GameSettingsState.MinSensitivity, settings.VerticalSensitivity);
            Assert.AreEqual(GameSettingsState.MaxSensitivity, settings.HorizontalSensitivity);
            Assert.AreEqual(GameSettingsState.MinLookSensitivity, settings.LookSensitivity);
        }
    }
}
