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
        public void SensitivityIsClampedToDefinedRange()
        {
            var settings = GameSettingsState.CreateDefault();
            settings.SetSensitivity(0f);
            Assert.AreEqual(GameSettingsState.MinSensitivity, settings.Sensitivity);
            settings.SetSensitivity(999f);
            Assert.AreEqual(GameSettingsState.MaxSensitivity, settings.Sensitivity);
            settings.SetSensitivity(10f);
            Assert.AreEqual(10f, settings.Sensitivity);
        }

        [Test]
        public void ConstructorAlsoClampsInvalidValues()
        {
            var settings = new GameSettingsState(true, sfxVolume: 2f, sensitivity: 1f);
            Assert.IsTrue(settings.IsArcadeMode);
            Assert.AreEqual(1f, settings.SfxVolume);
            Assert.AreEqual(GameSettingsState.MinSensitivity, settings.Sensitivity);
        }
    }
}
