namespace PocketBlaster.Gameplay
{
    /// <summary>
    /// 得点(「倒すとジュースになる」の量、docs/requirements.md 決定済み事項)を積み上げるだけの
    /// 純粋なクラス。UnityEngine非依存でEditModeテストから検証できる。
    /// </summary>
    public class ScoreState
    {
        public int TotalScore { get; private set; }

        public void AddPoints(int points)
        {
            if (points <= 0) return;
            TotalScore += points;
        }
    }
}
