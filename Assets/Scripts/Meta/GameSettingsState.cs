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
    /// 個人差を吸収しきれない。
    /// </summary>
    public class GameSettingsState
    {
        public const float MinSensitivity = 4f;
        public const float MaxSensitivity = 24f;

        public bool IsArcadeMode { get; private set; }
        public float SfxVolume { get; private set; }
        public float VerticalSensitivity { get; private set; }
        public float HorizontalSensitivity { get; private set; }

        public GameSettingsState(bool isArcadeMode, float sfxVolume, float verticalSensitivity, float horizontalSensitivity)
        {
            IsArcadeMode = isArcadeMode;
            SfxVolume = ClampUnit(sfxVolume);
            VerticalSensitivity = ClampSensitivity(verticalSensitivity);
            HorizontalSensitivity = ClampSensitivity(horizontalSensitivity);
        }

        public static GameSettingsState CreateDefault()
        {
            return new GameSettingsState(isArcadeMode: false, sfxVolume: 1f, verticalSensitivity: 12f, horizontalSensitivity: 12f);
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
    }
}
