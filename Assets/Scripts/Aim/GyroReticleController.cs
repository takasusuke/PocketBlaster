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
    /// </summary>
    [RequireComponent(typeof(PhoneControllerServer))]
    [RequireComponent(typeof(AudioSource))]
    public class GyroReticleController : MonoBehaviour
    {
        [SerializeField] private float degreesToScreenPixels = 12f;
        [SerializeField] private int magazineSize = 6;
        [SerializeField] private Camera aimCamera;
        [SerializeField] private LayerMask hitLayerMask = ~0;
        [SerializeField] private float maxHitDistance = 1000f;

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

        private bool _hasReference;
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
            if (!_hasReference && _server.IsConnected)
            {
                Recenter();
            }

            _timeSinceReload += Time.deltaTime;
            if (_emptyClickFlashTimer > 0f) _emptyClickFlashTimer -= Time.deltaTime;

            var betaDelta = _server.LatestBeta - _refBeta;
            var gammaDelta = _server.LatestGamma - _refGamma;

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

            var ammoLine = _ammo.CurrentAmmo > 0
                ? $"弾: {_ammo.CurrentAmmo}/{_ammo.MagazineSize}"
                : "弾切れ！リロードしてください";
            if (_emptyClickFlashTimer > 0f)
            {
                ammoLine += "  (弾切れでの発射操作を無視しました)";
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
            if (!_ammo.Shoot())
            {
                _emptyClickFlashTimer = 1f;
                _audioSource.PlayOneShot(_emptyClickClip);
                _lastShotResult = "弾切れ";
                return;
            }

            _audioSource.PlayOneShot(_shotClip);
            _lastShotResult = TryHitTargetAtReticle() ? "命中" : "はずれ";
        }

        /// <returns>Targetにヒットしたか</returns>
        private bool TryHitTargetAtReticle()
        {
            var cam = aimCamera != null ? aimCamera : Camera.main;
            if (cam == null)
            {
                _audioSource.PlayOneShot(_missClip);
                return false;
            }

            // レティクルはUI Toolkit座標(原点が左上・下方向がプラス)なので、
            // Cameraのスクリーン座標(原点が左下・上方向がプラス)へY軸を反転して合わせる。
            var screenPoint = new Vector3(_reticleScreenX, Screen.height - _reticleScreenY, 0f);
            var ray = cam.ScreenPointToRay(screenPoint);

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
            _hasReference = true;
            _timeSinceReload = 0f;
        }

        private void BuildUi()
        {
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();

            var uiDocumentGo = new GameObject("GyroReticleUI");
            uiDocumentGo.transform.SetParent(transform, false);
            _uiDocument = uiDocumentGo.AddComponent<UIDocument>();
            _uiDocument.panelSettings = _panelSettings;

            var root = _uiDocument.rootVisualElement;

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
        }
    }
}
