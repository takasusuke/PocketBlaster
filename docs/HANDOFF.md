# HANDOFF

セッションを立て直したら、まずここを読む。詳細は[`requirements.md`](requirements.md)。

## 現状（2026-09-05）

マイルストーン1・2（`docs/requirements.md`§4）を実装済み・自動テスト通過。**ただし実機での
動作確認・ドリフトの実測はまだ誰も行っていない。**

- Unity `6000.3.21f1`（`../StoneKnights`/`../FeelFreeFlying`と揃えた）でプロジェクト作成済み。
- `Assets/Scripts/Networking/PhoneOrientationServer.cs`: 外部ライブラリ無しの最小HTTP/
  WebSocketサーバー（`TcpListener`ベース、RFC6455ハンドシェイクとフレーム解読を自前実装）。
  `webapp/index.html`を静的配信しつつ、同じポートでWebSocketアップグレードも受ける。
- `Assets/Scripts/Networking/PhoneControllerServer.cs`: 上記をラップするMonoBehaviour。
  起動時にこのPCのIPv4アドレス一覧をログに出す。"orientation"/"reload"/"shoot"の3種類の
  メッセージをイベントとして流す。
- `Assets/Scripts/Aim/AmmoState.cs`: 弾数の状態機械（UnityEngine非依存）。マガジンサイズ既定6発。
- `Assets/Scripts/Aim/GyroReticleController.cs`: ジャイロの基準からの差分をUI Toolkitの
  レティクル（実行時に生成、PanelSettingsもコード生成でアセット不要）へ反映。"shoot"で
  `AmmoState.Shoot()`を呼び、弾切れなら発射を無視。"reload"で弾を補充しつつ
  `Recenter()`（設計上の不変条件2そのもの）を実行。ステータス表示に残弾数と
  「前回リロードからの経過時間」を出している。
- `webapp/index.html`: スマホ側の単一HTMLページ。iOS Safariの`requestPermission()`呼び出し・
  `deviceorientation`の間引き送信（~20Hz）・「撃つ」「リロード」ボタンを実装。
- `Assets/Scenes/Milestone1_GyroAimTest.unity`: 上記を配置した検証用シーン
  （`Assets/Editor/Milestone1SceneBuilder.cs`の`-executeMethod`で生成。手でも
  「PocketBlaster > Build Milestone1 Scene」メニューから再生成できる。マイルストーン2の
  変更もこのシーンのコンポーネントに乗るので、シーン自体は再生成不要）。
- `Assets/Tests/EditMode/`: `PhoneOrientationServerTests`（WSハンドシェイク・フレーム解読・
  静的HTML配信・"shoot"のルーティング）と`AmmoStateTests`（弾数の状態機械）で計6件、
  すべてpass。`run-unity.ps1 -ProjectPath . -ExpectOutput TestResults.xml -UnityArgs
  @('-batchmode','-nographics','-runTests','-testPlatform','EditMode','-testResults',
  'TestResults.xml','-logFile','test_run.log')`で再実行できる。

## 次にやること — 実機での検証（まだ誰もやっていない）

1. Unity Editorでこのプロジェクトを開き、`Milestone1_GyroAimTest`シーンをPlay Modeで実行する。
2. コンソールに出るこのPCのIPアドレスとポート（既定7777）を確認する。
3. **スマホをPCと同じWi-Fiに繋いだ状態で**、スマホのブラウザで`http://<PCのIP>:7777/`を開く。
4. 「接続する」をタップ → iOSなら許可ダイアログが出るはず（**ここが最大の未検証ポイント**。
   未決事項#4）。
5. スマホを動かして、Unity画面上のレティクルが動くか確認する。「撃つ」を6回押すと弾切れに
   なり、それ以上は発射操作が無視される（ステータス欄にその旨が出る）ことを確認する。
6. **リロード＝再キャリブレーションの検証**: リロード後、スマホを完全に静止させたまま
   「前回リロードからの経過時間」が伸びるのを眺め、レティクルが中心から動くかどうかを見る。
   - 動かなければ、この持ち方でのジャイロドリフトは実用上問題ない（設計上の不変条件2が
     成立）と判断してよい。
   - 静止しているのに目に見えて動くようなら、ドリフトが実用に耐えない可能性がある。
     どれくらいの時間でどれくらいずれるかをメモしておくと、`degreesToScreenPixels`の調整や
     リロード頻度（マガジンサイズ）の見直しの判断材料になる。

うまくいかない場合にまず疑うところ:
- PCのファイアウォールが7777番ポートへのインバウンド接続をブロックしていないか。
- スマホとPCが本当に同じWi-Fi（同一サブネット）にいるか。

## 未検証のまま残っているリスク

- **ジンバルロック**: `DeviceOrientationEvent`のalpha/beta/gammaはオイラー角のため、
  スマホを「銃のように構える」姿勢（beta/gammaが±90°付近になりやすい）だと、値の変化が
  直感と合わなくなる領域が理論上ある。実機で構えてみて明らかに操作が破綻するようなら、
  クォータニオンベースの計算に切り替える必要があるが、現時点では過剰実装を避けて
  様子見にしている。

## 判断が必要な時に見るもの

- 設計判断の基準は`CLAUDE.md`の「立ち返る問い」。
- 未決事項は`docs/requirements.md`§7にまとめてある。着手の過程で決まったら随時更新する。
