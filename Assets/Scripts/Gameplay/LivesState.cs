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
        public int RemainingLives { get; private set; }
        public bool IsGameOver => RemainingLives <= 0;

        public LivesState(int startingLives)
        {
            RemainingLives = startingLives;
        }

        /// <returns>この1回でゲームオーバーになったか</returns>
        public bool LoseLife()
        {
            if (IsGameOver) return false; // 既にゲームオーバー後の重複呼び出しは無視
            RemainingLives--;
            return IsGameOver;
        }
    }
}
