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
    /// 6ウェーブ(2026-09-06、オーナー要望「1ラウンドの長さや敵の出現頻度を多く、
    /// 長くしてください」を受けて4→6ウェーブに拡張)、最後をパンプキンボスの
    /// 多段ヒット(3発、respawns:falseと組み合わせて「3発当てるまで倒れない」ボス戦に
    /// する、TargetHitState/Target参照)にしてある。
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

            var waveWaypoints = new Transform[6];
            for (var i = 0; i < waveWaypoints.Length; i++)
            {
                var name = i == waveWaypoints.Length - 1 ? $"Waypoint_Wave{i + 1}_Boss" : $"Waypoint_Wave{i + 1}";
                waveWaypoints[i] = CreateWaypoint(name, new Vector3(0f, 1.6f, i));
            }

            var playerRigGo = new GameObject("PlayerRig");
            playerRigGo.transform.position = waveWaypoints[0].position;
            playerRigGo.transform.rotation = waveWaypoints[0].rotation;

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

            // 敵はもっと遠く・小さく出す(オーナー要望、2026-09-06:「敵はもっともっと遠くて
            // 小さいところから出てくるイメージです」、以前はz=15-18・scale1.3-2.4)。
            // 被弾可能回数・移動速度・移動パターン(蛇行するか)は種類ごとにEnemyFactory側で
            // 固定のプロフィールとして持つ(オーナー要望「敵ごとに同じパラメータに
            // ならないように」)。
            const float normalScale = 0.85f;
            const float bossScale = 1.8f;

            var wave1Enemies = new[]
            {
                EnemyFactory.CreateVegetableZombie("Wave1_Tomato_L", new Vector3(-3f, 1.6f, 32f), EnemyFactory.VegetableKind.Tomato, tomatoSprite, TomatoJuice, normalScale, respawns: false, approaches: true),
                EnemyFactory.CreateVegetableZombie("Wave1_Tomato_R", new Vector3(3f, 1.6f, 32f), EnemyFactory.VegetableKind.Tomato, tomatoSprite, TomatoJuice, normalScale, respawns: false, approaches: true),
            };

            var wave2Enemies = new[]
            {
                EnemyFactory.CreateVegetableZombie("Wave2_Carrot_L", new Vector3(-4f, 1.6f, 30f), EnemyFactory.VegetableKind.Carrot, carrotSprite, CarrotJuice, normalScale, respawns: false, approaches: true),
                EnemyFactory.CreateVegetableZombie("Wave2_Onion_C", new Vector3(0f, 1.6f, 30.5f), EnemyFactory.VegetableKind.Onion, onionSprite, OnionJuice, normalScale, respawns: false, approaches: true),
                EnemyFactory.CreateVegetableZombie("Wave2_Carrot_R", new Vector3(4f, 1.6f, 30f), EnemyFactory.VegetableKind.Carrot, carrotSprite, CarrotJuice, normalScale, respawns: false, approaches: true),
            };

            var wave3Enemies = new[]
            {
                EnemyFactory.CreateVegetableZombie("Wave3_Tomato_L", new Vector3(-4f, 1.6f, 28f), EnemyFactory.VegetableKind.Tomato, tomatoSprite, TomatoJuice, normalScale, respawns: false, approaches: true),
                EnemyFactory.CreateVegetableZombie("Wave3_Carrot_ML", new Vector3(-1.3f, 1.6f, 28.5f), EnemyFactory.VegetableKind.Carrot, carrotSprite, CarrotJuice, normalScale, respawns: false, approaches: true),
                EnemyFactory.CreateVegetableZombie("Wave3_Carrot_MR", new Vector3(1.3f, 1.6f, 28.5f), EnemyFactory.VegetableKind.Carrot, carrotSprite, CarrotJuice, normalScale, respawns: false, approaches: true),
                EnemyFactory.CreateVegetableZombie("Wave3_Tomato_R", new Vector3(4f, 1.6f, 28f), EnemyFactory.VegetableKind.Tomato, tomatoSprite, TomatoJuice, normalScale, respawns: false, approaches: true),
            };

            var wave4Enemies = new[]
            {
                EnemyFactory.CreateVegetableZombie("Wave4_Onion_L", new Vector3(-4.5f, 1.6f, 26f), EnemyFactory.VegetableKind.Onion, onionSprite, OnionJuice, normalScale, respawns: false, approaches: true),
                EnemyFactory.CreateVegetableZombie("Wave4_Tomato_C", new Vector3(0f, 1.6f, 26.5f), EnemyFactory.VegetableKind.Tomato, tomatoSprite, TomatoJuice, normalScale, respawns: false, approaches: true),
                EnemyFactory.CreateVegetableZombie("Wave4_Onion_R", new Vector3(4.5f, 1.6f, 26f), EnemyFactory.VegetableKind.Onion, onionSprite, OnionJuice, normalScale, respawns: false, approaches: true),
            };

            var wave5Enemies = new[]
            {
                EnemyFactory.CreateVegetableZombie("Wave5_Carrot_L", new Vector3(-5f, 1.6f, 24f), EnemyFactory.VegetableKind.Carrot, carrotSprite, CarrotJuice, normalScale, respawns: false, approaches: true),
                EnemyFactory.CreateVegetableZombie("Wave5_Carrot_ML", new Vector3(-1.7f, 1.6f, 24.5f), EnemyFactory.VegetableKind.Carrot, carrotSprite, CarrotJuice, normalScale, respawns: false, approaches: true),
                EnemyFactory.CreateVegetableZombie("Wave5_Carrot_MR", new Vector3(1.7f, 1.6f, 24.5f), EnemyFactory.VegetableKind.Carrot, carrotSprite, CarrotJuice, normalScale, respawns: false, approaches: true),
                EnemyFactory.CreateVegetableZombie("Wave5_Carrot_R", new Vector3(5f, 1.6f, 24f), EnemyFactory.VegetableKind.Carrot, carrotSprite, CarrotJuice, normalScale, respawns: false, approaches: true),
            };

            // ボス: 3発当てるまで倒れない(EnemyFactoryのPumpkinBossプロフィール、
            // TargetHitState/Target参照)。1〜2発目は短いFlashだけでまたIdleに戻り、
            // 狙い続けられる。最後の1発でようやくKnockDown〜Defeatedへ進む。
            var wave6Enemies = new[]
            {
                EnemyFactory.CreateVegetableZombie("Wave6_PumpkinBoss", new Vector3(0f, 2f, 22f), EnemyFactory.VegetableKind.PumpkinBoss, pumpkinSprite, PumpkinJuice, bossScale, respawns: false, approaches: true),
            };

            // フィールドの障害物(オーナー要望、2026-09-06:「フィールドの構築が必要です。
            // オブジェクトを配置したり...パルクールをして上ったり」)。低い箱(高さ0.4、
            // PlayerLocomotion.stepUpHeight=0.6以下)は自動で乗り越えられ、高い壁(高さ1.6)は
            // 通れない。まずは最初の3ウェーブぶんだけ試作として置く(その後の調整で
            // 増減・配置を見直す前提)。
            ObstacleFactory.CreateBox("Obstacle_Wave1_LowBox", new Vector3(-2f, 0f, 3f), 0.6f, 0.4f, new Color(0.6f, 0.45f, 0.3f));
            ObstacleFactory.CreateBox("Obstacle_Wave1_Wall", new Vector3(2.5f, 0f, 4f), 0.5f, 1.6f, new Color(0.4f, 0.4f, 0.45f));
            ObstacleFactory.CreateBox("Obstacle_Wave2_LowBox", new Vector3(1.5f, 0f, 4f), 0.6f, 0.4f, new Color(0.6f, 0.45f, 0.3f));
            ObstacleFactory.CreateBox("Obstacle_Wave3_LowBox", new Vector3(-1.5f, 0f, 5f), 0.6f, 0.4f, new Color(0.6f, 0.45f, 0.3f));

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
            directorSo.FindProperty("reticleController").objectReferenceValue = reticleController;
            directorSo.FindProperty("playerLocomotion").objectReferenceValue = locomotion;

            var wavesProp = directorSo.FindProperty("waves");
            wavesProp.arraySize = 6;
            SetWave(wavesProp.GetArrayElementAtIndex(0), waveWaypoints[0], wave1Enemies);
            SetWave(wavesProp.GetArrayElementAtIndex(1), waveWaypoints[1], wave2Enemies);
            SetWave(wavesProp.GetArrayElementAtIndex(2), waveWaypoints[2], wave3Enemies);
            SetWave(wavesProp.GetArrayElementAtIndex(3), waveWaypoints[3], wave4Enemies);
            SetWave(wavesProp.GetArrayElementAtIndex(4), waveWaypoints[4], wave5Enemies);
            SetWave(wavesProp.GetArrayElementAtIndex(5), waveWaypoints[5], wave6Enemies);

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
