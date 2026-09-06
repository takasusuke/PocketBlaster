using System.IO;
using PocketBlaster.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PocketBlaster.EditorTools
{
    /// <summary>
    /// 起動画面(docs/requirements.md、オーナー要望2026-09-06)の検証用シーン。
    /// `TitleScreenController`だけを置いた最小構成。他のシーンビルダーと同じく
    /// Unity自身にYAMLを生成させる。
    /// `Unity.exe -batchmode -quit -executeMethod PocketBlaster.EditorTools.TitleSceneBuilder.Build`
    /// で実行する。
    /// </summary>
    public static class TitleSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Title.unity";

        [MenuItem("PocketBlaster/Build Title Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.08f, 0.12f);
            cameraGo.AddComponent<AudioListener>();

            var titleGo = new GameObject("TitleScreen");
            titleGo.AddComponent<TitleScreenController>();

            var dir = Path.GetDirectoryName(ScenePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            BuildSettingsHelper.EnsureSceneInBuildSettings(ScenePath);
            Debug.Log($"[TitleSceneBuilder] シーンを保存しました: {ScenePath}");
        }
    }
}
