using UnityEngine;

namespace PocketBlaster.Gameplay
{
    /// <summary>
    /// スプライトを常にカメラの方へ向ける(House of the Dead等の古典的ライトガンゲームで
    /// 使われる、3D空間に置いた2Dスプライトの手法)。3Dモデルを用意しなくても
    /// 既存の2D画像生成パイプラインでそのまま敵アートを作れるのが狙い。
    /// </summary>
    public class Billboard : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;

        private void LateUpdate()
        {
            var cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null) return;

            var toCamera = transform.position - cam.transform.position;
            if (toCamera.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.LookRotation(toCamera);
        }
    }
}
