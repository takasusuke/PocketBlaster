using PocketBlaster.Aim;
using PocketBlaster.Meta;
using PocketBlaster.Networking;
using UnityEngine;

namespace PocketBlaster.Gameplay
{
    /// <summary>
    /// プレイヤーの移動・視界回転(オーナー要望、2026-09-06)。「構える」ボタン
    /// (PhoneControllerServer.IsAiming)を押していない間、スマホの傾きが照準ではなく
    /// 視界の回転の入力になる — 「構えるときの感度と、構えていない時の感度は
    /// それぞれ調整できるようにして下さい。構えていない時は、視界が回転するイメージ
    /// です」。傾きは1系統しか無いため、狙いと視界回転のどちらに使うかをこの
    /// ボタンで明示的に切り替える設計(GameSettings.LookSensitivity参照 — 狙いの
    /// 感度(角度→ピクセル)とは単位も意味も違う(角度→回転の角速度)ため別設定)。
    ///
    /// 構えを解いた瞬間の傾きを「回転の中立姿勢」として基準化し(<see cref="_lookRefBeta"/>
    /// /<see cref="_lookRefGamma"/>)、そこからの傾き(前後=beta→見上げ/見下ろし、
    /// 左右=gamma→左右旋回)の大きさに応じて`movableRoot`のローカル回転を継続的に
    /// 変化させる(倒し続けるほど速く回る、ラジコンのスティックに近い操作感)。
    /// 傾きが小さい間は不感帯(moveTiltDeadzoneDegrees)で無視し、意図しない回転を防ぐ。
    ///
    /// 実際の歩行は足踏み("step"メッセージ)が担当する。構えている間は狙っている
    /// 方向へ、構えていない間は現在向いている方向(視界回転後のmovableRoot.forward)へ
    /// 進む — 「視界を回してから足踏みで歩く」という一般的なFPS操作に近い。
    ///
    /// フィールドの障害物・パルクール(オーナー要望、2026-09-06:「フィールドの構築が
    /// 必要です。オブジェクトを配置したり、小さなオブジェクトに対して...パルクールを
    /// して上ったり」)に対応する。移動先が<see cref="Obstacle"/>と重なる場合、
    /// その高さが`stepUpHeight`以下なら自動的に「乗り越え」(見た目の高さだけ上げる)、
    /// それより高ければ移動そのものをブロックする(ObstacleCrossing参照)。
    /// 物理エンジン(CharacterController等)には頼らない割り切った実装。
    ///
    /// このゲームは「オンレール式」が決定済み事項(docs/requirements.md §1)なので、
    /// 大きなワープのような移動はできず、`movableRoot`の初期位置から`maxOffsetRadius`を
    /// 超えて離れられないようPlayerOffsetStateでクランプする — ウェーブ間の大きな移動は
    /// 引き続きStageDirectorが担う。
    ///
    /// `movableRoot`未指定時はこのGameObject自身を動かす。StageDirectorと共存する場合は、
    /// カメラをStageDirectorが動かす親(Rig)の子にし、`movableRoot`にカメラのTransformを
    /// 指定する — 親(Rigのウェーブ間Lerp)と子(このステップ移動・視界回転)が同じ
    /// Transformの同じプロパティを取り合わないようにするため。
    /// </summary>
    public class PlayerLocomotion : MonoBehaviour
    {
        [SerializeField] private Transform movableRoot;
        [SerializeField] private GyroReticleController aimSource;
        // オーナー要望2026-09-06「プレイヤーの移動速度を速めてください」を受けて
        // 0.3→0.7に引き上げた(1歩ぶんの移動距離)。
        [SerializeField] private float stepDistance = 0.7f;
        [SerializeField] private float maxOffsetRadius = 9f;
        [SerializeField] private float moveTiltDeadzoneDegrees = 5f;
        [SerializeField] private float moveTiltMaxDegrees = 30f;
        [SerializeField] private float maxLookPitchDegrees = 60f;
        [SerializeField] private float stepUpHeight = 0.6f;

        private PhoneControllerServer _server;
        private PlayerOffsetState _offsetState;
        private Obstacle[] _obstacles;
        private bool _wasAiming = true;
        private float _lookRefBeta;
        private float _lookRefGamma;
        private float _lookYaw;
        private float _lookPitch;

        private bool _isInitialized;

        private void Awake()
        {
            EnsureInitialized();
        }

        /// <summary>
        /// Unityは異なるコンポーネント間でのAwake()の実行順序を保証しない。
        /// StageDirector.Awake()がStartNextWave()経由でこちらのResetForNewWave()を
        /// 呼ぶ時、このコンポーネント自身のAwake()がまだ実行されていない場合があり、
        /// _offsetStateが未初期化のままNullReferenceExceptionになっていた
        /// (2026-09-06、オーナー報告)。Awake()からもResetForNewWave()からも呼べる
        /// 初期化をここにまとめ、二重実行を防ぐ。
        /// </summary>
        private void EnsureInitialized()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            // GetComponentではなくGetOrCreate() — PhoneControllerServerはシーンをまたぐ
            // 永続シングルトン(2026-09-06、再挑戦での接続断対応。PhoneControllerServer.cs参照)。
            _server = PhoneControllerServer.GetOrCreate();
            if (movableRoot == null) movableRoot = transform;
            if (aimSource == null) aimSource = GetComponent<GyroReticleController>();

