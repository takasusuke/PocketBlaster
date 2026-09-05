using System.IO;
using PocketBlaster.Aim;
using PocketBlaster.Gameplay;
using PocketBlaster.Networking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PocketBlaster.EditorTools
{
    /// <summary>
    /// マイルストーン3(docs/requirements.md §4)の検証用シーン。固定の仮の敵1体を
    /// スマホの狙いで撃てるだけの最小構成。Unity自身にYAMLを生成させる方針は
    /// Milestone1SceneBuilderと同じ。
    /// `Unity.exe -batchmode -quit -executeMethod PocketBlaster.EditorTools.Milestone3SceneBuilder.Build`
    /// で実行する。
    /// </summary>
    public static class Milestone3SceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Milestone3_ShootTarget.unity";

        [MenuItem("PocketBlaster/Build Milestone3 Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.35f, 0.55f, 0.75f);
            cameraGo.transform.position = new Vector3(0f, 1.6f, 0f);
            cameraGo.AddComponent<AudioListener>();

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var targetGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            targetGo.name = "Target_Placeholder";
            targetGo.transform.position = new Vector3(0f, 1.6f, 8f);
            var targetRenderer = targetGo.GetComponent<Renderer>();
            targetRenderer.sharedMaterial = new Material(targetRenderer.sharedMaterial)
            {
                color = new Color(0.8f, 0.25f, 0.25f)
            };
            targetGo.AddComponent<Target>();

            var rigGo = new GameObject("GyroAimTestRig");
            rigGo.AddComponent<PhoneControllerServer>();
            rigGo.AddComponent<GyroReticleController>();

            var dir = Path.GetDirectoryName(ScenePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[Milestone3SceneBuilder] シーンを保存しました: {ScenePath}");
        }
    }
}
