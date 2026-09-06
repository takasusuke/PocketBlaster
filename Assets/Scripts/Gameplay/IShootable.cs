namespace PocketBlaster.Gameplay
{
    /// <summary>
    /// レイキャストで撃てるものの共通契約(GyroReticleController.TryHitTargetAtReticle参照)。
    /// Target(敵)とPickup(アイテム)はどちらも「狙って撃つ」対象という点で同じなので、
    /// この最小限のインターフェースだけを共有する。得点・ジュース演出はTarget固有、
    /// 効果の適用はPickup固有のまま、狙撃判定のコードだけを1本化できる。
    /// </summary>
    public interface IShootable
    {
        bool IsHittable { get; }
        void TakeHit();
    }
}
