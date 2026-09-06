using UnityEditor;
using UnityEngine;

namespace PocketBlaster.EditorTools
{
    /// <summary>
    /// `Assets/Resources/Pickups/`配下のPickup用アート(PickupFactory参照)を
    /// Spriteとして読み込めるようにimport設定を矯正する。EnemyFactoryは
    /// シーン構築(Editor実行)のたびに同様の矯正を行うが、PickupFactoryは
    /// ランタイムコード(UnityEditor非依存)であるため、代わりにこの独立した
    /// Editorユーティリティを画像生成後に一度実行する必要がある。
    /// </summary>
    public static class PickupArtImporter
    {
        private const string PickupsFolder = "Assets/Resources/Pickups";

        [MenuItem("Tools/PocketBlaster/Fix Pickup Art Import Settings")]
        public static void FixImportSettings()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { PickupsFolder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                if (importer.textureType == TextureImporterType.Sprite) continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
                Debug.Log($"[PickupArtImporter] Sprite化しました: {path}");
            }
        }
    }
}
