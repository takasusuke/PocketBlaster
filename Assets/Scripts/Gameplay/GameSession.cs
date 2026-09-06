using System.Collections;
using PocketBlaster.Aim;
using PocketBlaster.Meta;
using PocketBlaster.Networking;
using PocketBlaster.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace PocketBlaster.Gameplay
{
    /// <summary>
    /// 難易度モード(docs/requirements.md §8 将来の拡張)。フェールの条件は2つ:
    /// 「狙って撃ってはずした」(GyroReticleController.OnShotResolved、弾切れの空撃ちは
    /// ノーカウント)と、「敵に近づかれ過ぎた」(StageDirector.OnEnemyReachedPlayer、
    /// EnemyApproach参照、2026-09-06追加)。どちらもアーケードモードでのみ残機を減らす。
    ///
    /// モードは起動画面(Title、TitleScreenController)で選び、GameSettings(PlayerPrefs)
    /// 経由でシーンをまたいで受け渡される(2026-09-06、以前はスマホ側の接続直後の
    /// "mode"メッセージで選んでいたが、起動画面の新設に伴いPC側へ一本化した)。
    ///
    /// スマホ側からの一時停止・再挑戦(オーナー要望、2026-09-06)もここで扱う。
    /// 一時停止はTime.timeScaleを0/1で切り替えるだけ(ジャイロ移動・敵の接近・カメラの
    /// Lerp移動などTime.deltaTimeに依存する処理は自動的に止まる)。射撃だけは
    /// timeScaleの影響を受けないため、GyroReticleControllerを明示的に無効化する。
    /// 再挑戦はシーンを丸ごとリロードする(スコア・残機・ウェーブ進行がすべて初期化される)。
    /// </summary>
    [RequireComponent(typeof(PhoneControllerServer))]
    public class GameSession : MonoBehaviour
    {
        public enum Mode
        {
            Casual,
            Arcade
        }

        [SerializeField] private int startingLives = 3;
        [SerializeField] private GyroReticleController reticleController;
        [SerializeField] private StageDirector stageDirector;

        private PhoneControllerServer _server;
        private LivesState _lives;
        private Mode _mode = Mode.Casual;
        private bool _isGameOver;
        private bool _isPaused;
        private string _gameOverReason = "";

        private UIDocument _uiDocument;
        private PanelSettings _panelSettings;
        private Label _sessionLabel;
        private Label _pauseLabel;
        private VisualElement _damageFlash;
        private Coroutine _damageFlashRoutine;

        private void Awake()
        {
            _server = GetComponent<PhoneControllerServer>();
            if (reticleController == null) reticleController = GetComponent<GyroReticleController>();
            if (stageDirector == null) stageDirector = FindFirstObjectByType<StageDirector>();

            _mode = GameSettings.Current.IsArcadeMode ? Mode.Arcade : Mode.Casual;
            _lives = _mode == Mode.Arcade ? new LivesState(startingLives) : null;

            _server.OnPauseToggleRequested += HandlePauseToggleRequested;
            _server.OnRetryRequested += HandleRetryRequested;
            if (reticleController != null) reticleController.OnShotResolved += HandleShotResolved;
            if (stageDirector != null) stageDirector.OnEnemyReachedPlayer += HandleEnemyReachedPlayer;

            BuildUi();
            UpdateLabel();
        }

        private void OnDestroy()
        {
            if (_server != null)
            {
                _server.OnPauseToggleRequested -= HandlePauseToggleRequested;
                _server.OnRetryRequested -= HandleRetryRequested;
            }
            if (reticleController != null) reticleController.OnShotResolved -= HandleShotResolved;
            if (stageDirector != null) stageDirector.OnEnemyReachedPlayer -= HandleEnemyReachedPlayer;
            if (_panelSettings != null) Destroy(_panelSettings);

            // このGameObjectが破棄される時(シーン遷移等)にtimeScaleが0のまま
            // 残ると次のシーンまで止まったままになるため、必ず戻しておく。
            Time.timeScale = 1f;
        }

        private void HandlePauseToggleRequested()
        {
            if (_isGameOver) return;

            _isPaused = !_isPaused;
            Time.timeScale = _isPaused ? 0f : 1f;
            if (reticleController != null) reticleController.enabled = !_isPaused;
            _pauseLabel.style.display = _isPaused ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void HandleRetryRequested()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void HandleShotResolved(bool didHit)
        {
            if (didHit) return;
            LoseLifeIfArcade("はずれ");
        }

        private void HandleEnemyReachedPlayer()
        {
            LoseLifeIfArcade("敵の接近");
        }

        private void LoseLifeIfArcade(string reason)
        {
            if (_mode != Mode.Arcade || _isGameOver) return;

            var isGameOverNow = _lives.LoseLife();
            TriggerDamageFlash();
            if (isGameOverNow)
            {
                _isGameOver = true;
                _gameOverReason = reason;
                if (reticleController != null) reticleController.enabled = false;
            }
            UpdateLabel();
        }

        /// <summary>
        /// ダメージを受けたことを画面全体の赤い明滅で伝える(オーナー要望2026-09-06:
        /// 「ダメージをくらったときのアニメーションを追加して」)。
        /// </summary>
        private void TriggerDamageFlash()
        {
            if (_damageFlashRoutine != null) StopCoroutine(_damageFlashRoutine);
            _damageFlashRoutine = StartCoroutine(DamageFlashRoutine());
        }

        private IEnumerator DamageFlashRoutine()
        {
            const float duration = 0.4f;
            var t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                var alpha = Mathf.Lerp(0.5f, 0f, t / duration);
                _damageFlash.style.backgroundColor = new Color(1f, 0f, 0f, alpha);
                yield return null;
            }
            _damageFlash.style.backgroundColor = new Color(1f, 0f, 0f, 0f);
            _damageFlashRoutine = null;
        }

        private void UpdateLabel()
        {
            if (_mode == Mode.Casual)
            {
                _sessionLabel.text = "モード: カジュアル（無制限）";
                return;
            }

            _sessionLabel.text = _isGameOver
                ? $"ゲームオーバー（{_gameOverReason}で残機が尽きました）"
                : $"モード: アーケード  残機: {_lives.RemainingLives}";
        }

        private void BuildUi()
        {
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            // StageDirector.csと同じ理由でsortingOrderを明示する。
            _panelSettings.sortingOrder = 5;

            var uiDocumentGo = new GameObject("GameSessionUI");
            uiDocumentGo.transform.SetParent(transform, false);
            _uiDocument = uiDocumentGo.AddComponent<UIDocument>();
            _uiDocument.panelSettings = _panelSettings;

            _sessionLabel = new Label();
            _sessionLabel.style.position = Position.Absolute;
            _sessionLabel.style.bottom = 12;
            _sessionLabel.style.left = 12;
            _sessionLabel.style.color = Color.white;
            _sessionLabel.style.fontSize = 18;
            RuntimeLabelStyle.ApplyDefaultFont(_sessionLabel);
            _uiDocument.rootVisualElement.Add(_sessionLabel);

            _pauseLabel = new Label("一時停止中");
            _pauseLabel.style.display = DisplayStyle.None;
            _pauseLabel.style.position = Position.Absolute;
            _pauseLabel.style.top = Length.Percent(50);
            _pauseLabel.style.left = Length.Percent(50);
            _pauseLabel.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
            _pauseLabel.style.color = Color.white;
            _pauseLabel.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
            _pauseLabel.style.fontSize = 36;
            _pauseLabel.style.paddingTop = 16;
            _pauseLabel.style.paddingBottom = 16;
            _pauseLabel.style.paddingLeft = 32;
            _pauseLabel.style.paddingRight = 32;
            _pauseLabel.style.borderTopLeftRadius = 12;
            _pauseLabel.style.borderTopRightRadius = 12;
            _pauseLabel.style.borderBottomLeftRadius = 12;
            _pauseLabel.style.borderBottomRightRadius = 12;
            RuntimeLabelStyle.ApplyDefaultFont(_pauseLabel);
            _uiDocument.rootVisualElement.Add(_pauseLabel);

            _damageFlash = new VisualElement();
            _damageFlash.style.position = Position.Absolute;
            _damageFlash.style.left = 0;
            _damageFlash.style.right = 0;
            _damageFlash.style.top = 0;
            _damageFlash.style.bottom = 0;
            _damageFlash.style.backgroundColor = new Color(1f, 0f, 0f, 0f);
            _damageFlash.pickingMode = PickingMode.Ignore; // クリック判定を奪わない(マウスデバッグ等に影響しないように)
            _uiDocument.rootVisualElement.Add(_damageFlash);
        }
    }
}
