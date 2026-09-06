using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PocketBlaster.EditorTools
{
    /// <summary>
    /// Unity EditorのGUI起動時に、指定したシーンを自動で開く仕組み(オーナー要望、2026-09-06:
    /// 「Unityで開くべきシーンは、デフォルトシーンに随時設定するようにして」)。
    ///
    /// 試して効かなかった方法(2026-09-06):
    ///   1. バッチモードでEditorSceneManager.OpenScene()してから-quit
    ///      → 次回GUI起動時の「最後に開いていたシーン」に反映されない
    ///        (ウィンドウレイアウトの一部としてGUIセッションでしか保存されない模様)
    ///   2. GUI起動コマンドの引数にシーンファイルパスを直接渡す(Unity.exe <sceneパス>)
    ///      → これも反映されず、Untitled Sceneが開いた
    ///
    /// この方式は、GUIとして実際に起動するEditorプロセス自身のコードとして動く
    /// ([InitializeOnLoad]は起動時に必ず走る)ため、上記2つと違い外部プロセスからの
    /// 押し付けに頼らない。マーカーファイル(プロジェクト直下、.gitignore済み)に
    /// 開きたいシーンのパスを書いてからUnityを起動する運用にする。
    /// </summary>
    [InitializeOnLoad]
    public static class PendingSceneOpener
    {
        private const string MarkerFileName = ".pending-scene-to-open.txt";

        static PendingSceneOpener()
        {
            EditorApplication.delayCall += TryOpenPendingScene;
        }

        private static void TryOpenPendingScene()
        {
            var projectRoot = Path.Combine(Application.dataPath, "..");
            var markerPath = Path.Combine(projectRoot, MarkerFileName);

            if (!File.Exists(markerPath))
            {
                return;
            }

            var scenePath = File.ReadAllText(markerPath).Trim();
            File.Delete(markerPath);

            if (string.IsNullOrEmpty(scenePath))
            {
                return;
            }

            var fullScenePath = Path.Combine(projectRoot, scenePath);
            if (!File.Exists(fullScenePath))
            {
                Debug.LogWarning($"[PendingSceneOpener] マーカーが指すシーンが見つかりません: {scenePath}");
                return;
            }

            EditorSceneManager.OpenScene(scenePath);
            Debug.Log($"[PendingSceneOpener] マーカーに従ってシーンを開きました: {scenePath}");
        }
    }
}
