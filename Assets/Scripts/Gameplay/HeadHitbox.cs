using UnityEngine;

namespace PocketBlaster.Gameplay
{
    /// <summary>
    /// 敵の頭部に相当する小さな当たり判定(オーナー要望、2026-09-06:「反動コントロール
    /// 要素として、敵のヘッドショットなど部位別のダメージ量変化を実装してもらって
    /// 試したい」)。ここへの被弾は残り被弾可能回数を無視して即座に倒す
    /// (Target.TakeHeadshot、TargetHitState.TryHit(isCritical:true)参照)。
    ///
    /// Targetの本体コライダー(全身を覆う)とは別の、頭部だけを覆う小さなコライダーを
    /// 持つ子オブジェクトに付ける(EnemyFactory参照)。GyroReticleControllerは
    /// Physics.RaycastAllで視線上の全ヒットを見て、この当たり判定が含まれていれば
    /// 本体コライダーより優先する(奥行きの近さでの取り合いを避けるため)。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class HeadHitbox : MonoBehaviour
    {
        [SerializeField] private Target target;

        public Target Target => target;

        private void Awake()
        {
            if (target == null) target = GetComponentInParent<Target>();
        }
    }
}
