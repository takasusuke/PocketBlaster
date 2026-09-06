using System;
using System.Collections.Generic;
using PocketBlaster.Meta;
using PocketBlaster.Networking;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace PocketBlaster.UI
{
    /// <summary>
    /// 起動画面(オーナー要望、2026-09-06:「起動画面を実装して。そこから難易度選択や
    /// 設定などができるようにして」)。PC上のマウス操作だけで完結する画面 —
    /// スマホをまだつながなくても難易度・感度・SE音量を選べる。選んだ内容は
    /// GameSettings(PlayerPrefs)へ保存し、遷移先のステージシーンがAwakeで読み取る
    /// (GameSession・GyroReticleController参照)。
    ///
    /// 難易度モードはこれまでスマホ側(webapp/index.html)の接続直後のラジオボタンで
    /// 選んでいたが、起動画面の新設に伴いこちらへ一本化した(重複した選択UIを持たない)。
    ///
    /// スマホからの「狙って撃つ」操作でもこの画面を操作できる(オーナー要望、2026-09-06:
    /// 「起動画面についてもスマホから狙って撃つアクションで操作できるようにしてください」)。
    /// ゲームプレイ中のGyroReticleControllerと同じ仕組み(ジャイロの基準からの角度差分を
    /// 画面座標へ変換)を、ボタン用に簡略化してここに持たせている
    /// (専用の`_clickTargets`にボタンを登録し、"shoot"受信時にレティクル位置と
    /// `VisualElement.worldBound`の当たり判定で押されたボタンを判定する — UI Toolkitの
    /// Button.clickedは外部から発火できないイベントのため、この方式にした)。
    /// キャリブレーションはゲームプレイ画面と同様「リロード」操作(webapp参照)で行うが、
    /// このメニュー画面では低リスクなので明示的な案内画面は出さず、軽いヒント表示のみにした。
    /// </summary>
    public class TitleScreenController : MonoBehaviour
    {
        [SerializeField] private string stage1SceneName = "Milestone4_Stage";
        [SerializeField] private string stage1DisplayName = "ステージ1";
        [SerializeField] private string stage2SceneName = "Stage2_BossRush";
        [SerializeField] private string stage2DisplayName = "ステージ2（ボスラッシュ）";

        private static readonly Color SelectedColor = new Color(0.30f, 0.45f, 0.95f);
        private static readonly Color UnselectedColor = new Color(0.2f, 0.2f, 0.26f);

        private UIDocument _uiDocument;
        private PanelSettings _panelSettings;
        private Button _casualButton;
        private Button _arcadeButton;
        private Label _sfxVolumeLabel;
        private Label _verticalSensitivityLabel;
        private Label _horizontalSensitivityLabel;

        private PhoneControllerServer _server;
        private VisualElement _phoneReticle;
        private Label _phoneHintLabel;
        private bool _isPhoneCalibrated;
        private float _refBeta;
        private float _refGamma;
        private float _cursorX;
        private float _cursorY;
        private readonly List<(VisualElement element, Action onActivate)> _clickTargets = new List<(VisualElement, Action)>();

        private void Awake()
        {
            BuildUi();
            RefreshModeButtons();

            // GetOrCreate() — PhoneControllerServerはシーンをまたぐ永続シングルトン
            // (PhoneControllerServer.cs参照)。起動画面が最初のシーンなら、ここで
            // サーバーが生成されステージ遷移後もそのまま生き続ける。
            _server = PhoneControllerServer.GetOrCreate();
            _server.OnReload += HandlePhoneCalibrate;
            _server.OnShoot += HandlePhoneShoot;
        }

        private void OnDestroy()
        {
            if (_server != null)
            {
                _server.OnReload -= HandlePhoneCalibrate;
                _server.OnShoot -= HandlePhoneShoot;
            }
            if (_panelSettings != null) Destroy(_panelSettings);
        }

        private void Update()
        {
            if (_server == null) return;

            if (!_server.IsConnected)
            {
                _phoneReticle.style.display = DisplayStyle.None;
                _phoneHintLabel.text = "（スマホ未接続 — 接続すると狙って撃つ操作でも選べます）";
                return;
            }

            if (!_isPhoneCalibrated)
            {
                _phoneReticle.style.display = DisplayStyle.None;
                _phoneHintLabel.text = "スマホ接続済み — 画面中央に向けて「リロード」を押すと狙えるようになります";
                return;
            }

            _phoneHintLabel.text = "スマホでの狙い: 有効（「撃つ」でボタンを選択）";

            var betaDelta = Mathf.DeltaAngle(_refBeta, _server.LatestBeta);
            var gammaDelta = Mathf.DeltaAngle(_refGamma, _server.LatestGamma);

            _cursorX = Mathf.Clamp(Screen.width / 2f + gammaDelta * GameSettings.Current.HorizontalSensitivity, 0, Screen.width);
            _cursorY = Mathf.Clamp(Screen.height / 2f + betaDelta * GameSettings.Current.VerticalSensitivity, 0, Screen.height);

            _phoneReticle.style.display = DisplayStyle.Flex;
            _phoneReticle.style.left = _cursorX - _phoneReticle.resolvedStyle.width / 2f;
            _phoneReticle.style.top = _cursorY - _phoneReticle.resolvedStyle.height / 2f;
        }

        private void HandlePhoneCalibrate()
        {
            _refBeta = _server.LatestBeta;
            _refGamma = _server.LatestGamma;
            _isPhoneCalibrated = true;
        }

        private void HandlePhoneShoot()
        {
            if (!_isPhoneCalibrated) return;

            var point = new Vector2(_cursorX, _cursorY);
            foreach (var (element, onActivate) in _clickTargets)
            {
                if (element.worldBound.Contains(point))
                {
                    onActivate();
                    return;
                }
            }
        }

        private void RegisterClickTarget(VisualElement element, Action onActivate)
        {
            _clickTargets.Add((element, onActivate));
        }

        private void SelectMode(bool isArcade)
        {
            GameSettings.SetMode(isArcade);
            RefreshModeButtons();
        }

        private void RefreshModeButtons()
        {
            var isArcade = GameSettings.Current.IsArcadeMode;
            _casualButton.style.backgroundColor = isArcade ? UnselectedColor : SelectedColor;
            _arcadeButton.style.backgroundColor = isArcade ? SelectedColor : UnselectedColor;
        }

        private void StartStage(string sceneName)
        {
            SceneManager.LoadScene(sceneName);
        }

        private void BuildUi()
        {
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            RuntimeLabelStyle.EnsureTheme(_panelSettings);

            var uiDocumentGo = new GameObject("TitleScreenUI");
            uiDocumentGo.transform.SetParent(transform, false);
            _uiDocument = uiDocumentGo.AddComponent<UIDocument>();
            _uiDocument.panelSettings = _panelSettings;

            var root = _uiDocument.rootVisualElement;
            root.style.flexDirection = FlexDirection.Column;
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;
            root.style.flexGrow = 1;

            var panel = new VisualElement();
            panel.style.width = 460;
            panel.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
            panel.style.paddingTop = 28;
            panel.style.paddingBottom = 28;
            panel.style.paddingLeft = 32;
            panel.style.paddingRight = 32;
            panel.style.borderTopLeftRadius = 16;
            panel.style.borderTopRightRadius = 16;
            panel.style.borderBottomLeftRadius = 16;
            panel.style.borderBottomRightRadius = 16;
            root.Add(panel);

            var title = new Label("ポケットブラスター");
            title.style.fontSize = 40;
            title.style.color = Color.white;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.marginBottom = 24;
            RuntimeLabelStyle.ApplyDefaultFont(title);
            panel.Add(title);

            panel.Add(BuildSectionLabel("難易度"));
            var modeRow = new VisualElement();
            modeRow.style.flexDirection = FlexDirection.Row;
            modeRow.style.marginBottom = 20;
            _casualButton = BuildOptionButton("カジュアル（無制限）", () => SelectMode(false));
            _arcadeButton = BuildOptionButton("アーケード（残機制）", () => SelectMode(true));
            RegisterClickTarget(_casualButton, () => SelectMode(false));
            RegisterClickTarget(_arcadeButton, () => SelectMode(true));
            _casualButton.style.marginRight = 8;
            modeRow.Add(_casualButton);
            modeRow.Add(_arcadeButton);
            panel.Add(modeRow);

            panel.Add(BuildSectionLabel("設定"));
            _sfxVolumeLabel = BuildValueLabel();
            panel.Add(_sfxVolumeLabel);
            var sfxSlider = new Slider(0f, 1f) { value = GameSettings.Current.SfxVolume };
            sfxSlider.style.marginBottom = 16;
            sfxSlider.RegisterValueChangedCallback(evt =>
            {
                GameSettings.SetSfxVolume(evt.newValue);
                UpdateSfxVolumeLabel();
            });
            panel.Add(sfxSlider);
            UpdateSfxVolumeLabel();

            // 上下・左右で別々に調整できるようにする(オーナー要望、2026-09-06:
            // 「上下左右方向の感度をユーザごとに調整できるようにしてください」)。
            _verticalSensitivityLabel = BuildValueLabel();
            panel.Add(_verticalSensitivityLabel);
            var verticalSlider = new Slider(GameSettingsState.MinSensitivity, GameSettingsState.MaxSensitivity)
            {
                value = GameSettings.Current.VerticalSensitivity
            };
            verticalSlider.style.marginBottom = 12;
            verticalSlider.RegisterValueChangedCallback(evt =>
            {
                GameSettings.SetVerticalSensitivity(evt.newValue);
                UpdateVerticalSensitivityLabel();
            });
            panel.Add(verticalSlider);
            UpdateVerticalSensitivityLabel();

            _horizontalSensitivityLabel = BuildValueLabel();
            panel.Add(_horizontalSensitivityLabel);
            var horizontalSlider = new Slider(GameSettingsState.MinSensitivity, GameSettingsState.MaxSensitivity)
            {
                value = GameSettings.Current.HorizontalSensitivity
            };
            horizontalSlider.style.marginBottom = 24;
            horizontalSlider.RegisterValueChangedCallback(evt =>
            {
                GameSettings.SetHorizontalSensitivity(evt.newValue);
                UpdateHorizontalSensitivityLabel();
            });
            panel.Add(horizontalSlider);
            UpdateHorizontalSensitivityLabel();

            panel.Add(BuildSectionLabel("ステージを選んでスタート"));
            var stage1Button = BuildStartButton(stage1DisplayName, () => StartStage(stage1SceneName));
            stage1Button.style.marginBottom = 8;
            RegisterClickTarget(stage1Button, () => StartStage(stage1SceneName));
            panel.Add(stage1Button);
            var stage2Button = BuildStartButton(stage2DisplayName, () => StartStage(stage2SceneName));
            RegisterClickTarget(stage2Button, () => StartStage(stage2SceneName));
            panel.Add(stage2Button);

            // スマホでの狙い操作用の状態表示とレティクル(root直下、パネルの外)。
            _phoneHintLabel = new Label();
            _phoneHintLabel.style.color = new Color(0.7f, 0.75f, 0.85f);
            _phoneHintLabel.style.fontSize = 13;
            _phoneHintLabel.style.marginTop = 16;
            _phoneHintLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            RuntimeLabelStyle.ApplyDefaultFont(_phoneHintLabel);
            panel.Add(_phoneHintLabel);

            _phoneReticle = new VisualElement();
            _phoneReticle.style.display = DisplayStyle.None;
            _phoneReticle.style.position = Position.Absolute;
            _phoneReticle.style.width = 30;
            _phoneReticle.style.height = 30;
            _phoneReticle.style.borderTopLeftRadius = 15;
            _phoneReticle.style.borderTopRightRadius = 15;
            _phoneReticle.style.borderBottomLeftRadius = 15;
            _phoneReticle.style.borderBottomRightRadius = 15;
            _phoneReticle.style.borderLeftWidth = 3;
            _phoneReticle.style.borderRightWidth = 3;
            _phoneReticle.style.borderTopWidth = 3;
            _phoneReticle.style.borderBottomWidth = 3;
            var phoneReticleColor = new Color(0.3f, 0.9f, 1f, 0.9f);
            _phoneReticle.style.borderLeftColor = phoneReticleColor;
            _phoneReticle.style.borderRightColor = phoneReticleColor;
            _phoneReticle.style.borderTopColor = phoneReticleColor;
            _phoneReticle.style.borderBottomColor = phoneReticleColor;
            root.Add(_phoneReticle);
        }

        private void UpdateSfxVolumeLabel()
        {
            _sfxVolumeLabel.text = $"SE音量: {Mathf.RoundToInt(GameSettings.Current.SfxVolume * 100)}%";
        }

        private void UpdateVerticalSensitivityLabel()
        {
            _verticalSensitivityLabel.text = $"感度（上下）: {GameSettings.Current.VerticalSensitivity:F1}";
        }

        private void UpdateHorizontalSensitivityLabel()
        {
            _horizontalSensitivityLabel.text = $"感度（左右）: {GameSettings.Current.HorizontalSensitivity:F1}";
        }

        private static Label BuildSectionLabel(string text)
        {
            var label = new Label(text);
            label.style.color = new Color(0.75f, 0.78f, 0.9f);
            label.style.fontSize = 16;
            label.style.marginBottom = 8;
            RuntimeLabelStyle.ApplyDefaultFont(label);
            return label;
        }

        private static Label BuildValueLabel()
        {
            var label = new Label();
            label.style.color = Color.white;
            label.style.fontSize = 14;
            label.style.marginBottom = 4;
            RuntimeLabelStyle.ApplyDefaultFont(label);
            return label;
        }

        private static Button BuildOptionButton(string text, System.Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.style.flexGrow = 1;
            button.style.color = Color.white;
            button.style.fontSize = 15;
            button.style.paddingTop = 10;
            button.style.paddingBottom = 10;
            button.style.borderTopLeftRadius = 8;
            button.style.borderTopRightRadius = 8;
            button.style.borderBottomLeftRadius = 8;
            button.style.borderBottomRightRadius = 8;
            RuntimeLabelStyle.ApplyDefaultFont(button);
            return button;
        }

        private static Button BuildStartButton(string text, System.Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.style.width = Length.Percent(100);
            button.style.color = Color.white;
            button.style.backgroundColor = new Color(0.85f, 0.3f, 0.25f);
            button.style.fontSize = 18;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.paddingTop = 14;
            button.style.paddingBottom = 14;
            button.style.borderTopLeftRadius = 10;
            button.style.borderTopRightRadius = 10;
            button.style.borderBottomLeftRadius = 10;
            button.style.borderBottomRightRadius = 10;
            RuntimeLabelStyle.ApplyDefaultFont(button);
            return button;
        }
    }
}
