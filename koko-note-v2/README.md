# KOKO Note 2.0

必要な瞬間だけ現れる、Windows 11 x64用の常駐メモ帳です。.NET 10／WinForms／Win32を使い、本文とタイトルをDPAPI CurrentUserで暗号化してSQLiteへ保存します。通信、テレメトリ、AI機能はありません。

既存の `../koko-note`（.NET 8／Avalonia版）を保全するため、このディレクトリに独立実装しました。旧版のデータを自動移行しません。

**macOSで直接動く版も追加しています。** Swift／AppKit版のビルド・起動・テスト方法は[macOS版の手順](docs/macos.md)を参照してください。Windows版とは暗号化方式とデータ保存先を分離しています。

## Windowsでビルド・テスト・起動

Windows 11 x64と.NET 10 SDKを用意し、このディレクトリでPowerShellを開きます。実行には.NET 10 **Windows Desktop Runtime x64**が必要です（SDKに同梱）。初回の開発用NuGet復元にはインターネットが必要ですが、アプリ実行時は完全オフラインです。

```powershell
dotnet restore KOKONote.sln --locked-mode
dotnet build KOKONote.sln -c Release --no-restore
dotnet test KOKONote.sln -c Release --no-build
dotnet run --project src/KOKONote.App -c Release --no-build
```

起動直後は画面を表示せず、トレイに常駐します。隠れているトレイアイコンも確認してください。起動中にもう一度実行した場合は通知して終了します。

配布用フォルダーを生成する場合:

```powershell
dotnet publish src/KOKONote.App -c Release -r win-x64 --self-contained false -o artifacts/win-x64
.\artifacts\win-x64\KOKONote.App.exe
```

SQLiteのネイティブDLLを含め、出力フォルダー全体を扱ってください。単一exe配布、インストーラー、署名、自動起動登録はMVPの対象外です。

## 操作

| 操作 | キー／手順 |
|---|---|
| 表示／前面復帰／非表示 | Ctrl + Shift + Space |
| 新規ノート | Ctrl + N |
| 次／前のタブ | Ctrl + Tab / Ctrl + Shift + Tab |
| 即時保存・保存再試行 | Ctrl + S |
| ノート一覧・タイトル／本文検索 | Ctrl + L |
| 名前変更 | Ctrl + R（F2も有効） |
| タブを閉じる（削除しない） | Ctrl + W |
| 閉じたノートを再び開く | 一覧で選択してEnter |
| コピー／切り取り／貼り付け | Ctrl + C / X / V |
| Undo／Redo | Ctrl + Z / Ctrl + Shift + Z（RedoはCtrl + Yも有効） |
| 完全削除 | 「ノート」→「ノートを削除…」→確認（既定は「いいえ」） |
| アプリ終了 | トレイ→「終了」 |

画面の×ボタンは非表示にするだけです。ホットキーはMVPでは固定です。競合時は警告し、トレイの「設定」から登録を再試行できます。他アプリに隠れている場合もホットキーで前面復帰します。ダイアログ表示中はホットキーによる非表示を抑制します。

**Ctrlを押している間、左下の保存状態の上にショートカット一覧を表示します。** キーを離すか、別アプリへ移る・ウィンドウを非表示にすると一覧を閉じます。保存状態・保存エラーは一覧表示中も残ります。Mac版は⌘キーで同じ案内を表示します。

IME変換中のホットキー・終了操作は変換終了まで保留します。変換中のタブクリックはキャンセルし、Ctrl+Tabは変換終了後に処理します。Ctrl+N/S/W/L/R、Ctrl+Shift+ZとF2は変換中に抑制します。両OSの対応表と案内表示の条件は[共通操作仕様](docs/shortcuts.md)を参照してください。

## 保存と復旧

保存先は `%LOCALAPPDATA%\KOKONote\notes.db` です。同じディレクトリに `notes.db-wal`、`notes.db-shm`、`.instance.lock` が作成される場合があります。

入力停止から約500ms後、および切り替え・非表示・終了時に保存します。ステータスは「未保存」「保存中…」「保存済み」「保存失敗」を区別します。保存に失敗しても内容はメモリに保持し、Ctrl+Sで再試行できます。終了時の失敗は「再試行／キャンセル」で処理し、保存せず終了する選択肢は用意しません。

DPAPI復号・DB検証・アクセス権設定に失敗した場合は、既存ノートを新しい空データで置き換えず起動を中止します。元のWindowsユーザー／端末とDPAPIキー、別プロセスの起動、権限、空き容量を確認してください。DBを削除しての復旧は勧めません。

バックアップはアプリをトレイから正常終了した後にディレクトリ全体をコピーしてください。起動中の `notes.db` 単独コピーはWAL内の保存内容を欠く場合があります。内部のSQLite Backup APIはテスト済みですが、ユーザー向けバックアップUIは未実装です。別端末や別ユーザーへの単純コピーで復号できるとは限りません。

強制終了、停電、OSの強制シャットダウンによる直前の未保存入力まで保証しません。同一ユーザー権限のマルウェアからDPAPIや表示中の文字列を保護することもできません。詳細は[セキュリティ](docs/security.md)を参照してください。

## 検証状況

macOS ARM64上の.NET 10 SDKでWindows x64アプリをクロスビルドし、Coreの自動テストを実行しています。Windows専用テストは別途コンパイルを確認していますが、GUI・DPAPIの実行、IMEの実操作、Release x64の性能測定は未実施です。Windows上で検証済みの製品とは扱わないでください。

- [構成・保存制御](docs/architecture.md)
- [セキュリティ・復元制約](docs/security.md)
- [性能目標・実測手順](docs/performance.md)
- [設計判断・依存関係](docs/decisions.md)
- [必須15項目の検証一覧](docs/testing.md)
- [ビルド・テストの実行記録](docs/validation.md)
- [追加ファイル一覧](docs/changes.md)

macOS/LinuxでCoreテストを実行する場合も同じソリューションをrestore/build/testできます。Windowsではテストプロジェクトが `net10.0-windows` になり、GUIテストも含まれます。Windows専用部分を他OSからコンパイルだけ確認するには `dotnet build KOKONote.sln -p:WindowsUiTests=true` を使用してください。
