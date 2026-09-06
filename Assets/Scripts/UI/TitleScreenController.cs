using PocketBlaster.Meta;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace PocketBlaster.UI
{
    /// <summary>
    /// 起動画面(オーナー要望、2026-09-06:「起動画面を実装して。そこから難易度選択や
    /// 設定などができるようにして」)。PhoneControllerServerを持たない、PC上の
    /// マウス操作だけで完結する画面 — スマホをまだつながなくても難易度・感度・SE音量を
    /// 選べる。選んだ内容はGameSettings(PlayerPrefs)へ保存し、遷移先のステージシーンが
    /// Awakeで読み取る(GameSession・GyroReticleController参照)。
    ///
    /// 難易度モードはこれまでスマホ側(webapp/index.html)の接続直後のラジオボタンで
    /// 選んでいたが、起動画面の新設に伴いこちらへ一本化した(重複した選択UIを持たない)。
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
        private Label _sensitivityLabel;

        private void Awake()
        {
            BuildUi();
            RefreshModeButtons();
        }

        private void OnDestroy()
        {
            if (_panelSettings != null) Destroy(_panelSettings);
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

            _sensitivityLabel = BuildValueLabel();
            panel.Add(_sensitivityLabel);
            var sensitivitySlider = new Slider(GameSettingsState.MinSensitivity, GameSettingsState.MaxSensitivity)
            {
                value = GameSettings.Current.Sensitivity
            };
            sensitivitySlider.style.marginBottom = 24;
            sensitivitySlider.RegisterValueChangedCallback(evt =>
            {
                GameSettings.SetSensitivity(evt.newValue);
                UpdateSensitivityLabel();
            });
            panel.Add(sensitivitySlider);
            UpdateSensitivityLabel();

            panel.Add(BuildSectionLabel("ステージを選んでスタート"));
            var stage1Button = BuildStartButton(stage1DisplayName, () => StartStage(stage1SceneName));
            stage1Button.style.marginBottom = 8;
            panel.Add(stage1Button);
            var stage2Button = BuildStartButton(stage2DisplayName, () => StartStage(stage2SceneName));
            panel.Add(stage2Button);
        }

        private void UpdateSfxVolumeLabel()
        {
            _sfxVolumeLabel.text = $"SE音量: {Mathf.RoundToInt(GameSettings.Current.SfxVolume * 100)}%";
        }

        private void UpdateSensitivityLabel()
        {
            _sensitivityLabel.text = $"感度: {GameSettings.Current.Sensitivity:F1}";
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
