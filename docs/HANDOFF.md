# HANDOFF

セッションを立て直したら、まずここを読む。詳細は[`requirements.md`](requirements.md)。

**Unity EditorのGUIを起動する時、開くべきシーンは`PendingSceneOpener`
(`Assets/Editor/PendingSceneOpener.cs`)経由で指定する**（オーナー要望、2026-09-06:
「Unityで開くべきシーンは、デフォルトシーンに随時設定するようにして」）。
プロジェクト直下にマーカーファイル`.pending-scene-to-open.txt`（`.gitignore`済み）を
書いてから起動すると、Editor自身が`[InitializeOnLoad]`で起動時にそれを読んで
該当シーンを開き、マーカーは消費される。

```powershell
Set-Content -Path "<projectPath>\.pending-scene-to-open.txt" -Value "Assets/Scenes/<シーン名>.unity" -Encoding utf8 -NoNewline
Start-Process -FilePath "<Unity.exeのパス>" -ArgumentList @('-projectPath', '<projectPath>')
```

Editor.logの`[PendingSceneOpener] マーカーに従ってシーンを開きました: ...`で
実際に開けたか確認できる（`grep -n PendingSceneOpener` で探す）。

**効かなかった方法（2026-09-06、どちらも実機で確認して却下）**:
1. バッチモードで`EditorSceneManager.OpenScene()`してから`-quit`
   （`SceneLauncher.cs`として実装したが削除済み）→ 次回GUI起動時の
   「最後に開いていたシーン」に反映されない（ウィンドウレイアウトの一部として
   GUIセッションでしか保存されない模様）。
2. GUI起動コマンドの引数にシーンファイルパスを直接渡す（`Unity.exe -projectPath <p>
   <シーンのフルパス>`）→ これも反映されず、Untitled Sceneが開いた。
   （「ダブルクリックでシーンが開く」という一般的な理解は、少なくともこの起動経路
   （`Start-Process`での直接起動）には当てはまらなかった）

## 自由移動の最終決定・体力ゲージ・反動/ヘッドショット・落下ダメージ（2026-09-06）

- **「オンレール式」を完全に撤回（オーナー指示「自由に歩き回るようにします」）**:
  `StageDirector`はもうウェーブ間でプレイヤーの位置・向き・カメラを動かさない
  （`MoveCameraTo`コルーチンごと削除）。`PlayerLocomotion.maxOffsetRadius`を
  9m→30mに拡大。この撤回に伴い、両者の間の古い`playerLocomotion`参照配線
  （`StageDirector`・`Milestone4SceneBuilder`・`Stage2SceneBuilder`）を削除した。
  **未検証**: 30mという上限・床の実際の広さが体感としてちょうどいいか。
- **「はずれ＝残機減少」を却下（オーナー指示）**: `GameSession`はもう
  `GyroReticleController.OnShotResolved`を購読しない。フェール条件は「敵に近づかれ
  過ぎた」と「高すぎる場所からの落下」の2つだけになった。
- **残機(`LivesState`)→HPゲージ(`PlayerHealthState`)へ移行（オーナー要望「体力ゲージも
  UIとして実装してください」）**: 最大100の整数HP。敵接触ダメージ30・回復アイテム30回復
  （どちらも仮の値）。アーケードモードのみ画面下に体力バー（track+fill構成）を表示、
  カジュアルモードでは非表示。`LivesState.cs`/`LivesStateTests.cs`は削除した。
  **未検証**: ダメージ・回復量のバランス、バーの見た目。
- **落下ダメージ（オーナー要望「あまりに高いところから飛び降りる場合にはダメージが
  入るように」）**: `Obstacle.IsPlatform`（新規、`stepUpHeight`を無視して常に登れる
  足場）、`PlayerLocomotion`が直前の高さとの差分を追跡して`OnFallDamage`イベントを
  発火、`FallDamageCalculator`(安全高1.5m・20ダメージ/m、どちらも仮)がUnity非依存で
  ダメージ量を計算する。各ステージに試作の足場(`Obstacle_Platform`、高さ3m)を
  1つだけ配置済み。**未検証**: 実際に飛び降りてみての閾値・ダメージ量の調整。
- **反動・ヘッドショット等の部位別ダメージ（オーナー要望「まずは反動と、その反動
  コントロール要素として、敵のヘッドショットなど部位別のダメージ量変化を実装して
  もらって試したい」——ジャイロの操作感自体については「撃っていて楽しい、狙う感覚が
  いい感じ」と肯定的な評価をもらえたので、その先の掘り下げとして着手）**:
  - `GyroReticleController`: 発砲のたびにレティクルを上へ弾き、時間経過で戻す
    （`recoilKickPixels`/`recoilRecoverySpeedPixelsPerSecond`）。連射するほど
    自分で反動を抑える必要が生まれる設計。
  - `HeadHitbox`(新規)を各敵の見た目の上部20〜25%あたりに配置(`EnemyFactory`が生成、
    `SerializedObject`で明示的に`target`を配線 — Editor/バッチモードでの生成は
    `Awake()`が確実に走るとは限らないため自動解決に頼らなかった)。
  - `TryHitTargetAtReticle`を`Physics.RaycastAll`+距離ソートへ書き換え、頭部命中を
    優先判定してから通常の`IShootable`判定にフォールバックする。
  - ヘッドショットは`TargetHitState.TryHit(isCritical: true)`で残り被弾可能回数を
    無視して即座に倒し、`StageDirector`が撃破得点を2倍にする。
  - **未検証**: 反動の強さ、ヘッドショット判定域の大きさ・位置、いずれも実機での調整待ち。
- **確認方法**: EditMode 62件全て通過（Bash経由で`heavy_lock.py`がUnity.exeを直接
  ラップする形で実行——後述のPowerShellハングが今回も続いていたため）。
  `Milestone3_ShootTarget`/`Milestone4_Stage`/`Stage2_BossRush`の3シーンを
  `-executeMethod`で再ビルドし、`HeadHitbox`・`isPlatform`・`maxHealth`が
  意図通りシーンに反映されていることをYAML上で確認した。**実機(Play Mode)での
  動作確認はまだ行っていない**——上記の「未検証」項目はすべてこれに該当する。
  再ビルド前に、このプロジェクトを開いたままのGUI Editor(PID 16204)が1つ残っていて
  バッチ実行をブロックしていたため、`taskkill`で閉じてから実行した
  （このセッションの最後にUnity Editorを`Title.unity`で開き直す）。

## クラッシュ修正・上下反転の食い違い修正・視線カーソル・移動速度・床グリッド（2026-09-06）

- **NullReferenceExceptionの修正（オーナー報告）**: `StageDirector.Awake()`が
  `StartNextWave()`経由で`PlayerLocomotion.ResetForNewWave()`を呼ぶが、Unityは
  異なるコンポーネント間の`Awake()`実行順序を保証しないため、`PlayerLocomotion`
  自身の`Awake()`がまだ実行されておらず`_offsetState`が未初期化のまま
  `NullReferenceException`になっていた。`EnsureInitialized()`にまとめ、
  `Awake()`からも`ResetForNewWave()`からも安全に呼べるようにした(二重初期化防止つき)。
