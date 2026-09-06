using System.IO;
using PocketBlaster.Aim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PocketBlaster.EditorTools
{
    /// <summary>
    /// マイルストーン1(docs/requirements.md §4)の検証用シーンをコードから組み立てて保存する。
    /// 手書きの.unity YAMLはGUID管理が壊れやすいため、Unity自身に生成させる。
    /// `Unity.exe -batchmode -quit -executeMethod PocketBlaster.EditorTools.Milestone1SceneBuilder.Build`
    /// で実行する。
    /// </summary>
    public static class Milestone1SceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Milestone1_GyroAimTest.unity";

        [MenuItem("PocketBlaster/Build Milestone1 Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
            cameraGo.AddComponent<AudioListener>();

            var rigGo = new GameObject("GyroAimTestRig");
            // PhoneControllerServerはシーンに直接置かない — 永続シングルトンとして
            // GyroReticleController.Awake()がGetOrCreate()で取得/生成する
            // (2026-09-06、再挑戦での接続断対応。PhoneControllerServer.cs参照)。
            rigGo.AddComponent<GyroReticleController>();

            var dir = Path.GetDirectoryName(ScenePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            BuildSettingsHelper.EnsureSceneInBuildSettings(ScenePath);
            Debug.Log($"[Milestone1SceneBuilder] シーンを保存しました: {ScenePath}");
        }
    }
}
