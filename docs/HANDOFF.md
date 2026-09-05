# HANDOFF

セッションを立て直したら、まずここを読む。詳細は[`requirements.md`](requirements.md)。

## 現状（2026-09-05）

マイルストーン1（`docs/requirements.md`§4）を実装済み・自動テスト通過。**ただし実機での
動作確認はまだ行っていない。**

- Unity `6000.3.21f1`（`../StoneKnights`/`../FeelFreeFlying`と揃えた）でプロジェクト作成済み。
- `Assets/Scripts/Networking/PhoneOrientationServer.cs`: 外部ライブラリ無しの最小HTTP/
  WebSocketサーバー（`TcpListener`ベース、RFC6455ハンドシェイクとフレーム解読を自前実装）。
  `webapp/index.html`を静的配信しつつ、同じポートでWebSocketアップグレードも受ける。
- `Assets/Scripts/Networking/PhoneControllerServer.cs`: 上記をラップするMonoBehaviour。
  起動時にこのPCのIPv4アドレス一覧をログに出す。
- `Assets/Scripts/Aim/GyroReticleController.cs`: ジャイロの基準からの差分をUI Toolkitの
  レティクル（実行時に生成、PanelSettingsもコード生成でアセット不要）へ反映。"reload"
  メッセージでの再キャリブレーション配線（`Recenter()`）も先に用意済み（本格的な
  リロードのゲームプレイ自体はマイルストーン2）。
- `webapp/index.html`: スマホ側の単一HTMLページ。iOS Safariの`requestPermission()`呼び出し・
  `deviceorientation`の間引き送信（~20Hz）・リロードボタンを実装。
- `Assets/Scenes/Milestone1_GyroAimTest.unity`: 上記を配置した検証用シーン
  （`Assets/Editor/Milestone1SceneBuilder.cs`の`-executeMethod`で生成。手でも
  「PocketBlaster > Build Milestone1 Scene」メニューから再生成できる）。
- `Assets/Tests/EditMode/PhoneOrientationServerTests.cs`: 実機の代わりにTCPクライアントで
  WSハンドシェイク・マスク付きフレーム送信・静的HTML配信を検証する自動テスト（2/2 pass）。
  `run-unity.ps1 -ProjectPath . -ExpectOutput TestResults.xml -UnityArgs @('-batchmode',
  '-nographics','-runTests','-testPlatform','EditMode','-testResults','TestResults.xml',
  '-logFile','test_run.log')`で再実行できる。

## 次にやること — 実機での検証（まだ誰もやっていない）

1. Unity Editorでこのプロジェクトを開き、`Milestone1_GyroAimTest`シーンをPlay Modeで実行する。
2. コンソールに出るこのPCのIPアドレスとポート（既定7777）を確認する。
3. **スマホをPCと同じWi-Fiに繋いだ状態で**、スマホのブラウザで`http://<PCのIP>:7777/`を開く。
4. 「接続する」をタップ → iOSなら許可ダイアログが出るはず（**ここが最大の未検証ポイント**。
   未決事項#4）。
5. スマホを動かして、Unity画面上のレティクルが動くか確認する。「リロード」ボタンで
   基準位置がリセットされるかも確認する。

うまくいかない場合にまず疑うところ:
- PCのファイアウォールが7777番ポートへのインバウンド接続をブロックしていないか。
- スマホとPCが本当に同じWi-Fi（同一サブネット）にいるか。

## 判断が必要な時に見るもの

- 設計判断の基準は`CLAUDE.md`の「立ち返る問い」。
- 未決事項は`docs/requirements.md`§7にまとめてある。着手の過程で決まったら随時更新する。
