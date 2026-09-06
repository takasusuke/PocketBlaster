using System.IO;
using PocketBlaster.Aim;
using PocketBlaster.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PocketBlaster.EditorTools
{
    /// <summary>
    /// 練習モード(オーナー要望、2026-09-06:「敵は出てこず、ただ移動して、構えて撃つだけの
    /// 練習をするモードを実装してください。このモードには、動かない的をステージ上に
    /// いくつか配置してください」)。ウェーブ進行・スコア・クリア判定(StageDirector)は
    /// 持たない — 自由に歩き回りながら、動かない的(Target、respawns:true・
    /// approaches:false)を好きなだけ撃って練習できるだけのシーン。的は倒れても
    /// 自動で復帰する(TargetHitState、Milestone3と同じ仕組み)ので回数制限が無い。
    /// Titleシーンから3つ目の開始ボタンとして選べる(TitleScreenController参照)。
    /// `Unity.exe -batchmode -quit -executeMethod PocketBlaster.EditorTools.PracticeRangeSceneBuilder.Build`
    /// で実行する。
    /// </summary>
    public static class PracticeRangeSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/PracticeRange.unity";

        private const string TomatoSpritePath = "Assets/Art/Enemies/tomato_zombie.png";
        private const string CarrotSpritePath = "Assets/Art/Enemies/carrot_zombie.png";
        private const string OnionSpritePath = "Assets/Art/Enemies/onion_zombie.png";

        [MenuItem("PocketBlaster/Build Practice Range Scene")]
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

            // 移動している量が分かるように床にグリッドを敷く(他のステージと同じ、
            // GroundFactory参照)。PlayerLocomotion.maxOffsetRadius(180m)を余裕を
            // 持ってカバーする大きさにしてある。
            GroundFactory.CreateGrid("Ground", Vector3.zero, 400f);

            var tomatoSprite = EnemyFactory.LoadSpriteOrPlaceholder(TomatoSpritePath);
            var carrotSprite = EnemyFactory.LoadSpriteOrPlaceholder(CarrotSpritePath);
            var onionSprite = EnemyFactory.LoadSpriteOrPlaceholder(OnionSpritePath);

            // 動かない的(オーナー要望:「動かない的をステージ上にいくつか配置して
            // ください」)。ウェーブ・撃破カウントの管理は一切持たず、倒れても
            // 自動で復帰する(respawns:true・approaches:false)ので回数を気にせず
            // 狙う練習だけに専念できる。距離・角度に幅を持たせて配置した。
            var targets = new[]
            {
                (Name: "Target_Near_L", Position: new Vector3(-5f, 1.6f, 10f), Kind: EnemyFactory.VegetableKind.Tomato, Sprite: tomatoSprite, Juice: new Color(0.9f, 0.15f, 0.1f)),
                (Name: "Target_Near_C", Position: new Vector3(0f, 1.6f, 12f), Kind: EnemyFactory.VegetableKind.Carrot, Sprite: carrotSprite, Juice: new Color(0.95f, 0.55f, 0.1f)),
                (Name: "Target_Near_R", Position: new Vector3(5f, 1.6f, 10f), Kind: EnemyFactory.VegetableKind.Onion, Sprite: onionSprite, Juice: new Color(0.85f, 0.8f, 0.9f)),
                (Name: "Target_Mid_L", Position: new Vector3(-14f, 1.6f, 18f), Kind: EnemyFactory.VegetableKind.Carrot, Sprite: carrotSprite, Juice: new Color(0.95f, 0.55f, 0.1f)),
                (Name: "Target_Mid_R", Position: new Vector3(14f, 1.6f, 18f), Kind: EnemyFactory.VegetableKind.Tomato, Sprite: tomatoSprite, Juice: new Color(0.9f, 0.15f, 0.1f)),
                (Name: "Target_Far_C", Position: new Vector3(0f, 1.6f, 30f), Kind: EnemyFactory.VegetableKind.Onion, Sprite: onionSprite, Juice: new Color(0.85f, 0.8f, 0.9f)),
            };
            foreach (var t in targets)
            {
                EnemyFactory.CreateVegetableZombie(t.Name, t.Position, t.Kind, t.Sprite, t.Juice, scale: 1.2f, respawns: true, approaches: false);
            }

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

            var dir = Path.GetDirectoryName(ScenePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            BuildSettingsHelper.EnsureSceneInBuildSettings(ScenePath);
            Debug.Log($"[PracticeRangeSceneBuilder] シーンを保存しました: {ScenePath}");
        }
    }
}
