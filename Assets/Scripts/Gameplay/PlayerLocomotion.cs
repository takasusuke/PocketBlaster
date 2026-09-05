using PocketBlaster.Aim;
using PocketBlaster.Networking;
using UnityEngine;

namespace PocketBlaster.Gameplay
{
    /// <summary>
    /// 「足踏みを検知して、狙っている方向に移動する」プレイヤー移動(オーナー要望、2026-09-06)。
    /// スマホ側(webapp/index.html)が加速度センサーから足踏みを検出して"step"メッセージを
    /// 送ってくるたびに、現在の狙い(GyroReticleController.GetAimRay())の水平成分方向へ
    /// 一定距離だけ動く。
    ///
    /// このゲームは「オンレール式」が決定済み事項(docs/requirements.md §1)なので、
    /// 自由に歩き回れるわけではなく、`movableRoot`の初期位置から`maxOffsetRadius`を
    /// 超えて離れられないようPlayerOffsetStateでクランプする — ウェーブ間の大きな移動は
    /// 引き続きStageDirectorが担い、これはその場での小さな踏み込み・回避だけを担当する。
    ///
    /// `movableRoot`未指定時はこのGameObject自身を動かす。StageDirectorと共存する場合は、
    /// カメラをStageDirectorが動かす親(Rig)の子にし、`movableRoot`にカメラのTransformを
    /// 指定する — 親(Rigのウェーブ間Lerp)と子(このステップ移動)が同じTransformの
    /// 同じプロパティを取り合わないようにするため。
    /// </summary>
    [RequireComponent(typeof(PhoneControllerServer))]
    public class PlayerLocomotion : MonoBehaviour
    {
        [SerializeField] private Transform movableRoot;
        [SerializeField] private GyroReticleController aimSource;
        [SerializeField] private float stepDistance = 0.3f;
        [SerializeField] private float maxOffsetRadius = 2.5f;

        private PhoneControllerServer _server;
        private PlayerOffsetState _offsetState;

        private void Awake()
        {
            _server = GetComponent<PhoneControllerServer>();
            if (movableRoot == null) movableRoot = transform;
            if (aimSource == null) aimSource = GetComponent<GyroReticleController>();

            _offsetState = new PlayerOffsetState(maxOffsetRadius);
            _server.OnStep += HandleStep;
        }

        private void OnDestroy()
        {
            if (_server != null) _server.OnStep -= HandleStep;
        }

        private void HandleStep()
        {
            if (aimSource == null) return;
            var aimRay = aimSource.GetAimRay();
            if (aimRay == null) return;

            var offset = _offsetState.Step(aimRay.Value.direction, stepDistance);
            movableRoot.localPosition = offset;
        }
    }
}
