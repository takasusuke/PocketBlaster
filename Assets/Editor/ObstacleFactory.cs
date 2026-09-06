using PocketBlaster.Gameplay;
using UnityEditor;
using UnityEngine;

namespace PocketBlaster.EditorTools
{
    /// <summary>
    /// フィールドに置く障害物(Obstacle)をシーンビルダーから組み立てるヘルパー
    /// (オーナー要望、2026-09-06:「フィールドの構築が必要です。オブジェクトを
    /// 配置したり」)。EnemyFactoryと同じく、実際の見た目は正式なアートが無いので
    /// 組み込みの箱プリミティブで済ませる(../CLAUDE.md 11「初期実装では画像を
    /// 作らない」と同じ考え方)。`GameObject.CreatePrimitive`が付けるBoxColliderは
    /// そのまま残す — 射撃のレイキャストも自然にこの障害物でブロックされるようにするため。
    /// </summary>
    public static class ObstacleFactory
    {
        public static Obstacle CreateBox(string name, Vector3 groundPosition, float radius, float height, Color color, bool isPlatform = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = groundPosition + Vector3.up * (height / 2f);
            go.transform.localScale = new Vector3(radius * 2f, height, radius * 2f);

            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = new Material(Shader.Find("Standard")) { color = color };

            var obstacle = go.AddComponent<Obstacle>();
            var so = new SerializedObject(obstacle);
            so.FindProperty("radius").floatValue = radius;
            so.FindProperty("height").floatValue = height;
            so.FindProperty("isPlatform").boolValue = isPlatform;
            so.ApplyModifiedPropertiesWithoutUndo();

            return obstacle;
        }
    }
}
