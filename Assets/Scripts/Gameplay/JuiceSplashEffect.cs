using UnityEngine;

namespace PocketBlaster.Gameplay
{
    /// <summary>
    /// 「野菜ゾンビを倒すとジュースになる」という世界観(docs/requirements.md 決定済み事項)を
    /// 着弾フィードバックに直結させるための、色付きパーティクルの飛び散り演出。
    /// 専用のVFXアセットは用意せず、組み込みのSprites/Defaultシェーダーだけで完結させている
    /// (正式なアートはスプライト側で担保し、着弾演出はこの軽量な仮実装のままでよいと判断)。
    /// </summary>
    public static class JuiceSplashEffect
    {
        public static void SpawnAt(Vector3 position, Color juiceColor)
        {
            var go = new GameObject("JuiceSplash");
            go.transform.position = position;

            var ps = go.AddComponent<ParticleSystem>();
            // AddComponent<ParticleSystem>()はplayOnAwake既定trueのため、この時点で
            // Awake/OnEnableが同期的に走りもう再生中になっている。再生中はmain.durationを
            // 変更できない("Setting the duration while system is still playing is not
            // supported"エラー)ため、設定前に一度明示的に止める。
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.startLifetime = 0.4f;
            main.startSpeed = 3f;
            main.startSize = 0.12f;
            main.startColor = juiceColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Sprites/Default"));

            ps.Play();

            Object.Destroy(go, main.duration + main.startLifetime.constantMax + 0.5f);
        }
    }
}
