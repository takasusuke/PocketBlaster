using PocketBlaster.Gameplay;
using UnityEditor;
using UnityEngine;

namespace PocketBlaster.EditorTools
{
    /// <summary>
    /// スプライトベースの敵(野菜ゾンビ、docs/requirements.md 決定済み事項)を
    /// シーンビルダーから共通の手順で組み立てるためのヘルパー。ルート(Billboard+コライダー)と
    /// 子(Visual、SpriteRenderer+倒れ込み回転)を分けているのはTarget.cs参照。
    /// </summary>
    public static class EnemyFactory
    {
        public static Target CreateVegetableZombie(
            string name, Vector3 position, Sprite sprite, Color juiceColor, float scale, bool respawns)
        {
            var root = new GameObject(name);
            root.transform.position = position;
            root.AddComponent<Billboard>();

            var spriteSize = sprite != null ? (Vector3)sprite.bounds.size : Vector3.one;
            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(spriteSize.x * scale, spriteSize.y * scale, 0.3f);

            var visualGo = new GameObject("Visual");
            visualGo.transform.SetParent(root.transform, false);
            visualGo.transform.localScale = Vector3.one * scale;
            var spriteRenderer = visualGo.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;

            var target = root.AddComponent<Target>();
            var so = new SerializedObject(target);
            so.FindProperty("visualTransform").objectReferenceValue = visualGo.transform;
            so.FindProperty("respawnsAfterDefeat").boolValue = respawns;
            so.FindProperty("juiceColor").colorValue = juiceColor;
            so.ApplyModifiedPropertiesWithoutUndo();

            return target;
        }

        /// <summary>
        /// アセットパスからSpriteをロードする。読み込めない場合は白い仮のSpriteにフォールバックする
        /// (画像生成がまだキューで待っている間もシーン自体は組み立てられるようにするため)。
        /// </summary>
        public static Sprite LoadSpriteOrPlaceholder(string assetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath) != null)
            {
                EnsureSpriteImportSettings(assetPath);
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite != null) return sprite;

            Debug.LogWarning($"[EnemyFactory] スプライトが見つかりません({assetPath})。仮の白い四角で代替します。まだ生成中の可能性があります。");
            var texture = Texture2D.whiteTexture;
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// 新しく生成されたPNGは既定でTexture(Default)としてimportされ、Spriteとして
        /// 読み込めない。SpriteかつAlpha透過ありに矯正する。
        /// </summary>
        private static void EnsureSpriteImportSettings(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;
            if (importer.textureType == TextureImporterType.Sprite) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }
    }
}
