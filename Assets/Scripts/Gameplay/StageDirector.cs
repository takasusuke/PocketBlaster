using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace PocketBlaster.Gameplay
{
    /// <summary>
    /// マイルストーン4(docs/requirements.md §4): オンレールの敵配置・複数ウェーブを持つ
    /// 短い1ステージ。「ゲームとしての手触りを通しで確認する」ことが目的なので、
    /// カメラの移動は滑らかなレール(スプライン)ではなく、ウェーブごとの固定ポイント間を
    /// 単純に補間するだけの最小実装にしてある。
    ///
    /// 各ウェーブの敵(Target、respawnsAfterDefeat=falseにしておく必要がある)を全滅させると
    /// 次のウェーブのカメラ位置へ移動する。最後のウェーブをクリアすると「ステージクリア」を表示。
    ///
    /// 足踏みでの微移動(PlayerLocomotion)と共存させる場合は、カメラをこのスクリプトが
    /// 直接動かすのではなく`moveTarget`(通常はカメラの親、Rig)を動かす — カメラ自身は
    /// PlayerLocomotionがRigからのローカルオフセットとして動かすため、同じTransformの
    /// 同じプロパティを2つのスクリプトが取り合わないようにする。`moveTarget`未指定時は
    /// 従来通りカメラ自身を動かす。
    ///
    /// 得点(docs/requirements.md §8 将来の拡張)は、このステージ内のTarget.OnDefeatedを
    /// 全部購読しているのでここに乗せている(Target.PointValueの合計、ScoreState参照)。
    /// ハイスコアはシーン名ごとにPlayerPrefsへ保存する(ステージをまたいだ通算スコアでは
    /// なく、ステージ単体のベスト記録)。
    ///
    /// 敵がプレイヤーに近づき過ぎて退場した場合(EnemyApproach.OnReachedPlayer)も、
    /// 撃って倒した場合と同様にウェーブの残り数からは減らすが、加点はしない。
    /// OnEnemyReachedPlayerでGameSessionへ中継し、難易度モードの残機減少に使う。
    /// </summary>
    public class StageDirector : MonoBehaviour
    {
        [System.Serializable]
        public class Wave
        {
            public Transform cameraWaypoint;
            public Target[] enemies;
        }

        [SerializeField] private Camera stageCamera;
        [SerializeField] private Transform moveTarget;
        [SerializeField] private Wave[] waves;
        [SerializeField] private float cameraMoveDurationSeconds = 1.5f;

        /// <summary>
        /// 敵がプレイヤーに近づき過ぎて退場した(EnemyApproach.OnReachedPlayer)瞬間に
        /// 中継される。GameSessionが難易度モード(アーケード)の残機減少に使う。
        /// </summary>
        public event System.Action OnEnemyReachedPlayer;

        private StageProgressState _progress;
        private ScoreState _score;
        private string _highScorePrefsKey;
        private UIDocument _uiDocument;
        private PanelSettings _panelSettings;
        private Label _waveLabel;
        private Label _scoreLabel;

        private void Awake()
        {
            if (stageCamera == null) stageCamera = Camera.main;
            if (moveTarget == null) moveTarget = stageCamera.transform;

            _score = new ScoreState();
            _highScorePrefsKey = $"PocketBlaster.HighScore.{gameObject.scene.name}";

            var enemyCounts = new int[waves.Length];
            for (var i = 0; i < waves.Length; i++)
            {
                enemyCounts[i] = waves[i].enemies.Length;
                foreach (var enemy in waves[i].enemies)
                {
                    enemy.gameObject.SetActive(false);
                }
            }
            _progress = new StageProgressState(enemyCounts);

            BuildUi();
            StartNextWave();
        }

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

            if (wave.cameraWaypoint != null)
            {
                StopAllCoroutines();
                StartCoroutine(MoveCameraTo(wave.cameraWaypoint.position, wave.cameraWaypoint.rotation));
            }
        }

        private void HandleEnemyDefeated(Target defeatedTarget)
        {
            _score.AddPoints(defeatedTarget.PointValue);
            AdvanceWaveState();
        }

        /// <summary>敵が近づき過ぎてプレイヤーに到達した場合。撃って倒したのではないので加点はしない。</summary>
        private void HandleEnemyReachedPlayer(Target reachedTarget)
        {
            OnEnemyReachedPlayer?.Invoke();
            AdvanceWaveState();
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
                StartNextWave();
            }
        }

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

        private void UpdateWaveLabel()
        {
            _waveLabel.text = $"ウェーブ {_progress.CurrentWaveIndex + 1}/{_progress.WaveCount}" +
                               $"  残り敵: {_progress.RemainingInCurrentWave}";
            _scoreLabel.text = $"スコア {_score.TotalScore}";
        }

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
            _waveLabel.text = $"ステージクリア！\n{highScoreLine}";
        }

        private void BuildUi()
        {
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();

            var uiDocumentGo = new GameObject("StageDirectorUI");
            uiDocumentGo.transform.SetParent(transform, false);
            _uiDocument = uiDocumentGo.AddComponent<UIDocument>();
            _uiDocument.panelSettings = _panelSettings;

            _waveLabel = new Label();
            _waveLabel.style.position = Position.Absolute;
            _waveLabel.style.top = 12;
            _waveLabel.style.right = 12;
            _waveLabel.style.color = Color.white;
            _waveLabel.style.fontSize = 20;
            _waveLabel.style.unityTextAlign = TextAnchor.UpperRight;
            _uiDocument.rootVisualElement.Add(_waveLabel);

            // スコアは狙っている最中も視界の端で常に見えるように、画面上部中央へ
            // 大きく単独表示する(オーナーからのプレイテストFB、2026-09-06:
            // 「スコア表示はゲーム画面のほうに出してください」)。
            _scoreLabel = new Label();
            _scoreLabel.style.position = Position.Absolute;
            _scoreLabel.style.top = 12;
            _scoreLabel.style.left = Length.Percent(50);
            _scoreLabel.style.translate = new Translate(Length.Percent(-50), 0);
            _scoreLabel.style.color = Color.white;
            _scoreLabel.style.fontSize = 32;
            _scoreLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _scoreLabel.style.unityTextAlign = TextAnchor.UpperCenter;
            _uiDocument.rootVisualElement.Add(_scoreLabel);
        }

        private void OnDestroy()
        {
            if (_panelSettings != null) Destroy(_panelSettings);
        }
    }
}
