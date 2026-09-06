using UnityEngine;
using UnityEngine.UIElements;

namespace PocketBlaster.UI
{
    /// <summary>
    /// 実行時に`ScriptableObject.CreateInstance&lt;PanelSettings&gt;()`で生成した
    /// PanelSettingsには、エディタの「Create > UI Toolkit > Panel Settings Asset」で
    /// 作った場合と違い既定のThemeStyleSheetが付かない。その結果Labelがフォントを
    /// 解決できず、テキストが一切描画されない(2026-09-06、プレイテストのスクリーン
    /// ショットで発覚: スコア等のLabelが画面に何も出ていなかった一方、枠線だけの
    /// VisualElement(レティクル)は正しく描画されていたことから、テーマ全体ではなく
    /// テキスト描画だけがフォント未解決で欠落していると特定した)。
    /// Unity組み込みフォントを明示指定してテーマに依存せず描画させる。
    ///
    /// あわせて、ThemeStyleSheet未設定のPanelSettingsはUnity自身が
    /// "No Theme Style Sheet set to PanelSettings, UI will not render properly"という
    /// warningを出す(2026-09-06、ScorePopupBehaviourが敵を倒すたびに新規PanelSettingsを
    /// 作るため大量に出て発覚)。中身が空でもThemeStyleSheetインスタンスを割り当てれば
    /// このwarning自体は消える(実際の描画は上記のインラインstyle指定で賄っているため
    /// 空のテーマで問題ない)。
    /// </summary>
    public static class RuntimeLabelStyle
    {
        private static Font _builtinFont;
        private static bool _lookedUpFont;
        private static ThemeStyleSheet _blankTheme;

        /// <summary>
        /// 実行時生成のPanelSettingsに空のThemeStyleSheetを割り当て、Unity自身が出す
        /// "No Theme Style Sheet"警告を消す。BuildUi()でPanelSettingsを作った直後に呼ぶ。
        /// </summary>
        public static void EnsureTheme(PanelSettings panelSettings)
        {
            if (panelSettings.themeStyleSheet != null) return;
            if (_blankTheme == null)
            {
                _blankTheme = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            }
            panelSettings.themeStyleSheet = _blankTheme;
        }

        // Label・Button等、テキストを持つ要素はすべてTextElementを継承しているため
        // ここで一括して受ける(起動画面のButton/Sliderラベルでも同じ問題が起きるため)。
        public static void ApplyDefaultFont(TextElement element)
        {
            if (!_lookedUpFont)
            {
                _lookedUpFont = true;
                // "Arial.ttf"はUnity 6ではもう無効(GetBuiltinResourceが
                // ArgumentExceptionを投げる、2026-09-06に実機確認)。フォールバックには
                // 使えないので"LegacyRuntime.ttf"だけを使う。
                _builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_builtinFont == null)
                {
                    Debug.LogWarning("[RuntimeLabelStyle] 組み込みフォントが見つかりませんでした。テキストが描画されない可能性があります。");
                }
            }

            if (_builtinFont != null)
            {
                element.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(_builtinFont));
            }
        }
    }
}
