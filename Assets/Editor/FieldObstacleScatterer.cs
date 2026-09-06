using UnityEngine;

namespace PocketBlaster.EditorTools
{
    /// <summary>
    /// 自由に歩き回れるようになったフィールド(PlayerLocomotion.maxOffsetRadius=180m、
    /// オーナー要望2026-09-06:「移動できる範囲は6倍にしてください」)を埋めるための
    /// 追加オブジェクト配置(オーナー要望、同日:「他にもオブジェクトを配置してください」
    /// 「パルクールなども実装してください」)。
    ///
    /// 2種類を用意する:
    /// - <see cref="Scatter"/>: 低い箱(乗り越え)・高い壁(通行不可)・足場(常に登れる)を
    ///   固定シードで(=シーンを再ビルドしても毎回同じ配置になるよう)円環状にばら撒く。
    ///   ウェーブの敵配置・アイテム出現エリア(原点付近〜奥行き30m程度)とは重ならない
    ///   よう、内側の安全半径より外側だけに撒く。
    /// - <see cref="BuildParkourStaircase"/>: 高さが段々に上がる足場を隣接させて並べた、
    ///   実際に登っていける一本道のパルクール構造物。プレイヤー出発点の近くに置き、
    ///   まず触ってみてもらいやすくする。落下ダメージ(FallDamageCalculator参照)を
    ///   実際に試せる高さ(4m)まで登れる。
    /// </summary>
    public static class FieldObstacleScatterer
    {
        private static readonly Color LowBoxColor = new Color(0.6f, 0.45f, 0.3f);
        private static readonly Color WallColor = new Color(0.4f, 0.4f, 0.45f);
        private static readonly Color PlatformColor = new Color(0.5f, 0.5f, 0.55f);

        public static void Scatter(string namePrefix, int seed, float innerRadius, float outerRadius, int count)
        {
            var previousState = Random.state;
            Random.InitState(seed);

            var platformHeights = new[] { 1f, 2f, 3f, 4f };
            for (var i = 0; i < count; i++)
            {
                var angle = Random.Range(0f, Mathf.PI * 2f);
                var radius = Random.Range(innerRadius, outerRadius);
                var position = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                switch (i % 3)
                {
                    case 0:
                        ObstacleFactory.CreateBox($"{namePrefix}_LowBox_{i}", position, 0.7f, 0.4f, LowBoxColor);
                        break;
                    case 1:
                        ObstacleFactory.CreateBox($"{namePrefix}_Wall_{i}", position, 0.6f, 1.8f, WallColor);
                        break;
                    default:
                        var height = platformHeights[i % platformHeights.Length];
                        ObstacleFactory.CreateBox($"{namePrefix}_Platform_{i}", position, 1.2f, height, PlatformColor, isPlatform: true);
                        break;
                }
            }

            Random.state = previousState;
        }

        /// <summary>
        /// 高さ1m→2m→3m→4mの足場を一直線に隣接させる。各足場の円判定(半径1.4m)が
        /// 隣とわずかに重なるくらいの間隔(2.2m)にしてあり、歩いて渡るときに
        /// 途切れなく次の段の判定に入れる(Obstacle.IsPlatformはstepUpHeightを
        /// 無視するため、段差の大きさに関わらず登れる)。
        /// </summary>
        public static void BuildParkourStaircase(string namePrefix, Vector3 basePosition, Vector3 direction)
        {
            var heights = new[] { 1f, 2f, 3f, 4f };
            const float spacing = 2.2f;
            const float radius = 1.4f;
            var forward = direction.normalized;

            for (var i = 0; i < heights.Length; i++)
            {
                var position = basePosition + forward * (spacing * i);
                ObstacleFactory.CreateBox($"{namePrefix}_Step{i + 1}", position, radius, heights[i], PlatformColor, isPlatform: true);
            }
        }
    }
}
