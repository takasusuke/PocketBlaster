namespace PocketBlaster.Gameplay
{
    /// <summary>
    /// 「撃たれた敵」の時間経過による状態遷移をUnityEngine非依存で表現したもの。
    /// MonoBehaviour(Target)はTick()で進め、CurrentPhase/PhaseProgress01を見て
    /// 色・回転などの見た目を反映するだけにする。Coroutineに頼らないことで、
    /// EditModeテストからPlay Modeなしで検証できる(AmmoStateと同じ狙い)。
    /// </summary>
    public class TargetHitState
    {
        public enum Phase
        {
            Idle,
            Flash,
            KnockDown,
            Down,
            RecoverUp
        }

        private readonly float _flashDuration;
        private readonly float _knockDuration;
        private readonly float _downDuration;
        private float _elapsedInPhase;

        public Phase CurrentPhase { get; private set; } = Phase.Idle;
        public bool IsHittable => CurrentPhase == Phase.Idle;

        /// <summary>現在のフェーズ内での進捗(0〜1)。KnockDown/RecoverUpの角度補間に使う。</summary>
        public float PhaseProgress01
        {
            get
            {
                switch (CurrentPhase)
                {
                    case Phase.Flash: return Clamp01(_elapsedInPhase / _flashDuration);
                    case Phase.KnockDown: return Clamp01(_elapsedInPhase / _knockDuration);
                    case Phase.Down: return 1f;
                    case Phase.RecoverUp: return Clamp01(_elapsedInPhase / _knockDuration);
                    default: return 0f;
                }
            }
        }

        public TargetHitState(float flashDuration, float knockDuration, float downDuration)
        {
            _flashDuration = flashDuration;
            _knockDuration = knockDuration;
            _downDuration = downDuration;
        }

        /// <returns>実際にヒット処理を開始できたか(反応中の二重ヒットは無視してfalse)</returns>
        public bool TryHit()
        {
            if (CurrentPhase != Phase.Idle) return false;
            Advance(Phase.Flash);
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (CurrentPhase == Phase.Idle) return;

            _elapsedInPhase += deltaTime;
            switch (CurrentPhase)
            {
                case Phase.Flash:
                    if (_elapsedInPhase >= _flashDuration) Advance(Phase.KnockDown);
                    break;
                case Phase.KnockDown:
                    if (_elapsedInPhase >= _knockDuration) Advance(Phase.Down);
                    break;
                case Phase.Down:
                    if (_elapsedInPhase >= _downDuration) Advance(Phase.RecoverUp);
                    break;
                case Phase.RecoverUp:
                    if (_elapsedInPhase >= _knockDuration) Advance(Phase.Idle);
                    break;
            }
        }

        private void Advance(Phase next)
        {
            CurrentPhase = next;
            _elapsedInPhase = 0f;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
