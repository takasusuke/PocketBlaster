using UnityEngine;

namespace PocketBlaster.Audio
{
    /// <summary>
    /// 仮の効果音をコードで生成する(サイン波1音+末尾フェードアウト)。正式なSEアセットが
    /// 無い段階で「撃った感触」を試すためのプレースホルダー — ../CLAUDE.md 11
    /// 「初期実装では画像を作らない」と同じ考え方を効果音に適用したもの。
    /// 差し替える時はこの呼び出し元(GyroReticleController)だけを直せばよい。
    /// </summary>
    public static class ProceduralSfx
    {
        private const int SampleRate = 44100;

        public static AudioClip CreateTone(string name, float frequencyHz, float durationSeconds, float fadeOutSeconds)
        {
            var sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * durationSeconds));
            var fadeOutSamples = Mathf.Clamp(Mathf.RoundToInt(SampleRate * fadeOutSeconds), 1, sampleCount);

            var data = new float[sampleCount];
            for (var i = 0; i < sampleCount; i++)
            {
                var t = (float)i / SampleRate;
                var samplesFromEnd = sampleCount - i;
                var envelope = samplesFromEnd < fadeOutSamples ? samplesFromEnd / (float)fadeOutSamples : 1f;
                data[i] = Mathf.Sin(2f * Mathf.PI * frequencyHz * t) * envelope * 0.5f;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
