namespace PocketBlaster.Gameplay
{
    /// <summary>
    /// プレイヤーの体力(オーナー要望、2026-09-06:「プレイヤーの体力ゲージもUIとして
    /// 実装してください」)。以前は残機(LivesState、1回の被弾でライフ全損)で表現して
    /// いたが、敵の接触・落下でダメージ量が変わるようになった(EnemyContactDamage・
    /// FallDamageCalculator参照)ため、連続値のHPへ置き換えた。UnityEngineに依存しない
    /// 純粋なクラスにして、EditModeテストで検証できるようにしてある。
    /// </summary>
    public class PlayerHealthState
    {
        public int MaxHealth { get; }
        public int CurrentHealth { get; private set; }
        public bool IsDead => CurrentHealth <= 0;

        public PlayerHealthState(int maxHealth)
        {
            MaxHealth = maxHealth;
            CurrentHealth = maxHealth;
        }

        /// <returns>このダメージで死亡したか</returns>
        public bool TakeDamage(int amount)
        {
            if (amount <= 0 || IsDead) return false;
            CurrentHealth -= amount;
            if (CurrentHealth < 0) CurrentHealth = 0;
            return IsDead;
        }

        public void Heal(int amount)
        {
            if (amount <= 0) return;
            CurrentHealth += amount;
            if (CurrentHealth > MaxHealth) CurrentHealth = MaxHealth;
        }
    }
}