- **上下反転の食い違いを修正（オーナー報告「構えているときの上下左右の反転の設定が、
  構えていない時に反映されていないように思えます」）**: webapp側の反転設定自体は
  正しく両方の入力に効いていたが、`PlayerLocomotion`の視界回転(ピッチ)の符号が
  `GyroReticleController`の照準マッピングと逆になっていた(`_lookPitch - pitchInput...`
  になっていた箇所を`+`に修正)。同じ持ち方・同じ傾け方なのに、構えている時と
  構えていない時とで上下が逆に感じられていた真因はこれ。
- **構えていない時の視線カーソル（オーナー要望「構えていない時も、向いている方向を
  示すためにカーソルを表示してほしいです」）**: `GyroReticleController`が、構えて
  いない間はレティクルを画面中央に固定して表示するようにした。構えていない間は
  `PlayerLocomotion`がカメラ自体を回転させる設計のため、画面中央＝現在向いている
  方向と一致する。狙い(赤)と見た目で区別できるよう水色に変える。
- **移動速度を引き上げ（オーナー要望「プレイヤーの移動速度を速めてください」）**:
  `PlayerLocomotion.stepDistance`を0.3→0.7に引き上げた。
- **床にグリッドを追加（オーナー要望「移動している量が分かるように床にグリッドなど
  をつけてほしいです」）**: `GroundFactory`(新規、Editor)が手続き生成した格子模様の
  テクスチャを貼った床(Planeプリミティブ)を`Milestone4_Stage`・`Stage2_BossRush`
  それぞれのステージ全体をカバーするサイズで敷いた。
- **作業メモ（この日の後半、Windows PowerShellが応答不能になった）**: 原因不明の
  理由でこのマシンの`powershell.exe`自体が(このセッションのツール経由でも、Bashから
  直接起動しても)ハングするようになり、通常使っている`scripts/run-unity.ps1`
  経由のUnityバッチ実行ができなくなった。`scripts/heavy_lock.py`はPythonスクリプト
  なのでBash経由で直接呼び出せることを確認し、`python heavy_lock.py -- "<Unity.exeの
  パス>" -batchmode ...`の形でロック付きのUnityバッチ実行を代替した(この後の
  検証はすべてこの方法で行った)。`run-unity.ps1`が持つ追加の安全機構(隣接プロジェクトの
  Unity検知・出力の再取得リトライ等)は今回使えていない点に注意——次回セッションで
  PowerShellが復旧しているか確認すること。

## 撃てないバグの再修正・感度分離・視界回転への設計変更（2026-09-06）

- **バグ再修正（オーナー報告「構えたボタンは長押しにしたままで、撃つボタンを押す
  アクションを取っていますが撃てません」）**: 前回の`setPointerCapture`修正では
  直らなかった。真因は別にあった——「撃つ」は必ず「構える」を押しっぱなしの状態で
  行うことになった結果、**あらゆる射撃テストが必然的に2本指同時タッチになった**。
  `shootBtn`等は`'click'`イベント（ブラウザが生成する合成イベント）で拾っていたが、
  別の指が別要素に触れたままだと、モバイルSafari等では合成が遅延・抑制されることが
  ある。`shootBtn`・`reloadBtn`・`pauseBtn`・`retryBtn`を全て`'pointerdown'`
  （合成を待たずその場で発火）に統一して解消を図った。**未検証**: 実機での再確認待ち。
- **感度をさらに分離**（オーナー要望「構えるときの感度と、構えていない時の感度は
  それぞれ調整できるようにして下さい」）: `GameSettingsState`に`LookSensitivity`
  （新規、4-24ではなく20-120の別レンジ——単位が「角度→ピクセル」ではなく
  「角度→回転の角速度」で意味が違うため）を追加。起動画面に3本目のスライダー
  「感度（構えない・視界回転）」を追加し、既存2本も「感度（構える・上下/左右）」に
  改名して区別を明確にした。
- **「構えていない間」の設計変更**（オーナー要望「構えていない時は、視界が回転する
  イメージです」）: 前回実装した「傾き→平行移動(アナログスティック)」を撤回し、
  「傾き→視界回転(角速度)」に置き換えた。`PlayerLocomotion`が構えを解いた瞬間の
  傾きを基準に、そこからの傾き角度に比例した速さで`movableRoot`のローカル回転
  （左右=yaw、前後=pitch、見上げ/見下ろしは±60度に制限）を継続的に変化させる。
  **実際の歩行は引き続き足踏み("step")が担当**——構えている間は狙っている方向、
  構えていない間は視界回転後の現在向いている方向へ進む(`HandleStep`)。
  - ウェーブが切り替わる時、前のウェーブで振り向いた向き・動いた位置を持ち越すと
    次のウェーブで敵が正面ではなく横に見える等の混乱を招くため、`StageDirector`が
    ウェーブ開始のたびに`PlayerLocomotion.ResetForNewWave()`を呼んで視界回転・
    移動オフセットを両方リセットするようにした。
  - **未検証**: 「傾けた分だけ視界が回り続ける」感触が実際に直感的か、
    角速度の基準値(60度/秒)・不感帯・見上げ/見下ろしの可動域(±60度)が
    適切かは実機確認が必要。

## 「構える」ボタンのバグ修正・フィールド障害物とパルクール（2026-09-06、実装済み・未検証）

- **バグ修正（オーナー報告「構えている間に弾を撃つを押しても、弾が出ません」）**:
  原因は`webapp/index.html`の「構える」ボタンが`pointerleave`でも構えを解除して
  いたこと。スマホを傾けながら押し続けると、指が画面上でボタンの外へわずかに
  ずれて`pointerleave`が発火し、「撃つ」を押す前に構えが解除されていた。
  `setPointerCapture()`でこの指の入力をボタンへ固定し、要素の外に出ても
  `pointerup`/`pointercancel`まで構え続けるように修正した(`pointerleave`の
  購読は削除)。Unity側の照準/発射ロジック自体にバグは無かった。
- **フィールドの障害物・パルクール**（オーナー要望「フィールドの構築が必要です。
  オブジェクトを配置したり、小さなオブジェクトに対して...パルクールをして
  上ったり」への第一段階の対応、「着手して。そのあと調整します」を受けて着手）:
  - `Obstacle`(新規)を追加。物理エンジン(CharacterController等)には頼らない
    割り切った実装で、水平距離だけで「重なっているか」を判定する。
  - `PlayerLocomotion`が移動先に障害物が重なっていないか確認してから位置を
    確定するようにした(`ObstacleCrossing.Evaluate`、EditModeテストあり)。
    障害物の高さが`stepUpHeight`(既定0.6m)以下なら自動的に「乗り越え」(見た目の
    高さをその場だけ上げる)、それより高ければ移動そのものをブロックする。
  - 見た目は正式なアートが無いため、`ObstacleFactory`(Editor)が組み込みの
    箱プリミティブで生成する(色分けのみ)。`CreatePrimitive`が付けるBoxColliderは
    そのまま残しているので、射撃のレイキャストも自然にブロックされる
    (障害物の向こうの敵に弾が当たらない)。
  - `PlayerLocomotion.maxOffsetRadius`を2.5m→9mに拡大し、実際に「歩き回る」と
    呼べる範囲にした。
  - `Milestone4_Stage`・`Stage2_BossRush`それぞれに低い箱(乗り越えられる)3個・
    高い壁(通れない)1個を試作として配置した(最初の3ウェーブぶんのみ)。
  - **今回のスコープ外**: 敵(EnemyApproach)は障害物を避けたり止まったりせず、
    直線移動のまま視覚的に障害物を貫通しうる(物理・パスファインディングは
    未実装)。障害物の配置・数・サイズは仮のもので、実際に遊んでから調整が必要
    (オーナーの想定通り)。フィールド自体の形状(壁・段差の配置パターン)も
    まだ「試作」の域を出ていない。

