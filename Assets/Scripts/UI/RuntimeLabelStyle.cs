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
    /// </summary>
    public static class RuntimeLabelStyle
    {
        private static Font _builtinFont;
        private static bool _lookedUpFont;

        public static void ApplyDefaultFont(Label label)
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
                    Debug.LogWarning("[RuntimeLabelStyle] 組み込みフォントが見つかりませんでした。Labelのテキストが描画されない可能性があります。");
                }
            }

            if (_builtinFont != null)
            {
                label.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(_builtinFont));
            }
        }
    }
}
