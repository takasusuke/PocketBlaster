using UnityEngine;

namespace PocketBlaster.Gameplay
{
    /// <summary>
    /// 敵を倒した時にその場で加点(「+100」等)を浮かび上がらせて消す演出
    /// (オーナー要望2026-09-06:「敵を倒した時にスコアを表示するようにしてください」)。
    /// StageDirector.HandleEnemyDefeatedから、倒した敵のワールド座標と加点を渡して呼ぶ。
    /// 実体はScorePopupBehaviour(1個ずつ生成して自分で寿命管理する)。
    /// </summary>
    public static class ScorePopupEffect
    {
        public static void SpawnAt(Vector3 worldPosition, int points, Camera camera)
        {
            if (camera == null) camera = Camera.main;
            if (camera == null || points <= 0) return;

            var go = new GameObject("ScorePopup");
            go.AddComponent<ScorePopupBehaviour>().Initialize(worldPosition, points, camera);
        }
    }
}
