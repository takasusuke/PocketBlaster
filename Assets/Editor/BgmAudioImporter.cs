using UnityEditor;
using UnityEngine;

namespace PocketBlaster.EditorTools
{
    /// <summary>
    /// `Assets/Resources/Audio/BGM/`配下のBGM(WAV、ローカルBGM生成パイプライン
    /// 由来)のimport設定を矯正する(2026-09-08、オーナー要望「同様のアーケードゲームと
    /// 比べて機能やUIやUXで足りていない部分を...実装する」——BGM新設)。WAVの既定
    /// import設定(Decompress On Load)のままだと、数MBのループ音楽を毎回全展開して
    /// メモリに載せることになる——`PickupArtImporter`と同じパターンで、生成直後に
    /// 一度だけ実行する。
    /// </summary>
    public static class BgmAudioImporter
    {
        private const string BgmFolder = "Assets/Resources/Audio/BGM";

        [MenuItem("Tools/PocketBlaster/Fix BGM Audio Import Settings")]
        public static void FixImportSettings()
        {
            var guids = AssetDatabase.FindAssets("t:AudioClip", new[] { BgmFolder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null) continue;

                var settings = importer.defaultSampleSettings;
                if (settings.loadType == AudioClipLoadType.CompressedInMemory &&
                    settings.compressionFormat == AudioCompressionFormat.Vorbis)
                {
                    continue;
                }

                settings.loadType = AudioClipLoadType.CompressedInMemory;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.7f;
                importer.defaultSampleSettings = settings;
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
                Debug.Log($"[BgmAudioImporter] 圧縮設定を適用しました: {path}");
            }
        }
    }
}
