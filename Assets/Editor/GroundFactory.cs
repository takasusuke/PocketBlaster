using UnityEditor;
using UnityEngine;

namespace PocketBlaster.EditorTools
{
    /// <summary>
    /// 床にグリッドを敷くヘルパー(オーナー要望、2026-09-06:「移動している量が
    /// 分かるように床にグリッドなどをつけてほしいです」)。移動範囲(maxOffsetRadius)が
    /// 2.5m→9mに広がり、視界も回転できるようになったことで、何も目印が無いと
    /// 自分がどれだけ動いたか分かりにくくなっていた。専用アートは無いので、
    /// 手続き的に生成した格子模様のテクスチャをタイル張りする
    /// (../CLAUDE.md 11「初期実装では画像を作らない」と同じ考え方)。
    /// </summary>
    public static class GroundFactory
    {
        private static Texture2D _cachedGridTexture;

        public static void CreateGrid(string name, Vector3 center, float size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = name;
            go.transform.position = center;
            // 組み込みPlaneは1辺10mが基準サイズ。
            var scale = size / 10f;
            go.transform.localScale = new Vector3(scale, 1f, scale);

            var renderer = go.GetComponent<Renderer>();
            var material = new Material(Shader.Find("Standard"))
            {
                mainTexture = GetGridTexture()
            };
            // 1マスがだいたい1m四方になるようタイル数をサイズに合わせる。
            material.mainTextureScale = new Vector2(size, size);
            renderer.sharedMaterial = material;
        }

        private static Texture2D GetGridTexture()
        {
            if (_cachedGridTexture != null) return _cachedGridTexture;

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

            _cachedGridTexture = texture;
            return texture;
        }
    }
}
