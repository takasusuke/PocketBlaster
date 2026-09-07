namespace PocketBlaster.Gameplay
{
    /// <summary>
    /// 連続命中(コンボ)を数え、得点倍率を決める。命中でコンボが伸び、はずすと
    /// リセットされる(オーナー要望2026-09-08:「同様のアーケードゲームと比べて
    /// 機能やUIやUXで足りていない部分を...随時実装する」の一環——House of the Dead等
    /// 定番のコンボ倍率演出を追加した)。
    ///
    /// これはオーナーが却下した「はずれ＝残機減少」(2026-09-06)とは別の話——
    /// あちらはミスでゲームオーバーに近づく「罰」だったが、こちらは得点だけに
    /// 影響し、ライフ・ゲームオーバーには一切関与しない「ボーナス」。UnityEngineに
    /// 依存しない純粋なC#クラスにして、EditModeテストから直接検証できるようにしている
    /// (AmmoState等と同じ狙い)。
    /// </summary>
    public class ComboState
    {
        public int CurrentCombo { get; private set; }
        public int MaxCombo { get; private set; }

        public void RegisterShot(bool didHit)
        {
            if (didHit)
            {
                CurrentCombo++;
                if (CurrentCombo > MaxCombo) MaxCombo = CurrentCombo;
            }
            else
            {
                CurrentCombo = 0;
            }
        }

        /// <summary>コンボ数に応じた得点倍率。10刻みで段階的に上がる単純な階段状。</summary>
        public float Multiplier
        {
            get
            {
                if (CurrentCombo >= 20) return 3f;
                if (CurrentCombo >= 10) return 2f;
                if (CurrentCombo >= 5) return 1.5f;
                return 1f;
            }
        }
    }
}
