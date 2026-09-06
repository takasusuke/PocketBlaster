using System.Linq;
using UnityEditor;

namespace PocketBlaster.EditorTools
{
    /// <summary>
    /// SceneManager.LoadScene(名前 or buildIndex)はBuild Settingsに登録されている
    /// シーンしか読み込めない(Editorの再生モードでも同様)。GameSessionの「再挑戦」
    /// (現在のシーンを丸ごとリロード)のために、各シーンビルダーがシーンを保存した
    /// 直後にこれを呼んでBuild Settingsへ登録する。
    /// </summary>
    public static class BuildSettingsHelper
    {
        public static void EnsureSceneInBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == scenePath)) return;

            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
