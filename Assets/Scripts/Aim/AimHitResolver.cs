using PocketBlaster.Gameplay;
using UnityEngine;

namespace PocketBlaster.Aim
{
    /// <summary>
    /// 1本のレイから「何に当たったか」を判定する共通ロジック。`GyroReticleController`
    /// (シングルプレイヤー、既存)と`MultiplayerAimController`(マルチプレイヤー、新規)の
    /// 両方が同じ判定を必要とするため、`GyroReticleController.TryHitTargetAtReticle`から
    /// 切り出した(2026-09-07、マルチプレイヤーモード追加に伴うリファクタ)。
    ///
    /// 効果音の再生は呼び出し側の責務のまま残す(呼び出し側ごとにAudioSourceが違うため)。
    /// ここでは「何に当たったか」の判定と、命中対象への`TakeHit`/`TakeHeadshot`の
    /// 呼び出しまでを担う。
    /// </summary>
    public static class AimHitResolver
    {
        public enum Result
        {
            Miss,
            Hit,
            Headshot
        }

        /// <summary>
        /// ヘッドショット判定(オーナー要望、2026-09-06:「反動コントロール要素として、
        /// 敵のヘッドショットなど部位別のダメージ量変化」)。頭部コライダー(HeadHitbox)は
        /// 本体コライダーと奥行きがほぼ同じで前後関係が不安定なため、RaycastAllで視線上の
        /// 全ヒットを見て、頭部が含まれていれば距離に関わらず優先する。
        /// TargetとPickupはどちらもIShootable(共通の狙撃対象契約、IShootable.cs参照)
        /// なので、頭部以外はここで種類を区別せず同じ判定にまとめる。
        /// </summary>
        public static Result TryHit(Ray ray, float maxDistance, LayerMask layerMask)
        {
            var hits = Physics.RaycastAll(ray, maxDistance, layerMask);
            // RaycastAllは順序を保証しないため、距離順に並べてから見る
            // (複数の敵が視線上に重なっている場合に手前を優先するため)。
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var candidate in hits)
            {
                var headHitbox = candidate.collider.GetComponent<HeadHitbox>();
                if (headHitbox != null && headHitbox.Target != null && headHitbox.Target.IsHittable)
                {
                    headHitbox.Target.TakeHeadshot();
                    return Result.Headshot;
                }
            }

            foreach (var candidate in hits)
            {
                var shootable = candidate.collider.GetComponentInParent<IShootable>();
                if (shootable != null && shootable.IsHittable)
                {
                    shootable.TakeHit();
                    return Result.Hit;
                }
            }

            return Result.Miss;
        }
    }
}
