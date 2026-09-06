using System;
using System.Collections;
using PocketBlaster.Audio;
using PocketBlaster.Gameplay;
using PocketBlaster.Meta;
using PocketBlaster.Networking;
using PocketBlaster.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace PocketBlaster.Aim
{
    /// <summary>
    /// スマホのジャイロ値(alpha/beta/gamma)を画面座標のレティクル位置へマッピングする。
    /// 狙点はスティック選択ではなく「基準からの回転差分」で動かす(../CLAUDE.md 設計上の
    /// 不変条件1)。基準は初回受信時、または"reload"メッセージ受信時(Recenter)に取り直す。
    ///
    /// マイルストーン2: リロードを「弾切れ時に必ず行う」実際のゲームプレイに結び付けた
    /// (AmmoState)。弾切れ→リロード→再キャリブレーションという一連の流れが自然に起きる
    /// ことで、ドリフトが実用上気になるかを実機でのプレイを通して検証できるようにする
    /// (docs/requirements.md §4 マイルストーン2)。ドリフト自体の量は実機でしか測れないため、
    /// 「直近のリロードからの経過時間」を画面に出し、静止したまま経過時間を伸ばして
    /// レティクルが動くかどうかを目視できるようにしてある(docs/HANDOFF.md 検証手順)。
    ///
    /// マイルストーン3: 弾が当たったら、レティクルのスクリーン座標からカメラの視線を
    /// 飛ばして`Target`(固定の仮の敵)にヒット判定する。着弾フィードバックは仮の
    /// 効果音(ProceduralSfx)のみ — 「撃つ感触」そのものを詰める段階なので、
    /// アート・SEアセットより先にゲームプレイの手触りを検証する
    /// (../CLAUDE.md 11「初期実装では画像を作らない」と同じ考え方)。
    ///
    /// UI ToolkitはPackages/manifest.jsonの追加なしで使えるため、PanelSettingsも
    /// 実行時に生成しシーンにアセットを持たせない。
    ///
    /// 初回キャリブレーション(オーナー要望、2026-09-06): 接続直後に自動でRecenter()する
    /// のをやめ、「スマホを画面中央に向けて『リロード』を押してください」という明示的な
    /// 画面を出して、プレイヤーが実際にそう構えた上で操作するのを待つ。ボタンを押す動作の
    /// 最中の不安定な向きがそのまま基準になってしまう問題を避けるため。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class GyroReticleController : MonoBehaviour
    {
        // 上下・左右で別々に持つ(オーナー要望、2026-09-06:「上下左右方向の感度を
        // ユーザごとに調整できるようにしてください」)。既定値はAwakeでGameSettings
        // (起動画面での設定、PlayerPrefs保存)の値に上書きされる。
        [SerializeField] private float verticalSensitivity = 12f;
        [SerializeField] private float horizontalSensitivity = 12f;
        [SerializeField] private int magazineSize = 6;
        [SerializeField] private float reloadDurationSeconds = 0.6f;
        [SerializeField] private Camera aimCamera;
        [SerializeField] private LayerMask hitLayerMask = ~0;
        [SerializeField] private float maxHitDistance = 1000f;

        /// <summary>
        /// PC上でマウスでも狙えるようにする(オーナー要望、2026-09-06:「あくまでデバッグを
        /// 容易にするため」)。実機(スマホ)が無くてもPlay Modeだけで狙撃・命中判定を
        /// 試せるようにする、開発用の割り切り。<see cref="IsMouseDebugActive"/>参照 —
        /// スマホが接続されている間は自動的に無効化され、常にスマホ側が優先される
        /// (2026-09-06、オーナー報告: 当初は常時マウス優先にしていたため実機での照準が
        /// 効かなくなっていた)。
        /// </summary>
        [SerializeField] private bool enableMouseDebugAim = true;

        /// <summary>
        /// マウスデバッグが実際に有効か。設定がオンでも、スマホが接続されている間は
        /// スマホ側の入力を奪わないよう常にfalseになる — 「スマホが無い時の代役」に
        /// 限定するため。
        /// </summary>
        private bool IsMouseDebugActive => enableMouseDebugAim && (_server == null || !_server.IsConnected);

        /// <summary>
        /// 狙って撃った結果(true=命中、false=はずれ)。難易度モード(GameSession)が
        /// アーケードモードでの残機判定に使う。弾切れでの空撃ちはここでは発火しない
        /// (狙いの結果ではなく弾数の運用ミスなので、残機には数えない設計)。
        /// </summary>
        public event Action<bool> OnShotResolved;

        /// <summary>
        /// キャリブレーション(初回接続後の「リロード」操作)が完了しているか。
        /// EnemyApproachが「スマホが接続してアクションを取るまで敵を近づかせない」
        /// (オーナー要望、2026-09-06)ために参照する。
        /// </summary>
        public bool IsCalibrated => _isCalibrated;

        private PhoneControllerServer _server;
        private AmmoState _ammo;
        private AudioSource _audioSource;
        private AudioClip _shotClip;
        private AudioClip _hitClip;
        private AudioClip _missClip;
        private AudioClip _emptyClickClip;
        private UIDocument _uiDocument;
        private PanelSettings _panelSettings;
        private VisualElement _reticle;
        private Label _statusLabel;
        private Label _calibrationLabel;
        private Label _ammoLabel;
        private VisualElement _reloadBarTrack;
        private VisualElement _reloadBarFill;
        private VisualElement _ammoPipsContainer;
        private VisualElement[] _ammoPips;

        private bool _isCalibrated;
        private bool _isReloading;
        private float _reloadElapsed;
        private float _refBeta;
        private float _refGamma;
        private float _offsetX;
        private float _offsetY;
        private float _reticleScreenX;
        private float _reticleScreenY;
        private float _timeSinceReload;
        private float _emptyClickFlashTimer;
        private string _lastShotResult = "-";

        private static readonly Color AmmoPipFilledColor = new Color(1f, 0.75f, 0.15f);
        private static readonly Color AmmoPipEmptyColor = new Color(1f, 1f, 1f, 0.18f);
        private static readonly Color AimReticleColor = new Color(1f, 0.2f, 0.2f, 0.9f);
        private static readonly Color LookCursorColor = new Color(0.3f, 0.9f, 1f, 0.85f);

        private void Awake()
        {
            // GetComponentではなくGetOrCreate() — PhoneControllerServerはシーンをまたぐ
            // 永続シングルトンにしてある(2026-09-06、再挑戦での接続断対応。同ファイル参照)。
            _server = PhoneControllerServer.GetOrCreate();
            _ammo = new AmmoState(magazineSize);
            _server.OnReload += HandleReload;
            _server.OnShoot += HandleShoot;

            // 起動画面(Title)で選んだ感度(上下・左右別)・SE音量を適用する(2026-09-06)。
            verticalSensitivity = GameSettings.Current.VerticalSensitivity;
            horizontalSensitivity = GameSettings.Current.HorizontalSensitivity;

            _audioSource = GetComponent<AudioSource>();
            _audioSource.volume = GameSettings.Current.SfxVolume;
            _shotClip = ProceduralSfx.CreateTone("sfx_shot", 880f, 0.05f, 0.03f);
            _hitClip = ProceduralSfx.CreateTone("sfx_hit", 440f, 0.15f, 0.1f);
            _missClip = ProceduralSfx.CreateTone("sfx_miss", 220f, 0.08f, 0.06f);
            _emptyClickClip = ProceduralSfx.CreateTone("sfx_empty", 120f, 0.05f, 0.02f);

            BuildUi();
        }

        private void OnDestroy()
        {
            if (_server != null)
            {
                _server.OnReload -= HandleReload;
                _server.OnShoot -= HandleShoot;
            }
            if (_panelSettings != null) Destroy(_panelSettings);
        }

        private void Update()
        {
            // マウスデバッグ(enableMouseDebugAim)は「スマホが無い時の代役」に限定する。
            // スマホが接続されている間は常にスマホ側を優先し、マウスには一切判定を
            // 持たせない(2026-09-06、オーナー報告: 常時マウス優先にしていたら実機での
            // 照準が効かなくなった)。IsMouseDebugActiveを参照。
            if (IsMouseDebugActive && !_isCalibrated && Input.GetMouseButtonDown(0))
            {
                Recenter();
            }

            // 初回は自動でキャリブレーションせず、明示的な画面を出して「リロード」操作を
            // 待つ(オーナー要望、2026-09-06: 接続直後の姿勢をそのまま基準にしてしまうと、
            // ボタンを押す動作中の不安定な向きが基準になりかねないため)。
            if (!_isCalibrated)
            {
                _calibrationLabel.style.display = _server.IsConnected ? DisplayStyle.Flex : DisplayStyle.None;
                _reticle.style.display = DisplayStyle.None;
                _statusLabel.style.display = DisplayStyle.None;
                _ammoLabel.style.display = DisplayStyle.None;
                _ammoPipsContainer.style.display = DisplayStyle.None;
                _reloadBarTrack.style.display = DisplayStyle.None;
                return;
            }

            _calibrationLabel.style.display = DisplayStyle.None;
            _reticle.style.display = DisplayStyle.Flex;
            _statusLabel.style.display = DisplayStyle.Flex;
            _ammoLabel.style.display = DisplayStyle.Flex;
            _ammoPipsContainer.style.display = DisplayStyle.Flex;

            _timeSinceReload += Time.deltaTime;
            if (_emptyClickFlashTimer > 0f) _emptyClickFlashTimer -= Time.deltaTime;

            // Mathf.DeltaAngleで0/360境界をまたぐ回転(スマホの向き=alphaをwebapp側で
            // 左右方向に割り当てた場合)でも正しい符号付き差分になるようにする。
            // beta/gammaは通常この境界をまたがないが、統一しておいて問題はない。
            var betaDelta = Mathf.DeltaAngle(_refBeta, _server.LatestBeta);
            var gammaDelta = Mathf.DeltaAngle(_refGamma, _server.LatestGamma);

            _offsetX = gammaDelta * horizontalSensitivity;
            _offsetY = betaDelta * verticalSensitivity;

            // 「構える」ボタンを押している間だけ傾きを照準に使う(オーナー要望、2026-09-06:
            // 「構えている間は照準を動かして、構えていない間は移動する」— 傾きは1系統しか
            // 無いため、狙いと移動のどちらに使うかをボタンで切り替える。PlayerLocomotion
            // 参照)。マウスデバッグは実機の代役なので、この切り替えの影響を受けない。
            //
            // 構えていない間も「今向いている方向」を示すカーソルを画面中央に表示する
            // (オーナー要望、2026-09-06:「構えていない時も、向いている方向を示すために
            // カーソルを表示してほしいです」)。構えていない間はPlayerLocomotionが
            // カメラ自体を回転させる設計のため、画面中央=カメラの正面=現在向いている
            // 方向と一致する。狙い(赤)と見た目で区別できるよう色を変える。
            var isAimActive = IsMouseDebugActive || _server.IsAiming;
            SetReticleColor(isAimActive ? AimReticleColor : LookCursorColor);

            float x, y;
            if (isAimActive)
            {
                if (IsMouseDebugActive)
                {
                    // UI Toolkitは左上原点・下方向がプラスだが、Input.mousePositionは
                    // 左下原点・上方向がプラスなのでYを反転する。
                    x = Mathf.Clamp(Input.mousePosition.x, 0, Screen.width);
                    y = Mathf.Clamp(Screen.height - Input.mousePosition.y, 0, Screen.height);
                }
                else
                {
                    var halfW = Screen.width / 2f;
                    var halfH = Screen.height / 2f;
                    x = Mathf.Clamp(halfW + _offsetX, 0, Screen.width);
                    y = Mathf.Clamp(halfH + _offsetY, 0, Screen.height);
                }
            }
            else
            {
                x = Screen.width / 2f;
                y = Screen.height / 2f;
            }

            _reticleScreenX = x;
            _reticleScreenY = y;
            _reticle.style.left = x - _reticle.resolvedStyle.width / 2f;
            _reticle.style.top = y - _reticle.resolvedStyle.height / 2f;

            // リロードアニメーション(オーナー要望、2026-09-06:「リロードアニメーションを
            // 実装して」)。レティクルの真下に進捗バーを追従表示する — レティクル自体は
            // 円形で回転させても見た目が変わらないため、別要素の進捗バーで表現する。
            if (_isReloading)
            {
                const float barWidth = 56f;
                const float barHeight = 6f;
                _reloadBarTrack.style.display = DisplayStyle.Flex;
                _reloadBarTrack.style.left = x - barWidth / 2f;
                _reloadBarTrack.style.top = y + _reticle.resolvedStyle.height / 2f + 6f;
                var progress = Mathf.Clamp01(_reloadElapsed / reloadDurationSeconds);
                _reloadBarFill.style.width = Length.Percent(progress * 100f);
            }
            else
            {
                _reloadBarTrack.style.display = DisplayStyle.None;
            }

            if (IsMouseDebugActive && Input.GetMouseButtonDown(0))
            {
                HandleShoot();
            }

            var ammoLine = _isReloading
                ? "リロード中..."
                : _ammo.CurrentAmmo > 0
                    ? $"弾: {_ammo.CurrentAmmo}/{_ammo.MagazineSize}"
                    : "弾切れ！リロードしてください";
            if (_emptyClickFlashTimer > 0f)
            {
                ammoLine += "  (弾切れでの発射操作を無視しました)";
            }

            // オーナーからのプレイテストFB(2026-09-06)「残り弾数をゲーム画面に表示して」を
            // 受けて、上のammoLine(詳細デバッグ表示の一部)とは別に、見つけやすい大きな
            // 専用表示を画面右下に出す。
            _ammoLabel.text = _isReloading
                ? "リロード中..."
                : $"残弾 {_ammo.CurrentAmmo} / {_ammo.MagazineSize}";
            _ammoLabel.style.color = (_ammo.CurrentAmmo == 0 && !_isReloading)
                ? new Color(1f, 0.35f, 0.35f)
                : Color.white;

            for (var i = 0; i < _ammoPips.Length; i++)
            {
                var isLoaded = !_isReloading && i < _ammo.CurrentAmmo;
                _ammoPips[i].style.backgroundColor = isLoaded ? AmmoPipFilledColor : AmmoPipEmptyColor;
            }

            _statusLabel.text =
                $"接続: {(_server.IsConnected ? "済" : "未接続")}  port {_server.Port}\n" +
                $"alpha={_server.LatestAlpha:F1} beta={_server.LatestBeta:F1} gamma={_server.LatestGamma:F1}\n" +
                $"基準からの差分  β:{betaDelta:F1} γ:{gammaDelta:F1}\n" +
                $"{ammoLine}\n" +
                $"前回リロードからの経過時間: {_timeSinceReload:F1}秒" +
                "（静止したままこの値が伸びてもレティクルが動かなければドリフトは無視できる）\n" +
                $"直近の射撃結果: {_lastShotResult}";
        }

        private void HandleShoot()
        {
            if (!_isCalibrated) return; // キャリブレーション完了前は撃てない
            if (_isReloading) return; // リロードアニメーション中は撃てない
            // 「構える」ボタンを押していない間は撃てない(オーナー要望、2026-09-06)。
            // マウスデバッグは実機の代役なので対象外。
            if (!IsMouseDebugActive && !_server.IsAiming) return;

            if (!_ammo.Shoot())
            {
                _emptyClickFlashTimer = 1f;
                _audioSource.PlayOneShot(_emptyClickClip);
                _lastShotResult = "弾切れ";
                return;
            }

            _audioSource.PlayOneShot(_shotClip);
            var didHit = TryHitTargetAtReticle();
            _lastShotResult = didHit ? "命中" : "はずれ";
            OnShotResolved?.Invoke(didHit);

            if (_ammo.CurrentAmmo == 0)
            {
                // 残弾0での自動リロード(オーナー要望、2026-09-06)。狙いの基準の取り直し
                // (Recenter)は行わない — 弾切れになった瞬間にどこを狙っているかは不定なので、
                // それを新しい基準にするとドリフト補正どころか逆にずれの原因になる
                // (../CLAUDE.md 設計上の不変条件2)。
                BeginReload(recenterOnComplete: false);
            }
        }

        /// <summary>
        /// リロードアニメーションを開始する(オーナー要望、2026-09-06:「リロード
        /// アニメーションを実装して」)。手動リロード("reload"メッセージ・フリック
        /// ジェスチャー)・残弾0での自動リロードのどちらもこれを通る。既に進行中なら
        /// 何もしない(二重開始防止)。
        /// </summary>
        private void BeginReload(bool recenterOnComplete)
        {
            if (_isReloading) return;
            StartCoroutine(ReloadRoutine(recenterOnComplete));
        }

        private IEnumerator ReloadRoutine(bool recenterOnComplete)
        {
            _isReloading = true;
            _reloadElapsed = 0f;
            while (_reloadElapsed < reloadDurationSeconds)
            {
                _reloadElapsed += Time.deltaTime;
                yield return null;
            }
            _ammo.Reload();
            if (recenterOnComplete) Recenter();
            _isReloading = false;
        }

        /// <summary>
        /// 現在のレティクル位置からカメラの視線を飛ばしたレイ。射撃判定(TryHitTargetAtReticle)
        /// だけでなく、足踏み移動(PlayerLocomotion)が「狙っている方向」を知るためにも使う。
        /// nullを返すことがある(カメラが見つからない場合)ので呼び出し側で判定すること。
        /// </summary>
        public Ray? GetAimRay()
        {
            var cam = aimCamera != null ? aimCamera : Camera.main;
            if (cam == null) return null;

            // レティクルはUI Toolkit座標(原点が左上・下方向がプラス)なので、
            // Cameraのスクリーン座標(原点が左下・上方向がプラス)へY軸を反転して合わせる。
            var screenPoint = new Vector3(_reticleScreenX, Screen.height - _reticleScreenY, 0f);
            return cam.ScreenPointToRay(screenPoint);
        }

        /// <returns>何か(敵・アイテム等のIShootable)にヒットしたか</returns>
        private bool TryHitTargetAtReticle()
        {
            var aimRay = GetAimRay();
            if (aimRay == null)
            {
                _audioSource.PlayOneShot(_missClip);
                return false;
            }

            var ray = aimRay.Value;

            // TargetとPickupはどちらもIShootable(共通の狙撃対象契約、IShootable.cs参照)
            // なので、ここでは種類を区別せず同じ判定にまとめている。
            if (Physics.Raycast(ray, out var hit, maxHitDistance, hitLayerMask))
            {
                var shootable = hit.collider.GetComponentInParent<IShootable>();
                if (shootable != null && shootable.IsHittable)
                {
                    shootable.TakeHit();
                    _audioSource.PlayOneShot(_hitClip);
                    return true;
                }
            }

            _audioSource.PlayOneShot(_missClip);
            return false;
        }

        /// <summary>
        /// アイテム(Pickup)経由でのリロード(オーナー要望、2026-09-06:「リロードできる
        /// アイテム」)。手動リロードと違い再キャリブレーションは行わない — 弾切れ自動
        /// リロードと同じ理由(ドリフト対策、BeginReload参照)。
        /// </summary>
        public void ApplyReloadPickup()
        {
            BeginReload(recenterOnComplete: false);
        }

        /// <summary>アイテム(Pickup)経由での最大弾薬数増加(オーナー要望、2026-09-06)。</summary>
        public void ApplyAmmoUpPickup(int amount)
        {
            _ammo.IncreaseMagazineSize(amount);
        }

        private void SetReticleColor(Color color)
        {
            _reticle.style.borderLeftColor = color;
            _reticle.style.borderRightColor = color;
            _reticle.style.borderTopColor = color;
            _reticle.style.borderBottomColor = color;
        }

        private void HandleReload()
        {
            // 手動リロード("reload"メッセージ・フリックジェスチャー)は、再キャリブレーション
            // (Recenter)を伴う — プレイヤーが実際に画面中央へ構え直した上での操作なので、
            // 自動リロードと違い基準を取り直しても安全(../CLAUDE.md 設計上の不変条件2)。
            BeginReload(recenterOnComplete: true);
        }

        public void Recenter()
        {
            _refBeta = _server.LatestBeta;
            _refGamma = _server.LatestGamma;
            _isCalibrated = true;
            _timeSinceReload = 0f;
        }

        private void BuildUi()
        {
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            RuntimeLabelStyle.EnsureTheme(_panelSettings);
            // 他のUIDocument(StageDirector・GameSession)より確実に手前に出す
            // (キャリブレーション画面やレティクルがHUDの下に隠れては困るため)。
            _panelSettings.sortingOrder = 10;

            var uiDocumentGo = new GameObject("GyroReticleUI");
            uiDocumentGo.transform.SetParent(transform, false);
            _uiDocument = uiDocumentGo.AddComponent<UIDocument>();
            _uiDocument.panelSettings = _panelSettings;

            var root = _uiDocument.rootVisualElement;
            root.style.justifyContent = Justify.Center;
            root.style.alignItems = Align.Center;

            _calibrationLabel = new Label(
                "スマホを画面の中央に向けて構え、\n「リロード」を押してください\n（狙いの基準を決めます）");
            _calibrationLabel.style.color = Color.white;
            _calibrationLabel.style.fontSize = 26;
            _calibrationLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _calibrationLabel.style.backgroundColor = new Color(0f, 0f, 0f, 0.65f);
            _calibrationLabel.style.paddingLeft = 28;
            _calibrationLabel.style.paddingRight = 28;
            _calibrationLabel.style.paddingTop = 20;
            _calibrationLabel.style.paddingBottom = 20;
            _calibrationLabel.style.borderTopLeftRadius = 16;
            _calibrationLabel.style.borderTopRightRadius = 16;
            _calibrationLabel.style.borderBottomLeftRadius = 16;
            _calibrationLabel.style.borderBottomRightRadius = 16;
            RuntimeLabelStyle.ApplyDefaultFont(_calibrationLabel);
            root.Add(_calibrationLabel);

            _reticle = new VisualElement();
            _reticle.style.position = Position.Absolute;
            _reticle.style.width = 32;
            _reticle.style.height = 32;
            _reticle.style.borderTopLeftRadius = 16;
            _reticle.style.borderTopRightRadius = 16;
            _reticle.style.borderBottomLeftRadius = 16;
            _reticle.style.borderBottomRightRadius = 16;
            _reticle.style.borderLeftWidth = 3;
            _reticle.style.borderRightWidth = 3;
            _reticle.style.borderTopWidth = 3;
            _reticle.style.borderBottomWidth = 3;
            SetReticleColor(AimReticleColor);
            root.Add(_reticle);

            // リロード進捗バー(オーナー要望、2026-09-06:「リロードアニメーションを実装して」)。
            // レティクルは円形で回転させても見た目が変わらないため、レティクルの真下に
            // 追従する小さな進捗バーで表現する。既定は非表示、Update()でリロード中だけ表示する。
            _reloadBarTrack = new VisualElement();
            _reloadBarTrack.style.display = DisplayStyle.None;
            _reloadBarTrack.style.position = Position.Absolute;
            _reloadBarTrack.style.width = 56;
            _reloadBarTrack.style.height = 6;
            _reloadBarTrack.style.backgroundColor = new Color(0f, 0f, 0f, 0.5f);
            _reloadBarTrack.style.borderTopLeftRadius = 3;
            _reloadBarTrack.style.borderTopRightRadius = 3;
            _reloadBarTrack.style.borderBottomLeftRadius = 3;
            _reloadBarTrack.style.borderBottomRightRadius = 3;
            root.Add(_reloadBarTrack);

            _reloadBarFill = new VisualElement();
            _reloadBarFill.style.position = Position.Absolute;
            _reloadBarFill.style.left = 0;
            _reloadBarFill.style.top = 0;
            _reloadBarFill.style.bottom = 0;
            _reloadBarFill.style.width = Length.Percent(0);
            _reloadBarFill.style.backgroundColor = new Color(1f, 0.75f, 0.15f);
            _reloadBarFill.style.borderTopLeftRadius = 3;
            _reloadBarFill.style.borderTopRightRadius = 3;
            _reloadBarFill.style.borderBottomLeftRadius = 3;
            _reloadBarFill.style.borderBottomRightRadius = 3;
            _reloadBarTrack.Add(_reloadBarFill);

            _statusLabel = new Label();
            _statusLabel.style.position = Position.Absolute;
            _statusLabel.style.left = 12;
            _statusLabel.style.top = 12;
            _statusLabel.style.color = Color.white;
            _statusLabel.style.fontSize = 18;
            _statusLabel.style.whiteSpace = WhiteSpace.Normal;
            RuntimeLabelStyle.ApplyDefaultFont(_statusLabel);
            root.Add(_statusLabel);

            // 残弾は上のstatusLabel内にも既に出ているが、デバッグ情報に埋もれて
            // 見つけにくいという指摘(オーナーからのプレイテストFB、2026-09-06)を受けて、
            // 画面右下に大きく単独表示する。
            _ammoLabel = new Label();
            _ammoLabel.style.position = Position.Absolute;
            _ammoLabel.style.bottom = 24;
            _ammoLabel.style.right = 24;
            _ammoLabel.style.color = Color.white;
            _ammoLabel.style.fontSize = 36;
            _ammoLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _ammoLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            _ammoLabel.style.backgroundColor = new Color(0f, 0f, 0f, 0.45f);
            _ammoLabel.style.paddingLeft = 18;
            _ammoLabel.style.paddingRight = 18;
            _ammoLabel.style.paddingTop = 8;
            _ammoLabel.style.paddingBottom = 8;
            _ammoLabel.style.borderTopLeftRadius = 12;
            _ammoLabel.style.borderTopRightRadius = 12;
            _ammoLabel.style.borderBottomLeftRadius = 12;
            _ammoLabel.style.borderBottomRightRadius = 12;
            RuntimeLabelStyle.ApplyDefaultFont(_ammoLabel);
            root.Add(_ammoLabel);

            // 残弾を数字だけでなく一目でわかるアイコン(弾のピップ)でも示す
            // (オーナー要望、2026-09-06:「弾の残り弾数は、数字だけでなくアイコンでも
            // 分かりやすくなるようにして」)。残弾表示のすぐ上に、装填数ぶんの小さな
            // 四角を並べ、残っている分だけ明るい色にする。
            _ammoPipsContainer = new VisualElement();
            _ammoPipsContainer.style.position = Position.Absolute;
            _ammoPipsContainer.style.bottom = 88;
            _ammoPipsContainer.style.right = 24;
            _ammoPipsContainer.style.flexDirection = FlexDirection.Row;
            root.Add(_ammoPipsContainer);

            _ammoPips = new VisualElement[Mathf.Max(magazineSize, 1)];
            for (var i = 0; i < _ammoPips.Length; i++)
            {
                var pip = new VisualElement();
                pip.style.width = 16;
                pip.style.height = 22;
                pip.style.marginLeft = 4;
                pip.style.borderTopLeftRadius = 3;
                pip.style.borderTopRightRadius = 3;
                pip.style.borderBottomLeftRadius = 3;
                pip.style.borderBottomRightRadius = 3;
                pip.style.backgroundColor = AmmoPipFilledColor;
                _ammoPipsContainer.Add(pip);
                _ammoPips[i] = pip;
            }
        }
    }
}
