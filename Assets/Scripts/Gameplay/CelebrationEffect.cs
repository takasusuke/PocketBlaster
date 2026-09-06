using UnityEngine;

namespace PocketBlaster.Gameplay
{
    /// <summary>
    /// ステージクリア画面の紙吹雪風パーティクル(オーナー要望2026-09-06:「スコア画面に
    /// AからEの総合評価を表示するなど、高揚感の高まる演出、色にして」)。
    /// JuiceSplashEffectと同じ最小構成(ParticleSystemを1個生成して自壊させる)だが、
    /// 単色ではなくcolorOverLifetimeで複数色を経由させ「お祝い感」を出す。
    /// </summary>
    public static class CelebrationEffect
    {
        public static void SpawnAt(Vector3 position)
        {
            var go = new GameObject("CelebrationBurst");
            go.transform.position = position;

            var ps = go.AddComponent<ParticleSystem>();
            // AddComponent直後はplayOnAwake既定でもう再生中になっているため、
            // 設定前に一度止める(JuiceSplashEffect参照)。
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.3f;
            main.loop = false;
            main.startLifetime = 1.4f;
            main.startSpeed = 4.5f;
            main.startSize = 0.16f;
            main.startColor = Color.white; // 実際の色はcolorOverLifetimeが担う
            main.gravityModifier = 0.4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 90) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 45f;
            shape.radius = 0.2f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.85f, 0.2f), 0f),
                    new GradientColorKey(new Color(1f, 0.3f, 0.6f), 0.4f),
                    new GradientColorKey(new Color(0.3f, 0.7f, 1f), 0.75f),
                    new GradientColorKey(new Color(0.3f, 0.7f, 1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.8f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Sprites/Default"));

            ps.Play();

            Object.Destroy(go, main.duration + main.startLifetime.constantMax + 0.5f);
        }
    }
}
