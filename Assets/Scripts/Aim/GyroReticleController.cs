using System;
using System.Collections;
using PocketBlaster.Audio;
using PocketBlaster.Gameplay;
using PocketBlaster.Networking;
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
    [RequireComponent(typeof(PhoneControllerServer))]
    [RequireComponent(typeof(AudioSource))]
    public class GyroReticleController : MonoBehaviour
    {
        [SerializeField] private float degreesToScreenPixels = 12f;
        [SerializeField] private int magazineSize = 6;
        [SerializeField] private float autoReloadDelaySeconds = 0.6f;
        [SerializeField] private Camera aimCamera;
        [SerializeField] private LayerMask hitLayerMask = ~0;
        [SerializeField] private float maxHitDistance = 1000f;

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

        private bool _isCalibrated;
        private bool _isAutoReloading;
        private float _refBeta;
        private float _refGamma;
        private float _offsetX;
        private float _offsetY;
        private float _reticleScreenX;
        private float _reticleScreenY;
        private float _timeSinceReload;
        private float _emptyClickFlashTimer;
        private string _lastShotResult = "-";

        private void Awake()
        {
            _server = GetComponent<PhoneControllerServer>();
            _ammo = new AmmoState(magazineSize);
            _server.OnReload += HandleReload;
            _server.OnShoot += HandleShoot;

            _audioSource = GetComponent<AudioSource>();
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
            // 初回は自動でキャリブレーションせず、明示的な画面を出して「リロード」操作を
            // 待つ(オーナー要望、2026-09-06: 接続直後の姿勢をそのまま基準にしてしまうと、
            // ボタンを押す動作中の不安定な向きが基準になりかねないため)。
            if (!_isCalibrated)
            {
                _calibrationLabel.style.display = _server.IsConnected ? DisplayStyle.Flex : DisplayStyle.None;
                _reticle.style.display = DisplayStyle.None;
                _statusLabel.style.display = DisplayStyle.None;
                _ammoLabel.style.display = DisplayStyle.None;
                return;
            }

            _calibrationLabel.style.display = DisplayStyle.None;
            _reticle.style.display = DisplayStyle.Flex;
            _statusLabel.style.display = DisplayStyle.Flex;
            _ammoLabel.style.display = DisplayStyle.Flex;

            _timeSinceReload += Time.deltaTime;
            if (_emptyClickFlashTimer > 0f) _emptyClickFlashTimer -= Time.deltaTime;

            // Mathf.DeltaAngleで0/360境界をまたぐ回転(スマホの向き=alphaをwebapp側で
            // 左右方向に割り当てた場合)でも正しい符号付き差分になるようにする。
            // beta/gammaは通常この境界をまたがないが、統一しておいて問題はない。
            var betaDelta = Mathf.DeltaAngle(_refBeta, _server.LatestBeta);
            var gammaDelta = Mathf.DeltaAngle(_refGamma, _server.LatestGamma);

            _offsetX = gammaDelta * degreesToScreenPixels;
            _offsetY = betaDelta * degreesToScreenPixels;

            var halfW = Screen.width / 2f;
            var halfH = Screen.height / 2f;
            var x = Mathf.Clamp(halfW + _offsetX, 0, Screen.width);
            var y = Mathf.Clamp(halfH + _offsetY, 0, Screen.height);

            _reticleScreenX = x;
            _reticleScreenY = y;
            _reticle.style.left = x - _reticle.resolvedStyle.width / 2f;
            _reticle.style.top = y - _reticle.resolvedStyle.height / 2f;

            var ammoLine = _isAutoReloading
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
            _ammoLabel.text = _isAutoReloading
                ? "リロード中..."
                : $"残弾 {_ammo.CurrentAmmo} / {_ammo.MagazineSize}";
            _ammoLabel.style.color = (_ammo.CurrentAmmo == 0 && !_isAutoReloading)
                ? new Color(1f, 0.35f, 0.35f)
                : Color.white;

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
            if (_isAutoReloading) return; // 自動リロード中(のちのちここにリロードアニメーションが入る)

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
                StartCoroutine(AutoReloadRoutine());
            }
        }

        /// <summary>
        /// 残弾0で自動的にリロードする(オーナー要望、2026-09-06)。手動リロード
        /// (HandleReload、"reload"メッセージ・フリックジェスチャー)とは異なり
        /// <see cref="Recenter"/>は呼ばない — 弾切れになった瞬間にどこを狙っているかは
        /// 不定なので、それを新しい基準にするとドリフト補正どころか逆にずれの原因になる。
        /// 狙いの基準を取り直すのは、引き続き「画面中央に構え直してリロード操作をする」
        /// 明示的な行動だけに限定する(../CLAUDE.md 設計上の不変条件2)。
        /// autoReloadDelaySecondsは今は単なる待ち時間だが、後で差し替える
        /// リロードアニメーションの尺として使う想定の置き場所。
        /// </summary>
        private IEnumerator AutoReloadRoutine()
        {
            _isAutoReloading = true;
            yield return new WaitForSeconds(autoReloadDelaySeconds);
            _ammo.Reload();
            _isAutoReloading = false;
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

        /// <returns>Targetにヒットしたか</returns>
        private bool TryHitTargetAtReticle()
        {
            var aimRay = GetAimRay();
            if (aimRay == null)
            {
                _audioSource.PlayOneShot(_missClip);
                return false;
            }

            var ray = aimRay.Value;

            if (Physics.Raycast(ray, out var hit, maxHitDistance, hitLayerMask))
            {
                var target = hit.collider.GetComponentInParent<Target>();
                if (target != null && target.IsHittable)
                {
                    target.TakeHit();
                    _audioSource.PlayOneShot(_hitClip);
                    return true;
                }
            }

            _audioSource.PlayOneShot(_missClip);
            return false;
        }

        private void HandleReload()
        {
            _ammo.Reload();
            Recenter();
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
            var reticleColor = new Color(1f, 0.2f, 0.2f, 0.9f);
            _reticle.style.borderLeftColor = reticleColor;
            _reticle.style.borderRightColor = reticleColor;
            _reticle.style.borderTopColor = reticleColor;
            _reticle.style.borderBottomColor = reticleColor;
            root.Add(_reticle);

            _statusLabel = new Label();
            _statusLabel.style.position = Position.Absolute;
            _statusLabel.style.left = 12;
            _statusLabel.style.top = 12;
            _statusLabel.style.color = Color.white;
            _statusLabel.style.fontSize = 18;
            _statusLabel.style.whiteSpace = WhiteSpace.Normal;
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
            root.Add(_ammoLabel);
        }
    }
}
