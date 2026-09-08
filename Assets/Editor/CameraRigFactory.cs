using PocketBlaster.Gameplay;
using UnityEngine;

namespace PocketBlaster.EditorTools
{
    /// <summary>
    /// カメラの「動かす側」(PlayerLocomotion.movableRoot / (Multiplayer)StageDirector.
    /// moveTarget)と「実体」(Cameraコンポーネント)を分離して生成する共通ヘルパー
    /// (オーナー承認2026-09-08、画面シェイク導入のため)。
    ///
    /// 従来は各シーンビルダーが1つのGameObjectに直接Cameraコンポーネントを持たせ、
    /// PlayerLocomotion/MultiplayerStageDirector.MoveCameraToがそのTransformへ
    /// 直接書き込んでいた。画面シェイク(CameraShake)をこれらと安全に共存させるため、
    /// 実際にCameraコンポーネントを持つ子(Lens)を1段追加した——既存の2つの仕組みは
    /// 引き続き返り値の`rigGo`だけを動かせばよく、呼び出し側の書き換えは
    /// 「カメラ生成の数行」だけで済む(movableRoot/moveTargetに渡す値は
    /// 従来通り`rigGo.transform`のまま)。
    /// </summary>
    public static class CameraRigFactory
    {
        public static (GameObject rigGo, Camera camera, CameraShake shake) Create(string name)
        {
            var rigGo = new GameObject(name);

            var lensGo = new GameObject($"{name}_Lens");
            lensGo.tag = "MainCamera";
            lensGo.transform.SetParent(rigGo.transform, false);
            var camera = lensGo.AddComponent<Camera>();
            lensGo.AddComponent<AudioListener>();
            var shake = lensGo.AddComponent<CameraShake>();

            return (rigGo, camera, shake);
        }
    }
}