## 「構える」ボタン — 照準と移動の入力切り替え（2026-09-06、実装済み・未検証）

「プレイヤーがマップ内を動き回って、敵がプレイヤーの位置に近づいてくるような遊び方に
してください」という大きな要望に対して、まず「スマホの傾きは1系統しかないのに、
狙いと移動のどちらに使うか」という入力面の矛盾をどう解決するかを確認したところ、
オーナーから「『構える』ボタンを新たに配置して、構えている間は照準を動かして、
構えていない間は移動する」という具体的な設計指示があり、そのまま実装した。

- `PhoneControllerServer.IsAiming`（新規）: スマホ側の「構える」ボタンを押している間
  だけtrue。webapp/index.htmlに新規追加した「構える」ボタンは押している間だけ
  `"aim_start"`、離したら`"aim_end"`を送る(pointerdownイベント系でマウス/タッチ両対応)。
- `GyroReticleController`: `IsAiming`(またはマウスデバッグ)の間だけ傾きをレティクルに
  反映し、「撃つ」も有効になる。構えていない間はレティクルを最後の位置で凍結し、
  半透明にして「今は狙えない」ことを示す。
- `PlayerLocomotion`: 構えていない間、傾き(構えを解いた瞬間を基準にした前後=beta・
  左右=gamma)をアナログスティックのように使い、カメラの向き基準で連続的に移動する
  (不感帯5度・最大30度)。足踏み("step")での小さな踏み込みは、構えている間だけ
  引き続き有効(狙いを定めたまま横に避ける、という元の用途に残した)。
- **今回のスコープ**: 入力の切り替え機構のみを実装した。移動範囲は既存の
  `maxOffsetRadius`(2.5m)のまま据え置いており、フィールド全体を自由に歩き回れる
  ようにする拡張・障害物の配置・パルクール(小さな段差を乗り越える動作)は
  まだ手をつけていない。オンレールという決定済み事項(docs/requirements.md §1)を
  どこまで・どう崩すかは、この入力方式を実際に触ってみてから決める。
- **未検証**: 実機で「構える→傾きで狙う→離す→傾きで歩く」という一連の切り替えが
  直感的に感じられるか、不感帯・最大角・移動速度の値が適切かは要確認。

## 敵の種類ごとの個性・アイテム・ロング化（2026-09-06、実装済み・未検証）

オーナー要望3件に対応。まとまった変更のため`Milestone4_Stage`・`Stage2_BossRush`は
シーンを再生成した。

1. **敵の種類ごとの個性**（「それぞれの敵に応じて被弾可能回数や移動速度や移動方法
   （左右によけながら移動するなど）を定義して、敵ごとに同じパラメータにならない
   ようにしてください」）: `EnemyFactory`に`VegetableKind`(Tomato/Carrot/Onion/
   PumpkinBoss)ごとの固定プロフィール(被弾可能回数・接近速度・蛇行の振幅と周期・
   得点)を持たせた。トマト＝1発・速い・直進、キャロット＝1発・中速・大きく蛇行、
   オニオン＝2発・遅い、パンプキンボス＝3発・遅い・軽く蛇行、という「性格」の違いを
   種類ごとに固定し、どのステージに出てきても同じ個性を持つようにした。蛇行は
   `EnemyApproach`に追加(`weaveAmplitude`/`weaveFrequency`) — 直進の基準位置を
   まっすぐ進め、見た目の位置だけ進行方向に垂直なサイン波で振る(到達判定は基準位置で
   行うため、見た目のブレで判定がバタつかない)。
2. **敵をさらに遠く・小さく**（「敵はもっともっと遠くて小さいところから出てくる
   イメージです」）: 出現距離をさらに離した(Milestone4: z=14-16→22-26、Stage2:
   z=15-18→22-32)。スケールも縮小(通常0.85-0.9、ボス1.7-1.8)。
3. **ラウンドを長く・敵を増やす**（「1ラウンドの長さや敵の出現頻度を多く、長くして
   ください」）: `Milestone4_Stage`を3→5ウェーブ、`Stage2_BossRush`を4→6ウェーブに
   拡張。敵の総数もあわせて増やした。
4. **撃つと効果を得られるアイテム**（「撃つとプレイヤーの体力を回復するアイテムや、
   リロードできるアイテム、最大弾薬数を増加させるアイテムなどがマップ内にランダムに
   配置されたり出現されるようにしてください」）: `Pickup`(新規、`IShootable`実装)を
   追加。`Target`も同じ`IShootable`を実装するようにし、`GyroReticleController`の
   レイキャスト判定を「Targetかどうか」ではなく「IShootableかどうか」に一般化した。
   `StageDirector`が各ウェーブ開始時に50%の確率で1個、ウェーブのカメラ位置より
   やや手前(奥行き8-14)にランダム生成する(`PickupFactory`、専用アートはまだ無いので
   種類ごとに色分けした円形スプライトを手続き生成)。
   - **体力回復**: このゲームには連続値のHPが無いため、「残機を1つ戻す」ことにした
     (`LivesState.RestoreLife`、上限は開始残機)。カジュアルモードには残機が
     無いため、そもそも出現候補から除外する。
   - **リロード**: `GyroReticleController.ApplyReloadPickup()`(再キャリブレーションは
     行わない、弾切れ自動リロードと同じ理由)。
   - **弾薬増加**: `AmmoState.IncreaseMagazineSize(2)`（`GyroReticleController.
     ApplyAmmoUpPickup`）。最大弾数と即座に使える弾の両方が増える。
   **未検証**: 出現確率(50%)・出現位置(手前8-14)・弾薬増加量(+2)は仮の値。
   実際に遊んでみて頻度・位置・効果量の調整が必要になる可能性が高い。

## PanelSettingsのテーマ警告の解消・上下左右の感度分離・スマホボタン拡大（2026-09-06）

- **"No Theme Style Sheet set to PanelSettings"警告の解消（実機確認済み）**: オーナーが
  実際にコンソールで確認した警告。原因は既知（フォント未描画問題と同根 — 実行時生成の
  `PanelSettings`にThemeStyleSheetが付かない）だが、今回`ScorePopupBehaviour`が敵を
  倒すたびに新しい`PanelSettings`を作るため大量に出て顕在化した。
  `RuntimeLabelStyle.EnsureTheme(PanelSettings)`を追加し、空の`ThemeStyleSheet`を
  割り当てることでこの警告自体を消した（描画自体は既存のインラインstyle指定で
  賄っているので、テーマの中身が空でも問題ない）。5箇所全てのPanelSettings生成箇所
  （`GyroReticleController`・`StageDirector`・`GameSession`・`TitleScreenController`・
  `ScorePopupBehaviour`）に適用した。
