using UnityEditor;
using UnityEngine;

namespace PocketBlaster.EditorTools
{
    /// <summary>
    /// 床を敷くヘルパー。当初は移動量が分かるように手続き生成した格子模様だった
    /// (オーナー要望、2026-09-06:「移動している量が分かるように床にグリッドなどを
    /// つけてほしいです」)。専用アートが用意できたため、格子模様ではなく起動画面の
    /// 背景(`Assets/Resources/UI/title_background.png`)と同じモチーフ(裏庭の土・
    /// 芝生・落ち葉)の絵柄をタイル張りする方式に切り替えた(オーナー要望、2026-09-07:
    /// 「床はグリッドではなく、起動画面の背景の地面と同じモチーフで絵柄を配置して」)。
    /// 画像がまだ無い場合(生成中等)は、以前の格子模様へフォールバックする
    /// (../CLAUDE.md 11「初期実装では画像を作らない」と同じ考え方——アートが無くても
    /// 動作は止めない)。
    /// </summary>
    public static class GroundFactory
    {
        private const string GroundTexturePath = "Assets/Art/Environment/ground_texture.png";
        // 1タイル(生成画像1枚ぶん)が何m四方に相当するか。人が歩く庭の一角を
        // 描いた絵なので、グリッド時代の「1マス=1m」よりずっと大きく取る。
        private const float TileWorldSize = 4f;

        private static Texture2D _cachedGroundTexture;
        private static Texture2D _cachedFallbackGridTexture;

        public static void CreateGrid(string name, Vector3 center, float size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = name;
            go.transform.position = center;
            // 組み込みPlaneは1辺10mが基準サイズ。
            var scale = size / 10f;
            go.transform.localScale = new Vector3(scale, 1f, scale);

            var texture = GetGroundTexture();
            var tileWorldSize = texture == _cachedFallbackGridTexture ? 1f : TileWorldSize;

            var renderer = go.GetComponent<Renderer>();
            var material = new Material(Shader.Find("Standard"))
            {
                mainTexture = texture
            };
            material.mainTextureScale = new Vector2(size / tileWorldSize, size / tileWorldSize);
            renderer.sharedMaterial = material;
        }

        private static Texture2D GetGroundTexture()
        {
            if (_cachedGroundTexture != null) return _cachedGroundTexture;

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(GroundTexturePath);
            if (texture == null)
            {
                Debug.LogWarning($"[GroundFactory] 地面テクスチャが見つかりません({GroundTexturePath})。仮の格子模様で代替します。まだ生成中の可能性があります。");
                _cachedGroundTexture = GetFallbackGridTexture();
                return _cachedGroundTexture;
            }

            EnsureTiledImportSettings(GroundTexturePath);
            // import設定を変更した直後は参照が変わることがあるため読み直す。
            _cachedGroundTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(GroundTexturePath);
            return _cachedGroundTexture;
        }

        /// <summary>
        /// 新しく生成されたPNGは既定でwrapModeがRepeatになっていないことがあるため、
        /// タイル張りできるよう矯正する(EnemyFactory.EnsureSpriteImportSettingsと
        /// 同じ理由・同じパターン)。
        /// </summary>
        private static void EnsureTiledImportSettings(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;
            if (importer.wrapMode == TextureWrapMode.Repeat) return;

            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        private static Texture2D GetFallbackGridTexture()
        {
            if (_cachedFallbackGridTexture != null) return _cachedFallbackGridTexture;

            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            var background = new Color(0.24f, 0.27f, 0.3f);
            var line = new Color(0.5f, 0.56f, 0.6f);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var isLine = x < 1 || y < 1;
                    texture.SetPixel(x, y, isLine ? line : background);
                }
            }
            texture.Apply();
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Point;

            _cachedFallbackGridTexture = texture;
            return texture;
        }
    }
}
