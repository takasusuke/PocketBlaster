using UnityEngine;

namespace PocketBlaster.Gameplay
{
    /// <summary>
    /// 足踏み("step"メッセージ)・傾き入力それぞれから、狙っている/移動したい方向へ
    /// どれだけ動いたかを積み上げる。実際のTransform操作(PlayerLocomotion)から計算部分
    /// だけを切り出し、EditModeテストからPlay Modeなしで検証できるようにしてある
    /// (AmmoState等と同じ狙い)。
    ///
    /// オンレールという企画の前提(docs/requirements.md §1 決定済み事項)を崩さないよう、
    /// 移動量は原点(このオフセットの基準になった場所、通常はウェーブの立ち位置)から
    /// 一定半径(maxRadius)を超えないようクランプする。
    ///
    /// フィールドに障害物を置く(オーナー要望、2026-09-06)にあたり、PlayerLocomotion側で
    /// 「この移動は障害物に阻まれないか」を先に判定してから実際に位置を反映したいため、
    /// 内部状態を変更せずに結果だけ計算する<see cref="ComputeStepResult"/>と、判定後に
    /// 確定させる<see cref="SetOffset"/>を分けている。
    /// </summary>
    public class PlayerOffsetState
    {
        private readonly float _maxRadius;

        public Vector3 Offset { get; private set; }

        public PlayerOffsetState(float maxRadius)
        {
            _maxRadius = maxRadius;
        }

        /// <param name="direction">正規化されていなくてもよい(内部で正規化する)</param>
        /// <returns>更新後のOffset</returns>
        public Vector3 Step(Vector3 direction, float distance)
        {
            Offset = ComputeStepResult(direction, distance);
            return Offset;
        }

        /// <summary>
        /// Step()と同じ計算を、内部状態を変更せずに試算する。障害物判定など
        /// 「移動してよいか先に確かめたい」場面で使う。
        /// </summary>
        public Vector3 ComputeStepResult(Vector3 direction, float distance)
        {
            var flatDirection = new Vector3(direction.x, 0f, direction.z);
            var result = Offset;
            if (flatDirection.sqrMagnitude > 0.0001f)
            {
                result += flatDirection.normalized * distance;
                if (result.magnitude > _maxRadius)
                {
                    result = result.normalized * _maxRadius;
                }
            }
            return result;
        }

        /// <summary>障害物判定など、Step()を経由せず直接確定させたい時に使う。</summary>
        public void SetOffset(Vector3 offset)
        {
            Offset = offset;
        }

        public void Reset()
        {
            Offset = Vector3.zero;
        }
    }
}