- **上下左右の感度を分離（オーナー要望「上下左右方向の感度をユーザごとに調整できる
  ようにしてください」、実装済み・未検証）**: これまで`degreesToScreenPixels`という
  単一の値を上下・左右両方に使っていたが、`GameSettingsState`の`Sensitivity`を
  `VerticalSensitivity`/`HorizontalSensitivity`の2値に分割した。起動画面の設定欄にも
  スライダーを2本（「感度（上下）」「感度（左右）」）用意した。持ち方やスマホの機種で
  上下・左右の振れやすさが違う場合に個別調整できるようにする狙い。
- **スマホの「撃つ」「リロード」ボタンを拡大（オーナー要望、実装済み・未検証）**:
  `webapp/index.html`で「撃つ」ボタンをfont-size 22px→32px・padding 28px→40pxへ、
  「リロード」ボタンをfont-size 18px→24px・padding 16px→26pxへ、それぞれ拡大した
  （`max-width`も360px→420pxに広げた）。

## 再挑戦での接続断の修正・起動画面のスマホ操作対応（2026-09-06、実装済み・未検証）

オーナー報告「たしかに再挑戦すると、スマホとの接続が切れてしまいました」への対応と、
「起動画面についてもスマホから狙って撃つアクションで操作できるようにしてください」への対応。

- **根本原因**: `GameSession.HandleRetryRequested`は`SceneManager.LoadScene`でシーンを
  丸ごとリロードしていたが、`PhoneControllerServer`(TCP/WebSocketサーバー本体)も
  シーン内の"GyroAimTestRig"に載っていたMonoBehaviourだったため、リロードのたびに
  破棄・再生成され、生きていたソケットごと道連れで切れていた。
- **修正**: `PhoneControllerServer`をシーンをまたいで生き続ける永続シングルトンにした
  （`Instance`静的プロパティ＋`GetOrCreate()`、`Awake()`で重複インスタンスは即破棄し
  唯一のインスタンスだけ`DontDestroyOnLoad`する）。`GyroReticleController`・
  `GameSession`・`PlayerLocomotion`は`GetComponent`ではなく`GetOrCreate()`経由で
  取得するように変更し、シーン(`GyroAimTestRig`)へ直接`AddComponent`する箇所を
  4つのシーンビルダー全てから削除、対応する4シーンを再生成した。
  これで「再挑戦」も「起動画面からステージへ遷移」も、同じTCP接続を保ったまま
  MonoBehaviour側だけが安全に破棄・再生成されるようになった。
  **副次効果**: 証明書生成・TCPリスンも以前は毎回のシーンリロードで再実行されていたが、
  Play Mode開始後に1回だけになった（無駄なポート再バインドが無くなった）。
  **未検証（重要）**: 「再挑戦」後、`GyroReticleController`側の状態(`_isCalibrated`等)は
  スコア・残機と同様に初期化される設計のままなので、再挑戦のたびに「リロード」操作で
  再キャリブレーションが必要になる。これは接続断ではなく想定内の挙動だが、実際に
  触ってみて「毎回リロードが要る」のが煩わしいと感じるなら見直しの余地がある。
- **起動画面のスマホ操作**: `TitleScreenController`が`PhoneControllerServer`（同じ
  永続シングルトン、`GetOrCreate()`）を参照し、ゲームプレイ中と同様にジャイロの基準
  からの角度差分でレティクル(水色)を動かす。「リロード」でキャリブレーション、
  「撃つ」でボタンを選択する。キャリブレーション画面は出さず(メニューは低リスクなため)、
  代わりに画面下部にヒント文言（未接続／接続済み・未キャリブレーション／有効）を表示する。
  UI Toolkitの`Button.clicked`は外部から発火できないイベントのため、ボタンごとに
  `_clickTargets`へ登録した`Action`を`VisualElement.worldBound`とレティクル座標の
  当たり判定で直接呼ぶ方式にした。**現状ボタン(難易度選択・ステージ開始)のみ対応で、
  スライダー(SE音量・感度)はスマホでは操作できない**（マウスでの操作は従来通り可能）。
  **未検証**: Play Modeで実機から実際にレティクルが動きボタンが選べるかは未確認。

## リロードアニメーション・残弾アイコン表示（2026-09-06、実装済み・未検証）

「リロードアニメーションを実装して」「弾の残り弾数は、数字だけでなくアイコンでも
分かりやすくなるようにして」（オーナー要望）。

- **リロードの統一**: これまで手動リロード（"reload"メッセージ・フリックジェスチャー）は
  即座に完了していたが、残弾0での自動リロードと同じ`ReloadRoutine`（旧
  `AutoReloadRoutine`を一般化）を通るようにし、`reloadDurationSeconds`（既定0.6秒、
  旧`autoReloadDelaySeconds`から改名）かけて完了するようにした。手動リロードは完了時に
  `Recenter()`（再キャリブレーション）を伴い、自動リロードは伴わない
  （`BeginReload(recenterOnComplete)`で区別、ドリフト対策の理由は既存コメント参照）。
  **設計変更点として認識してほしいこと**: 手動リロードが「即座」ではなくなり、
  0.6秒の間は発射できない。実機で「リロードが遅く感じる」ようなら
  `reloadDurationSeconds`を下げる。
- **リロード進捗バー**: レティクルの真下に追従する小さなバー（`_reloadBarTrack`/
  `_reloadBarFill`）を追加し、リロード中だけ表示して進捗(0→100%)を示す。レティクル
  自体は円形で回転させても見た目が変わらないため、別要素にした。
- **残弾のアイコン表示**: 画面右下の残弾テキストのすぐ上に、装填数ぶんの小さな
  四角（ピップ）を並べ、残っている分だけ明るい色（金色）・使った分は暗い色にする
  （`_ammoPips`、`GyroReticleController.BuildUi`）。マガジンサイズ(既定6)が変わっても
  `magazineSize`から動的に生成するので対応する。

**未検証**: リロードバーの位置（レティクルからのオフセット6px）・ピップの配置
（残弾テキストの上88px、ラベルの実際の高さから逆算した見積もり値）が実際に重ならず
見やすいかは、Play Modeで見た目を確認する必要がある。

## 演出強化: 加点ポップアップ・被弾時の色変化・ダメージ演出・総合評価（2026-09-06、実装済み・未検証）

オーナー要望4件に対応。

1. **敵を倒した時の加点表示**: `ScorePopupEffect.SpawnAt`（実体は`ScorePopupBehaviour`）を
   `StageDirector.HandleEnemyDefeated`から呼び、倒した場所に「+100」等を1秒かけて
   浮かび上がらせながらフェードアウトする。ワールド座標を`Camera.WorldToScreenPoint`で
   毎フレーム変換しているので、カメラがウェーブ間で動いても追従する。
2. **複数回被弾する敵の色変化**: `Target.cs`に`damagedColor`（既定は暗い赤）を追加。
   `TargetHitState.MaxHitPoints`（新規公開）と`RemainingHitPoints`の比率から、
   Idle時の色を基本色→damagedColorへ徐々に寄せる。1発で倒れる通常の敵は
   `MaxHitPoints`が常に1なので変化なし（ボス等の多段ヒット敵だけに効く）。
   既存の被弾直後の白フラッシュ（`hitFlashColor`）はそのまま残り、フラッシュが
   収まった後に「今どれだけ削れているか」が色でわかるようになる。
