namespace PocketBlaster.Gameplay
{
    /// <summary>
    /// ステージの進行(現在のウェーブ番号・そのウェーブの残り敵数・クリア判定)を
    /// UnityEngine非依存で表現したもの。カメラ移動や敵の有効化はStageDirector
    /// (MonoBehaviour)側が、この状態を見て行う。
    /// </summary>
    public class StageProgressState
    {
        private readonly int[] _enemyCountPerWave;

        /// <summary>-1は「まだ最初のウェーブが始まっていない」ことを表す</summary>
        public int CurrentWaveIndex { get; private set; } = -1;
        public int RemainingInCurrentWave { get; private set; }
        public bool IsStageCleared => CurrentWaveIndex >= _enemyCountPerWave.Length;
        public int WaveCount => _enemyCountPerWave.Length;

        public StageProgressState(int[] enemyCountPerWave)
        {
            _enemyCountPerWave = enemyCountPerWave;
        }

        /// <returns>次のウェーブへ進めたか(falseならステージクリア済み、IsStageClearedを見る)</returns>
        public bool AdvanceToNextWave()
        {
            if (IsStageCleared) return false;
            CurrentWaveIndex++;
            if (IsStageCleared) return false;
            RemainingInCurrentWave = _enemyCountPerWave[CurrentWaveIndex];
            return true;
        }

        /// <returns>この通知で現在のウェーブの敵を全滅させた(クリアした)か</returns>
        public bool NotifyEnemyDefeated()
        {
            if (RemainingInCurrentWave <= 0) return false; // 既にクリア済みのウェーブからの遅延通知等は無視
            RemainingInCurrentWave--;
            return RemainingInCurrentWave <= 0;
        }
    }
}
