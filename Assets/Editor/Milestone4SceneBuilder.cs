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
    /// マイルストーン4(docs/requirements.md §4)の検証用シーン。3ウェーブの固定敵配置と、
    /// ウェーブごとに切り替わるカメラ位置を持つ短いオンレールステージ。
    /// `Unity.exe -batchmode -quit -executeMethod PocketBlaster.EditorTools.Milestone4SceneBuilder.Build`
    /// で実行する。
    /// </summary>
    public static class Milestone4SceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Milestone4_Stage.unity";

        [MenuItem("PocketBlaster/Build Milestone4 Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var wave1Waypoint = CreateWaypoint("Waypoint_Wave1", new Vector3(0f, 1.6f, 0f));
            var wave2Waypoint = CreateWaypoint("Waypoint_Wave2", new Vector3(0f, 1.6f, 1f));
            var wave3Waypoint = CreateWaypoint("Waypoint_Wave3", new Vector3(0f, 1.6f, 2f));

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.35f, 0.55f, 0.75f);
            cameraGo.transform.position = wave1Waypoint.position;
            cameraGo.transform.rotation = wave1Waypoint.rotation;
            cameraGo.AddComponent<AudioListener>();

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var wave1Enemies = new[]
            {
                CreateEnemy("Wave1_Enemy_L", new Vector3(-2f, 1.6f, 8f), Color.red, 1f, respawns: false),
                CreateEnemy("Wave1_Enemy_R", new Vector3(2f, 1.6f, 8f), Color.red, 1f, respawns: false),
            };

            var wave2Enemies = new[]
            {
                CreateEnemy("Wave2_Enemy_L", new Vector3(-3f, 1.6f, 7f), new Color(0.9f, 0.5f, 0.1f), 1f, respawns: false),
                CreateEnemy("Wave2_Enemy_C", new Vector3(0f, 1.6f, 7.5f), new Color(0.9f, 0.5f, 0.1f), 1f, respawns: false),
                CreateEnemy("Wave2_Enemy_R", new Vector3(3f, 1.6f, 7f), new Color(0.9f, 0.5f, 0.1f), 1f, respawns: false),
            };

            var wave3Enemies = new[]
            {
                CreateEnemy("Wave3_Finale", new Vector3(0f, 1.8f, 6f), new Color(0.5f, 0.1f, 0.6f), 1.5f, respawns: false),
            };

            var rigGo = new GameObject("GyroAimTestRig");
            rigGo.AddComponent<PhoneControllerServer>();
            rigGo.AddComponent<GyroReticleController>();

            var directorGo = new GameObject("StageDirector");
            var director = directorGo.AddComponent<StageDirector>();
            var directorSo = new SerializedObject(director);
            directorSo.FindProperty("stageCamera").objectReferenceValue = camera;

            var wavesProp = directorSo.FindProperty("waves");
            wavesProp.arraySize = 3;
            SetWave(wavesProp.GetArrayElementAtIndex(0), wave1Waypoint, wave1Enemies);
            SetWave(wavesProp.GetArrayElementAtIndex(1), wave2Waypoint, wave2Enemies);
            SetWave(wavesProp.GetArrayElementAtIndex(2), wave3Waypoint, wave3Enemies);

            directorSo.ApplyModifiedPropertiesWithoutUndo();

            var dir = Path.GetDirectoryName(ScenePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[Milestone4SceneBuilder] シーンを保存しました: {ScenePath}");
        }

        private static Transform CreateWaypoint(string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            go.transform.rotation = Quaternion.identity;
            return go.transform;
        }

        private static Target CreateEnemy(string name, Vector3 position, Color color, float scale, bool respawns)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = Vector3.one * scale;

            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = new Material(renderer.sharedMaterial) { color = color };

            var target = go.AddComponent<Target>();
            var so = new SerializedObject(target);
            so.FindProperty("respawnsAfterDefeat").boolValue = respawns;
            so.ApplyModifiedPropertiesWithoutUndo();

            return target;
        }

        private static void SetWave(SerializedProperty waveProp, Transform waypoint, Target[] enemies)
        {
            waveProp.FindPropertyRelative("cameraWaypoint").objectReferenceValue = waypoint;
            var enemiesProp = waveProp.FindPropertyRelative("enemies");
            enemiesProp.arraySize = enemies.Length;
            for (var i = 0; i < enemies.Length; i++)
            {
                enemiesProp.GetArrayElementAtIndex(i).objectReferenceValue = enemies[i];
            }
        }
    }
}
