using UnityEngine;

namespace PocketBlaster.Gameplay
{
    /// <summary>
    /// アイテム(Pickup)を実行時に生成するヘルパー。EnemyFactory(Editorのみ、シーン構築時)
    /// と違い、ウェーブ開始のたびにランダムな位置へ生成する必要があるためランタイム
    /// コードにしてある(StageDirector参照)。専用アートはまだ無いので、種類ごとに
    /// 色分けした円形スプライトを手続き的に生成する(../CLAUDE.md 11「初期実装では
    /// 画像を作らない」と同じ考え方)。
    /// </summary>
    public static class PickupFactory
    {
        private static Sprite _cachedCircleSprite;

        public static Pickup Create(PickupType type, Vector3 position, float scale = 1.2f)
        {
            var root = new GameObject($"Pickup_{type}");
            root.transform.position = position;
            root.AddComponent<Billboard>();

            var sprite = GetCircleSprite();
            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(scale, scale, 0.3f);

            var visualGo = new GameObject("Visual");
            visualGo.transform.SetParent(root.transform, false);
            visualGo.transform.localScale = Vector3.one * scale;
            var spriteRenderer = visualGo.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = TypeColor(type);

            var pickup = root.AddComponent<Pickup>();
            pickup.Initialize(type);
            return pickup;
        }

        private static Color TypeColor(PickupType type)
        {
            switch (type)
            {
                case PickupType.Health: return new Color(0.3f, 0.9f, 0.4f);
                case PickupType.Reload: return new Color(0.3f, 0.6f, 1f);
                case PickupType.AmmoUp: return new Color(1f, 0.85f, 0.2f);
                default: return Color.white;
            }
        }

        private static Sprite GetCircleSprite()
        {
            if (_cachedCircleSprite != null) return _cachedCircleSprite;

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var center = new Vector2(size / 2f, size / 2f);
            var radius = size / 2f - 2f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var alpha = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) <= radius ? 1f : 0f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            texture.Apply();

            _cachedCircleSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _cachedCircleSprite;
        }
    }
}
