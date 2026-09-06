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
    /// 難易度モード(docs/requirements.md §8 将来の拡張)。フェールの条件は
    /// 「敵に近づかれ過ぎた」(StageDirector.OnEnemyReachedPlayer、EnemyApproach参照、
    /// 2026-09-06追加)と「高すぎる場所からの落下」のみで、いずれも**アーケードモードでのみ
    /// ゲームオーバーに繋がる**。「狙って撃ってはずした」も残機を減らす条件だったが、
    /// 実際に遊んでみて厳しすぎるという判断からオーナーが却下した(2026-09-06:「難易度
    /// モード「はずれ＝残機減少」は却下です」)。`GyroReticleController.OnShotResolved`
    /// イベント自体は残しているが、このクラスではもう購読しない。
    ///
    /// モードは起動画面(Title、TitleScreenController)で選び、GameSettings(PlayerPrefs)
    /// 経由でシーンをまたいで受け渡される(2026-09-06、以前はスマホ側の接続直後の
    /// "mode"メッセージで選んでいたが、起動画面の新設に伴いPC側へ一本化した)。
    ///
    /// スマホ側からの一時停止・再挑戦(オーナー要望、2026-09-06)もここで扱う。
    /// 一時停止はTime.timeScaleを0/1で切り替えるだけ(ジャイロ移動・敵の接近・カメラの
    /// Lerp移動などTime.deltaTimeに依存する処理は自動的に止まる)。射撃だけは
    /// timeScaleの影響を受けないため、GyroReticleControllerを明示的に無効化する。
    /// 再挑戦はシーンを丸ごとリロードする(スコア・体力・ウェーブ進行がすべて初期化される)。
    ///
    /// 体力は「残機」(整数の残り回数)から「HPゲージ」(PlayerHealthState)へ移行した
    /// (オーナー要望2026-09-06:「プレイヤーの体力ゲージもUIとして実装してください」)。
    /// ダメージ量は発生源によって変える — 敵接触は大きめ、落下は高さに応じて可変
    /// (PlayerLocomotion.OnFallDamage、FallDamageCalculator参照)。
    ///
    /// HPゲージ自体はモードに関わらず常時表示する(オーナー要望2026-09-06:「体力バーは
    /// モードに限らず表示して」)。カジュアルモードでもダメージ・回復は同じようにHPへ
    /// 反映するが、「無制限」の名の通りHPが尽きてもゲームオーバーにはしない
    /// (0で床止まりのまま、フェール判定はアーケードモードだけが行う)。
    /// </summary>
    public class GameSession : MonoBehaviour
    {
        public enum Mode
        {
            Casual,
            Arcade
        }

        [SerializeField] private int maxHealth = 100;
        [SerializeField] private int enemyContactDamage = 30;
        [SerializeField] private int healthPickupHealAmount = 30;
        [SerializeField] private float returnToTitleDelaySeconds = 5f;
        [SerializeField] private GyroReticleController reticleController;
        [SerializeField] private StageDirector stageDirector;
        [SerializeField] private PlayerLocomotion playerLocomotion;

        private PhoneControllerServer _server;
        private PlayerHealthState _health;
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
        private VisualElement _healthBarTrack;
        private VisualElement _healthBarFill;

        private void Awake()
        {
            // GetComponentではなくGetOrCreate() — PhoneControllerServerはシーンをまたぐ
            // 永続シングルトンにしてある(2026-09-06、オーナー報告「再挑戦すると、スマホとの
            // 接続が切れてしまいました」への対応。PhoneControllerServer.cs参照)。
            _server = PhoneControllerServer.GetOrCreate();
            if (reticleController == null) reticleController = GetComponent<GyroReticleController>();
            if (stageDirector == null) stageDirector = FindFirstObjectByType<StageDirector>();
            if (playerLocomotion == null) playerLocomotion = GetComponent<PlayerLocomotion>();

            _mode = GameSettings.Current.IsArcadeMode ? Mode.Arcade : Mode.Casual;
            _health = new PlayerHealthState(maxHealth);

            _server.OnPauseToggleRequested += HandlePauseToggleRequested;
            _server.OnRetryRequested += HandleRetryRequested;
            if (stageDirector != null)
            {
                stageDirector.OnEnemyReachedPlayer += HandleEnemyReachedPlayer;
                stageDirector.OnHealthPickupCollected += HandleHealthPickupCollected;
            }
            if (playerLocomotion != null) playerLocomotion.OnFallDamage += HandleFallDamage;

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
            if (stageDirector != null)
            {
                stageDirector.OnEnemyReachedPlayer -= HandleEnemyReachedPlayer;
                stageDirector.OnHealthPickupCollected -= HandleHealthPickupCollected;
            }
            if (playerLocomotion != null) playerLocomotion.OnFallDamage -= HandleFallDamage;
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

        private void HandleEnemyReachedPlayer()
        {
            TakeDamage(enemyContactDamage, "敵の接近");
        }

        /// <summary>
        /// 高すぎる場所からの落下ダメージ(オーナー要望、2026-09-06:「あまりに高い
        /// ところから飛び降りる場合にはダメージが入るようにしてください」)。
        /// PlayerLocomotion側で落差と安全な高さから算出済みの量をそのまま適用する。
        /// </summary>
        private void HandleFallDamage(int amount)
        {
            TakeDamage(amount, "落下");
        }

        /// <summary>
        /// 体力回復アイテム(オーナー要望、2026-09-06)。カジュアルモードでも見た目上は
        /// 回復するが、そもそもStageDirector側でカジュアルモードではこのアイテム自体を
        /// 出現候補から除外しているため、実際に呼ばれるのはアーケードモードだけになる。
        /// </summary>
        private void HandleHealthPickupCollected()
        {
            if (_isGameOver) return;
            _health.Heal(healthPickupHealAmount);
            UpdateLabel();
        }

        /// <summary>
        /// HPへの反映自体はモードに関わらず行う(体力バーを常時表示する以上、
        /// カジュアルモードでも見た目に反応が無いと不自然なため)。ゲームオーバーに
        /// なるのはアーケードモードだけ — 「無制限」のカジュアルモードはHPが尽きても
        /// 0で床止まりするだけで続行する。
        /// </summary>
        private void TakeDamage(int amount, string reason)
        {
            if (_isGameOver) return;

            var isGameOverNow = _health.TakeDamage(amount);
            TriggerDamageFlash();
            if (isGameOverNow && _mode == Mode.Arcade)
            {
                _isGameOver = true;
                _gameOverReason = reason;
                if (reticleController != null) reticleController.enabled = false;
                // ステージ終了(ゲームオーバー)後は起動画面に戻れるようにする
                // (オーナー要望、2026-09-06:「ステージ終了後は起動画面に戻れるように
                // してください」)。スコア・回数直後の「再挑戦」操作(スマホ)は
                // シーンリロードでこのコルーチンごと消えるため、両立して問題ない。
                StartCoroutine(ReturnToTitleAfterDelay(returnToTitleDelaySeconds));
            }
            UpdateLabel();
        }

        private IEnumerator ReturnToTitleAfterDelay(float delaySeconds)
        {
            yield return new WaitForSeconds(delaySeconds);
            SceneManager.LoadScene("Title");
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
            var modeText = _mode == Mode.Casual ? "モード: カジュアル（無制限）" : "モード: アーケード";
            _sessionLabel.text = _isGameOver
                ? $"ゲームオーバー（{_gameOverReason}でHPが尽きました）"
                : $"{modeText}  HP: {_health.CurrentHealth}/{_health.MaxHealth}";

            // 体力バーはモードに関わらず常時表示する(オーナー要望2026-09-06:
            // 「体力バーはモードに限らず表示して」)。
            var ratio = Mathf.Clamp01(_health.CurrentHealth / (float)_health.MaxHealth);
            _healthBarFill.style.width = Length.Percent(ratio * 100f);
        }

        private void BuildUi()
        {
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            RuntimeLabelStyle.EnsureTheme(_panelSettings);
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

            // 体力ゲージ(オーナー要望2026-09-06)。GyroReticleControllerのリロードバーと
            // 同じtrack+fillの構成。モードに関わらず常時表示する(UpdateLabel参照)。
            _healthBarTrack = new VisualElement();
            _healthBarTrack.style.position = Position.Absolute;
            _healthBarTrack.style.left = 12;
            _healthBarTrack.style.bottom = 40;
            _healthBarTrack.style.width = 220;
            _healthBarTrack.style.height = 16;
            _healthBarTrack.style.backgroundColor = new Color(0f, 0f, 0f, 0.5f);
            _healthBarTrack.style.borderTopLeftRadius = 4;
            _healthBarTrack.style.borderTopRightRadius = 4;
            _healthBarTrack.style.borderBottomLeftRadius = 4;
            _healthBarTrack.style.borderBottomRightRadius = 4;
            _uiDocument.rootVisualElement.Add(_healthBarTrack);

            _healthBarFill = new VisualElement();
            _healthBarFill.style.position = Position.Absolute;
            _healthBarFill.style.left = 0;
            _healthBarFill.style.top = 0;
            _healthBarFill.style.bottom = 0;
            _healthBarFill.style.width = Length.Percent(100);
            _healthBarFill.style.backgroundColor = new Color(0.2f, 0.8f, 0.3f);
            _healthBarFill.style.borderTopLeftRadius = 4;
            _healthBarFill.style.borderTopRightRadius = 4;
            _healthBarFill.style.borderBottomLeftRadius = 4;
            _healthBarFill.style.borderBottomRightRadius = 4;
            _healthBarTrack.Add(_healthBarFill);

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
