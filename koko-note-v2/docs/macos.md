# macOSネイティブ版

Windows版を維持したまま、このMacで直接動くSwift／AppKit版を `macos/` に追加しました。仮想マシン、WebView、Electron、.NETランタイムは使いません。実装言語・暗号化方式・保存先はWindows版と別です。旧Avalonia版のデータを自動移行しません。

## ビルド・テスト・起動

Apple Silicon Mac、Swift 6を含むXcode Command Line Toolsが必要です。この端末ではSwift 6.1.2、macOS 26.6.2でビルド・テストしています。デプロイ対象はmacOS 13以降ですが、旧OSでの実行は未確認です。

リポジトリの `koko-note-v2` で実行します。

```sh
bash scripts/build-macos.sh
open "artifacts/KOKO Note.app"
```

メニューバーにメモのアイコンが現れます。起動時のノート画面は非表示です。**Control + Shift + Space** またはメニューバーの「表示」で開けます。

テストはCommand Line Toolsに同梱されるSwift Testingを使用します。Xcode本体のXCTestは要求しません。

```sh
CLANG_MODULE_CACHE_PATH=/tmp/koko-swift-clang-cache \
SWIFTPM_MODULECACHE_OVERRIDE=/tmp/koko-swift-module-cache \
swift test --package-path macos --disable-sandbox --disable-xctest --enable-swift-testing
```

直接の外部パッケージ依存はありません。AppKit、CarbonのホットキーAPI、Security／Keychain、CryptoKit、OS付属SQLiteを使用します。Swift Testingは開発用で、アプリへ同梱しません。

## 操作

Windows版とのキー対応・案内表示の条件は[共通操作仕様](shortcuts.md)にまとめています。

| 操作 | キー |
|---|---|
| 表示／前面復帰／非表示 | Control + Shift + Space |
| 新規ノート | ⌘N |
| 保存・再試行 | ⌘S |
| タブを閉じる（削除しない） | ⌘W |
| ノート一覧・本文／タイトル検索 | ⌘L |
| 名前変更 | ⌘R |
| 次／前のタブ | Control + Tab / Control + Shift + Tab |
| Undo／Redo | ⌘Z / ⌘Shift+Z |
| 完全削除 | 「ノート」→「ノートを削除…」→確認 |
| アプリ終了 | メニューバー→「終了」、または⌘Q |

赤い閉じるボタンは非表示にするだけです。保存は入力停止約500ms後、切り替え・非表示・終了時に実行します。IME変換中は切り替えと非表示を保留し、未確定文字をWorkspaceへ反映しません。閉じたノートは一覧から再び開けます。

**⌘を押している間、左下の保存状態の上にショートカット一覧を表示します。** 保存状態・保存エラーはそのまま残し、⌘を離すか、別アプリへ移る・画面を非表示にすると案内を閉じます。入力を消費せず、IMEやクリップボードには触れません。監視対象は自アプリの修飾キー変更イベントだけです。

## 暗号化と保存先

保存先は `~/Library/Application Support/KOKONote2/notes-mac.db`、鍵はlogin Keychainのservice `com.kokonote.mac.v2`、account `content-key-v1` です。本文とタイトルをAES-256-GCMで個別に暗号化し、ノートIDとフィールド名を認証データに含めます。Keychain項目の同期は無効です。Keychainを失うとDB単独では復元できません。

既存DBがある場合に鍵が見つからなければ、新しい鍵を生成せず起動を中止します。復号に1件でも失敗した場合も起動を中止し、空データへの置き換えを防ぎます。鍵の平文ファイル、ハードコード鍵、パスワードの固定値、復号失敗時の平文保存への切り替えはありません。

SQLiteはWAL、synchronous=FULL、temp_store=MEMORYを使い、SQLには暗号化済みBLOBだけを渡します。時刻・ID・順序・閉じた状態・画面位置・選択範囲などのメタデータは平文です。時刻はUnix秒のREAL値で、モデルの時刻をミリ秒精度に統一します。

保存先は所有者を確認し、拡張ACLを除去したうえでディレクトリ0700・直接配下のファイル0600を設定します。プロセスのumaskは0077です。シンボリックリンクや想定外のサブディレクトリを拒否します。排他的flockで多重起動を防止します。

同じユーザー権限のマルウェア、管理者、画面取得、プロセスメモリ取得まで防げません。Keychainとメモリ中の鍵の保護はmacOSのセキュリティ境界に依存します。SwiftのString・Dataや編集コントロールの全コピーを確実にゼロ化できるとは保証しません。強制終了や停電で直前の未保存入力が失われる可能性があります。

開発ビルドの署名はad-hocです。再ビルドや別の場所からの起動で、macOSがKeychainへのアクセス確認を求める場合があります。許可が得られなければエラーとして扱い、暗号化を弱めて回避しません。Developer ID署名・公証・インストーラーは未実装です。

## 確認結果

自動テスト16件がこのMacで成功しました。日本語・絵文字、実Keychainの保存と再取得、AES-GCMの改ざん・別フィールド検知、実SQLiteの保存・再読込・失敗時ロールバック、保存世代、削除確認のロジック、閉じたノート、画面外復元、flock、権限、シンボリックリンク拒否、DB・WAL・バックアップの平文マーカー走査を含みます。

Keychainテストでは専用の `com.kokonote.tests.<UUID>` 項目だけを作り、終了時に削除します。実際のノート用の鍵を削除しません。Windows専用のDPAPI・WinFormsの未検証項目は、Mac版の成功によって検証済みとは扱いません。

AppKit統合テスト24項目も、このMacのネイティブウィンドウ上で成功しました。初期非表示、表示、メニューのアクション経路、500ms自動保存、連続入力中の保存抑制、日本語・絵文字、選択保持、Undo／Redo、複数タブ、Control+Tab、閉じたノート、検索、状態再読込、ホットキー競合を含みます。IMEは `setMarkedText`／`insertText` を使った合成した未確定・確定のテストです。実際の日本語入力ソースでの候補選択・再変換・物理ホットキー押下、描画の見た目、マルチモニター実機は未確認です。

UI操作ツールが接続タイムアウトしたため、画面の目視・スクリーンショット確認は行えていません。アプリのプロセス、実データ用DBの初期保存とBLOB型、ディレクトリ0700／ファイル0600は別途確認しました。起動中のアプリには確認用のノートを追加していません。

まとめて検証する場合:

```sh
bash scripts/verify-macos.sh
```

このスクリプトは依存解決、Releaseビルド、Swift Testing、`.app`生成、AppKit統合テスト、署名検証を行い、結果を `artifacts/macos-ui-test.json` に出力します。統合テストは明示的な `--self-test` モードで別の一時DBとテスト専用Keychain項目を使い、通常のノートを読み書きしません。他のアプリへキーを注入しません。通常起動にはテスト用の鍵やデータが混ざりません。

## このMacでの手動チェック

1. Control+Shift+Spaceで表示・非表示し、他アプリから前面復帰できることを確認する。
2. 日本語IMEで変換候補を開いたままホットキー・タブクリック・Control+Tabを試し、確定・取消後も文章が失われないことを確認する。
3. ⌘Nで数枚作り、⌘Rで名前を付ける。⌘Wで閉じ、⌘Lの検索から再び開く。
4. 入力後に「保存済み」を確認してメニューバーから終了し、もう一度起動して内容・選択・画面位置を確認する。
5. 保存先の鍵やDBは削除せず、障害試験は専用ユーザーまたはテストモードで実施する。

Windowsのメモリ・表示時間の目標はMac版では測定していません。統合テストの待機時間は測定値ではありません。
