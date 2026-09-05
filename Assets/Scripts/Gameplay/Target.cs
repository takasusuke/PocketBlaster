using UnityEngine;

namespace PocketBlaster.Gameplay
{
    /// <summary>
    /// マイルストーン3用の固定的な仮の敵。見た目はプリミティブのプレースホルダーで、
    /// 「撃った時の感触」(着弾フィードバック・敵の反応、docs/requirements.md §2 体験の核)を
    /// 検証するためだけの最小実装。本番のアート・モチーフは未決(同ファイル未決事項#2)。
    /// 時間経過の状態遷移はTargetHitStateに任せ、ここでは見た目への反映だけを行う。
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class Target : MonoBehaviour
    {
        [SerializeField] private float flashDurationSeconds = 0.08f;
        [SerializeField] private float knockDurationSeconds = 0.25f;
        [SerializeField] private float downDurationSeconds = 1.2f;
        [SerializeField] private Color hitFlashColor = Color.white;
        [SerializeField] private float knockbackAngleDegrees = 70f;
        [SerializeField] private bool respawnsAfterDefeat = true;

        /// <summary>撃たれるたびに呼ばれる(復帰する場合も含む)</summary>
        public event System.Action OnHit;
        /// <summary>倒された後に復帰しない設定(respawnsAfterDefeat=false)の時、退場が確定した瞬間に1回だけ呼ばれる</summary>
        public event System.Action OnDefeated;

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
            _renderer = GetComponent<Renderer>();
            _collider = GetComponent<Collider>();
            _baseColor = _renderer.material.color;
            _baseRotation = transform.localRotation;
            _knockedRotation = _baseRotation * Quaternion.Euler(knockbackAngleDegrees, 0f, 0f);
            _state = new TargetHitState(flashDurationSeconds, knockDurationSeconds, downDurationSeconds, respawnsAfterDefeat);
        }

        public void TakeHit()
        {
            if (_state.TryHit())
            {
                OnHit?.Invoke();
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
                OnDefeated?.Invoke();
            }
        }

        private void ApplyVisual()
        {
            switch (_state.CurrentPhase)
            {
                case TargetHitState.Phase.Idle:
                    _renderer.material.color = _baseColor;
                    transform.localRotation = _baseRotation;
                    break;
                case TargetHitState.Phase.Flash:
                    _renderer.material.color = hitFlashColor;
                    transform.localRotation = _baseRotation;
                    break;
                case TargetHitState.Phase.KnockDown:
                    _renderer.material.color = _baseColor;
                    transform.localRotation = Quaternion.Slerp(_baseRotation, _knockedRotation, _state.PhaseProgress01);
                    break;
                case TargetHitState.Phase.Down:
                    transform.localRotation = _knockedRotation;
                    break;
                case TargetHitState.Phase.RecoverUp:
                    transform.localRotation = Quaternion.Slerp(_knockedRotation, _baseRotation, _state.PhaseProgress01);
                    break;
            }
        }
    }
}
