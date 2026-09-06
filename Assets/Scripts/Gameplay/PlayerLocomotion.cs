using PocketBlaster.Aim;
using PocketBlaster.Networking;
using UnityEngine;

namespace PocketBlaster.Gameplay
{
    /// <summary>
    /// プレイヤー移動(オーナー要望、2026-09-06)。「構える」ボタン
    /// (PhoneControllerServer.IsAiming)を押していない間、スマホの傾きが照準ではなく
    /// 移動方向の入力になる — 「『構える』ボタンを新たに配置して、構えている間は
    /// 照準を動かして、構えていない間は移動する」。傾きは1系統しか無いため、狙いと
    /// 移動のどちらに使うかをこのボタンで明示的に切り替える設計。
    ///
    /// 構えを解いた瞬間の傾きを「移動の中立姿勢」として基準化し(<see cref="_moveRefBeta"/>
    /// /<see cref="_moveRefGamma"/>)、そこからの傾き(前後=beta、左右=gamma)を
    /// カメラの向きを基準にした移動方向へマッピングする(アナログスティックに近い操作感)。
    /// 傾きが小さい間は不感帯(moveTiltDeadzoneDegrees)で無視し、意図しない移動を防ぐ。
    ///
    /// 足踏み("step"メッセージ)での小さな踏み込みは、構えている間だけ引き続き有効
    /// (狙いを定めたまま横に避ける、という元の用途に残す)。
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
    /// 指定する — 親(Rigのウェーブ間Lerp)と子(このステップ移動)が同じTransformの
    /// 同じプロパティを取り合わないようにするため。
    /// </summary>
    public class PlayerLocomotion : MonoBehaviour
    {
        [SerializeField] private Transform movableRoot;
        [SerializeField] private GyroReticleController aimSource;
        [SerializeField] private float stepDistance = 0.3f;
        [SerializeField] private float maxOffsetRadius = 9f;
        [SerializeField] private float moveSpeed = 1.4f;
        [SerializeField] private float moveTiltDeadzoneDegrees = 5f;
        [SerializeField] private float moveTiltMaxDegrees = 30f;
        [SerializeField] private float stepUpHeight = 0.6f;

        private PhoneControllerServer _server;
        private PlayerOffsetState _offsetState;
        private Obstacle[] _obstacles;
        private bool _wasAiming = true;
        private float _moveRefBeta;
        private float _moveRefGamma;

        private void Awake()
        {
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

        private void Update()
        {
            if (_server.IsAiming)
            {
                _wasAiming = true;
                return; // 構えている間の移動は足踏み(HandleStep)だけが担当する
            }

            if (_wasAiming)
            {
                // 構えを解いた瞬間の傾きを、移動の「ニュートラル」姿勢として記録する。
                _moveRefBeta = _server.LatestBeta;
                _moveRefGamma = _server.LatestGamma;
                _wasAiming = false;
            }

            var forwardInput = ApplyDeadzone(Mathf.DeltaAngle(_moveRefBeta, _server.LatestBeta));
            var sideInput = ApplyDeadzone(Mathf.DeltaAngle(_moveRefGamma, _server.LatestGamma));
            if (forwardInput == 0f && sideInput == 0f) return;

            var forward = movableRoot.forward;
            forward.y = 0f;
            forward.Normalize();
            var right = movableRoot.right;
            right.y = 0f;
            right.Normalize();

            var direction = forward * forwardInput + right * sideInput;
            TryMoveTo(_offsetState.ComputeStepResult(direction, moveSpeed * Time.deltaTime));
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
            if (!_server.IsAiming) return; // 移動中は傾き入力(Update)が担当するため足踏みは無視
            if (aimSource == null) return;
            var aimRay = aimSource.GetAimRay();
            if (aimRay == null) return;

            TryMoveTo(_offsetState.ComputeStepResult(aimRay.Value.direction, stepDistance));
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
