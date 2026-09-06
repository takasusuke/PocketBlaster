using System.IO;
using PocketBlaster.Aim;
using PocketBlaster.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PocketBlaster.EditorTools
{
    /// <summary>
    /// マイルストーン4(docs/requirements.md §4)の検証用シーン。5ウェーブの固定敵配置と、
    /// ウェーブごとに切り替わるカメラ位置を持つオンレールステージ(2026-09-06、オーナー
    /// 要望「1ラウンドの長さや敵の出現頻度を多く、長くしてください」を受けて3→5ウェーブに拡張)。
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

            var waveWaypoints = new Transform[5];
            for (var i = 0; i < waveWaypoints.Length; i++)
            {
                waveWaypoints[i] = CreateWaypoint($"Waypoint_Wave{i + 1}", new Vector3(0f, 1.6f, i));
            }

            // PlayerRig(StageDirectorがウェーブ間でLerp移動させる)の子にカメラを置く。
            // カメラ自身のローカル位置はPlayerLocomotionが足踏みのたびに動かす — 親(Rigの
            // ウェーブ間移動)と子(その場の微移動)が同じTransformの同じプロパティを
            // 取り合わないようにするため(StageDirector.cs参照)。
            var playerRigGo = new GameObject("PlayerRig");
            playerRigGo.transform.position = waveWaypoints[0].position;
            playerRigGo.transform.rotation = waveWaypoints[0].rotation;

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

            // 敵はもっと遠く・小さく出す(オーナー要望、2026-09-06:「敵はもっともっと遠くて
            // 小さいところから出てくるイメージです」、以前はz=14-16・scale1.4-2.2)。
            // 被弾可能回数・移動速度・移動パターン(蛇行するか)は種類ごとにEnemyFactory側で
            // 固定のプロフィールとして持つ(オーナー要望「敵ごとに同じパラメータに
            // ならないように」)。
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
                EnemyFactory.CreateVegetableZombie("Wave3_Tomato_L", new Vector3(-4f, 1.6f, 24f), EnemyFactory.VegetableKind.Tomato, tomatoSprite, TomatoJuice, normalScale, respawns: false, approaches: true),
                EnemyFactory.CreateVegetableZombie("Wave3_Onion_C", new Vector3(0f, 1.6f, 24.5f), EnemyFactory.VegetableKind.Onion, onionSprite, OnionJuice, normalScale, respawns: false, approaches: true),
                EnemyFactory.CreateVegetableZombie("Wave3_Carrot_R", new Vector3(4f, 1.6f, 24f), EnemyFactory.VegetableKind.Carrot, carrotSprite, CarrotJuice, normalScale, respawns: false, approaches: true),
            };

            var wave4Enemies = new[]
            {
                EnemyFactory.CreateVegetableZombie("Wave4_Tomato_L", new Vector3(-5f, 1.6f, 23f), EnemyFactory.VegetableKind.Tomato, tomatoSprite, TomatoJuice, normalScale, respawns: false, approaches: true),
                EnemyFactory.CreateVegetableZombie("Wave4_Carrot_ML", new Vector3(-1.5f, 1.6f, 23.5f), EnemyFactory.VegetableKind.Carrot, carrotSprite, CarrotJuice, normalScale, respawns: false, approaches: true),
                EnemyFactory.CreateVegetableZombie("Wave4_Carrot_MR", new Vector3(1.5f, 1.6f, 23.5f), EnemyFactory.VegetableKind.Carrot, carrotSprite, CarrotJuice, normalScale, respawns: false, approaches: true),
                EnemyFactory.CreateVegetableZombie("Wave4_Onion_R", new Vector3(5f, 1.6f, 23f), EnemyFactory.VegetableKind.Onion, onionSprite, OnionJuice, normalScale, respawns: false, approaches: true),
            };

            var wave5Enemies = new[]
            {
                EnemyFactory.CreateVegetableZombie("Wave5_PumpkinBoss", new Vector3(0f, 2f, 22f), EnemyFactory.VegetableKind.PumpkinBoss, pumpkinSprite, PumpkinJuice, bossScale, respawns: false, approaches: true),
            };

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

            var wavesProp = directorSo.FindProperty("waves");
            wavesProp.arraySize = 5;
            SetWave(wavesProp.GetArrayElementAtIndex(0), waveWaypoints[0], wave1Enemies);
            SetWave(wavesProp.GetArrayElementAtIndex(1), waveWaypoints[1], wave2Enemies);
            SetWave(wavesProp.GetArrayElementAtIndex(2), waveWaypoints[2], wave3Enemies);
            SetWave(wavesProp.GetArrayElementAtIndex(3), waveWaypoints[3], wave4Enemies);
            SetWave(wavesProp.GetArrayElementAtIndex(4), waveWaypoints[4], wave5Enemies);

            directorSo.ApplyModifiedPropertiesWithoutUndo();

            var dir = Path.GetDirectoryName(ScenePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            BuildSettingsHelper.EnsureSceneInBuildSettings(ScenePath);
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
