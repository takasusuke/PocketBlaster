using PocketBlaster.Aim;
using PocketBlaster.Networking;
using UnityEngine;
using UnityEngine.UIElements;

namespace PocketBlaster.Gameplay
{
    /// <summary>
    /// 難易度モード(docs/requirements.md §8 将来の拡張)。フェールの条件は2つ:
    /// 「狙って撃ってはずした」(GyroReticleController.OnShotResolved、弾切れの空撃ちは
    /// ノーカウント)と、「敵に近づかれ過ぎた」(StageDirector.OnEnemyReachedPlayer、
    /// EnemyApproach参照、2026-09-06追加)。どちらもアーケードモードでのみ残機を減らす。
    ///
    /// モードはスマホ側(webapp/index.html、接続直後に"mode"メッセージ)で選ぶ。
    /// 未選択のまま(メッセージが来る前)はカジュアル扱い — 何もフェールしない安全側の既定。
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
        private string _gameOverReason = "";

        private UIDocument _uiDocument;
        private PanelSettings _panelSettings;
        private Label _sessionLabel;

        private void Awake()
        {
            _server = GetComponent<PhoneControllerServer>();
            if (reticleController == null) reticleController = GetComponent<GyroReticleController>();
            if (stageDirector == null) stageDirector = FindFirstObjectByType<StageDirector>();

            _server.OnModeSelected += HandleModeSelected;
            if (reticleController != null) reticleController.OnShotResolved += HandleShotResolved;
            if (stageDirector != null) stageDirector.OnEnemyReachedPlayer += HandleEnemyReachedPlayer;

            BuildUi();
            UpdateLabel();
        }

        private void OnDestroy()
        {
            if (_server != null) _server.OnModeSelected -= HandleModeSelected;
            if (reticleController != null) reticleController.OnShotResolved -= HandleShotResolved;
            if (stageDirector != null) stageDirector.OnEnemyReachedPlayer -= HandleEnemyReachedPlayer;
            if (_panelSettings != null) Destroy(_panelSettings);
        }

        private void HandleModeSelected(string modeName)
        {
            _mode = modeName == "arcade" ? Mode.Arcade : Mode.Casual;
            _lives = _mode == Mode.Arcade ? new LivesState(startingLives) : null;
            _isGameOver = false;
            if (reticleController != null) reticleController.enabled = true;
            UpdateLabel();
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
            if (isGameOverNow)
            {
                _isGameOver = true;
                _gameOverReason = reason;
                if (reticleController != null) reticleController.enabled = false;
            }
            UpdateLabel();
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
            _uiDocument.rootVisualElement.Add(_sessionLabel);
        }
    }
}
