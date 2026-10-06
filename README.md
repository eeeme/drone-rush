# Drone Rush（仮）— スマホ向け3Dドローン・リングレース

3Dの街の上空をドローンで飛び、12個のリングを順番にくぐってタイムを競うゲームです。
モデル・画像などの外部アセットは使っていません（街もドローンもUIもコードで生成）。

## 遊び方
- 左スティック: 上下で上昇・下降、左右で旋回（実機ドローンの「モード2」と同じ配置）
- 右スティック: 前後左右に移動
- スティックは画面の左半分・右半分のどこを触っても、触った位置に出ます
- オレンジ色のリングが次の目標です。ドローン上の矢印がその方向を指します
- 建物に強くぶつかると +2秒 のペナルティ（地面や屋上への着地はペナルティなし）
- 右上の CAM ボタンで、後方視点とFPV（機体カメラ）を切り替え
- ベストタイムは端末に保存されます
- PCのエディタでは キーボードでも操作できます（WASD = 左スティック、矢印キー = 右スティック）

## 1. ブラウザで試す（GitHub Actions → GitHub Pages）
`main` に push するたびに、GitHub上でUnityがWebGL版をビルドし、GitHub Pages に公開します。
公開URL: `https://eeeme.github.io/drone-rush/`（スマホのブラウザで開いて横向きに）

### 初回だけの設定
1. **Unityライセンス**: PCに Unity Hub を入れてサインインすると、ライセンスファイルができます。
   - Windows: `C:\ProgramData\Unity\Unity_lic.ulf`
   - Mac: `/Library/Application Support/Unity/Unity_lic.ulf`
2. リポジトリの **Settings → Secrets and variables → Actions** に登録します。
   - `UNITY_LICENSE`: 上のファイルの中身（全文）
   - `UNITY_EMAIL` / `UNITY_PASSWORD`: Unityアカウントのメールアドレスとパスワード
3. **Settings → Pages → Build and deployment → Source** を「**GitHub Actions**」にします。
4. Actions タブで「Build」を Run workflow（または何か push）。初回は15〜25分ほどかかります。2回目以降はキャッシュが効きます。

## 2. PCのUnityで開く
1. Unity Hub で **Unity 6000.0.58f2 以降の Unity 6** をインストールします（Android Build Support モジュールも追加）。
2. このリポジトリを clone し、Unity Hub の「Add project from disk」で開きます。
3. ▶（Play）を押せば動きます。シーンには何も置く必要はありません（`GameManager` が起動時に街・ドローン・UIを自動生成）。
4. メニュー **Drone Rush > Build WebGL / Build Android (AAB)** から手元でもビルドできます。

## 3. 遊び心地の調整ポイント
スクリプトの数値を変えるだけで調整できます。

| 変えたいこと | 場所 |
|---|---|
| 速さ・上昇速度・旋回速度 | `DroneController.cs` の `maxSpeed` / `climbSpeed` / `yawSpeed` |
| キビキビ感（慣性の強さ） | `DroneController.cs` の `acceleration`（小さいほどフワッと滑る） |
| リングの数・コースの形 | `GameManager.cs` の `ringCount` / `courseSeed`（seed を変えると別のコース） |
| 激突ペナルティ | `GameManager.cs` の `crashPenalty` |
| カメラ距離・画角 | `DroneCamera.cs` の `chaseOffset` / `chaseFov` / `fpvFov` |
| スティックの大きさ・位置 | `Hud.cs` の `MakeStick` 内 `sizeDelta` / `anchoredPosition` |

## 4. Google Play で公開するまで
アプリ名・パッケージ名（`com.meisme.dronerush`）・画面の向き・IL2CPP/ARM64 などの設定は、`Assets/DroneRush/Editor/BuildScript.cs` がビルド時に自動で適用します。
パッケージ名は公開後に変更できないので、変える場合は最初のアップロード前に変えてください。

### 署名鍵（アップロード鍵）を作る（PCで1回だけ）
Java の keytool（Unity の Android モジュールに同梱の OpenJDK でも可）で作ります。
```
keytool -genkeypair -v -keystore upload.keystore -alias upload -keyalg RSA -keysize 2048 -validity 10000
base64 -w0 upload.keystore > upload.keystore.b64   # Mac は: base64 -i upload.keystore -o upload.keystore.b64
```
`upload.keystore` とパスワードは、なくさないように保管してください（Play App Signing を使えば、紛失時にリセットを申請できます）。リポジトリには絶対にコミットしないでください。
GitHub の Secrets に次の4つを追加します。
- `ANDROID_KEYSTORE_BASE64`: `upload.keystore.b64` の中身
- `ANDROID_KEYSTORE_PASS` / `ANDROID_KEYALIAS_PASS`: 設定したパスワード
- `ANDROID_KEYALIAS_NAME`: `upload`

### .aab をビルドする
Actions → Build → **Run workflow** で「Google Play 用の .aab もビルドする」にチェックを入れ、versionCode（毎回 +1）とバージョン名を入れて実行します。
完了すると、実行結果ページの Artifacts から `.aab` をダウンロードできます。

### Play Console
1. アプリを作成し、`.aab` をアップロード。
2. ストア掲載情報（説明文、スクリーンショット、512x512 アイコン、1024x500 フィーチャーグラフィック）、プライバシーポリシーのURL、データセーフティ、コンテンツのレーティング、ターゲット年齢を入力。
   このゲームはデータを送信しません。広告や分析を入れない限り、データセーフティは「収集なし」で申告できます。
3. 2023年11月以降に作成した個人デベロッパーアカウントは、本番公開の前に **クローズドテスト（12人以上・14日間）** が必要です。

## 5. 次に足すと良さそうなもの
- 効果音・BGM（リング通過音、プロペラ音のピッチを速度に連動）
- 複数コース・難易度選択、ゴースト（ベスト記録の軌跡）表示
- 風や重力のある「リアル操縦モード」
- オンラインランキング（Unity Leaderboards）、広告や課金による収益化
- 見た目の強化（ドローンや建物の3Dモデル、スカイボックス）

## ファイル構成
```
.github/workflows/build.yml  自動ビルド（WebGL → Pages、Android .aab）
Assets/DroneRush/Editor/
  BuildScript.cs      ビルドとプレイヤー設定（アプリ名・パッケージ名など）
Assets/DroneRush/Scripts/
  GameManager.cs      ゲーム進行（スタート→カウントダウン→プレイ→結果）、自動起動
  DroneController.cs  飛行制御、キーボード操作、激突判定
  DroneCamera.cs      後方視点 / FPV カメラ
  VirtualJoystick.cs  タッチ用バーチャルスティック（マルチタッチ対応）
  Ring.cs             リング（通過判定・色の切り替え）
  WorldBuilder.cs     街・コース・ドローンをコードで生成
  Hud.cs              画面UI（スティック、タイマー、ボタン）をコードで生成
```
自分でシーンを作り込みたくなったら、`GameManager.cs` の `autoBootstrap = true` を `false` に書き換えると自動生成を止められます。
