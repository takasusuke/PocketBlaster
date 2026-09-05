using PocketBlaster.Networking;
using UnityEngine;
using UnityEngine.UIElements;

namespace PocketBlaster.Aim
{
    /// <summary>
    /// マイルストーン1用: スマホのジャイロ値(alpha/beta/gamma)を画面座標のレティクル位置へ
    /// マッピングする。狙点はスティック選択ではなく「基準からの回転差分」で動かす
    /// (../CLAUDE.md 設計上の不変条件1)。基準は初回受信時、または"reload"メッセージ受信時
    /// (Recenter)に取り直す — マイルストーン2で本実装するリロード連動キャリブレーションの
    /// 配線だけ先に用意している。
    /// UI ToolkitはPackages/manifest.jsonの追加なしで使えるため、PanelSettingsも
    /// 実行時に生成しシーンにアセットを持たせない。
    /// </summary>
    [RequireComponent(typeof(PhoneControllerServer))]
    public class GyroReticleController : MonoBehaviour
    {
        [SerializeField] private float degreesToScreenPixels = 12f;

        private PhoneControllerServer _server;
        private UIDocument _uiDocument;
        private PanelSettings _panelSettings;
        private VisualElement _reticle;
        private Label _statusLabel;

        private bool _hasReference;
        private float _refBeta;
        private float _refGamma;
        private float _offsetX;
        private float _offsetY;

        private void Awake()
        {
            _server = GetComponent<PhoneControllerServer>();
            _server.OnReload += Recenter;

            BuildUi();
        }

        private void OnDestroy()
        {
            if (_server != null) _server.OnReload -= Recenter;
            if (_panelSettings != null) Destroy(_panelSettings);
        }

        private void Update()
        {
            if (!_hasReference && _server.IsConnected)
            {
                Recenter();
            }

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

            _statusLabel.text =
                $"接続: {(_server.IsConnected ? "済" : "未接続")}  port {_server.Port}\n" +
                $"alpha={_server.LatestAlpha:F1} beta={_server.LatestBeta:F1} gamma={_server.LatestGamma:F1}\n" +
                $"基準からの差分  β:{betaDelta:F1} γ:{gammaDelta:F1}";
        }

        public void Recenter()
        {
            _refBeta = _server.LatestBeta;
            _refGamma = _server.LatestGamma;
            _hasReference = true;
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
