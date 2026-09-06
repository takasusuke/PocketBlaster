using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PocketBlaster.EditorTools
{
    /// <summary>
    /// バッチモードから指定したシーンを開いて(生成はしない、既存のシーンを開くだけ)
    /// そのまま終了するための汎用ヘルパー。
    ///
    /// Unity EditorはGUIで最後に開いていたシーンを次回起動時に自動で復元する。
    /// これを利用し、シーンを作成・更新した後は必ずこれで「開いた状態」にしてから
    /// `-quit`することで、次にGUIを起動した時にそのシーンが表示されるようにする
    /// (オーナー要望、2026-09-06: 「Unityで開くべきシーンは、デフォルトシーンに
    /// 随時設定するようにして」)。EditModeテストの実行等、シーン作成後に別のバッチ
    /// 操作を挟むと「最後に開いていたシーン」が変わってしまうことがあるため、
    /// GUIを再起動する直前には必ずこれを最後に実行する。
    ///
    /// 実行例:
    /// Unity.exe -batchmode -nographics -projectPath &lt;path&gt;
    ///   -openScenePath "Assets/Scenes/Stage2_BossRush.unity"
    ///   -executeMethod PocketBlaster.EditorTools.SceneLauncher.OpenSceneFromArgs -quit
    /// </summary>
    public static class SceneLauncher
    {
        public static void OpenSceneFromArgs()
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-openScenePath")
                {
                    var scenePath = args[i + 1];
                    EditorSceneManager.OpenScene(scenePath);
                    Debug.Log($"[SceneLauncher] シーンを開きました(次回GUI起動時のデフォルトになります): {scenePath}");
                    return;
                }
            }

            Debug.LogWarning("[SceneLauncher] -openScenePath <シーンのパス> が指定されていません。");
        }
    }
}
