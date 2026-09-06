namespace PocketBlaster.Gameplay
{
    public enum ObstacleCrossingResult
    {
        Clear,
        StepUp,
        Blocked
    }

    /// <summary>
    /// 障害物に重なっている時、乗り越えられる(StepUp)か通れない(Blocked)かを
    /// 判定するだけの純粋な計算(PlayerLocomotion参照)。UnityEngine非依存にして
    /// EditModeテストで検証できるようにしてある。
    /// </summary>
    public static class ObstacleCrossing
    {
        public static ObstacleCrossingResult Evaluate(bool isOverlapping, float obstacleHeight, float stepUpHeight)
        {
            if (!isOverlapping) return ObstacleCrossingResult.Clear;
            return obstacleHeight <= stepUpHeight ? ObstacleCrossingResult.StepUp : ObstacleCrossingResult.Blocked;
        }
    }
}
