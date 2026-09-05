namespace PocketBlaster.Aim
{
    /// <summary>
    /// 弾数とリロードの状態機械。UnityEngineに依存しない純粋なC#クラスにして、
    /// EditModeテストから直接検証できるようにしている。
    /// 「リロード＝弾切れ時に画面中央へ構え直してキャリブレーションし直す」という
    /// 設計(../CLAUDE.md 設計上の不変条件2)を、実際に弾切れが起きるゲームプレイに
    /// 結び付けるための土台(マイルストーン2)。
    /// </summary>
    public class AmmoState
    {
        public int MagazineSize { get; }
        public int CurrentAmmo { get; private set; }
        public bool CanShoot => CurrentAmmo > 0;

        public AmmoState(int magazineSize)
        {
            MagazineSize = magazineSize;
            CurrentAmmo = magazineSize;
        }

        /// <returns>実際に発射できたか(弾切れなら false)</returns>
        public bool Shoot()
        {
            if (!CanShoot) return false;
            CurrentAmmo--;
            return true;
        }

        public void Reload()
        {
            CurrentAmmo = MagazineSize;
        }
    }
}
