using System.IO;
using PocketBlaster.Aim;
using PocketBlaster.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PocketBlaster.EditorTools
{
    /// <summary>
    /// docs/requirements.md §8「将来の拡張」のうち「複数ステージ・ボス戦」を実現する
    /// 2本目のステージ。Milestone4_Stageと同じ4種のスプライトを再利用しつつ、
    /// ウェーブを1つ増やし(4ウェーブ)、最後をパンプキンボスの多段ヒット(3発、
    /// respawns:falseと組み合わせて「3発当てるまで倒れない」ボス戦にする、
    /// TargetHitState/Target参照)にしてある。
    ///
    /// 意図的に別シーンとして独立させ、Milestone1/3/4と同じく単独でPlay Modeに入って
    /// 試せるようにしてある(ステージ1からの自動遷移は実装していない — 各シーンが
    /// 単体でテストできる既存の方針を優先した。docs/HANDOFF.md参照)。
    /// `Unity.exe -batchmode -quit -executeMethod PocketBlaster.EditorTools.Stage2SceneBuilder.Build`
    /// で実行する。
    /// </summary>
    public static class Stage2SceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Stage2_BossRush.unity";

        private const string TomatoSpritePath = "Assets/Art/Enemies/tomato_zombie.png";
        private const string CarrotSpritePath = "Assets/Art/Enemies/carrot_zombie.png";
        private const string OnionSpritePath = "Assets/Art/Enemies/onion_zombie.png";
        private const string PumpkinBossSpritePath = "Assets/Art/Enemies/pumpkin_zombie_boss.png";

        private static readonly Color TomatoJuice = new Color(0.9f, 0.15f, 0.1f);
        private static readonly Color CarrotJuice = new Color(0.95f, 0.55f, 0.1f);
        private static readonly Color OnionJuice = new Color(0.85f, 0.8f, 0.9f);
        private static readonly Color PumpkinJuice = new Color(0.9f, 0.45f, 0.05f);

        [MenuItem("PocketBlaster/Build Stage2 Scene")]
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
            var wave4Waypoint = CreateWaypoint("Waypoint_Wave4_Boss", new Vector3(0f, 1.6f, 3f));

            var playerRigGo = new GameObject("PlayerRig");
            playerRigGo.transform.position = wave1Waypoint.position;
            playerRigGo.transform.rotation = wave1Waypoint.rotation;

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            cameraGo.transform.SetParent(playerRigGo.transform, false);
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.3f, 0.4f, 0.6f);
            cameraGo.AddComponent<AudioListener>();

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // オーナーからのプレイテストFB(2026-09-06)「敵が大きすぎて狙う要素が少ない。
            // 遠くから小さいところから表示してほしい」を受けて、出現距離を離し
            // スケールも控えめにした(以前はz=6-9・scale2-4)。ウェーブが進むほど
            // approachSpeedを少し上げて、後半ほど緊迫感が増すようにしてある。
            var wave1Enemies = new[]
            {
                EnemyFactory.CreateVegetableZombie("Wave1_Tomato_L", new Vector3(-3f, 1.6f, 18f), tomatoSprite, TomatoJuice, 1.3f, respawns: false, approaches: true, approachSpeed: 1f),
                EnemyFactory.CreateVegetableZombie("Wave1_Tomato_R", new Vector3(3f, 1.6f, 18f), tomatoSprite, TomatoJuice, 1.3f, respawns: false, approaches: true, approachSpeed: 1f),
            };

            var wave2Enemies = new[]
            {
                EnemyFactory.CreateVegetableZombie("Wave2_Carrot_L", new Vector3(-4f, 1.6f, 17f), carrotSprite, CarrotJuice, 1.3f, respawns: false, approaches: true, approachSpeed: 1f),
                EnemyFactory.CreateVegetableZombie("Wave2_Onion_C", new Vector3(0f, 1.6f, 17.5f), onionSprite, OnionJuice, 1.3f, respawns: false, approaches: true, approachSpeed: 1f),
                EnemyFactory.CreateVegetableZombie("Wave2_Carrot_R", new Vector3(4f, 1.6f, 17f), carrotSprite, CarrotJuice, 1.3f, respawns: false, approaches: true, approachSpeed: 1f),
            };

            var wave3Enemies = new[]
            {
                EnemyFactory.CreateVegetableZombie("Wave3_Tomato_L", new Vector3(-4f, 1.6f, 16f), tomatoSprite, TomatoJuice, 1.3f, respawns: false, approaches: true, approachSpeed: 1.1f),
                EnemyFactory.CreateVegetableZombie("Wave3_Carrot_ML", new Vector3(-1.3f, 1.6f, 16.5f), carrotSprite, CarrotJuice, 1.3f, respawns: false, approaches: true, approachSpeed: 1.1f),
                EnemyFactory.CreateVegetableZombie("Wave3_Carrot_MR", new Vector3(1.3f, 1.6f, 16.5f), carrotSprite, CarrotJuice, 1.3f, respawns: false, approaches: true, approachSpeed: 1.1f),
                EnemyFactory.CreateVegetableZombie("Wave3_Tomato_R", new Vector3(4f, 1.6f, 16f), tomatoSprite, TomatoJuice, 1.3f, respawns: false, approaches: true, approachSpeed: 1.1f),
            };

            // ボス: 3発当てるまで倒れない(TargetHitStateのhitPoints)。1〜2発目は短いFlashだけで
            // またIdleに戻り、狙い続けられる。最後の1発でようやくKnockDown〜Defeatedへ進む。
            var bossEnemy = EnemyFactory.CreateVegetableZombie(
                "Wave4_PumpkinBoss", new Vector3(0f, 2f, 15f), pumpkinSprite, PumpkinJuice, 2.4f, respawns: false,
                approaches: true, approachSpeed: 0.65f);
            ConfigureAsBoss(bossEnemy, hitPoints: 3, pointValue: 1000);
            var wave4Enemies = new[] { bossEnemy };

            var rigGo = new GameObject("GyroAimTestRig");
            // PhoneControllerServerは永続シングルトン(GetOrCreate)経由で取得する。
            // Milestone1SceneBuilder.cs参照。
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
            wavesProp.arraySize = 4;
            SetWave(wavesProp.GetArrayElementAtIndex(0), wave1Waypoint, wave1Enemies);
            SetWave(wavesProp.GetArrayElementAtIndex(1), wave2Waypoint, wave2Enemies);
            SetWave(wavesProp.GetArrayElementAtIndex(2), wave3Waypoint, wave3Enemies);
            SetWave(wavesProp.GetArrayElementAtIndex(3), wave4Waypoint, wave4Enemies);

            directorSo.ApplyModifiedPropertiesWithoutUndo();

            var dir = Path.GetDirectoryName(ScenePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            BuildSettingsHelper.EnsureSceneInBuildSettings(ScenePath);
            Debug.Log($"[Stage2SceneBuilder] シーンを保存しました: {ScenePath}");
        }

        private static Transform CreateWaypoint(string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            go.transform.rotation = Quaternion.identity;
            return go.transform;
        }

        private static void ConfigureAsBoss(Target target, int hitPoints, int pointValue)
        {
            var so = new SerializedObject(target);
            so.FindProperty("hitPoints").intValue = hitPoints;
            so.FindProperty("pointValue").intValue = pointValue;
            so.ApplyModifiedPropertiesWithoutUndo();
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
