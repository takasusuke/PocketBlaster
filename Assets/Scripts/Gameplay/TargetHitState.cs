namespace PocketBlaster.Gameplay
{
    /// <summary>
    /// 「撃たれた敵」の時間経過による状態遷移をUnityEngine非依存で表現したもの。
    /// MonoBehaviour(Target)はTick()で進め、CurrentPhase/PhaseProgress01を見て
    /// 色・回転などの見た目を反映するだけにする。Coroutineに頼らないことで、
    /// EditModeテストからPlay Modeなしで検証できる(AmmoStateと同じ狙い)。
    ///
    /// マイルストーン3の単発フィードバック検証では、繰り返し試せるようDown後に
    /// 自動で起き上がる(RecoverUp→Idle)。マイルストーン4のウェーブ制ステージでは
    /// 「倒したら退場する」必要があるため、`respawns: false`でDownの後にDefeated
    /// (終端、二度と復帰しない)へ進むようにできる。
    ///
    /// `hitPoints`(既定1)を1より大きくすると、その回数分ヒットするまで倒れない
    /// 「ボス戦」用の多段ヒットになる(docs/requirements.md §8 将来の拡張)。
    /// 倒れるまでの各ヒットは短いFlashだけ行いIdleへ戻る(まだ狙える)。
    /// </summary>
    public class TargetHitState
    {
        public enum Phase
        {
            Idle,
            Flash,
            KnockDown,
            Down,
            RecoverUp,
            Defeated
        }

        private readonly float _flashDuration;
        private readonly float _knockDuration;
        private readonly float _downDuration;
        private readonly bool _respawns;
        private readonly int _maxHitPoints;
        private int _remainingHitPoints;
        private bool _pendingFatalHit;
        private float _elapsedInPhase;

        public Phase CurrentPhase { get; private set; } = Phase.Idle;
        public bool IsHittable => CurrentPhase == Phase.Idle;
        public int RemainingHitPoints => _remainingHitPoints;
        /// <summary>
        /// 生成時に渡された最大被弾回数。Target(見た目側)が「どれだけ削れたか」の比率
        /// (1 - RemainingHitPoints/MaxHitPoints)を出すのに使う(被弾での色変化、
        /// オーナー要望2026-09-06)。
        /// </summary>
        public int MaxHitPoints => _maxHitPoints;

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
                    case Phase.Defeated: return 1f;
                    default: return 0f;
                }
            }
        }

        public TargetHitState(float flashDuration, float knockDuration, float downDuration, bool respawns = true, int hitPoints = 1)
        {
            _flashDuration = flashDuration;
            _knockDuration = knockDuration;
            _downDuration = downDuration;
            _respawns = respawns;
            _maxHitPoints = hitPoints > 0 ? hitPoints : 1;
            _remainingHitPoints = _maxHitPoints;
        }

        /// <returns>実際にヒット処理を開始できたか(反応中の二重ヒットは無視してfalse)</returns>
        public bool TryHit()
        {
            if (CurrentPhase != Phase.Idle) return false;
            _remainingHitPoints--;
            _pendingFatalHit = _remainingHitPoints <= 0;
            Advance(Phase.Flash);
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (CurrentPhase == Phase.Idle || CurrentPhase == Phase.Defeated) return;

            _elapsedInPhase += deltaTime;
            switch (CurrentPhase)
            {
                case Phase.Flash:
                    if (_elapsedInPhase >= _flashDuration)
                    {
                        Advance(_pendingFatalHit ? Phase.KnockDown : Phase.Idle);
                    }
                    break;
                case Phase.KnockDown:
                    if (_elapsedInPhase >= _knockDuration) Advance(Phase.Down);
                    break;
                case Phase.Down:
                    if (_elapsedInPhase >= _downDuration) Advance(_respawns ? Phase.RecoverUp : Phase.Defeated);
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
