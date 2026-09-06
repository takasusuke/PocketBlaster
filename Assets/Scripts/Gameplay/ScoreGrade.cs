namespace PocketBlaster.Gameplay
{
    /// <summary>
    /// ステージクリア画面の総合評価(A〜E、オーナー要望2026-09-06:「スコア画面にAから
    /// Eの総合評価を表示するなど、高揚感の高まる演出、色にして」)。UnityEngine非依存の
    /// 純粋な計算だけをここに切り出し、EditModeテストから検証できるようにする
    /// (色・アニメーションはStageDirector側で担当)。
    ///
    /// 判定はそのステージで得られる最大スコア(全ウェーブの敵のPointValue合計)に対する
    /// 達成率で行う — ステージごとに敵の数・配点が違うため、スコアの絶対値ではなく
    /// 比率で揃える。
    /// </summary>
    public static class ScoreGrade
    {
        public static char Compute(int score, int maxPossibleScore)
        {
            if (maxPossibleScore <= 0) return 'E';

            var ratio = (float)score / maxPossibleScore;
            if (ratio >= 0.9f) return 'A';
            if (ratio >= 0.75f) return 'B';
            if (ratio >= 0.6f) return 'C';
            if (ratio >= 0.4f) return 'D';
            return 'E';
        }
    }
}
