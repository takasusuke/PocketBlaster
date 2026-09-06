namespace PocketBlaster.Gameplay
{
    /// <summary>
    /// アーケードモード(docs/requirements.md §8 将来の拡張「難易度モード」)の残機。
    /// このゲームには敵からの被弾という概念がまだ無いため、フェールの条件は
    /// 「狙って撃ってはずした」こと自体にしている(GyroReticleController.OnShotResolved)。
    /// カジュアルモードではこのクラスを使わない(GameSession参照)。
    /// </summary>
    public class LivesState
    {
        private readonly int _maxLives;

        public int RemainingLives { get; private set; }
        public bool IsGameOver => RemainingLives <= 0;

        public LivesState(int startingLives)
        {
            _maxLives = startingLives;
            RemainingLives = startingLives;
        }

        /// <returns>この1回でゲームオーバーになったか</returns>
        public bool LoseLife()
        {
            if (IsGameOver) return false; // 既にゲームオーバー後の重複呼び出しは無視
            RemainingLives--;
            return IsGameOver;
        }

        /// <summary>
        /// 体力回復アイテム(オーナー要望、2026-09-06)。初期残機を上限にそれ以上は
        /// 増えない。
        /// </summary>
        public void RestoreLife()
        {
            if (RemainingLives >= _maxLives) return;
            RemainingLives++;
        }
    }
}
