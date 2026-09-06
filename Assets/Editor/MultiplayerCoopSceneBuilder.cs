using System.IO;
using PocketBlaster.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PocketBlaster.EditorTools
{
    /// <summary>
    /// マルチプレイヤーモード(2026-09-07、オーナー承認: 画面共有・移動オート・各自
    /// レティクル案)専用の新規ステージ。既存ステージ(Milestone4_Stage等)の敵配置は
    /// 流用せず、新規に組んだ(オーナー確認2026-09-06:「新規の専用ステージを1本作る」)。
    ///
    /// `PlayerLocomotion`/`GyroReticleController`/`GameSession`は置かない——移動が
    /// 完全に自動(`MultiplayerStageDirector`がカメラをウェイポイント間でLerp移動)で、
    /// 狙い・弾薬はプレイヤーの接続ごとに動的生成される`MultiplayerAimController`が
    /// 担うため。障害物は移動が無いため意味を持たず、置いていない。
    /// `Unity.exe -batchmode -quit -executeMethod PocketBlaster.EditorTools.MultiplayerCoopSceneBuilder.Build`
    /// で実行する。
    /// </summary>
    public static class MultiplayerCoopSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/MultiplayerCoop.unity";

        private const string TomatoSpritePath = "Assets/Art/Enemies/tomato_zombie.png";
        private const string CarrotSpritePath = "Assets/Art/Enemies/carrot_zombie.png";
        private const string OnionSpritePath = "Assets/Art/Enemies/onion_zombie.png";
        private const string PumpkinBossSpritePath = "Assets/Art/Enemies/pumpkin_zombie_boss.png";

        private static readonly Color TomatoJuice = new Color(0.9f, 0.15f, 0.1f);
        private static readonly Color CarrotJuice = new Color(0.95f, 0.55f, 0.1f);
        private static readonly Color OnionJuice = new Color(0.85f, 0.8f, 0.9f);
        private static readonly Color PumpkinJuice = new Color(0.9f, 0.45f, 0.05f);

        [MenuItem("PocketBlaster/Build Multiplayer Coop Scene")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var tomatoSprite = EnemyFactory.LoadSpriteOrPlaceholder(TomatoSpritePath);
            var carrotSprite = EnemyFactory.LoadSpriteOrPlaceholder(CarrotSpritePath);
            var onionSprite = EnemyFactory.LoadSpriteOrPlaceholder(OnionSpritePath);
            var pumpkinSprite = EnemyFactory.LoadSpriteOrPlaceholder(PumpkinBossSpritePath);

            var waveWaypoints = new Transform[4];
            for (var i = 0; i < waveWaypoints.Length; i++)
            {
                waveWaypoints[i] = CreateWaypoint($"Waypoint_Wave{i + 1}", new Vector3(0f, 1.6f, i));
            }

            // シングルプレイヤーと違いPlayerLocomotionによるその場の微移動が無いため、
            // カメラを直接MultiplayerStageDirectorのmoveTargetにする(ラップするRigは不要)。
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = waveWaypoints[0].position;
            cameraGo.transform.rotation = waveWaypoints[0].rotation;
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.35f, 0.55f, 0.75f);
            cameraGo.AddComponent<AudioListener>();

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            GroundFactory.CreateGrid("Ground", new Vector3(0f, 0f, 13f), 60f);

            const float normalScale = 0.9f;
            const float bossScale = 1.7f;

            var wave1Enemies = new[]
            {
                EnemyFactory.CreateVegetableZombie("Wave1_Tomato_L", new Vector3(-3f, 1.6f, 26f), EnemyFactory.VegetableKind.Tomato, tomatoSprite, TomatoJuice, normalScale, respawns: false, approaches: true),
                EnemyFactory.CreateVegetableZombie("Wave1_Tomato_R", new Vector3(3f, 1.6f, 26f), EnemyFactory.VegetableKind.Tomato, tomatoSprite, TomatoJuice, normalScale, respawns: false, approaches: true),
            };

            var wave2Enemies = new[]
            {
                EnemyFactory.CreateVegetableZombie("Wave2_Carrot_L", new Vector3(-4f, 1.6f, 25f), EnemyFactory.VegetableKind.Carrot, carrotSprite, CarrotJuice, normalScale, respawns: false, approaches: true),
                EnemyFactory.CreateVegetableZombie("Wave2_Onion_C", new Vector3(0f, 1.6f, 25.5f), EnemyFactory.VegetableKind.Onion, onionSprite, OnionJuice, normalScale, respawns: false, approaches: true),
                EnemyFactory.CreateVegetableZombie("Wave2_Carrot_R", new Vector3(4f, 1.6f, 25f), EnemyFactory.VegetableKind.Carrot, carrotSprite, CarrotJuice, normalScale, respawns: false, approaches: true),
            };

            var wave3Enemies = new[]
            {
                EnemyFactory.CreateVegetableZombie("Wave3_Tomato_L", new Vector3(-5f, 1.6f, 23f), EnemyFactory.VegetableKind.Tomato, tomatoSprite, TomatoJuice, normalScale, respawns: false, approaches: true),
                EnemyFactory.CreateVegetableZombie("Wave3_Carrot_M", new Vector3(0f, 1.6f, 23.5f), EnemyFactory.VegetableKind.Carrot, carrotSprite, CarrotJuice, normalScale, respawns: false, approaches: true),
                EnemyFactory.CreateVegetableZombie("Wave3_Onion_R", new Vector3(5f, 1.6f, 23f), EnemyFactory.VegetableKind.Onion, onionSprite, OnionJuice, normalScale, respawns: false, approaches: true),
            };

            var wave4Enemies = new[]
            {
                EnemyFactory.CreateVegetableZombie("Wave4_PumpkinBoss", new Vector3(0f, 2f, 22f), EnemyFactory.VegetableKind.PumpkinBoss, pumpkinSprite, PumpkinJuice, bossScale, respawns: false, approaches: true),
            };

            var directorGo = new GameObject("MultiplayerStageDirector");
            var director = directorGo.AddComponent<MultiplayerStageDirector>();
            var directorSo = new SerializedObject(director);
            directorSo.FindProperty("stageCamera").objectReferenceValue = camera;
            directorSo.FindProperty("moveTarget").objectReferenceValue = cameraGo.transform;

            var wavesProp = directorSo.FindProperty("waves");
            wavesProp.arraySize = 4;
            SetWave(wavesProp.GetArrayElementAtIndex(0), waveWaypoints[0], wave1Enemies);
            SetWave(wavesProp.GetArrayElementAtIndex(1), waveWaypoints[1], wave2Enemies);
            SetWave(wavesProp.GetArrayElementAtIndex(2), waveWaypoints[2], wave3Enemies);
            SetWave(wavesProp.GetArrayElementAtIndex(3), waveWaypoints[3], wave4Enemies);

            directorSo.ApplyModifiedPropertiesWithoutUndo();

            var dir = Path.GetDirectoryName(ScenePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            BuildSettingsHelper.EnsureSceneInBuildSettings(ScenePath);
            Debug.Log($"[MultiplayerCoopSceneBuilder] シーンを保存しました: {ScenePath}");
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
