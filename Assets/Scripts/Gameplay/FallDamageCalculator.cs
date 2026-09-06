namespace PocketBlaster.Gameplay
{
    /// <summary>
    /// 落下ダメージの計算だけを切り出した純粋な関数(オーナー要望、2026-09-06:「あまりに
    /// 高いところから飛び降りる場合にはダメージが入るようにしてください」)。
    /// `safeHeight`以下の落下は無傷、それを超えた分だけ`damagePerMeterBeyondSafeHeight`
    /// を掛けてダメージ量にする。UnityEngine非依存にしてEditModeテストで検証できるように
    /// してある(PlayerLocomotion参照)。
    /// </summary>
    public static class FallDamageCalculator
    {
        public static int ComputeDamage(float dropHeight, float safeHeight, float damagePerMeterBeyondSafeHeight)
        {
            var excess = dropHeight - safeHeight;
            if (excess <= 0f) return 0;
            var raw = excess * damagePerMeterBeyondSafeHeight;
            return (int)(raw + 0.5f);
        }
    }
}
