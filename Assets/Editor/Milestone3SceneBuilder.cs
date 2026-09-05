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

        private const string TomatoSpritePath = "Assets/Art/Enemies/tomato_zombie.png";

        [MenuItem("PocketBlaster/Build Milestone3 Scene")]
        public static void Build()
        {
            AssetDatabase.Refresh();
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

            var tomatoSprite = EnemyFactory.LoadSpriteOrPlaceholder(TomatoSpritePath);
            EnemyFactory.CreateVegetableZombie(
                "Target_TomatoZombie", new Vector3(0f, 1.6f, 8f), tomatoSprite,
                juiceColor: new Color(0.9f, 0.15f, 0.1f), scale: 2f, respawns: true);

            var rigGo = new GameObject("GyroAimTestRig");
            rigGo.AddComponent<PhoneControllerServer>();
            var reticleController = rigGo.AddComponent<GyroReticleController>();
            var locomotion = rigGo.AddComponent<PlayerLocomotion>();
            var locomotionSo = new SerializedObject(locomotion);
            locomotionSo.FindProperty("movableRoot").objectReferenceValue = cameraGo.transform;
            locomotionSo.FindProperty("aimSource").objectReferenceValue = reticleController;
            locomotionSo.ApplyModifiedPropertiesWithoutUndo();
            rigGo.AddComponent<GameSession>();

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
