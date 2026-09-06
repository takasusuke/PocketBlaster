using System;
using UnityEngine;

namespace PocketBlaster.Gameplay
{
    public enum PickupType
    {
        Health,
        Reload,
        AmmoUp
    }

    /// <summary>
    /// 「撃つと効果を得られるアイテム」(オーナー要望、2026-09-06:「撃つとプレイヤーの
    /// 体力を回復するアイテムや、リロードできるアイテム、最大弾薬数を増加させる
    /// アイテムなどがマップ内にランダムに配置されたり出現されるようにしてください」)。
    /// 敵(Target)と同じくIShootableとして狙撃対象になるが、被弾演出(フラッシュ・
    /// 倒れ込み)は持たず、1発で即座に消費される。実際の効果適用はStageDirectorが
    /// <see cref="OnConsumed"/>を購読して行う(GyroReticleController/GameSessionへの
    /// 参照はStageDirectorが既に持っているため、ここでは何もしない)。
    /// </summary>
    public class Pickup : MonoBehaviour, IShootable
    {
        [SerializeField] private PickupType pickupType;

        public PickupType Type => pickupType;
        public bool IsHittable => !_isConsumed;

        /// <summary>撃たれて消費された瞬間に1回だけ呼ばれる。引数は自分自身。</summary>
        public event Action<Pickup> OnConsumed;

        private bool _isConsumed;

        /// <summary>MonoBehaviourにコンストラクタは使えないため、AddComponent直後に
        /// PickupFactoryから呼ぶ初期化メソッド。</summary>
        public void Initialize(PickupType type)
        {
            pickupType = type;
        }

        public void TakeHit()
        {
            if (_isConsumed) return;
            _isConsumed = true;
            OnConsumed?.Invoke(this);
            gameObject.SetActive(false);
        }
    }
}
