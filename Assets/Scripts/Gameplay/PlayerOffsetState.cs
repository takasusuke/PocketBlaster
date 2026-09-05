using UnityEngine;

namespace PocketBlaster.Gameplay
{
    /// <summary>
    /// 足踏み("step"メッセージ)1回ごとに、狙っている方向へどれだけ動いたかを積み上げる。
    /// 実際のTransform操作(PlayerLocomotion)から計算部分だけを切り出し、EditModeテストから
    /// Play Modeなしで検証できるようにしてある(AmmoState等と同じ狙い)。
    ///
    /// オンレールという企画の前提(docs/requirements.md §1 決定済み事項)を崩さないよう、
    /// 移動量は原点(このオフセットの基準になった場所、通常はウェーブの立ち位置)から
    /// 一定半径(maxRadius)を超えないようクランプする — 自由に歩き回れるわけではなく、
    /// その場での小さな踏み込み・回避に留める。
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
            var flatDirection = new Vector3(direction.x, 0f, direction.z);
            if (flatDirection.sqrMagnitude > 0.0001f)
            {
                Offset += flatDirection.normalized * distance;
                if (Offset.magnitude > _maxRadius)
                {
                    Offset = Offset.normalized * _maxRadius;
                }
            }
            return Offset;
        }

        public void Reset()
        {
            Offset = Vector3.zero;
        }
    }
}
