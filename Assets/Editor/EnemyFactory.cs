using System.Collections.Generic;
using PocketBlaster.Gameplay;
using UnityEditor;
using UnityEngine;

namespace PocketBlaster.EditorTools
{
    /// <summary>
    /// スプライトベースの敵(野菜ゾンビ、docs/requirements.md 決定済み事項)を
    /// シーンビルダーから共通の手順で組み立てるためのヘルパー。ルート(Billboard+コライダー)と
    /// 子(Visual、SpriteRenderer+倒れ込み回転)を分けているのはTarget.cs参照。
    ///
    /// 被弾可能回数・移動速度・移動パターン(左右に避けながら接近する等)は「種類ごと」に
    /// 固定のプロフィールとして持たせている(オーナー要望、2026-09-06:「それぞれの敵に
    /// 応じて被弾可能回数や移動速度や移動方法を定義して、敵ごとに同じパラメータに
    /// ならないようにしてください」)。同じ種類の野菜ゾンビはどのステージに出てきても
    /// 同じ「性格」を持つ、という一貫性を優先し、呼び出し側での個別上書きは設けていない。
    /// </summary>
    public static class EnemyFactory
    {
        public enum VegetableKind
        {
            Tomato,
            Carrot,
            Onion,
            PumpkinBoss
        }

        private readonly struct VegetableProfile
        {
            public readonly int HitPoints;
            public readonly float ApproachSpeed;
            public readonly float WeaveAmplitude;
            public readonly float WeaveFrequency;
            public readonly int PointValue;

            public VegetableProfile(int hitPoints, float approachSpeed, float weaveAmplitude, float weaveFrequency, int pointValue)
            {
                HitPoints = hitPoints;
                ApproachSpeed = approachSpeed;
                WeaveAmplitude = weaveAmplitude;
                WeaveFrequency = weaveFrequency;
                PointValue = pointValue;
            }
        }

        // トマト: 1発で倒れる代わりに足が速く、まっすぐ突っ込んでくる(反応速度を試す型)。
        // キャロット: すばしっこく、左右に大きく避けながら接近する(狙いを絞らせない型)。
        // オニオン: 硬く(2発)、その分足は遅い(見た目に反して脅威度が高い型)。
        // パンプキンボス: 硬く(3発)、足は遅いが軽く揺れながら迫る(ボスらしい重厚感)。
        private static readonly Dictionary<VegetableKind, VegetableProfile> Profiles = new Dictionary<VegetableKind, VegetableProfile>
        {
            { VegetableKind.Tomato, new VegetableProfile(hitPoints: 1, approachSpeed: 1.5f, weaveAmplitude: 0f, weaveFrequency: 0f, pointValue: 100) },
            { VegetableKind.Carrot, new VegetableProfile(hitPoints: 1, approachSpeed: 1.0f, weaveAmplitude: 1.0f, weaveFrequency: 1.6f, pointValue: 130) },
            { VegetableKind.Onion, new VegetableProfile(hitPoints: 2, approachSpeed: 0.6f, weaveAmplitude: 0f, weaveFrequency: 0f, pointValue: 180) },
            { VegetableKind.PumpkinBoss, new VegetableProfile(hitPoints: 3, approachSpeed: 0.55f, weaveAmplitude: 0.35f, weaveFrequency: 0.4f, pointValue: 500) },
        };

        public static Target CreateVegetableZombie(
            string name, Vector3 position, VegetableKind kind, Sprite sprite, Color juiceColor, float scale,
            bool respawns, bool approaches = false, float damageRange = 1.5f)
        {
            var profile = Profiles[kind];

            var root = new GameObject(name);
            root.transform.position = position;
            root.AddComponent<Billboard>();

            var spriteSize = sprite != null ? (Vector3)sprite.bounds.size : Vector3.one;
            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(spriteSize.x * scale, spriteSize.y * scale, 0.3f);

            var visualGo = new GameObject("Visual");
            visualGo.transform.SetParent(root.transform, false);
            visualGo.transform.localScale = Vector3.one * scale;
            var spriteRenderer = visualGo.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;

            var target = root.AddComponent<Target>();
            var so = new SerializedObject(target);
            so.FindProperty("visualTransform").objectReferenceValue = visualGo.transform;
            so.FindProperty("respawnsAfterDefeat").boolValue = respawns;
            so.FindProperty("juiceColor").colorValue = juiceColor;
            so.FindProperty("hitPoints").intValue = profile.HitPoints;
            so.FindProperty("pointValue").intValue = profile.PointValue;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 固定的な仮の敵(Milestone3)には付けない。ウェーブ制のステージだけ
            // 「近づいてくる」動きを持たせる(オーナー要望、2026-09-06)。
            if (approaches)
            {
                var approach = root.AddComponent<EnemyApproach>();
                var approachSo = new SerializedObject(approach);
                approachSo.FindProperty("approachSpeed").floatValue = profile.ApproachSpeed;
                approachSo.FindProperty("damageRange").floatValue = damageRange;
                approachSo.FindProperty("weaveAmplitude").floatValue = profile.WeaveAmplitude;
                approachSo.FindProperty("weaveFrequency").floatValue = profile.WeaveFrequency;
                approachSo.ApplyModifiedPropertiesWithoutUndo();
            }

            return target;
        }

        /// <summary>
        /// アセットパスからSpriteをロードする。読み込めない場合は白い仮のSpriteにフォールバックする
        /// (画像生成がまだキューで待っている間もシーン自体は組み立てられるようにするため)。
        /// </summary>
        public static Sprite LoadSpriteOrPlaceholder(string assetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath) != null)
            {
                EnsureSpriteImportSettings(assetPath);
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite != null) return sprite;

            Debug.LogWarning($"[EnemyFactory] スプライトが見つかりません({assetPath})。仮の白い四角で代替します。まだ生成中の可能性があります。");
            var texture = Texture2D.whiteTexture;
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// 新しく生成されたPNGは既定でTexture(Default)としてimportされ、Spriteとして
        /// 読み込めない。SpriteかつAlpha透過ありに矯正する。
        /// </summary>
        private static void EnsureSpriteImportSettings(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;
            if (importer.textureType == TextureImporterType.Sprite) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }
    }
}
