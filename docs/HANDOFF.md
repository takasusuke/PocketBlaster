# HANDOFF

セッションを立て直したら、まずここを読む。詳細は[`requirements.md`](requirements.md)。

## 現状（2026-09-05）

マイルストーン1〜3（`docs/requirements.md`§4）を実装済み・自動テスト通過。**実機（iPhone
Safari）からスマホ→PC間の接続自体は動作確認済み。** ドリフトの実測・「撃つ感触」の判断は
実機で試した内容を次回セッションで`docs/requirements.md`未決事項へ反映する。

実機接続までに実際に踏んだ問題と対処（同様の構成を他プロジェクトで作る時の参考）:
1. Windowsファイアウォール（Publicプロファイル）がポート7777への受信を拒否していた
   → `New-NetFirewallRule`で許可ルールを追加（`scripts/check-port-firewall.ps1`参照、
   `~/AIFiles/docs/windows-pitfalls.md`に一般化して記録済み）
2. iOS Safari等はhttp://ではDeviceOrientationEventのセンサー値を一切渡さない（権限
   ダイアログも出ない）→ HTTPS化（`PhoneOrientationServer`をTLS対応に変更）
3. 自己署名証明書への「警告が出たらこのまま進む」操作は、iOS Safariでは警告ページから
   進めず、Chrome/Braveではabout:blankに落ちるだけで、ブラウザによって不安定だった
   → 証明書を構成プロファイルとしてインストールし「常に信頼する」設定にする方式に変更
   （`CertificateDownloadServer`が別ポートで証明書の公開部分を配布）
4. 新しく追加した証明書配布用ポート（7778）にも、7777と同じファイアウォール許可ルールが
   別途必要だった（プログラム単位のブロック解除だけでは足りない）

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
- `Assets/Scripts/Gameplay/TargetHitState.cs`: 「撃たれた敵」の時間経過の状態遷移
  （Idle→Flash→KnockDown→Down→RecoverUp→Idle）をUnityEngine非依存で表現したもの。
- `Assets/Scripts/Gameplay/Target.cs`: 上記を見た目（色フラッシュ・回転での倒れ込み）に
  反映するだけの薄いMonoBehaviour。プリミティブ(Capsule)のプレースホルダー。
- `Assets/Scripts/Audio/ProceduralSfx.cs`: サイン波1音の仮効果音をコード生成するヘルパー。
  正式なSEアセットが無い段階で「撃つ感触」を先に検証するための割り切り
  （`../CLAUDE.md`11「初期実装では画像を作らない」と同じ考え方を音にも適用）。
- `GyroReticleController`の"shoot"処理を拡張: レティクルのスクリーン座標からカメラの
  視線を飛ばして`Target`にレイキャストし、命中/はずれ/弾切れそれぞれで別の仮効果音を鳴らす。
  ステータス欄に「直近の射撃結果」も表示。
- `Assets/Scenes/Milestone3_ShootTarget.unity`: 上記一式（`GyroAimTestRig`+固定の
  `Target_Placeholder`）を配置した検証用シーン（`Milestone3SceneBuilder`の`-executeMethod`
  または「PocketBlaster > Build Milestone3 Scene」で生成）。
- `Assets/Tests/EditMode/`: `PhoneOrientationServerTests`・`AmmoStateTests`・
  `TargetHitStateTests`・`ProceduralSfxTests`で計12件、すべてpass。
  `run-unity.ps1 -ProjectPath . -ExpectOutput TestResults.xml -UnityArgs
  @('-batchmode','-nographics','-runTests','-testPlatform','EditMode','-testResults',
  'TestResults.xml','-logFile','test_run.log')`で再実行できる。
  **レイキャストでのヒット判定自体（UI Toolkit座標→Cameraスクリーン座標のY反転を含む）は
  EditModeテストの対象外** — Physicsが動くにはPlay Modeが要るため、下記の実機検証で
  一緒に確認する。

## 実機での接続手順（確認済み、2026-09-05）

初回だけ証明書のインストールが必要。2回目以降は手順3から。

