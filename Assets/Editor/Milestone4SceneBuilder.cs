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

        private const string TomatoSpritePath = "Assets/Art/Enemies/tomato_zombie.png";
        private const string CarrotSpritePath = "Assets/Art/Enemies/carrot_zombie.png";
        private const string OnionSpritePath = "Assets/Art/Enemies/onion_zombie.png";
        private const string PumpkinBossSpritePath = "Assets/Art/Enemies/pumpkin_zombie_boss.png";

        private static readonly Color TomatoJuice = new Color(0.9f, 0.15f, 0.1f);
        private static readonly Color CarrotJuice = new Color(0.95f, 0.55f, 0.1f);
        private static readonly Color OnionJuice = new Color(0.85f, 0.8f, 0.9f);
        private static readonly Color PumpkinJuice = new Color(0.9f, 0.45f, 0.05f);

        [MenuItem("PocketBlaster/Build Milestone4 Scene")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var tomatoSprite = EnemyFactory.LoadSpriteOrPlaceholder(TomatoSpritePath);
            var carrotSprite = EnemyFactory.LoadSpriteOrPlaceholder(CarrotSpritePath);
            var onionSprite = EnemyFactory.LoadSpriteOrPlaceholder(OnionSpritePath);
            var pumpkinSprite = EnemyFactory.LoadSpriteOrPlaceholder(PumpkinBossSpritePath);

            var wave1Waypoint = CreateWaypoint("Waypoint_Wave1", new Vector3(0f, 1.6f, 0f));
            var wave2Waypoint = CreateWaypoint("Waypoint_Wave2", new Vector3(0f, 1.6f, 1f));
            var wave3Waypoint = CreateWaypoint("Waypoint_Wave3", new Vector3(0f, 1.6f, 2f));

            // PlayerRig(StageDirectorがウェーブ間でLerp移動させる)の子にカメラを置く。
            // カメラ自身のローカル位置はPlayerLocomotionが足踏みのたびに動かす — 親(Rigの
            // ウェーブ間移動)と子(その場の微移動)が同じTransformの同じプロパティを
            // 取り合わないようにするため(StageDirector.cs参照)。
            var playerRigGo = new GameObject("PlayerRig");
            playerRigGo.transform.position = wave1Waypoint.position;
            playerRigGo.transform.rotation = wave1Waypoint.rotation;

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            cameraGo.transform.SetParent(playerRigGo.transform, false);
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.35f, 0.55f, 0.75f);
            cameraGo.AddComponent<AudioListener>();

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var wave1Enemies = new[]
            {
                EnemyFactory.CreateVegetableZombie("Wave1_Tomato_L", new Vector3(-2f, 1.6f, 8f), tomatoSprite, TomatoJuice, 2f, respawns: false, approaches: true),
                EnemyFactory.CreateVegetableZombie("Wave1_Carrot_R", new Vector3(2f, 1.6f, 8f), carrotSprite, CarrotJuice, 2f, respawns: false, approaches: true),
            };

            var wave2Enemies = new[]
            {
                EnemyFactory.CreateVegetableZombie("Wave2_Tomato_L", new Vector3(-3f, 1.6f, 7f), tomatoSprite, TomatoJuice, 2f, respawns: false, approaches: true),
                EnemyFactory.CreateVegetableZombie("Wave2_Onion_C", new Vector3(0f, 1.6f, 7.5f), onionSprite, OnionJuice, 2f, respawns: false, approaches: true),
                EnemyFactory.CreateVegetableZombie("Wave2_Carrot_R", new Vector3(3f, 1.6f, 7f), carrotSprite, CarrotJuice, 2f, respawns: false, approaches: true),
            };

            var wave3Enemies = new[]
            {
                EnemyFactory.CreateVegetableZombie("Wave3_PumpkinBoss", new Vector3(0f, 2f, 6f), pumpkinSprite, PumpkinJuice, 3.5f, respawns: false, approaches: true, approachSpeed: 0.4f),
            };

            var rigGo = new GameObject("GyroAimTestRig");
            rigGo.AddComponent<PhoneControllerServer>();
            var reticleController = rigGo.AddComponent<GyroReticleController>();
            var locomotion = rigGo.AddComponent<PlayerLocomotion>();
            var locomotionSo = new SerializedObject(locomotion);
            locomotionSo.FindProperty("movableRoot").objectReferenceValue = cameraGo.transform;
            locomotionSo.FindProperty("aimSource").objectReferenceValue = reticleController;
            locomotionSo.ApplyModifiedPropertiesWithoutUndo();
            rigGo.AddComponent<GameSession>();

            var directorGo = new GameObject("StageDirector");
            var director = directorGo.AddComponent<StageDirector>();
            var directorSo = new SerializedObject(director);
            directorSo.FindProperty("stageCamera").objectReferenceValue = camera;
            directorSo.FindProperty("moveTarget").objectReferenceValue = playerRigGo.transform;

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
