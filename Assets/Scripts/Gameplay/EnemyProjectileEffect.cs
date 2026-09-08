using System;
using UnityEngine;

namespace PocketBlaster.Gameplay
{
    /// <summary>
    /// 敵の遠距離攻撃(オーナー要望2026-09-08、EnemyApproach.OnRangedAttackHit参照)の
    /// 投擲物。専用アートは無く、Pickupの円形スプライトと同じ手続き生成の
    /// 小さな球で済ませている(../CLAUDE.md 11「初期実装では画像を作らない」と
    /// 同じ考え方)。飛翔が終わった瞬間にコールバックを呼ぶだけで、ダメージ適用そのものは
    /// 呼び出し側(EnemyApproach)に残す——このクラスは見た目とタイミングだけの責務。
    /// TargetやPickupと同じIShootableとして狙撃対象になり、着弾前に撃ち落とせる
    /// (回避手段が無いままでは理不尽な被弾になるため、対処のしようを持たせた)。
    /// 実体はEnemyProjectileBehaviour(1個ずつ生成して自分で寿命管理する、
    /// ScorePopupEffect/ScorePopupBehaviourと同じ分割)。
    /// </summary>
    public static class EnemyProjectileEffect
    {
        public static void Launch(Vector3 from, Vector3 to, float travelSeconds, Action onArrival)
        {
            var go = new GameObject("EnemyProjectile");
            go.AddComponent<EnemyProjectileBehaviour>().Initialize(from, to, travelSeconds, onArrival);
        }
    }

    internal class EnemyProjectileBehaviour : MonoBehaviour, IShootable
    {
        private static Sprite _cachedSprite;

        private Vector3 _from;
        private Vector3 _to;
        private float _duration;
        private float _elapsed;
        private Action _onArrival;
        private bool _isShotDown;

        /// <summary>撃たれるまでは常にヒット可能。着弾済み(自壊直前)は無いので
        /// falseになるのは撃ち落とされた瞬間だけ。</summary>
        public bool IsHittable => !_isShotDown;

        public void Initialize(Vector3 from, Vector3 to, float travelSeconds, Action onArrival)
        {
            _from = from;
            _to = to;
            _duration = Mathf.Max(travelSeconds, 0.05f);
            _onArrival = onArrival;
            transform.position = from;

            gameObject.AddComponent<Billboard>();
            var spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = GetSprite();
            spriteRenderer.color = new Color(0.55f, 0.85f, 0.25f); // 野菜っぽい緑
            transform.localScale = Vector3.one * 0.3f;

            var collider = gameObject.AddComponent<SphereCollider>();
            collider.radius = 0.5f;
        }

        /// <summary>プレイヤーが着弾前に撃ち落とした場合。ダメージは発生せず消える
        /// (オーナー要望の「撃ち返してくる」に対する対処のしよう、2026-09-08)。</summary>
        public void TakeHit()
        {
            if (_isShotDown) return;
            _isShotDown = true;
            Destroy(gameObject);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(_elapsed / _duration);
            transform.position = Vector3.Lerp(_from, _to, t);

            if (t >= 1f)
            {
                _onArrival?.Invoke();
                Destroy(gameObject);
            }
        }

        private static Sprite GetSprite()
        {
            if (_cachedSprite != null) return _cachedSprite;

            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var center = new Vector2(size / 2f, size / 2f);
            var radius = size / 2f - 2f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var alpha = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) <= radius ? 1f : 0f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            texture.Apply();

            _cachedSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _cachedSprite;
        }
    }
}
