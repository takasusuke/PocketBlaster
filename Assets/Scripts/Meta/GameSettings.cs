using UnityEngine;

namespace PocketBlaster.Meta
{
    /// <summary>
    /// 起動画面(Title)で選んだ難易度・感度・SE音量を、シーンをまたいで受け渡すための
    /// 入れ物(オーナー要望、2026-09-06:「起動画面を実装して。そこから難易度選択や
    /// 設定などができるようにして」)。PlayerPrefsに保存するので次回起動時も前回の
    /// 選択を覚えている。値の妥当性はGameSettingsState側で担保する。
    ///
    /// 難易度はこれまでスマホ側(webapp/index.html)の接続前ラジオボタンで選んでいたが、
    /// 起動画面をPC側に作るのに伴いこちらへ一本化した(GameSession参照)。
    /// </summary>
    public static class GameSettings
    {
        private const string ModeKey = "PocketBlaster.Settings.IsArcadeMode";
        private const string SfxVolumeKey = "PocketBlaster.Settings.SfxVolume";
        private const string SensitivityKey = "PocketBlaster.Settings.Sensitivity";

        private static GameSettingsState _current;

        public static GameSettingsState Current
        {
            get
            {
                if (_current == null) _current = Load();
                return _current;
            }
        }

        public static void SetMode(bool isArcadeMode)
        {
            Current.SetMode(isArcadeMode);
            Save();
        }

        public static void SetSfxVolume(float value)
        {
            Current.SetSfxVolume(value);
            Save();
        }

        public static void SetSensitivity(float value)
        {
            Current.SetSensitivity(value);
            Save();
        }

        private static GameSettingsState Load()
        {
            var def = GameSettingsState.CreateDefault();
            return new GameSettingsState(
                PlayerPrefs.GetInt(ModeKey, def.IsArcadeMode ? 1 : 0) == 1,
                PlayerPrefs.GetFloat(SfxVolumeKey, def.SfxVolume),
                PlayerPrefs.GetFloat(SensitivityKey, def.Sensitivity));
        }

        private static void Save()
        {
            PlayerPrefs.SetInt(ModeKey, Current.IsArcadeMode ? 1 : 0);
            PlayerPrefs.SetFloat(SfxVolumeKey, Current.SfxVolume);
            PlayerPrefs.SetFloat(SensitivityKey, Current.Sensitivity);
            PlayerPrefs.Save();
        }
    }
}
