using System;
using PocketBlaster.Aim;
using UnityEngine;

namespace PocketBlaster.Gameplay
{
    /// <summary>
    /// 「敵が一定以上近づいてきたらダメージを受ける」(オーナー要望、2026-09-06)。
    /// それまで固定だった敵(Target)にプレイヤーへ向かってゆっくり近づく動きを足す。
    /// 一定距離(damageRange)まで近づくと、TakeHit()の被弾演出は経ずにそのまま退場し
    /// (撃たれて倒したのではなく取り逃がした、という区別のため)、OnReachedPlayerを発火する。
    ///
    /// このイベントはStageDirectorが受け取ってウェーブ進行(得点なしで退場扱い)に反映し、
    /// StageDirector.OnEnemyReachedPlayerとして中継されたものをGameSessionが受け取って
    /// 難易度モード(アーケード)の残機を減らす — GameSession/StageDirector参照。
    ///
    /// Milestone3の固定的な仮の敵1体(繰り返し試すためのもの)には付けない。ウェーブ制の
    /// ステージ(Milestone4_Stage・Stage2_BossRush)の敵にだけ、EnemyFactoryの
    /// `approaches: true`で付与する。
    ///
    /// スマホが未接続・未キャリブレーションの間は接近を止める(オーナー要望、2026-09-06:
    /// 「シーンが始まっても、スマホ側が接続してアクションを取るまで敵の接近は開始しない
    /// ようにしてください」)。「アクションを取る」の判定には
    /// <see cref="GyroReticleController.IsCalibrated"/>を使う — 接続してリロード操作を
    /// 済ませて初めてtrueになるため、「接続済み」かつ「実際に操作した」の両方を1つの
    /// フラグでまとめて表せる。
    ///
    /// 左右に避けながら接近する「蛇行」に対応する(オーナー要望、2026-09-06:「移動方法
    /// （左右によけながら移動するなど）を定義して」、EnemyFactoryのVegetableProfile参照)。
    /// 直進の基準位置(<see cref="_basePosition"/>)をまっすぐ進め、実際の見た目の位置は
    /// そこから進行方向に垂直な向きへサイン波で振らせる — 判定(damageRange)は蛇行前の
    /// 基準位置で行うため、見た目のブレで到達判定がバタつくことはない。
    /// </summary>
    [RequireComponent(typeof(Target))]
    public class EnemyApproach : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private float approachSpeed = 0.6f;
        [SerializeField] private float damageRange = 1.5f;
        [SerializeField] private float weaveAmplitude = 0f;
        [SerializeField] private float weaveFrequency = 0f;

        /// <summary>近づき過ぎて退場した瞬間に1回だけ呼ばれる。引数は自分自身。</summary>
        public event Action<Target> OnReachedPlayer;

        private Target _target;
        private bool _hasReachedPlayer;
        private GyroReticleController _readyGate;
        private Vector3 _basePosition;

        private void Awake()
        {
            _target = GetComponent<Target>();
            if (player == null)
            {
                var cam = Camera.main;
                if (cam != null) player = cam.transform;
            }
            _readyGate = FindFirstObjectByType<GyroReticleController>();
            _basePosition = transform.position;
        }

        private void Update()
        {
            if (_hasReachedPlayer || player == null || !_target.IsAlive) return;
            if (_readyGate != null && !_readyGate.IsCalibrated) return;

            // 被弾直後(Flash/KnockDown等)は一瞬立ち止まる — 撃たれた敵がそのまま
            // 滑るように前進し続けると不自然なため。IsHittable(=Idle)の間だけ進む。
            if (_target.IsHittable)
            {
                _basePosition = Vector3.MoveTowards(_basePosition, player.position, approachSpeed * Time.deltaTime);

                if (weaveAmplitude > 0f)
                {
                    var forward = (player.position - _basePosition).normalized;
                    var lateral = Vector3.Cross(Vector3.up, forward);
                    var offset = lateral * (Mathf.Sin(Time.time * weaveFrequency * Mathf.PI * 2f) * weaveAmplitude);
                    transform.position = _basePosition + offset;
                }
                else
                {
                    transform.position = _basePosition;
                }
            }

            if (Vector3.Distance(_basePosition, player.position) <= damageRange)
            {
                _hasReachedPlayer = true;
                gameObject.SetActive(false);
                OnReachedPlayer?.Invoke(_target);
            }
        }
    }
}
