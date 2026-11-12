# 実行記録（2026-11-12）

## 環境

- OS: macOS 26.6 / Darwin ARM64（Windows GUIなし）
- 既存SDK: .NET 8.0.124。既存版は変更していません。
- 検証SDK: .NET SDK 10.0.404 / Runtime 10.0.15、`/private/tmp/koko-dotnet`
- NuGetキャッシュ: `/private/tmp/koko-nuget`
- 対象: `KOKONote.sln`、Windows 11 x64用WinFormsアプリ

## 最終結果

| ゲート | 結果 |
|---|---|
| restore（通常・Windowsテスト構成ともlocked-mode） | pass |
| Debugビルド | pass、警告0・エラー0 |
| Debug自動テスト | pass、23成功・3skip・0失敗 |
| ReleaseアプリのWindows x64クロスビルド | pass、警告0・エラー0 |
| Release Windows専用GUIテストのクロスコンパイル | pass、警告0・エラー0。実行はしていない |
| Release Core自動テスト | pass、23成功・3skip・0失敗（計26） |
| Windows x64 publish（framework-dependent） | pass |
| format | skipped（必須ゲート外） |
| Windows GUI / 実DPAPI / ACL / IME実操作 | 未実行 |
| Release x64のPrivate Bytes・Working Set・起動／表示／切り替え時間・CPU | 未測定 |

Releaseテスト結果はローカルの `TestResults/macos-release.trx` にあります（生成物としてGit対象外）。Windows専用の3件は明示的なskipです。GUI4件はWindows用TFMでのみコンパイルされ、macOSで成功した26件の総数には含めていません。Windowsでの全30件の実行結果はまだありません。

## Phaseごとの確認

| 段階 | 実行結果 |
|---|---|
| Phase 1（常駐骨格） | 初回restoreがSQLite推移依存のNU1903で失敗。10.0.15へ更新して解消。SDK導入・復元待ちの間にCore実装も進めたため、Phase 1単独の成功ログはない |
| Phase 1 + 2（保存・タブ統合） | クロスビルドとテストを実行。WAL検査の失敗を修正後、21成功・3skip |
| Phase 3（検索・状態・起動制御） | クロスビルドとテストを実行。22成功・3skip。Windows専用テストもコンパイル |
| Phase 4（障害テスト・レビュー・文書） | 最終ゲートとRelease検証を実行。23成功・3skip。Windows実操作と性能は未実行として分類 |

最初のサンドボックス内ビルドは出力なく停止したため、ビルドサーバー無効の実行と、許可された環境外プロセスで再確認しました。その後、quality-gatesの通常コマンドでも成功しています。初期のテスト補助クラスの名前衝突とWinFormsのWFO1000コンパイルエラーも修正済みです。

## 再現コマンド

Windows専用テストを含むクロスコンパイル（実行しない）:

```sh
dotnet restore KOKONote.sln --locked-mode -p:WindowsUiTests=true
dotnet build KOKONote.sln -c Release --no-restore --disable-build-servers -m:1 -p:WindowsUiTests=true
dotnet publish src/KOKONote.App -c Release -r win-x64 --self-contained false --no-restore -o artifacts/win-x64
```

macOSで実行可能なCoreテスト:

```sh
dotnet restore KOKONote.sln --locked-mode
dotnet build KOKONote.sln -c Release --no-restore --disable-build-servers -m:1
dotnet test KOKONote.sln -c Release --no-build --logger 'trx;LogFileName=macos-release.trx' --results-directory TestResults
```

quality-gatesスキル指定の `scripts/run_quality_gates.sh KOKONote.sln` も実行し、restore/build/testが全てpassでした。SDKパス・NuGetキャッシュ・テレメトリ無効化は環境変数で指定しています。

`file` により `artifacts/win-x64/KOKONote.App.exe` と `e_sqlite3.dll` がいずれも **PE32+ x86-64 Windows** であることを確認しました。publish出力にライセンス・第三者通知とSQLiteのネイティブDLLが含まれ、テスト用ライブラリが含まれないことも確認しています。Windows上での起動成功を示す検証ではありません。

## セキュリティ確認

実SQLiteファイルで、トランザクション途中の失敗、暗号化前の失敗、暗号文改ざん、未知スキーマ、古い世代の保存、並行編集を試験しました。DB・保持したWAL・SHM・バックアップの平文マーカー走査と、同じ保護キーによるバックアップ再読込も成功しています。実DPAPIでの同じ検査はWindows待ちです。

