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
    ///
    /// 感度スライダー自体はスマホの「狙って撃つ」では連続的にドラッグできないため、
    /// 各スライダーに"−"/"＋"ボタンを添えて段階調整できるようにしてある(オーナー要望、
    /// 2026-09-06:「起動画面で感度を上下されるボタンをスマホから狙って撃つことで
    /// 調整できるようにしてください」。BuildAdjustableSlider参照)。マウスでのドラッグも
    /// 引き続き使える。
    /// </summary>
    public class TitleScreenController : MonoBehaviour
    {
        [SerializeField] private string stage1SceneName = "Milestone4_Stage";
        [SerializeField] private string stage1DisplayName = "ステージ1";
        [SerializeField] private string stage2SceneName = "Stage2_BossRush";
        [SerializeField] private string stage2DisplayName = "ステージ2（ボスラッシュ）";
        // 練習モード(オーナー要望、2026-09-06:「敵は出てこず、ただ移動して、構えて撃つ
        // だけの練習をするモードを実装してください」)。PracticeRangeSceneBuilder参照。
        [SerializeField] private string practiceSceneName = "PracticeRange";
        [SerializeField] private string practiceDisplayName = "練習（射撃レンジ）";

        private static readonly Color SelectedColor = new Color(0.30f, 0.45f, 0.95f);
        private static readonly Color UnselectedColor = new Color(0.2f, 0.2f, 0.26f);

        private UIDocument _uiDocument;
        private PanelSettings _panelSettings;
        private Button _casualButton;
        private Button _arcadeButton;
        private Label _sfxVolumeLabel;
        private Label _verticalSensitivityLabel;
        private Label _horizontalSensitivityLabel;
        private Label _lookSensitivityLabel;

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

            // 背景の一枚絵(オーナー要望、2026-09-06:「起動画面の背景に使える一枚絵を
            // 作成して適用して」)。ランタイムコード(UnityEditor非依存)なので
            // Resources.Loadで読み込む(Pickupの専用アートと同じ理由、PickupFactory
            // 参照)。見つからない場合はTitleSceneBuilderが設定したカメラの単色背景の
            // ままになる——生成待ちでも画面自体は問題なく動く。
            var backgroundTexture = Resources.Load<Texture2D>("UI/title_background");
            if (backgroundTexture != null)
            {
                root.style.backgroundImage = new StyleBackground(backgroundTexture);
                root.style.unityBackgroundScaleMode = new StyleEnum<ScaleMode>(ScaleMode.ScaleAndCrop);
            }

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
            BuildAdjustableSlider(
                panel, GameSettingsState.MinSensitivity, GameSettingsState.MaxSensitivity,
                GameSettings.Current.VerticalSensitivity, 12,
                v => { GameSettings.SetVerticalSensitivity(v); UpdateVerticalSensitivityLabel(); });
            UpdateVerticalSensitivityLabel();

            _horizontalSensitivityLabel = BuildValueLabel();
            panel.Add(_horizontalSensitivityLabel);
            BuildAdjustableSlider(
                panel, GameSettingsState.MinSensitivity, GameSettingsState.MaxSensitivity,
                GameSettings.Current.HorizontalSensitivity, 16,
                v => { GameSettings.SetHorizontalSensitivity(v); UpdateHorizontalSensitivityLabel(); });
            UpdateHorizontalSensitivityLabel();

            // 「構えていない間」(視界の回転、PlayerLocomotion参照)の感度は、狙いの
            // 感度とは単位も意味も違う(角度→ピクセルではなく角度→回転の角速度)ため
            // 別の設定にしてある(オーナー要望、2026-09-06:「構えるときの感度と、
            // 構えていない時の感度はそれぞれ調整できるようにして下さい」)。
            _lookSensitivityLabel = BuildValueLabel();
            panel.Add(_lookSensitivityLabel);
            BuildAdjustableSlider(
                panel, GameSettingsState.MinLookSensitivity, GameSettingsState.MaxLookSensitivity,
                GameSettings.Current.LookSensitivity, 24,
                v => { GameSettings.SetLookSensitivity(v); UpdateLookSensitivityLabel(); });
            UpdateLookSensitivityLabel();

            panel.Add(BuildSectionLabel("ステージを選んでスタート"));
            var stage1Button = BuildStartButton(stage1DisplayName, () => StartStage(stage1SceneName));
            stage1Button.style.marginBottom = 8;
            RegisterClickTarget(stage1Button, () => StartStage(stage1SceneName));
            panel.Add(stage1Button);
            var stage2Button = BuildStartButton(stage2DisplayName, () => StartStage(stage2SceneName));
            stage2Button.style.marginBottom = 8;
            RegisterClickTarget(stage2Button, () => StartStage(stage2SceneName));
            panel.Add(stage2Button);
            var practiceButton = BuildStartButton(practiceDisplayName, () => StartStage(practiceSceneName));
            RegisterClickTarget(practiceButton, () => StartStage(practiceSceneName));
            panel.Add(practiceButton);

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
            _verticalSensitivityLabel.text = $"感度（構える・上下）: {GameSettings.Current.VerticalSensitivity:F1}";
        }

        private void UpdateHorizontalSensitivityLabel()
        {
            _horizontalSensitivityLabel.text = $"感度（構える・左右）: {GameSettings.Current.HorizontalSensitivity:F1}";
        }

        private void UpdateLookSensitivityLabel()
        {
            _lookSensitivityLabel.text = $"感度（構えない・視界回転）: {GameSettings.Current.LookSensitivity:F1}";
        }

        /// <summary>
        /// スライダー本体に加えて"−"/"＋"の段階調整ボタンを横に並べた行をpanelへ追加する。
        /// ボタンはマウスクリックに加えて<see cref="RegisterClickTarget"/>でスマホの
        /// 「狙って撃つ」操作からも押せるようにする(オーナー要望、2026-09-06)。
        /// ボタンはslider.valueを変更するだけで、実際の設定への反映はスライダー本体の
        /// RegisterValueChangedCallback(呼び出し側から渡されるonChanged)に任せる —
        /// マウスドラッグと同じ経路を通ることで、値の反映漏れを防ぐ。
        /// </summary>
        private Slider BuildAdjustableSlider(VisualElement panel, float min, float max, float initialValue, float marginBottom, Action<float> onChanged)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = marginBottom;

            var slider = new Slider(min, max) { value = initialValue };
            slider.style.flexGrow = 1;
            slider.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            row.Add(slider);

            var step = (max - min) / 20f;
            void Decrease() => slider.value = Mathf.Clamp(slider.value - step, min, max);
            void Increase() => slider.value = Mathf.Clamp(slider.value + step, min, max);

            var minusButton = BuildAdjustButton("−", Decrease);
            var plusButton = BuildAdjustButton("＋", Increase);
            RegisterClickTarget(minusButton, Decrease);
            RegisterClickTarget(plusButton, Increase);
            row.Add(minusButton);
            row.Add(plusButton);

            panel.Add(row);
            return slider;
        }

        private static Button BuildAdjustButton(string text, Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.style.width = 34;
            button.style.height = 34;
            button.style.marginLeft = 6;
            button.style.color = Color.white;
            button.style.fontSize = 18;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.borderTopLeftRadius = 8;
            button.style.borderTopRightRadius = 8;
            button.style.borderBottomLeftRadius = 8;
            button.style.borderBottomRightRadius = 8;
            RuntimeLabelStyle.ApplyDefaultFont(button);
            return button;
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
