using System.Collections;
using System.Collections.Generic;
using PocketBlaster.Networking;
using PocketBlaster.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace PocketBlaster.Gameplay
{
    /// <summary>
    /// マルチプレイヤーモード(2026-09-07、オーナー承認: 画面共有・移動オート・各自
    /// レティクル案)専用のステージ進行役。シングルプレイヤーの`StageDirector`
    /// (ウェーブ管理)と`GameSession`(一時停止・再挑戦・体力)の責務を1クラスに
    /// 統合している——`GameSession`は`FindFirstObjectByType&lt;StageDirector&gt;`という
    /// シングルプレイヤー専用の型を探しに行く設計のため、そのまま流用せず
    /// このシーンには置かない形にした。
    ///
    /// 最大の違いは「移動がプレイヤー操作ではなく自動」であること。ウェーブを
    /// クリアするたびに、削除済みだった(2026-09-06、自由に歩き回る方式への変更に
    /// 伴い)`StageDirector.MoveCameraTo`と同じLerp/Slerpのカメラ移動コルーチンを
    /// このクラス専用として復活させた。
    ///
    /// 体力・スコアは個人ではなく共有(協力プレイのため、オーナー確認2026-09-06:
    /// 「共有の体力・残機を持たせる」)。プレイヤーの接続/切断は
    /// `PlayerSlotAssigner`でスロット(0/1、色分け)を割り当て、
    /// `MultiplayerAimController`を動的に生成/破棄することで扱う。
    ///
    /// アイテム(Pickup)はシングルプレイヤーの`StageDirector.MaybeSpawnPickup`と同じ
    /// 仕組みで出現させる(2026-09-07、オーナー要望「アイテムを実装して」で追加、
    /// 計画時点ではv1スコープ外としていた)。効果の振り分けは`MultiplayerAimController`
    /// 側で行う(誰が撃ったかを知っているのはあちら) — 弾薬回復/最大弾薬数増加は
    /// 撃った本人へ、体力回復は`OnHealthPickupCollected`経由でこちらの共有HPへ。
    ///
    /// v1のスコープ外(計画時に明示、変更なし): プレイヤーごとの感度設定、
    /// 3人以上への拡張。
    /// </summary>
    public class MultiplayerStageDirector : MonoBehaviour
    {
        [System.Serializable]
        public class Wave
        {
            public Transform cameraWaypoint;
            public Target[] enemies;
        }

        private static readonly Color[] PlayerColors =
        {
            new Color(0.9f, 0.25f, 0.25f), // プレイヤー1: 赤
            new Color(0.25f, 0.55f, 0.95f) // プレイヤー2: 青
        };
        private static readonly string[] PlayerColorNames = { "red", "blue" };

        [SerializeField] private Camera stageCamera;
        [SerializeField] private Transform moveTarget;
        [SerializeField] private Wave[] waves;
        [SerializeField] private float cameraMoveDurationSeconds = 1.5f;
        [SerializeField] private int maxHealth = 150;
        [SerializeField] private int enemyContactDamage = 25;
        [SerializeField] private int healthPickupHealAmount = 30;
        [SerializeField, Range(0f, 1f)] private float pickupSpawnChance = 0.5f;
        [SerializeField] private float returnToTitleDelaySeconds = 5f;
        [SerializeField] private int playerCapacity = 2;

        private PhoneControllerServer _server;
        private PlayerSlotAssigner _slotAssigner;
        private readonly Dictionary<int, MultiplayerAimController> _aimControllers = new Dictionary<int, MultiplayerAimController>();
        private Pickup _currentPickup;

        private StageProgressState _progress;
        private ScoreState _score;
        private ComboState _combo;
        private ShotAccuracyState _accuracy;
        private PlayerHealthState _health;
        private string _highScorePrefsKey;
        private int _maxPossibleScore;
        private bool _isGameOver;
        private bool _isPaused;

        private UIDocument _uiDocument;
        private PanelSettings _panelSettings;
        private Label _waveLabel;
        private Label _scoreLabel;
        private Label _gradeLabel;
        private Label _pauseLabel;
        private VisualElement _healthBarTrack;
        private VisualElement _healthBarFill;
        private VisualElement _damageFlash;
        private Coroutine _cameraMoveRoutine;
        private Coroutine _damageFlashRoutine;

        private void Awake()
        {
            if (stageCamera == null) stageCamera = Camera.main;
            if (moveTarget == null) moveTarget = stageCamera.transform;

            _server = PhoneControllerServer.GetOrCreate();
            _slotAssigner = new PlayerSlotAssigner(playerCapacity);
            _score = new ScoreState();
            _combo = new ComboState();
            _accuracy = new ShotAccuracyState();
            _health = new PlayerHealthState(maxHealth);
            _highScorePrefsKey = $"PocketBlaster.HighScore.{gameObject.scene.name}";

            var enemyCounts = new int[waves.Length];
            for (var i = 0; i < waves.Length; i++)
            {
                enemyCounts[i] = waves[i].enemies.Length;
                foreach (var enemy in waves[i].enemies)
                {
                    enemy.gameObject.SetActive(false);
                    _maxPossibleScore += enemy.PointValue;
                }
            }
            _progress = new StageProgressState(enemyCounts);

            _server.OnPlayerConnected += HandlePlayerConnected;
            _server.OnPlayerDisconnected += HandlePlayerDisconnected;
            _server.OnPauseToggleRequested += HandlePauseToggleRequested;
            _server.OnRetryRequested += HandleRetryRequested;
            _server.OnReturnToTitleRequested += HandleReturnToTitleRequested;

            BuildUi();
            UpdateWaveLabel();
            UpdateHealthBar();
            StartNextWave();
        }

        private void OnDestroy()
        {
            if (_server != null)
            {
                _server.OnPlayerConnected -= HandlePlayerConnected;
                _server.OnPlayerDisconnected -= HandlePlayerDisconnected;
                _server.OnPauseToggleRequested -= HandlePauseToggleRequested;
                _server.OnRetryRequested -= HandleRetryRequested;
                _server.OnReturnToTitleRequested -= HandleReturnToTitleRequested;
            }
            if (_panelSettings != null) Destroy(_panelSettings);
            Time.timeScale = 1f;
        }

        // ------------------------------------------------------------ 接続管理

        private void HandlePlayerConnected(int connectionId)
        {
            var slot = _slotAssigner.Assign(connectionId);
            if (slot == null)
            {
                _server.SendToPlayer(connectionId, "{\"type\":\"full\"}");
                return;
            }

            var go = new GameObject($"MultiplayerAimController_Player{slot.Value + 1}");
            var controller = go.AddComponent<MultiplayerAimController>();
            controller.Initialize(connectionId, slot.Value, PlayerColors[slot.Value], _server);
            controller.OnHealthPickupCollected += HandleHealthPickupCollected;
            // コンボ・命中率は個人ではなく共有(協力プレイのため、オーナー要望2026-09-08)。
            // どちらが撃っても同じComboState/ShotAccuracyStateへ積む。
            controller.OnShotResolved += HandleShotResolved;
            _aimControllers[connectionId] = controller;

            _server.SendToPlayer(connectionId, $"{{\"type\":\"welcome\",\"color\":\"{PlayerColorNames[slot.Value]}\"}}");
        }

        private void HandlePlayerDisconnected(int connectionId)
        {
            _slotAssigner.Release(connectionId);
            if (_aimControllers.TryGetValue(connectionId, out var controller))
            {
                if (controller != null)
                {
                    controller.OnHealthPickupCollected -= HandleHealthPickupCollected;
                    controller.OnShotResolved -= HandleShotResolved;
                    Destroy(controller.gameObject);
                }
                _aimControllers.Remove(connectionId);
            }
        }

        /// <summary>体力回復アイテムを撃った本人ではなく、共有HPプールへ反映する
        /// (オーナー確認2026-09-06:「共有の体力・残機を持たせる」)。</summary>
        private void HandleHealthPickupCollected()
        {
            if (_isGameOver) return;
            _health.Heal(healthPickupHealAmount);
            UpdateHealthBar();
        }

        // ------------------------------------------------------------- ウェーブ進行

        private void StartNextWave()
        {
            if (!_progress.AdvanceToNextWave())
            {
                ShowStageClear();
                return;
            }

            var wave = waves[_progress.CurrentWaveIndex];
            foreach (var enemy in wave.enemies)
            {
                enemy.gameObject.SetActive(true);
                enemy.OnDefeated += HandleEnemyDefeated;
                var approach = enemy.GetComponent<EnemyApproach>();
                if (approach != null) approach.OnReachedPlayer += HandleEnemyReachedPlayer;
            }

            UpdateWaveLabel();
            MaybeSpawnPickup(wave);

            if (wave.cameraWaypoint != null)
            {
                if (_cameraMoveRoutine != null) StopCoroutine(_cameraMoveRoutine);
                _cameraMoveRoutine = StartCoroutine(MoveCameraTo(wave.cameraWaypoint.position, wave.cameraWaypoint.rotation));
            }
        }

        /// <summary>
        /// ウェーブ開始時に一定確率でアイテムを1個出現させる(シングルプレイヤーの
        /// StageDirector.MaybeSpawnPickupと同じ仕組み、2026-09-07、オーナー要望
        /// 「アイテムを実装して」)。マルチプレイヤーには残機の概念(カジュアル/
        /// アーケードの区別)が無いため、体力回復も常に候補に入れる。
        /// </summary>
        private void MaybeSpawnPickup(Wave wave)
        {
            if (Random.value > pickupSpawnChance) return;

            var origin = wave.cameraWaypoint != null ? wave.cameraWaypoint : moveTarget;
            var depth = Random.Range(8f, 14f);
            var xOffset = Random.Range(-4f, 4f);
            var position = origin.position + origin.forward * depth + Vector3.right * xOffset;
            position.y = 1.6f;

            _currentPickup = PickupFactory.Create(ChooseRandomPickupType(), position);
            _currentPickup.OnConsumed += HandlePickupConsumed;
        }

        private static PickupType ChooseRandomPickupType()
        {
            var options = new[] { PickupType.Health, PickupType.Reload, PickupType.AmmoUp };
            return options[Random.Range(0, options.Length)];
        }

        /// <summary>
        /// 効果の実際の適用は撃った本人(MultiplayerAimController)側で行う——ここでは
        /// 出現管理の後片付け(購読解除・参照クリア)だけ。
        /// </summary>
        private void HandlePickupConsumed(Pickup pickup)
        {
            pickup.OnConsumed -= HandlePickupConsumed;
            if (_currentPickup == pickup) _currentPickup = null;
        }

        /// <summary>ウェーブが切り替わる時、取り残されたアイテムは片付ける。</summary>
        private void ClearCurrentPickup()
        {
            if (_currentPickup == null) return;
            _currentPickup.OnConsumed -= HandlePickupConsumed;
            Destroy(_currentPickup.gameObject);
            _currentPickup = null;
        }

        /// <summary>移動オート(オーナー確認2026-09-06「画面は共通で移動はオートとして」)。
        /// 削除済みだったシングルプレイヤー旧StageDirector.MoveCameraToと同じ実装。</summary>
        private IEnumerator MoveCameraTo(Vector3 targetPosition, Quaternion targetRotation)
        {
            var startPosition = moveTarget.position;
            var startRotation = moveTarget.rotation;
            var t = 0f;
            while (t < cameraMoveDurationSeconds)
            {
                t += Time.deltaTime;
                var p = Mathf.Clamp01(t / cameraMoveDurationSeconds);
                moveTarget.position = Vector3.Lerp(startPosition, targetPosition, p);
                moveTarget.rotation = Quaternion.Slerp(startRotation, targetRotation, p);
                yield return null;
            }
            moveTarget.position = targetPosition;
            moveTarget.rotation = targetRotation;
        }

        /// <summary>コンボ・命中率の更新(オーナー要望2026-09-08、StageDirectorと同じ役割
        /// をどちらのプレイヤーの射撃にも適用する——協力プレイのため個人ではなく共有)。</summary>
        private void HandleShotResolved(bool didHit)
        {
            _combo.RegisterShot(didHit);
            _accuracy.RegisterShot(didHit);
            UpdateWaveLabel();
        }

        private void HandleEnemyDefeated(Target defeatedTarget)
        {
            var basePoints = defeatedTarget.WasLastHitHeadshot ? defeatedTarget.PointValue * 2 : defeatedTarget.PointValue;
            var points = Mathf.RoundToInt(basePoints * _combo.Multiplier);
            _score.AddPoints(points);
            ScorePopupEffect.SpawnAt(defeatedTarget.transform.position, points, stageCamera);
            AdvanceWaveState();
        }

        private void HandleEnemyReachedPlayer(Target reachedTarget)
        {
            AdvanceWaveState();
            TakeDamage(enemyContactDamage, "敵の接近");
        }

        private void AdvanceWaveState()
        {
            var wave = waves[_progress.CurrentWaveIndex];
            var waveCleared = _progress.NotifyEnemyDefeated();
            UpdateWaveLabel();

            if (waveCleared)
            {
                foreach (var enemy in wave.enemies)
                {
                    enemy.OnDefeated -= HandleEnemyDefeated;
                    var approach = enemy.GetComponent<EnemyApproach>();
                    if (approach != null) approach.OnReachedPlayer -= HandleEnemyReachedPlayer;
                }
                ClearCurrentPickup();
                StartNextWave();
            }
        }

        // --------------------------------------------------------------- 体力

        private void TakeDamage(int amount, string reason)
        {
            if (_isGameOver) return;

            var isGameOverNow = _health.TakeDamage(amount);
            TriggerDamageFlash();
            UpdateHealthBar();
            if (isGameOverNow)
            {
                _isGameOver = true;
                _waveLabel.text = $"ゲームオーバー（{reason}でHPが尽きました）";
                StartCoroutine(ReturnToTitleAfterDelay(returnToTitleDelaySeconds));
            }
        }

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

        private void UpdateHealthBar()
        {
            var ratio = Mathf.Clamp01(_health.CurrentHealth / (float)_health.MaxHealth);
            _healthBarFill.style.width = Length.Percent(ratio * 100f);
        }

        // ------------------------------------------------------------- クリア

        private void ShowStageClear()
        {
            var previousHighScore = PlayerPrefs.GetInt(_highScorePrefsKey, 0);
            var isNewHighScore = _score.TotalScore > previousHighScore;
            if (isNewHighScore)
            {
                PlayerPrefs.SetInt(_highScorePrefsKey, _score.TotalScore);
                PlayerPrefs.Save();
            }

            var highScoreLine = isNewHighScore
                ? $"ハイスコア更新！ {_score.TotalScore}"
                : $"スコア: {_score.TotalScore}（ハイスコア: {previousHighScore}）";
            var statsLine = $"命中率: {_accuracy.AccuracyPercent:F0}%（{_accuracy.Hits}/{_accuracy.ShotsFired}）" +
                             $"  最大コンボ: {_combo.MaxCombo}";
            _waveLabel.text = $"ステージクリア！\n{highScoreLine}\n{statsLine}";

            ShowGrade(ScoreGrade.Compute(_score.TotalScore, _maxPossibleScore));
            StartCoroutine(ReturnToTitleAfterDelay(returnToTitleDelaySeconds));
        }

        private void ShowGrade(char grade)
        {
            _gradeLabel.text = grade.ToString();
            _gradeLabel.style.color = GradeColor(grade);
            _gradeLabel.style.display = DisplayStyle.Flex;
            StartCoroutine(GradePopRoutine());

            if (grade == 'A' || grade == 'B')
            {
                var spawnPosition = stageCamera.transform.position + stageCamera.transform.forward * 3f;
                CelebrationEffect.SpawnAt(spawnPosition);
            }
        }

        private static Color GradeColor(char grade)
        {
            switch (grade)
            {
                case 'A': return new Color(1f, 0.85f, 0.2f);
                case 'B': return new Color(0.6f, 0.85f, 1f);
                case 'C': return Color.white;
                case 'D': return new Color(1f, 0.6f, 0.3f);
                default: return new Color(1f, 0.35f, 0.35f);
            }
        }

        private IEnumerator GradePopRoutine()
        {
            const float duration = 0.45f;
            const float overshootUntil = 0.7f;
            var t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                var p = Mathf.Clamp01(t / duration);
                var scale = p < overshootUntil
                    ? Mathf.Lerp(0.2f, 1.15f, p / overshootUntil)
                    : Mathf.Lerp(1.15f, 1f, (p - overshootUntil) / (1f - overshootUntil));
                _gradeLabel.style.scale = new Scale(new Vector3(scale, scale, 1f));
                yield return null;
            }
            _gradeLabel.style.scale = new Scale(Vector3.one);
        }

        private IEnumerator ReturnToTitleAfterDelay(float delaySeconds)
        {
            yield return new WaitForSeconds(delaySeconds);
            SceneManager.LoadScene("Title");
        }

        // ---------------------------------------------------- 一時停止・再挑戦・戻る

        private void HandlePauseToggleRequested()
        {
            if (_isGameOver) return;
            _isPaused = !_isPaused;
            Time.timeScale = _isPaused ? 0f : 1f;
            _pauseLabel.style.display = _isPaused ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void HandleRetryRequested()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void HandleReturnToTitleRequested()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("Title");
        }

        // ------------------------------------------------------------------ UI

        private void UpdateWaveLabel()
        {
            _waveLabel.text = $"ウェーブ {_progress.CurrentWaveIndex + 1}/{_progress.WaveCount}" +
                               $"  残り敵: {_progress.RemainingInCurrentWave}";
            _scoreLabel.text = _combo.CurrentCombo >= 2
                ? $"スコア {_score.TotalScore}  {_combo.CurrentCombo}COMBO x{_combo.Multiplier:0.0}"
                : $"スコア {_score.TotalScore}";
        }

        private void BuildUi()
        {
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            RuntimeLabelStyle.EnsureTheme(_panelSettings);
            _panelSettings.sortingOrder = 5;

            var uiDocumentGo = new GameObject("MultiplayerStageDirectorUI");
            uiDocumentGo.transform.SetParent(transform, false);
            _uiDocument = uiDocumentGo.AddComponent<UIDocument>();
            _uiDocument.panelSettings = _panelSettings;
            var root = _uiDocument.rootVisualElement;

            _waveLabel = new Label();
            _waveLabel.style.position = Position.Absolute;
            _waveLabel.style.top = 12;
            _waveLabel.style.right = 12;
            _waveLabel.style.color = Color.white;
            _waveLabel.style.fontSize = 20;
            _waveLabel.style.unityTextAlign = TextAnchor.UpperRight;
            RuntimeLabelStyle.ApplyDefaultFont(_waveLabel);
            root.Add(_waveLabel);

            _scoreLabel = new Label();
            _scoreLabel.style.position = Position.Absolute;
            _scoreLabel.style.top = 12;
            _scoreLabel.style.left = 0;
            _scoreLabel.style.right = 0;
            _scoreLabel.style.color = Color.white;
            _scoreLabel.style.fontSize = 32;
            _scoreLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _scoreLabel.style.unityTextAlign = TextAnchor.UpperCenter;
            RuntimeLabelStyle.ApplyDefaultFont(_scoreLabel);
            root.Add(_scoreLabel);

            // 共有の体力バー(協力プレイのため1本だけ、画面上部中央のスコアの下)。
            _healthBarTrack = new VisualElement();
            _healthBarTrack.style.position = Position.Absolute;
            _healthBarTrack.style.top = 56;
            _healthBarTrack.style.left = Length.Percent(50);
            _healthBarTrack.style.translate = new Translate(Length.Percent(-50), 0);
            _healthBarTrack.style.width = 260;
            _healthBarTrack.style.height = 16;
            _healthBarTrack.style.backgroundColor = new Color(0f, 0f, 0f, 0.5f);
            _healthBarTrack.style.borderTopLeftRadius = 4;
            _healthBarTrack.style.borderTopRightRadius = 4;
            _healthBarTrack.style.borderBottomLeftRadius = 4;
            _healthBarTrack.style.borderBottomRightRadius = 4;
            root.Add(_healthBarTrack);

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

            _gradeLabel = new Label();
            _gradeLabel.style.display = DisplayStyle.None;
            _gradeLabel.style.position = Position.Absolute;
            _gradeLabel.style.top = Length.Percent(30);
            _gradeLabel.style.left = 0;
            _gradeLabel.style.right = 0;
            _gradeLabel.style.fontSize = 160;
            _gradeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _gradeLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            RuntimeLabelStyle.ApplyDefaultFont(_gradeLabel);
            root.Add(_gradeLabel);

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
            root.Add(_pauseLabel);

            _damageFlash = new VisualElement();
            _damageFlash.style.position = Position.Absolute;
            _damageFlash.style.left = 0;
            _damageFlash.style.right = 0;
            _damageFlash.style.top = 0;
            _damageFlash.style.bottom = 0;
            _damageFlash.style.backgroundColor = new Color(1f, 0f, 0f, 0f);
            _damageFlash.pickingMode = PickingMode.Ignore;
            root.Add(_damageFlash);
        }
    }
}
