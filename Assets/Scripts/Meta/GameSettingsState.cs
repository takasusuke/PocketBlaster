namespace PocketBlaster.Meta
{
    /// <summary>
    /// 起動画面(Title)で選ぶ難易度・設定の値そのもの。UnityEngineに依存しない
    /// 純粋なC#クラスにして、EditModeテストから直接検証できるようにしている
    /// (../CLAUDE.md 10「機械的な作業はsonnet/haikuに出す」と同じ思想で、
    /// 値の妥当性チェックだけを切り出す)。PlayerPrefsへの保存/読み込みは
    /// GameSettings(こちらはUnityEngine依存)が担当する。
    ///
    /// 感度は上下(Vertical)・左右(Horizontal)を別々に持つ(オーナー要望、2026-09-06:
    /// 「上下左右方向の感度をユーザごとに調整できるようにしてください」)。持ち方や
    /// スマホの機種によって上下と左右で振れやすさが違うことがあるため、1本の値では
    /// 個人差を吸収しきれない。これは「構えている間」(照準)の感度。
    ///
    /// 「構えていない間」(視界の回転、PlayerLocomotion参照)は狙いとは意味も単位も
    /// 違う値(角度→ピクセルのオフセットではなく、角度→回転の角速度)なので、
    /// 別の設定(LookSensitivity)として独立させている(オーナー要望、2026-09-06:
    /// 「構えるときの感度と、構えていない時の感度はそれぞれ調整できるようにして
    /// 下さい」)。既定・上下限は範囲ごと5倍にしてある(オーナー要望、2026-09-06:
    /// 「構えていない状態の感度を5倍にしてください」)。
    /// </summary>
    public class GameSettingsState
    {
        public const float MinSensitivity = 4f;
        public const float MaxSensitivity = 24f;
        public const float MinLookSensitivity = 100f;
        public const float MaxLookSensitivity = 600f;

        public bool IsArcadeMode { get; private set; }
        public float SfxVolume { get; private set; }
        public float VerticalSensitivity { get; private set; }
        public float HorizontalSensitivity { get; private set; }
        public float LookSensitivity { get; private set; }

        public GameSettingsState(bool isArcadeMode, float sfxVolume, float verticalSensitivity, float horizontalSensitivity, float lookSensitivity)
        {
            IsArcadeMode = isArcadeMode;
            SfxVolume = ClampUnit(sfxVolume);
            VerticalSensitivity = ClampSensitivity(verticalSensitivity);
            HorizontalSensitivity = ClampSensitivity(horizontalSensitivity);
            LookSensitivity = ClampLookSensitivity(lookSensitivity);
        }

        public static GameSettingsState CreateDefault()
        {
            return new GameSettingsState(
                isArcadeMode: false, sfxVolume: 1f,
                verticalSensitivity: 12f, horizontalSensitivity: 12f, lookSensitivity: 300f);
        }

        public void SetMode(bool isArcadeMode)
        {
            IsArcadeMode = isArcadeMode;
        }

        public void SetSfxVolume(float value)
        {
            SfxVolume = ClampUnit(value);
        }

        public void SetVerticalSensitivity(float value)
        {
            VerticalSensitivity = ClampSensitivity(value);
        }

        public void SetHorizontalSensitivity(float value)
        {
            HorizontalSensitivity = ClampSensitivity(value);
        }

        public void SetLookSensitivity(float value)
        {
            LookSensitivity = ClampLookSensitivity(value);
        }

        private static float ClampUnit(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }

        private static float ClampSensitivity(float value)
        {
            if (value < MinSensitivity) return MinSensitivity;
            if (value > MaxSensitivity) return MaxSensitivity;
            return value;
        }

        private static float ClampLookSensitivity(float value)
        {
            if (value < MinLookSensitivity) return MinLookSensitivity;
            if (value > MaxLookSensitivity) return MaxLookSensitivity;
            return value;
        }
    }
}
