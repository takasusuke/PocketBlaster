using UnityEngine;

namespace PocketBlaster.Gameplay
{
    /// <summary>
    /// アイテム(Pickup)を実行時に生成するヘルパー。EnemyFactory(Editorのみ、シーン構築時)
    /// と違い、ウェーブ開始のたびにランダムな位置へ生成する必要があるためランタイム
    /// コードにしてある(StageDirector参照)。
    ///
    /// 体力回復(Health)・弾薬回復(Reload)は専用アートを用意した(オーナー要望2026-09-06:
    /// 「弾薬回復や体力回復の間にも画像を適用して」)。`Assets/Resources/Pickups/`配下に
    /// 置き、`Resources.Load`で読み込む — EnemyFactoryと違いランタイムコード(UnityEditor
    /// 非依存)からアセットパスで読み込む必要があるため、`Resources`フォルダの規約を使う。
    /// 見つからない場合(まだ生成中、またはAmmoUpのようにまだ専用アートが無い種類)は
    /// 手続き生成した色分け円形スプライトへ自動的にフォールバックする
    /// (../CLAUDE.md 11「初期実装では画像を作らない」と同じ考え方——アートが無くても
    /// 動作は止めない)。
    /// </summary>
    public static class PickupFactory
    {
        private static Sprite _cachedCircleSprite;

        public static Pickup Create(PickupType type, Vector3 position, float scale = 1.2f)
        {
            var root = new GameObject($"Pickup_{type}");
            root.transform.position = position;
            root.AddComponent<Billboard>();

            var resourcePath = ArtResourcePath(type);
            var artSprite = resourcePath != null ? Resources.Load<Sprite>(resourcePath) : null;
            var sprite = artSprite != null ? artSprite : GetCircleSprite();
            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(scale, scale, 0.3f);

            var visualGo = new GameObject("Visual");
            visualGo.transform.SetParent(root.transform, false);
            visualGo.transform.localScale = Vector3.one * scale;
            var spriteRenderer = visualGo.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            // 専用アートは絵そのものが種類を伝えるので色は付けない。フォールバックの
            // 円だけ、種類が見た目で区別できるよう色分けする。
            if (artSprite == null) spriteRenderer.color = TypeColor(type);

            var pickup = root.AddComponent<Pickup>();
            pickup.Initialize(type);
            return pickup;
        }

        /// <summary>`Resources.Load`に渡すパス(拡張子・"Resources/"接頭辞なし)。
        /// 専用アートが無い種類はnullを返し、呼び出し側で円にフォールバックさせる。</summary>
        private static string ArtResourcePath(PickupType type)
        {
            switch (type)
            {
                case PickupType.Health: return "Pickups/health_pickup";
                case PickupType.Reload: return "Pickups/ammo_reload_pickup";
                default: return null;
            }
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