1. Unity Editorでこのプロジェクトを開き、`Milestone1_GyroAimTest`か
   `Milestone3_ShootTarget`のどちらかのシーンをPlay Modeで実行する
   （後者は固定の敵1体入り。「撃つ感触」を見るならこちら）。
2. **初回のみ**: コンソールに出る証明書ダウンロード用URL（既定 `http://<PCのIP>:7778/`）を
   スマホのブラウザで開き、ダウンロードした証明書を「設定」→「一般」→「VPNとデバイス管理」
   からプロファイルとしてインストール。続けて「設定」→「一般」→「情報」→一番下の
   「証明書信頼設定」で「PocketBlaster Dev Server」を完全に信頼する設定にする。
3. **スマホをPCと同じWi-Fiに繋いだ状態で**、スマホのブラウザで`https://<PCのIP>:7777/`を開く
   （証明書を信頼済みなら警告なしで開けるはず）。
4. 「接続する」をタップ → センサー利用の許可ダイアログが出るので許可する。
5. スマホを動かして、Unity画面上のレティクルが動くか確認する。「撃つ」を6回押すと弾切れに
   なり、それ以上は発射操作が無視される（ステータス欄にその旨が出る）ことを確認する。
6. **リロード＝再キャリブレーションの検証**: リロード後、スマホを完全に静止させたまま
   「前回リロードからの経過時間」が伸びるのを眺め、レティクルが中心から動くかどうかを見る。
   - 動かなければ、この持ち方でのジャイロドリフトは実用上問題ない（設計上の不変条件2が
     成立）と判断してよい。
   - 静止しているのに目に見えて動くようなら、ドリフトが実用に耐えない可能性がある。
     どれくらいの時間でどれくらいずれるかをメモしておくと、`degreesToScreenPixels`の調整や
     リロード頻度（マガジンサイズ）の見直しの判断材料になる。
7. **`Milestone3_ShootTarget`での「撃つ感触」の検証**（体験の核 = docs/requirements.md §2）:
   - レティクルを敵（赤いカプセル）に重ねて「撃つ」→ 白フラッシュ＋倒れ込み＋命中音が
     鳴るか、ステータス欄の「直近の射撃結果」が「命中」になるかを見る。
   - 明後日の方向を狙って「撃つ」→ 「はずれ」になり、敵は反応しないことを見る。
   - **一番大事な判断**: この着弾フィードバック（フラッシュ・倒れ込み・仮の効果音）だけで
     「狙って撃った」という手応えを感じられるか。物足りなければ、次に足すべきは
     見た目のクオリティ（本物のアート・SE）ではなく、フィードバックの種類そのもの
     （画面の揺れ、命中時のポーズ、弾痕など）である可能性が高い — 判断してから次に進む。

**手順5〜7の実際の判断（ジャイロ操作感・ドリフト・撃つ感触）はまだ言語化して記録していない。**
次回のセッションで、実機テストで確認した内容を`docs/requirements.md`未決事項へ反映すること。

うまくいかない場合にまず疑うところ:
- 新しいポートを足した場合、`scripts/check-port-firewall.ps1 -Port <port>`（`~/AIFiles/`）で
  ファイアウォールの許可有無を確認する。
- スマホとPCが本当に同じWi-Fi（同一サブネット）にいるか。
- 証明書の信頼設定が本当に完了しているか（未完了だと警告ページ or about:blankになる）。

## 未検証のまま残っているリスク

- **ジンバルロック**: `DeviceOrientationEvent`のalpha/beta/gammaはオイラー角のため、
  スマホを「銃のように構える」姿勢（beta/gammaが±90°付近になりやすい）だと、値の変化が
  直感と合わなくなる領域が理論上ある。実機で構えてみて明らかに操作が破綻するようなら、
  クォータニオンベースの計算に切り替える必要があるが、現時点では過剰実装を避けて
  様子見にしている。

## 判断が必要な時に見るもの

- 設計判断の基準は`CLAUDE.md`の「立ち返る問い」。
- 未決事項は`docs/requirements.md`§7にまとめてある。着手の過程で決まったら随時更新する。
