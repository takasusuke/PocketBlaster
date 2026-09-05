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
    /// UI ToolkitはPackages/manifest.jsonの追加なしで使えるため、PanelSettingsも
    /// 実行時に生成しシーンにアセットを持たせない。
    /// </summary>
    [RequireComponent(typeof(PhoneControllerServer))]
    public class GyroReticleController : MonoBehaviour
    {
        [SerializeField] private float degreesToScreenPixels = 12f;
        [SerializeField] private int magazineSize = 6;

        private PhoneControllerServer _server;
        private AmmoState _ammo;
        private UIDocument _uiDocument;
        private PanelSettings _panelSettings;
        private VisualElement _reticle;
        private Label _statusLabel;

        private bool _hasReference;
        private float _refBeta;
        private float _refGamma;
        private float _offsetX;
        private float _offsetY;
        private float _timeSinceReload;
        private float _emptyClickFlashTimer;

        private void Awake()
        {
            _server = GetComponent<PhoneControllerServer>();
            _ammo = new AmmoState(magazineSize);
            _server.OnReload += HandleReload;
            _server.OnShoot += HandleShoot;

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
                "（静止したままこの値が伸びてもレティクルが動かなければドリフトは無視できる）";
        }

        private void HandleShoot()
        {
            if (!_ammo.Shoot())
            {
                _emptyClickFlashTimer = 1f;
            }
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
