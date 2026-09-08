using System;
using UnityEngine;

namespace PocketBlaster.Gameplay
{
    public enum PickupType
    {
        Health,
        Reload,
        AmmoUp,
        /// <summary>一定時間だけ1発の発射で扇状に複数レイを飛ばす特殊武器
        /// (オーナー要望2026-09-08:「同様のアーケードゲームと比べて機能やUIやUXで
        /// 足りていない部分を...実装する」——ショットガン等の一定時間だけ持てる
        /// 特殊武器が定番なのに未実装だった)。AimHitResolver.TryHitSpread参照。</summary>
        Shotgun
    }

    /// <summary>
    /// 「撃つと効果を得られるアイテム」(オーナー要望、2026-09-06:「撃つとプレイヤーの
    /// 体力を回復するアイテムや、リロードできるアイテム、最大弾薬数を増加させる
    /// アイテムなどがマップ内にランダムに配置されたり出現されるようにしてください」)。
    /// 敵(Target)と同じくIShootableとして狙撃対象になるが、被弾演出(フラッシュ・
    /// 倒れ込み)は持たず、1発で即座に消費される。実際の効果適用はStageDirectorが
    /// <see cref="OnConsumed"/>を購読して行う(GyroReticleController/GameSessionへの
    /// 参照はStageDirectorが既に持っているため、ここでは何もしない)。
    ///
    /// 出現から一定時間で消える(オーナー要望2026-09-08:「同様のアーケードゲームと
    /// 比べて機能やUIやUXで足りていない部分を...実装する」——アイテムがずっと残り
    /// 続けると緊張感が無いという、House of the Dead等の定番演出を追う)。消える前は
    /// 点滅して予告する。時間切れは<see cref="OnExpired"/>で通知し、効果を伴う
    /// <see cref="OnConsumed"/>とは区別する——時間切れに効果は無い。
    /// </summary>
    public class Pickup : MonoBehaviour, IShootable
    {
        [SerializeField] private PickupType pickupType;
        [SerializeField] private float lifetimeSeconds = 8f;
        [SerializeField] private float blinkWarningSeconds = 3f;
        [SerializeField] private float blinkIntervalSeconds = 0.15f;

        public PickupType Type => pickupType;
        public bool IsHittable => !_isConsumed;

        /// <summary>撃たれて消費された瞬間に1回だけ呼ばれる。引数は自分自身。</summary>
        public event Action<Pickup> OnConsumed;

        /// <summary>撃たれずに時間切れで消えた瞬間に1回だけ呼ばれる。引数は自分自身。
        /// 効果は伴わない(OnConsumedとは別——出現元の後片付け専用)。</summary>
        public event Action<Pickup> OnExpired;

        private bool _isConsumed;
        private float _elapsed;
        private SpriteRenderer _spriteRenderer;

        /// <summary>MonoBehaviourにコンストラクタは使えないため、AddComponent直後に
        /// PickupFactoryから呼ぶ初期化メソッド。</summary>
        public void Initialize(PickupType type)
        {
            pickupType = type;
        }

        private void Awake()
        {
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void Update()
        {
            if (_isConsumed) return;
            _elapsed += Time.deltaTime;

            var remaining = lifetimeSeconds - _elapsed;
            if (remaining <= blinkWarningSeconds && _spriteRenderer != null)
            {
                _spriteRenderer.enabled = Mathf.PingPong(Time.time / blinkIntervalSeconds, 1f) > 0.5f;
            }

            if (_elapsed >= lifetimeSeconds)
            {
                _isConsumed = true;
                OnExpired?.Invoke(this);
                gameObject.SetActive(false);
            }
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
