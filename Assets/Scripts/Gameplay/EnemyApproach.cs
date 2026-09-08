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
    ///
    /// 遠距離攻撃(オーナー要望2026-09-08:「同様のアーケードゲームと比べて機能や
    /// UIやUXで足りていない部分を...実装する」——House of the Dead等では近づかれる前
    /// から撃ち返してくるが、こちらは「近づかれ過ぎた」の一種類しか脅威が無く単調
    /// だった)。`enableRangedAttack`をオンにした種類(EnemyFactoryのVegetableProfile
    /// 参照——足の遅いオニオン・ボスに付与し、遅さを遠距離攻撃で補う設計)は、
    /// 接触範囲より外側にいる間も一定間隔でプレイヤーへ投げつけてくる。
    /// </summary>
    [RequireComponent(typeof(Target))]
    public class EnemyApproach : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private float approachSpeed = 0.6f;
        [SerializeField] private float damageRange = 1.5f;
        [SerializeField] private float weaveAmplitude = 0f;
        [SerializeField] private float weaveFrequency = 0f;

        [SerializeField] private bool enableRangedAttack = false;
        [SerializeField] private float rangedAttackIntervalSeconds = 3f;
        [SerializeField] private float rangedAttackChance = 0.5f;
        [SerializeField] private int rangedAttackDamage = 10;
        [SerializeField] private float rangedAttackProjectileSpeed = 6f;

        /// <summary>近づき過ぎて退場した瞬間に1回だけ呼ばれる。引数は自分自身。</summary>
        public event Action<Target> OnReachedPlayer;

        /// <summary>遠距離攻撃がプレイヤーに命中した瞬間に呼ばれる(投擲物の飛翔後、
        /// 着弾のタイミング)。引数はダメージ量。近づかれ過ぎた場合とは別のダメージ源
        /// として扱う(StageDirector/MultiplayerStageDirector参照)。</summary>
        public event Action<int> OnRangedAttackHit;

        private Target _target;
        private bool _hasReachedPlayer;
        private GyroReticleController _readyGate;
        private Vector3 _basePosition;
        private float _rangedAttackTimer;

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

                if (enableRangedAttack) UpdateRangedAttack();
            }

            if (Vector3.Distance(_basePosition, player.position) <= damageRange)
            {
                _hasReachedPlayer = true;
                gameObject.SetActive(false);
                OnReachedPlayer?.Invoke(_target);
            }
        }

        private void UpdateRangedAttack()
        {
            _rangedAttackTimer += Time.deltaTime;
            if (_rangedAttackTimer < rangedAttackIntervalSeconds) return;
            _rangedAttackTimer = 0f;

            // 接触範囲より内側にいる時は近づかれ過ぎたダメージだけで十分なので
            // 遠距離攻撃は撃たない(同時に二重でダメージが入るのを避ける)。
            if (Vector3.Distance(_basePosition, player.position) <= damageRange) return;
            if (UnityEngine.Random.value > rangedAttackChance) return;

            var from = transform.position + Vector3.up * 0.3f;
            var to = player.position;
            var travelSeconds = Vector3.Distance(from, to) / Mathf.Max(rangedAttackProjectileSpeed, 0.01f);
            EnemyProjectileEffect.Launch(from, to, travelSeconds, () => OnRangedAttackHit?.Invoke(rangedAttackDamage));
        }
    }
}