            _offsetState = new PlayerOffsetState(maxOffsetRadius);
            _obstacles = FindObjectsByType<Obstacle>(FindObjectsSortMode.None);
            _server.OnStep += HandleStep;
        }

        private void OnDestroy()
        {
            if (_server != null) _server.OnStep -= HandleStep;
        }

        /// <summary>
        /// ウェーブが切り替わる時にStageDirectorから呼ぶ。視界回転・移動オフセットを
        /// 両方リセットしないと、前のウェーブで振り向いた向きや動いた位置のまま新しい
        /// ウェーブのカメラ位置に合流してしまい、敵が正面ではなく横に見える等の混乱を
        /// 招く(移動範囲が2.5m→9mに広がったことで顕在化しうる問題、2026-09-06)。
        /// </summary>
        public void ResetForNewWave()
        {
            EnsureInitialized();
            _offsetState.Reset();
            _lookYaw = 0f;
            _lookPitch = 0f;
            movableRoot.localPosition = Vector3.zero;
            movableRoot.localRotation = Quaternion.identity;
        }

        private void Update()
        {
            if (_server.IsAiming)
            {
                _wasAiming = true;
                return; // 構えている間は照準側(GyroReticleController)が傾きを使う
            }

            if (_wasAiming)
            {
                // 構えを解いた瞬間の傾きを、視界回転の「ニュートラル」姿勢として記録する。
                _lookRefBeta = _server.LatestBeta;
                _lookRefGamma = _server.LatestGamma;
                _wasAiming = false;
            }

            var yawInput = ApplyDeadzone(Mathf.DeltaAngle(_lookRefGamma, _server.LatestGamma));
            var pitchInput = ApplyDeadzone(Mathf.DeltaAngle(_lookRefBeta, _server.LatestBeta));
            if (yawInput == 0f && pitchInput == 0f) return;

            // 符号はGyroReticleControllerの照準マッピング(betaDelta*sensitivityで
            // そのままオフセットに加算)と揃えてある。ここを反転させると、構えている
            // 時と構えていない時とで同じ持ち方・同じ傾け方なのに上下が逆に感じられる
            // ("反転設定が反映されていないように見える"というオーナー報告の原因はこれ
            // だった。webapp側の反転設定自体は正しく両方に効いていた——構えていない
            // 間のUnity側マッピングだけがずれていた)。
            var lookSensitivity = GameSettings.Current.LookSensitivity;
            _lookYaw += yawInput * lookSensitivity * Time.deltaTime;
            _lookPitch = Mathf.Clamp(_lookPitch + pitchInput * lookSensitivity * Time.deltaTime, -maxLookPitchDegrees, maxLookPitchDegrees);
            movableRoot.localRotation = Quaternion.Euler(_lookPitch, _lookYaw, 0f);
        }

        /// <summary>傾き(度)を-1〜1の入力値へ変換する。不感帯以下は0、最大角以上は±1。</summary>
        private float ApplyDeadzone(float tiltDegrees)
        {
            var magnitude = Mathf.Abs(tiltDegrees);
            if (magnitude < moveTiltDeadzoneDegrees) return 0f;
            var normalized = Mathf.Clamp01((magnitude - moveTiltDeadzoneDegrees) / (moveTiltMaxDegrees - moveTiltDeadzoneDegrees));
            return Mathf.Sign(tiltDegrees) * normalized;
        }

        private void HandleStep()
        {
            Vector3 direction;
            if (_server.IsAiming)
            {
                // 構えている間は、狙いを定めたまま横に避ける、という元の用途。
                if (aimSource == null) return;
                var aimRay = aimSource.GetAimRay();
                if (aimRay == null) return;
                direction = aimRay.Value.direction;
            }
            else
            {
                // 構えていない間は、視界回転後に現在向いている方向へ歩く。
                direction = movableRoot.forward;
            }

            TryMoveTo(_offsetState.ComputeStepResult(direction, stepDistance));
        }

        /// <summary>
        /// 障害物を確認してから移動を確定する。乗り越えられる高さならその場だけ
        /// プレイヤーの高さを上げ、乗り越えられない高さならこの移動そのものを
        /// 諦めて現在位置に留まる(ObstacleCrossing参照)。
        /// </summary>
        private void TryMoveTo(Vector3 prospectiveOffset)
        {
            var blocking = FindOverlappingObstacle(prospectiveOffset);
            var crossing = ObstacleCrossing.Evaluate(blocking != null, blocking != null ? blocking.Height : 0f, stepUpHeight);
            if (crossing == ObstacleCrossingResult.Blocked) return;

            _offsetState.SetOffset(prospectiveOffset);
            var climbHeight = crossing == ObstacleCrossingResult.StepUp ? blocking.Height : 0f;
            movableRoot.localPosition = new Vector3(prospectiveOffset.x, climbHeight, prospectiveOffset.z);
        }

        private Obstacle FindOverlappingObstacle(Vector3 prospectiveOffset)
        {
            if (_obstacles == null || _obstacles.Length == 0) return null;

            var worldPos = movableRoot.parent != null
                ? movableRoot.parent.TransformPoint(prospectiveOffset)
                : prospectiveOffset;

            foreach (var obstacle in _obstacles)
            {
                if (obstacle == null) continue;
                var dx = worldPos.x - obstacle.Position.x;
                var dz = worldPos.z - obstacle.Position.z;
                var flatDistanceSqr = dx * dx + dz * dz;
                if (flatDistanceSqr <= obstacle.Radius * obstacle.Radius) return obstacle;
            }
            return null;
        }
    }
}