3. **被弾時のダメージ演出**: `GameSession.LoseLifeIfArcade`で残機が減るたびに、
   画面全体を覆う`_damageFlash`(VisualElement)を赤くフラッシュさせ0.4秒かけて
   フェードアウトする（`pickingMode = PickingMode.Ignore`でクリック判定は奪わない）。
4. **総合評価(A〜E)・高揚感演出**: 純粋なC#クラス`ScoreGrade.Compute(score,
   maxPossibleScore)`（EditModeテスト`ScoreGradeTests.cs`あり）でステージクリア時の
   達成率から評価を出す（達成率はそのステージの全敵`PointValue`合計に対する比率、
   90%以上A・75%以上B・60%以上C・40%以上D・それ未満E）。`StageDirector.ShowStageClear`が
   評価に応じた色（A=ゴールド、B=水色、C=白、D=オレンジ、E=赤）で画面中央に大きく
   表示し、弾むような拡大アニメーションで登場させる。A・Bの高評価では
   `CelebrationEffect`（複数色を経由する紙吹雪風パーティクル）も追加で再生する。

**未検証**: 4項目とも実装のみで、Play Modeで実際に見た目・タイミング・派手さの
バランスを確認したのはまだ誰もいない。特にポップアップの表示位置（カメラ追従）・
被弾時の赤フラッシュの強さ・評価画面の演出量が「高揚感」として狙い通りかは
実際に遊んで判断が必要。

## 起動画面（2026-09-06、実装済み・未検証）

「起動画面を実装して。そこから難易度選択や設定などができるようにして」（オーナー要望）。

- 新規シーン`Assets/Scenes/Title.unity`（`Assets/Editor/TitleSceneBuilder.cs`の
  `-executeMethod`または「PocketBlaster > Build Title Scene」で生成）を追加。
  `TitleScreenController`が全て(UI Toolkit)をコードから組み立てる、`PhoneControllerServer`
  非依存の画面 — スマホを一切つながなくてもマウスだけで完結する。
- 選べる項目: 難易度（カジュアル/アーケード）・SE音量（0-100%）・感度
  （`degreesToScreenPixels`相当、4-24の範囲、`GameSettingsState`が妥当性を保証）。
  「ステージ1」「ステージ2（ボスラッシュ）」ボタンでそれぞれ`Milestone4_Stage`/
  `Stage2_BossRush`へ`SceneManager.LoadScene`で遷移する。
- 選択内容は`Assets/Scripts/Meta/GameSettings.cs`（PlayerPrefs）に保存され、シーンを
  またいで（次回起動時も）覚えている。純粋な値検証部分は`GameSettingsState`に切り出し、
  EditModeテスト(`GameSettingsStateTests.cs`)で担保している。
- **難易度モードの選択場所をスマホ側→PC側(起動画面)へ一本化した**。以前は
  `webapp/index.html`の接続前ラジオボタン("mode"メッセージ)で選んでいたが、起動画面と
  二重に持たせると混乱するため、スマホ側の当該UI・メッセージ・
  `PhoneControllerServer.OnModeSelected`・`GameSession.HandleModeSelected`は全て削除した。
  `GameSession`は`Awake()`で`GameSettings.Current.IsArcadeMode`を直接読む。
- `GyroReticleController`は`Awake()`で`GameSettings.Current.Sensitivity`/`SfxVolume`を
  読んで`degreesToScreenPixels`/`AudioSource.volume`に反映する。
- **未検証（重要）**: 起動画面自体をPlay Modeで実際に操作して見た目・操作感を確認したのは
  まだ誰もいない。スライダーの表示・ボタンのクリック判定・ステージ遷移が実際に機能するか、
  Titleシーンを開いてPlay Modeで確認してほしい。

## 実機プレイ中に踏んだバグの修正（2026-09-06）

- **`JuiceSplashEffect`のパーティクル生成エラー**: 命中時に
  `"Setting the duration while system is still playing is not supported"`が
  コンソールに出ていた。原因は`go.AddComponent<ParticleSystem>()`の時点で
  `playOnAwake`既定trueによりAwake/OnEnableが同期的に走りもう再生中になっており、
  その後に`main.duration`等を変更しようとしていたため。`AddComponent`直後に
  `ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear)`で明示的に止めてから
  設定し、最後に`ps.Play()`で改めて再生する形に修正した。

## HUDのテキストが一切描画されない不具合の修正（2026-09-06、実機確認済み）

オーナーがゲーム終了画面のスクリーンショットを送ってくれたことで判明: レティクル(枠線だけの
VisualElement)は正しく描画されているのに、スコア・ウェーブ・残機等の**Labelのテキストが
画面上に一切表示されていなかった**。ラベルの中央寄せ計算(前回の修正)の問題ではなく、
もっと根本的な原因だった。

- **原因**: `ScriptableObject.CreateInstance<PanelSettings>()`で実行時生成した
  PanelSettingsには、エディタの「Create > UI Toolkit > Panel Settings Asset」経由と違い
  既定のThemeStyleSheetが付与されない。その結果`Label`がフォントを解決できず、
  テキストのレイアウト・描画自体が行われない(枠線やbackgroundColorはテーマ非依存なので
  正常に描画される — これが「レティクルは見えるのにテキストだけ見えない」という
  観測と一致する)。
- **調査で判明した副次情報**: Unity 6では`Resources.GetBuiltinResource<Font>("Arial.ttf")`が
  `ArgumentException`を投げるようになっている（"Arial.ttf is no longer a valid built in
  font. Please use LegacyRuntime.ttf"）。バッチモードでの実機確認で判明（診断用に
  一時的なEditorスクリプトを書いて`-executeMethod`で実行、確認後に削除した）。
- **修正**: 共通ヘルパー`Assets/Scripts/UI/RuntimeLabelStyle.cs`を新規作成し、
  `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")`を`Label.style
  .unityFontDefinition`に明示指定する`ApplyDefaultFont(Label)`を用意。
  `GyroReticleController`・`StageDirector`・`GameSession`が生成する全8個のLabel
  （キャリブレーション案内・ステータス・残弾・ウェーブ・スコア・セッション・一時停止）
  すべてに適用した。
  **実機確認済み**: `LegacyRuntime.ttf`が`null`にならず解決できることをバッチモードで
  直接確認した（実際にPlay Modeでテキストが表示されるかどうかの最終確認はオーナーに
  お願いしたい）。

## HUD表示・自動リロード・敵接近の開始タイミング対応（2026-09-06、実装済み・未検証）

オーナーからの追加プレイテストFB3件に対応。

1. **スコアが見つからない問題**: `StageDirector`のスコアラベルは
   `left: 50% + translate(-50%)`で中央寄せしていたが、レイアウト確定前の
   translate%計算に依存する、確証の薄い書き方だった。`left:0; right:0`で幅いっぱいに
   広げてから`unityTextAlign`で中央寄せする、より標準的な方法に変更した。
   あわせて、`GyroReticleController`・`StageDirector`・`GameSession`が別々に
   実行時生成している3つの`PanelSettings`に明示的な`sortingOrder`
   （レティクル系10、HUD系5）を振り、重なり順が既定値(全て0)で不定になる余地を消した。
   **未検証（重要）**: この修正で実際にスコアが見えるようになったかは、実機/Editorで
   目視確認するまで確証が持てない — 座標計算の問題ではなく別の原因だった場合は
   引き続き報告してほしい。
2. **残弾のゲーム画面表示**: これまで残弾数は`GyroReticleController`の
   デバッグ用ステータス欄（alpha/beta/gamma等と一緒くたの多行テキスト）にしか
   出ておらず、見つけにくかった。画面右下に大きく単独の残弾表示（`_ammoLabel`）を
   追加した。残弾0の間は赤みがかった色に変える。
3. **残弾0での自動リロード**: `GyroReticleController.HandleShoot()`で撃った結果
   残弾が0になったら、`AutoReloadRoutine`（`autoReloadDelaySeconds`、既定0.6秒）を
   自動的に開始し、遅延後に弾を補充する。**手動リロード(ボタン/フリックジェスチャー)とは
   異なり`Recenter()`は呼ばない** — 弾切れになった瞬間にどこを狙っているかは不定なので、
   それを新しい基準にすると設計上の不変条件2(ドリフト補正)が意図する効果ではなく
   逆にドリフトの原因になりかねないため。狙いの基準を取り直すのは、引き続き
   「画面中央に構え直してリロード操作をする」明示的な行動に限定してある。
   `autoReloadDelaySeconds`は今は単なる待ち時間だが、後で差し替える
   リロードアニメーションの尺として使う想定の置き場所（オーナー要望
   「のちのちリロードのアニメーションは発生させる」）。

## PC上のマウスでの狙い（デバッグ用、2026-09-06、実装済み・未検証）

「遊び方の部分はだいたい良いので、UIや設定や演出や拡張をすすめていきましょう。まず、PC上の
クリックでもカーソルを動かせるようにしてください。この機能はあくまでデバッグを容易にする
ためです」（オーナー要望）。`GyroReticleController`に`enableMouseDebugAim`（既定true）を
追加し、有効な間は常に`Input.mousePosition`がジャイロより優先してレティクル位置を決める。
左クリックで`HandleShoot()`を直接呼ぶ（弾数・命中判定・効果音は通常の射撃と完全に共通）。
スマホの実機テストで邪魔になる場合はInspectorでオフにする。

**追記（同日）**: 「マウスクリックがあれば敵の動き出しが開始するようにもしてください」との
要望を受けて、`enableMouseDebugAim`が有効な間は未キャリブレーションでの最初の左クリックで
自動的に`Recenter()`（キャリブレーション完了扱い）するようにした。`EnemyApproach`は
`GyroReticleController.IsCalibrated`を見て動き出すため、これだけでスマホを一切使わずに
「クリックで開始→狙って撃つ→敵が近づいてくる」まで一通り試せるようになった
（上記の「スコープ外」は解消済み）。
**修正（同日）**: 上記の「実機テスト中の誤クリックで乱れるリスク」は、当初の想定より深刻な
形で実際に発生した——オーナー報告「PCに対応した結果、マウスに判定が持っていかれているのか、
スマホで照準が効かなくなりました」。原因は`enableMouseDebugAim`が有効な間、
`_server.IsConnected`の状態にかかわらず常にマウス座標をジャイロより優先していたため、
スマホが接続されていても照準・発射判定がマウス側に奪われていた。
`IsMouseDebugActive`(`enableMouseDebugAim && !_server.IsConnected`)を導入し、
**スマホが接続されている間はマウスデバッグを自動的に完全無効化**するように修正した。
マウスデバッグは「スマホが無い時の代役」に限定され、実機接続後は一切判定を持たない。
Inspectorでの手動オフは不要になった。

## 敵の接近開始タイミング（2026-09-06、実装済み・未検証）

「シーンが始まっても、スマホ側が接続してアクションを取るまで敵の接近は開始しない
ようにしてください」（オーナー要望）。`EnemyApproach`が
`GyroReticleController.IsCalibrated`（新規公開プロパティ）を参照し、キャリブレーション
完了（接続後にリロード操作を済ませる）まで移動を止めるようにした。「接続済み」と
「実際に操作した」を1つのフラグ（キャリブレーション完了）で判定している。
**未検証**: ウェーブ開始時点で敵が本当に静止したまま待つか、キャリブレーション完了の
瞬間から自然に動き出すかは実機/Editor確認が必要。

## プレイテストFB対応（2026-09-06、実装済み・未検証）

オーナーが実機で`Stage2_BossRush`等を遊んでみたフィードバック4件に対応した。

1. **スコア表示をゲーム画面へ**: それまで右上に「ウェーブ/残り敵/スコア」を1つの
   `Label`にまとめていたが、スコアだけを画面上部中央に大きく（`StageDirector`の
   新しい`_scoreLabel`、32pt太字）独立表示するようにした。ウェーブ/残り敵の表示は
   従来通り右上に残している。
2. **敵の出現距離を離し、スケールを縮小**: 「敵が大きすぎて狙う要素が少ない」を受けて、
   `Milestone4_Stage`・`Stage2_BossRush`の敵の出現z座標を大幅に離した
   （以前z=6〜9・scale2〜4 → 現在z=14〜18・scale1.3〜2.4）。近づいてくる分の移動時間が
   延びるので`EnemyApproach.approachSpeed`も0.6〜1.1へ上げ、ウェーブの長さ自体が
   極端に伸びないよう調整した（`Stage2_BossRush`はウェーブが進むほど`approachSpeed`を
   わずかに上げ、後半ほど緊迫感が増すようにした）。**未検証**: 新しい距離・速度の
   組み合わせが「狙う要素」として適切か、approachSpeedを上げたことで逆に接近が
   速すぎないかは実機で確認が必要。`Milestone3_ShootTarget`の固定的な仮の敵1体は
   対象外（意図的に据え置き、繰り返し試す用途のため）。
3. **初回キャリブレーション画面**: それまでリロード操作で無言のまま基準位置が
   決まっていたが、`GyroReticleController`に明示的なキャリブレーション画面を追加した。
   接続直後（`_isCalibrated == false`の間）はレティクル・ステータス表示を隠し、代わりに
   「スマホを画面の中央に向けて構え、『リロード』を押してください」という案内を
   中央に表示する。リロードで`Recenter()`が呼ばれるまでは`shoot`も無視する
   （空撃ちにならないよう安全側にした）。
4. **スマホ側からの一時停止・再挑戦**: `webapp/index.html`に「一時停止」「再挑戦」
   ボタンを追加（`PhoneControllerServer`の新しい"pause"/"retry"メッセージ経由）。
   `GameSession`が受け取り、一時停止は`Time.timeScale`を0/1で切り替えつつ
   `GyroReticleController`を明示的に無効化（射撃はtimeScaleの影響を受けないため）、
   画面中央に「一時停止中」を表示する。再挑戦は`SceneManager.LoadScene`で現在の
   シーンを丸ごとリロードする（スコア・残機・ウェーブ進行が初期化される）。
   `SceneManager.LoadScene(buildIndex)`にはBuild Settingsへの登録が要るため、
   共通ヘルパー`Assets/Editor/BuildSettingsHelper.cs`を新規作成し、4本の
   シーンビルダー全てに`EnsureSceneInBuildSettings`呼び出しを追加した。
   **未検証（重要）**: シーンリロードでサーバーごと(`PhoneControllerServer`)作り直される
   ため、スマホ側のWebSocket接続もその瞬間に切れる可能性が高い。再接続が必要になるのか、
   自動で繋がり直すのかは実機で未確認 — 切れた場合は「再挑戦を押したら一度切断され、
   もう一度『接続する』を押す必要がある」という仕様として受け入れるか、UXとして
   直すか実機確認後に判断する。

## 現状（2026-09-06）

マイルストーン1〜5（`docs/requirements.md`§4）＋§8将来の拡張3項目を実装済み・自動テスト
通過。**実機（iPhone Safari）からスマホ→PC間の接続・ジャイロでのレティクル操作は動作確認済み。**
ドリフトの実測・「撃つ感触」の判断、マイルストーン4以降（複数ウェーブ・スプライト・移動・
リロードジェスチャー・スコア/ボス戦/難易度モード）の実機確認はまだ行っていない。

**将来の拡張（2026-09-06、実装済み・未検証）**: `docs/requirements.md`§8の3項目すべてに対応。
- **スコアリング**: 敵を倒すと得点（`Target.PointValue`、通常100・ボス1000）。ステージクリア時に
  シーン単位のローカルハイスコア（`PlayerPrefs`）と比較・更新して表示。
- **複数ステージ・ボス戦**: `Stage2_BossRush`（新規、4ウェーブ）を追加。`TargetHitState`に
  `hitPoints`（既定1）を追加し、1より大きい値で「その回数当てるまで倒れない」ボスを作れる
  ようにした（`Stage2_BossRush`のパンプキンボスは3発）。**各ステージは独立シーンのままで、
  クリアしても自動で次のステージへは進まない**（`Milestone1/3/4`と同じ「単体で開いて
  Play Modeに入れる」方針を優先。docs/requirements.md未決事項#6）。
- **難易度モード**: スマホ側（接続前）で「アーケード（残機制）」「カジュアル（無制限）」を
  選択。フェール条件は2つ: 「狙って撃ってはずした」（`GyroReticleController.OnShotResolved`、
  弾切れの空撃ちはノーカウント）と、下記の「敵に近づかれ過ぎた」（`EnemyApproach`）。
  **この設計判断自体が実機で遊んでみて妥当かどうかはまだ検証していない** —
  特に「はずれ」と「敵の接近」の両方が残機を削る今の設計は厳しすぎる可能性があり、
  窮屈に感じるようならどちらか一方に絞る、または許容回数を増やすといった見直しが必要になる。

**敵の接近ダメージ（オーナー要望、2026-09-06、実装済み・未検証）**: `EnemyApproach`を
ウェーブ制のステージ（`Milestone4_Stage`・`Stage2_BossRush`、`Milestone3`の固定敵は対象外）の
敵に付与し、プレイヤーへ向かってゆっくり近づくようにした。一定距離まで近づくと、撃たれた時の
演出は経ずに退場し（得点は入らないが、ウェーブの残り数からは減る）、アーケードモードでは
残機も減る。**未検証**: `EnemyApproach`の`approachSpeed`（既定0.6、ボスは0.35）・
`damageRange`（既定1.5）は仮の値で、実機で「近づいてくるのが分かるか」「間に合う速さか」の
調整が必要。

**マイルストーン5（2026-09-06）**: 世界観を「野菜×ゾンビ、倒すとジュースになる」に確定
（オーナー判断、詳細は`docs/requirements.md`決定済み事項）。3Dモデルの代わりに、House of
the Dead等の古典的ライトガンゲームで使われる「カメラに正対するスプライト(ビルボード)」方式を
採用し、既存の2D画像生成パイプラインで敵アート(tomato/carrot/onion/pumpkin_zombie_boss)を
作成、`Milestone3_ShootTarget`・`Milestone4_Stage`のカプセルと差し替えた。
- 生成時の実際の問題: キャロットは色距離キーイングの既定設定だと脚の間に穴が残る不良が
  2回連続で発生し、`--ml-segment`（isnet-anime、キャラクター向け）に切り替えて解決した。
  同じ「野菜ゾンビ」系の追加キャラを作る時は最初から`--ml-segment`を使うとよい
- パンプキンボスは牙が鋭く笑顔も攻撃的な仕上がりで、要件の「怖くないもの」からやや外れる
  懸念があったためオーナーに確認し、このまま使う判断をもらった（2026-09-06）
- **未検証**: 実際にUnity上で見た時のビルボードの見え方・スプライトのサイズ感・
  「野菜ゾンビ」のトーンが実機での見え方として狙い通りかは、まだ誰も確認していない

**プレイヤー移動・リロードジェスチャー（オーナー要望、2026-09-06、実装済み・未検証）**:
- 足踏み検知での移動: スマホ側で加速度センサーの合成値の跳ね上がりを歩数として検出し
  ("step"メッセージ)、Unity側は現在狙っている方向(`GyroReticleController.GetAimRay()`、
  射撃判定と共通)の水平成分へ一定距離動く(`PlayerLocomotion`)。オンレールという前提
  （決定済み事項）は崩さず、開始位置から一定半径（既定2.5m、`maxOffsetRadius`）に
  クランプしてある——大きな移動は引き続きウェーブクリアでのカメラ移動が担う。
- リロードジェスチャー: 持ち方設定で既に補正済みの「上下方向」の値が短時間で大きく振れて
  戻るパターンを検出し、ボタンを押さなくても"reload"を送る（ボタンとの併用可）。
- **未検証（実機での閾値調整が必要）**: `webapp/index.html`の`STEP_THRESHOLD`
  （歩数検知の感度）・`FLICK_MIN_SWING_DEG`/`FLICK_RETURN_TOLERANCE_DEG`
  （リロードジェスチャーの感度）は仮の値。実機で「歩いても反応しない」
  「意図せずリロードが暴発する」等が起きたら、まずこれらの定数を調整する。
  画面の「歩数: N」表示で歩数検知が正しく反応しているか確認できる。

実機テストで判明し対応済みの操作性の課題（2026-09-05）:
- スマホの持ち方（銃のように構える角度）によって、上下左右の動きがbeta/gammaのどちらに
  どの符号で出るかが変わる問題 → `webapp/index.html`に軸設定（上下:beta/gamma、
  左右:gamma(傾き)/alpha(回転)、それぞれ反転可）を追加。接続中でも変更可能
- 「スマホを傾ける」のではなく「スマホ自体を水平に回転させる（銃口を振るイメージ）」で
  左右を操作したい、という要望 → 左右方向にalpha(水平方向の回転)を選べるようにした。
  alphaは0/360の境界をまたぐため、Unity側の差分計算を`Mathf.DeltaAngle`に変更して
  ラップアラウンドを正しく処理するようにした

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
- `Assets/Scripts/Gameplay/StageProgressState.cs`: 現在のウェーブ番号・残り敵数・クリア判定を
  UnityEngine非依存で表現。`TargetHitState`に`respawns:false`で「倒したら退場する」
  終端フェーズ(Defeated)を追加し、マイルストーン3の「繰り返し試せる」仮の敵とは別の
  使い方をできるようにした。
- `Assets/Scripts/Gameplay/StageDirector.cs`: 上記を使い、ウェーブの敵を全滅させたら
  次のウェーブのカメラ位置へLerpで移動する進行管理。最後のウェーブクリアで
  「ステージクリア」表示。
- `Assets/Scenes/Milestone4_Stage.unity`: 3ウェーブ(2体→3体→仮の「ボス」1体、奥行きが
  手前に迫る配置)の短いステージ（`Milestone4SceneBuilder`の`-executeMethod`または
  「PocketBlaster > Build Milestone4 Scene」で生成）。
- `Assets/Scripts/Gameplay/PlayerOffsetState.cs`/`PlayerLocomotion.cs`: 足踏み移動
  （上記参照）。`Assets/Scripts/Gameplay/ScoreState.cs`/`LivesState.cs`/`GameSession.cs`:
  得点・残機・難易度モード（上記「将来の拡張」参照）。
- `Assets/Editor/Stage2SceneBuilder.cs` → `Assets/Scenes/Stage2_BossRush.unity`:
  2本目のステージ（4ウェーブ、3発ヒットのボス）。
- `Assets/Tests/EditMode/`: 計48件、すべてpass（2026-09-06プレイテストFB対応後に再確認）
  （`PhoneOrientationServerTests`・`AmmoStateTests`・`TargetHitStateTests`・
  `ProceduralSfxTests`・`CertificateDownloadServerTests`・`StageProgressStateTests`・
  `PlayerOffsetStateTests`・`ScoreStateTests`・`LivesStateTests`）。
  `run-unity.ps1 -ProjectPath . -ExpectOutput TestResults.xml -UnityArgs
  @('-batchmode','-nographics','-runTests','-testPlatform','EditMode','-testResults',
  'TestResults.xml','-logFile','test_run.log')`で再実行できる。
  **レイキャストでのヒット判定・カメラのウェーブ間移動・ウェーブ進行の見た目は
  EditModeテストの対象外** — Physicsやコルーチンが動くにはPlay Modeが要るため、
  下記の実機検証で一緒に確認する。

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
   - レティクルを敵（トマトゾンビのスプライト）に重ねて「撃つ」→ 白フラッシュ＋倒れ込み＋
     赤いジュースの飛び散り＋命中音が鳴るか、ステータス欄の「直近の射撃結果」が
     「命中」になるかを見る。
   - 明後日の方向を狙って「撃つ」→ 「はずれ」になり、敵は反応しないことを見る。
   - **一番大事な判断**: この着弾フィードバック（フラッシュ・倒れ込み・仮の効果音）だけで
     「狙って撃った」という手応えを感じられるか。物足りなければ、次に足すべきは
     見た目のクオリティ（本物のアート・SE）ではなく、フィードバックの種類そのもの
     （画面の揺れ、命中時のポーズ、弾痕など）である可能性が高い — 判断してから次に進む。

**手順5〜7の実際の判断（ドリフト・撃つ感触）はまだ言語化して記録していない。**
次回のセッションで、実機テストで確認した内容を`docs/requirements.md`未決事項へ反映すること。

8. **足踏み移動の検証**（2026-09-06実装、未検証）: 接続後、画面下部の「歩数: N」表示を
   見ながらその場で足踏みし、数字が増えるか確認する。増えなければ`STEP_THRESHOLD`
   （`webapp/index.html`）を下げる。逆に歩いていないのに増える・敏感すぎる場合は上げる。
   数字が正しく増えたら、狙う方向を変えながら足踏みし、Unity側のカメラ(視点)が
   狙った方向へ動くか確認する。
9. **リロードジェスチャーの検証**（2026-09-06実装、未検証）: スマホを素早く上に向けて
   すぐ戻す動作をしてみて、ボタンを押さなくても弾が補充される（残弾表示がリセットされる）
   か確認する。反応しない場合は`FLICK_MIN_SWING_DEG`を下げる、逆に普通に狙っているだけで
   誤発動する場合は上げる（`webapp/index.html`）。

## `Milestone4_Stage`での確認（まだ誰もやっていない）

`Milestone3_ShootTarget`と同じ接続手順（上記1〜4）の後、`Milestone4_Stage`シーンで
Play Modeに入って試す。

1. 1ウェーブ目（トマト×2）を両方倒すと、カメラが自動的に2ウェーブ目の位置へ動くか確認する。
2. 2ウェーブ目（トマト・オニオン・キャロット3体）→3ウェーブ目（パンプキンボス1体、
   一回り大きい）と順に進み、最後を倒すと画面右上に「ステージクリア！」と出るか確認する。
3. **判断したいこと**:
   - カメラが切り替わる時の見た目（Lerpでの単純な移動）が唐突に感じるか、ウェーブごとの
     敵の増減・距離の変化に「進んでいる感じ」があるか。
   - ビルボード(常にカメラへ正対するスプライト)が、カメラが動いている間も違和感なく
     見えるか（板が動いて見える等が起きていないか）。
   - パンプキンボスの見た目が実際にプレイして「怖すぎる」と感じるかどうか
     （2026-09-06にオーナーが「このまま使う」と判断済みだが、実際に動かして
     見え方が変わる可能性がある）。
   物足りなければ、カメラの移動を滑らかなレール（スプライン）にする、ウェーブ間に演出
   （フェード等）を挟む、といった改善が次の候補になる。
4. **スコアの検証**: 敵を倒すたびに画面右上のスコアが増えるか、ステージクリア時に
   「ハイスコア更新！」（初回は必ずこれになる）または「スコア: N（ハイスコア: M）」の
   どちらかが表示されるか確認する。もう一度同じシーンをPlay Modeで実行し、前回より
   低い点でクリアした場合に「ハイスコア更新！」にならないことも確認する。

## `Stage2_BossRush`での確認（まだ誰もやっていない）

`Milestone3_ShootTarget`と同じ接続手順（上記1〜4）の後、`Stage2_BossRush`シーンで
Play Modeに入って試す。

1. 4ウェーブを順に進み、最後のパンプキンボスに3発当てるまで倒れないことを確認する。
   1〜2発目は白くフラッシュするだけでまた狙える状態に戻り、3発目でようやく倒れ込む
   （見た目で「ダメージを与えている」ことが伝わるかが判断ポイント）。
2. クリア後のスコアが1000点以上（ボスの得点分）増えているか確認する。
3. **難易度モードの検証**: 接続前の「難易度モード」で「アーケード」を選んで接続する。
   画面左下に「モード: アーケード　残機: 3」と出るか確認する。わざと狙いを外して「撃つ」を
   3回行い、残機が減って0になったら「ゲームオーバー」表示になり、それ以上撃てなくなるか
   確認する。「カジュアル」を選んだ場合は何回はずしても残機表示が変わらないことも確認する。
   - **一番大事な判断**: 「はずれで残機が減る」という難易度設計が、実際に遊んでみて
     楽しい緊張感になっているか、それとも単に窮屈でストレスなだけか。後者なら
     フェール条件そのものを見直す（許容ミス回数を増やす、時間切れ制にする等）。
4. **敵の接近ダメージの検証**: 何もせずに敵を放置し、近づいてくるのが見えるか確認する。
   一定距離まで近づいたら被弾演出（フラッシュ・倒れ込み）を経ずにそのまま消え、
   ウェーブの残り数は減る（得点は増えない）ことを確認する。アーケードモードでは
   この時も残機が減る。
   - **判断したいこと**: 近づいてくる速さが「間に合う」感覚か、それとも速すぎる/
     遅すぎるか。近づいていることに気づきにくい場合は、演出（音・見た目の変化）を
     足す必要があるかもしれない。

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
