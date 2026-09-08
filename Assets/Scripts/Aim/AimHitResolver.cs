using System.Collections.Generic;
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
        ///
        /// <paramref name="hitShootable"/>には命中した対象(Target/Pickup等)を返す
        /// (2026-09-07、マルチプレイヤーのアイテム対応で追加)。マルチプレイヤーは
        /// Pickup(アイテム)の効果を「誰が撃ったか」で振り分ける必要があり
        /// (MultiplayerAimController参照)、そのために何に当たったかを呼び出し側が
        /// 判定できるようにした。GyroReticleControllerは`out _`で無視してよい
        /// (振る舞いは変えていない)。
        /// </summary>
        public static Result TryHit(Ray ray, float maxDistance, LayerMask layerMask, out IShootable hitShootable)
        {
            hitShootable = null;
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
                    hitShootable = headHitbox.Target;
                    return Result.Headshot;
                }
            }

            foreach (var candidate in hits)
            {
                var shootable = candidate.collider.GetComponentInParent<IShootable>();
                if (shootable != null && shootable.IsHittable)
                {
                    shootable.TakeHit();
                    hitShootable = shootable;
                    return Result.Hit;
                }
            }

            return Result.Miss;
        }

        /// <summary>ショットガン系の特殊武器(オーナー要望2026-09-08、Pickup.PickupType.
        /// Shotgun参照)用。中心のレイを基準に水平方向へ扇状に<paramref name="rayCount"/>本の
        /// レイを飛ばし、それぞれ独立して<see cref="TryHit"/>する——1発の弾薬消費で
        /// 複数の敵/アイテムに同時命中しうる。垂直方向には広げない(単純さのため)。</summary>
        public struct SpreadHitResult
        {
            public int HitCount;
            public int HeadshotCount;
            public List<IShootable> HitShootables;
        }

        public static SpreadHitResult TryHitSpread(Ray centerRay, float spreadAngleDegrees, int rayCount, float maxDistance, LayerMask layerMask)
        {
            var result = new SpreadHitResult { HitShootables = new List<IShootable>() };
            var safeRayCount = Mathf.Max(rayCount, 1);

            for (var i = 0; i < safeRayCount; i++)
            {
                var angleOffset = safeRayCount == 1
                    ? 0f
                    : Mathf.Lerp(-spreadAngleDegrees / 2f, spreadAngleDegrees / 2f, i / (float)(safeRayCount - 1));
                var direction = Quaternion.AngleAxis(angleOffset, Vector3.up) * centerRay.direction;
                var ray = new Ray(centerRay.origin, direction);

                var hitResult = TryHit(ray, maxDistance, layerMask, out var hitShootable);
                if (hitResult == Result.Miss) continue;

                result.HitCount++;
                if (hitResult == Result.Headshot) result.HeadshotCount++;
                if (hitShootable != null) result.HitShootables.Add(hitShootable);
            }

            return result;
        }
    }
}