本番ソースを検索し、ネットワークAPI、WebView、入力注入API、クリップボード監視、本文・タイトルのログ出力がないことを確認しました。復号例外と保存例外はUIで固定メッセージへ変換します。これは第三者による監査やWindows実動作の保証ではありません。

新たな再発防止項目は[障害表](fault-table.md)に5件記録しました。既存版の障害表・実装・未コミット変更は変更していません。

## macOSネイティブ版の追加検証

ユーザーの希望により、macOSへ直接インストールして動くSwift／AppKit版を追加しました。作業ブランチは `codex/koko-note-v2`、検証OSはmacOS 26.6.2 ARM64、Swiftは6.1.2です。Windows版の未実施項目は、そのまま未実施です。

| ゲート | 結果 |
|---|---|
| Swift Package resolve（外部パッケージなし） | pass |
| Mac版Releaseビルド | pass |
| Swift Testing（Debug / Release） | 各16件pass、skip・失敗なし |
| Release .appのAppKit統合テスト | 20項目pass |
| ad-hoc署名検証 | pass |
| 実アプリ起動・常駐プロセス | 確認済み |
| 実アプリ初期DB・BLOB型・所有者限定権限 | 確認済み |
| Windowsソリューションのrestore/build/test再実行 | pass、23件成功・3件skip |
| format | skipped |
| GUIの目視・物理ホットキー・実IME変換 | UI接続タイムアウトにより未確認 |
| Mac版のメモリ・表示遅延・CPU | 未測定 |

Macの統合テストは自アプリのNSWindow／NSTextViewを使い、実Keychain・暗号化SQLiteとの接続まで確認しています。IMEの未確定文字は合成した `setMarkedText` 呼び出しであり、日本語IMEの実際の候補選択操作を検証した結果ではありません。画面操作ツールは2回とも接続タイムアウトしたため、スクリーンショットや見た目の成功を報告しません。

テスト用Keychain項目とDBは本番用と分離し、終了時に削除しています。通常のノート用Keychain項目、既存Avalonia版、Windows版のデータには触れていません。実アプリの保存先には初期ノートだけを作成し、テスト用の本文は追加していません。

再現コマンドは `bash scripts/verify-macos.sh` です。結果JSONは `artifacts/macos-ui-test.json`。実装と操作方法は[macos.md](macos.md)、追加の再発防止項目V2-006〜008は[障害表](fault-table.md)に記録しました。

## Ctrl／⌘押下中のショートカット案内

保存状態とは別の行へ案内を追加しました。Macは自アプリのflagsChanged、Windowsは自アプリのメッセージフィルターで修飾キーを観測し、イベントは消費しません。キー解放・非表示・フォーカス離脱で解除し、保存状態・保存失敗の表示を上書きしません。

- Mac: Releaseビルド、Swift Testing 16件、AppKit統合テスト24項目、署名検証がpass。追加4項目は案内の表示、保存状態の保持、キー解放、非表示時の解除です。
- Windows: Ctrl案内と追加GUIテストをReleaseでクロスコンパイル済み。Windows用の総数は31件ですが、Windows GUIテストは未実行です。
- 通常のquality-gatesもrestore/build/testがpass（23成功・DPAPI 3skip）。formatはskipped。
- UIツールの接続制約は継続しており、物理キー操作と目視確認を成功扱いにはしません。

変更ファイルは[一覧](changes.md)の「修飾キーでのショートカット案内」に記載しています。今回新たな再発防止項目はありません。

## Windows操作仕様の統一

Windowsの名前変更をCtrl+R、RedoをCtrl+Shift+Zにも対応させ、F2・Ctrl+Yは互換操作として維持しました。案内と削除メニュー表記も揃え、[共通操作仕様](shortcuts.md)へ整理しました。

追加したWindows専用テストはCtrl+Rのメニュー登録と、Ctrl+Shift+ZがネイティブRedoへ届いて本文が復元されることを確認する内容です。テストのnull許容のコンパイルエラーを修正した後、Windows用の全32件をReleaseでクロスコンパイルし、警告0・エラー0でした。Windows上でのテスト実行は未実施です。

通常のquality-gatesはrestore/build/testがpass、23成功・DPAPI 3skip・0失敗、formatはskipped。Windows x64のpublishと配布ZIPも更新しています。Mac側のコードは今回変更しておらず、Macテストの再実行はしていません。今回新たな再発防止項目はありません。
