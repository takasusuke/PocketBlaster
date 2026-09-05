using UnityEngine;

namespace PocketBlaster.Gameplay
{
    /// <summary>
    /// 固定的な敵(野菜ゾンビ、docs/requirements.md 決定済み事項)。「撃った時の感触」
    /// (着弾フィードバック・敵の反応、同ファイル §2 体験の核)を担う。見た目は3Dモデルでは
    /// なく、カメラに正対するスプライト(Billboard参照、House of the Dead等の古典的手法)。
    /// 時間経過の状態遷移はTargetHitStateに任せ、ここでは見た目への反映だけを行う。
    ///
    /// 倒れ込みの回転は`visualTransform`(未設定ならこのGameObject自身)に対して行う —
    /// BillboardがこのGameObject自体の向きを毎フレームカメラ側へ強制するため、同じ
    /// Transformへ倒れ込み角度を適用すると上書きされて消えてしまう。スプライト構成では
    /// 子オブジェクト(Visual)側にSpriteRendererと倒れ込みを持たせ、ルート側はBillboardと
    /// コライダーだけを持つ。
    /// </summary>
    public class Target : MonoBehaviour
    {
        [SerializeField] private Transform visualTransform;
        [SerializeField] private float flashDurationSeconds = 0.08f;
        [SerializeField] private float knockDurationSeconds = 0.25f;
        [SerializeField] private float downDurationSeconds = 1.2f;
        [SerializeField] private Color hitFlashColor = Color.white;
        [SerializeField] private float knockbackAngleDegrees = 70f;
        [SerializeField] private bool respawnsAfterDefeat = true;
        [SerializeField] private Color juiceColor = Color.red;
        [SerializeField] private int hitPoints = 1;
        [SerializeField] private int pointValue = 100;

        /// <summary>撃たれるたびに呼ばれる(復帰する場合も含む)</summary>
        public event System.Action OnHit;
        /// <summary>
        /// 倒された後に復帰しない設定(respawnsAfterDefeat=false)の時、退場が確定した瞬間に
        /// 1回だけ呼ばれる。引数は自分自身(StageDirectorが得点計算にPointValueを使う)。
        /// </summary>
        public event System.Action<Target> OnDefeated;

        public int PointValue => pointValue;

        private Transform _visual;
        private Renderer _renderer;
        private Collider _collider;
        private Color _baseColor;
        private Quaternion _baseRotation;
        private Quaternion _knockedRotation;
        private TargetHitState _state;
        private bool _hasFiredDefeated;

        public bool IsHittable => _state.IsHittable;

        private void Awake()
        {
            _visual = visualTransform != null ? visualTransform : transform;
            _renderer = GetComponentInChildren<Renderer>();
            _collider = GetComponent<Collider>();
            _baseColor = _renderer.material.color;
            _baseRotation = _visual.localRotation;
            _knockedRotation = _baseRotation * Quaternion.Euler(knockbackAngleDegrees, 0f, 0f);
            _state = new TargetHitState(flashDurationSeconds, knockDurationSeconds, downDurationSeconds, respawnsAfterDefeat, hitPoints);
        }

        public void TakeHit()
        {
            if (_state.TryHit())
            {
                OnHit?.Invoke();
                JuiceSplashEffect.SpawnAt(transform.position, juiceColor);
            }
        }

        private void Update()
        {
            _state.Tick(Time.deltaTime);
            ApplyVisual();

            if (_state.CurrentPhase == TargetHitState.Phase.Defeated && !_hasFiredDefeated)
            {
                _hasFiredDefeated = true;
                if (_collider != null) _collider.enabled = false;
                _renderer.enabled = false;
                OnDefeated?.Invoke(this);
            }
        }

        private void ApplyVisual()
        {
            switch (_state.CurrentPhase)
            {
                case TargetHitState.Phase.Idle:
                    _renderer.material.color = _baseColor;
                    _visual.localRotation = _baseRotation;
                    break;
                case TargetHitState.Phase.Flash:
                    _renderer.material.color = hitFlashColor;
                    _visual.localRotation = _baseRotation;
                    break;
                case TargetHitState.Phase.KnockDown:
                    _renderer.material.color = _baseColor;
                    _visual.localRotation = Quaternion.Slerp(_baseRotation, _knockedRotation, _state.PhaseProgress01);
                    break;
                case TargetHitState.Phase.Down:
                    _visual.localRotation = _knockedRotation;
                    break;
                case TargetHitState.Phase.RecoverUp:
                    _visual.localRotation = Quaternion.Slerp(_knockedRotation, _baseRotation, _state.PhaseProgress01);
                    break;
            }
        }
    }
}
