using UnityEngine;

namespace PocketBlaster.Gameplay
{
    /// <summary>
    /// フィールドに配置する障害物(オーナー要望、2026-09-06:「フィールドの構築が必要です。
    /// オブジェクトを配置したり、小さなオブジェクトに対して引き続き歩く場合には
    /// パルクールをして上ったり...する動作が必要になります」)。
    ///
    /// 物理エンジン(CharacterController等)には頼らない割り切った実装 — オンレール前提の
    /// 小さな移動範囲内でだけ機能すればよいため。<see cref="Height"/>が
    /// PlayerLocomotionの`stepUpHeight`以下なら、近づくと自動で「乗り越える」
    /// (見た目上、その場だけプレイヤーの高さを上げる)。それより高い障害物は
    /// 通過できず、手前で移動がブロックされる。判定は水平距離(<see cref="Radius"/>)
    /// だけで行う単純な円柱あたり。
    ///
    /// GameObject自体はBoxCollider付きの箱(ObstacleFactory参照)なので、
    /// レイキャストでの射撃判定(GyroReticleController.TryHitTargetAtReticle)も
    /// 自然にブロックされる(障害物の向こうの敵に弾が当たらない)——追加の実装は不要。
    /// </summary>
    public class Obstacle : MonoBehaviour
    {
        [SerializeField] private float radius = 0.6f;
        [SerializeField] private float height = 0.4f;
        [SerializeField] private bool isPlatform;

        public Vector3 Position => transform.position;
        public float Radius => radius;
        public float Height => height;

        /// <summary>
        /// trueなら`stepUpHeight`を無視して常に登れる「足場」として扱う
        /// (オーナー要望2026-09-06の落下ダメージを試すための高低差を作る用途。
        /// PlayerLocomotion.TryMoveTo参照)。falseの既定は従来通りObstacleCrossingで判定する
        /// 低い箱(乗り越え)や高い壁(通行不可)。
        /// </summary>
        public bool IsPlatform => isPlatform;
    }
}
