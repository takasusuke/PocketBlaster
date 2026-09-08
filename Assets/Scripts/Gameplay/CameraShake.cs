using UnityEngine;

namespace PocketBlaster.Gameplay
{
    /// <summary>
    /// 画面シェイク(オーナー承認2026-09-08:「同様のアーケードゲームと比べて足りて
    /// いない部分を...実装する」の一環。House of the Dead等の被弾時の定番演出)。
    ///
    /// カメラの実体(Cameraコンポーネント)は、PlayerLocomotion.movableRoot /
    /// MultiplayerStageDirector.moveTargetが動かす「リグ」の子(Lens)に置く
    /// (CameraRigFactory参照)。このコンポーネントはLens自身のlocalPositionに
    /// ランダムな減衰オフセットを「最後に上乗せする」だけなので、リグ側の
    /// 既存の書き込み(足踏み移動・自動カメラ移動)と競合しない——別のTransformの
    /// 別のフレームタイミング(LateUpdate、リグ側のUpdateより後)で動くため。
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        private Vector3 _basePosition;
        private float _durationRemaining;
        private float _totalDuration;
        private float _magnitude;

        private void Awake()
        {
            _basePosition = transform.localPosition;
        }

        /// <summary>
        /// シェイクを開始/上書きする。進行中のシェイクより弱く・短い要求は無視する
        /// (小さな後発シェイクが大きい進行中のシェイクを弱めて見せてしまうのを防ぐ)。
        /// </summary>
        public void Shake(float durationSeconds, float magnitude)
        {
            if (durationSeconds <= _durationRemaining && magnitude <= _magnitude) return;
            _durationRemaining = durationSeconds;
            _totalDuration = durationSeconds;
            _magnitude = magnitude;
        }

        private void LateUpdate()
        {
            if (_durationRemaining <= 0f)
            {
                transform.localPosition = _basePosition;
                return;
            }

            _durationRemaining -= Time.deltaTime;
            var progress = Mathf.Clamp01(_durationRemaining / _totalDuration);
            var offset = Random.insideUnitSphere * (_magnitude * progress);
            transform.localPosition = _basePosition + offset;
        }
    }
}
