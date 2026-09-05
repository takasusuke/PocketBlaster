using NUnit.Framework;
using PocketBlaster.Audio;
using UnityEngine;

namespace PocketBlaster.Tests.EditMode
{
    public class ProceduralSfxTests
    {
        [Test]
        public void CreateToneProducesClipWithExpectedLength()
        {
            var clip = ProceduralSfx.CreateTone("test_tone", 440f, 0.05f, 0.01f);

            Assert.IsNotNull(clip);
            Assert.AreEqual(44100, clip.frequency);
            Assert.AreEqual(Mathf.RoundToInt(44100 * 0.05f), clip.samples);
            Assert.AreEqual(1, clip.channels);
        }

        [Test]
        public void GeneratedSamplesStayWithinAudioRange()
        {
            var clip = ProceduralSfx.CreateTone("test_range", 220f, 0.02f, 0.005f);
            var data = new float[clip.samples];
            clip.GetData(data, 0);

            foreach (var sample in data)
            {
                Assert.LessOrEqual(sample, 1f);
                Assert.GreaterOrEqual(sample, -1f);
            }
        }
    }
}
