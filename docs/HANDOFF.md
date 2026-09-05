# HANDOFF

セッションを立て直したら、まずここを読む。詳細は[`requirements.md`](requirements.md)。

## 現状（2026-09-05）

要件定義フェーズ。`docs/requirements.md`を作成した段階で、Unityプロジェクト自体はまだ
作成していない（このディレクトリにはドキュメントのみ存在）。

## 次にやること

1. Unityプロジェクト本体をこのリポジトリ直下に作成する（3Dテンプレート）。
2. マイルストーン1（`docs/requirements.md`§4）: スマホブラウザ→PC間のWebSocket通信で
   ジャイロ値を飛ばし、PC側で3D空間内の照準を動かせることを検証する最小構成を作る。
   - スマホ側: 単一HTMLページ + `DeviceOrientationEvent`。まずAndroid実機で確認し、
     iOS Safariの許可ダイアログ挙動は別途検証する（未決事項#4）。
   - PC側: Unity内にWebSocketサーバーを立てるか、軽量な仲介サーバーを別プロセスで
     立てるかは未決定。着手時に決めてよい（判断が割れる場合のみ報告）。

## 判断が必要な時に見るもの

- 設計判断の基準は`CLAUDE.md`の「立ち返る問い」。
- 未決事項は`docs/requirements.md`§7にまとめてある。着手の過程で決まったら随時更新する。
