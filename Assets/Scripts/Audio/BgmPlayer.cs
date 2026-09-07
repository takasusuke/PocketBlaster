using PocketBlaster.Meta;
using UnityEngine;

namespace PocketBlaster.Audio
{
    /// <summary>
    /// BGM(ループ音楽)を再生し続ける永続シングルトン(2026-09-08、オーナー要望
    /// 「同様のアーケードゲームと比べて機能やUIやUXで足りていない部分を...実装する」の
    /// 一環——現状は効果音(ProceduralSfx)のみでループ音楽が一切無かった)。
    ///
    /// `PhoneControllerServer.GetOrCreate()`と同じ設計(シーンをまたいで生き続ける)。
    /// 起動画面(Title)・各ステージがそれぞれ自分のBGMで`PlayLoop`を呼ぶだけで済み、
    /// シーン遷移をまたいだフェード/切り替えの面倒を各シーン側が個別に持たなくてよい。
    /// 同じクリップを指定された場合は何もしない(シーン再読み込み(再挑戦)のたびに
    /// 曲の先頭へ戻ってしまうのを防ぐ——同じステージへの再挑戦では鳴り続けたままでよい)。
    /// </summary>
    public class BgmPlayer : MonoBehaviour
    {
        public static BgmPlayer Instance { get; private set; }

        private AudioSource _source;
        private AudioClip _currentClip;

        public static BgmPlayer GetOrCreate()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("BgmPlayer(Persistent)");
            return go.AddComponent<BgmPlayer>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _source = gameObject.AddComponent<AudioSource>();
            _source.loop = true;
            _source.playOnAwake = false;
        }

        /// <summary>指定のクリップをループ再生する。既に同じクリップを再生中なら
        /// 何もしない(シーン再読み込みのたびに曲頭へ戻らないように)。</summary>
        public void PlayLoop(AudioClip clip)
        {
            if (clip == null) return;
            _source.volume = GameSettings.Current.BgmVolume;
            if (_currentClip == clip && _source.isPlaying) return;

            _currentClip = clip;
            _source.clip = clip;
            _source.Play();
        }

        /// <summary>起動画面の音量スライダー操作を即座に反映するため、再生中でも
        /// 毎フレーム軽く追従させる。</summary>
        private void Update()
        {
            if (_source.isPlaying) _source.volume = GameSettings.Current.BgmVolume;
        }

        public void Stop()
        {
            _source.Stop();
            _currentClip = null;
        }
    }
}
