namespace PocketBlaster.Gameplay
{
    /// <summary>
    /// 発射数・命中数から命中率を出す。House of the Dead等のリザルト画面にある
    /// 定番項目(オーナー要望2026-09-08:「同様のアーケードゲームと比べて...足りていない
    /// 部分を...随時実装する」の一環)。空撃ち(弾切れ)は発射に数えない——
    /// GyroReticleController.OnShotResolvedが空撃ちで発火しないのと同じ規約。
    /// UnityEngineに依存しない純粋なC#クラス。
    /// </summary>
    public class ShotAccuracyState
    {
        public int ShotsFired { get; private set; }
        public int Hits { get; private set; }

        public void RegisterShot(bool didHit)
        {
            ShotsFired++;
            if (didHit) Hits++;
        }

        /// <summary>0〜100の命中率。発射数0の間は0を返す(未発射をゼロ除算しない)。</summary>
        public float AccuracyPercent => ShotsFired == 0 ? 0f : Hits / (float)ShotsFired * 100f;
    }
}
