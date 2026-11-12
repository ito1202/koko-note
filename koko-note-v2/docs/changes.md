# 追加・変更したファイル

すべて `koko-note-v2/` 配下への新規追加です。既存の `koko-note/` と作業開始時から存在した未コミット変更は変更していません。

## ビルド・運用

- [.gitignore](../.gitignore)
- [global.json](../global.json)
- [Directory.Build.props](../Directory.Build.props)
- [KOKONote.sln](../KOKONote.sln)
- [README.md](../README.md)
- [THIRD-PARTY-NOTICES.txt](../THIRD-PARTY-NOTICES.txt)
- [licenses/Apache-2.0.txt](../licenses/Apache-2.0.txt)
- [licenses/Microsoft-MIT.txt](../licenses/Microsoft-MIT.txt)
- [scripts/verify.ps1](../scripts/verify.ps1)

## Windowsアプリ

- [src/KOKONote.App/KOKONote.App.csproj](../src/KOKONote.App/KOKONote.App.csproj)
- [src/KOKONote.App/Program.cs](../src/KOKONote.App/Program.cs)
- [src/KOKONote.App/ResidentContext.cs](../src/KOKONote.App/ResidentContext.cs)
- [src/KOKONote.App/Native.cs](../src/KOKONote.App/Native.cs)
- [src/KOKONote.App/MainForm.cs](../src/KOKONote.App/MainForm.cs)
- [src/KOKONote.App/PlainEditor.cs](../src/KOKONote.App/PlainEditor.cs)
- [src/KOKONote.App/NotePicker.cs](../src/KOKONote.App/NotePicker.cs)
- [src/KOKONote.App/SecureDirectory.cs](../src/KOKONote.App/SecureDirectory.cs)
- [src/KOKONote.App/PerformanceRecorder.cs](../src/KOKONote.App/PerformanceRecorder.cs)
- [src/KOKONote.App/AssemblyInfo.cs](../src/KOKONote.App/AssemblyInfo.cs)
- [src/KOKONote.App/app.manifest](../src/KOKONote.App/app.manifest)
- [src/KOKONote.App/packages.lock.json](../src/KOKONote.App/packages.lock.json)

## ロジック・永続化

- [src/KOKONote.Core/KOKONote.Core.csproj](../src/KOKONote.Core/KOKONote.Core.csproj)
- [src/KOKONote.Core/Models.cs](../src/KOKONote.Core/Models.cs)
- [src/KOKONote.Core/Workspace.cs](../src/KOKONote.Core/Workspace.cs)
- [src/KOKONote.Core/SqliteNoteStore.cs](../src/KOKONote.Core/SqliteNoteStore.cs)
- [src/KOKONote.Core/DpapiProtector.cs](../src/KOKONote.Core/DpapiProtector.cs)
- [src/KOKONote.Core/InteractionPolicy.cs](../src/KOKONote.Core/InteractionPolicy.cs)
- [src/KOKONote.Core/InstanceLease.cs](../src/KOKONote.Core/InstanceLease.cs)
- [src/KOKONote.Core/WindowGeometry.cs](../src/KOKONote.Core/WindowGeometry.cs)
- [src/KOKONote.Core/packages.lock.json](../src/KOKONote.Core/packages.lock.json)

## テスト

- [tests/KOKONote.Tests/KOKONote.Tests.csproj](../tests/KOKONote.Tests/KOKONote.Tests.csproj)
- [tests/KOKONote.Tests/InteractionTests.cs](../tests/KOKONote.Tests/InteractionTests.cs)
- [tests/KOKONote.Tests/PersistenceTests.cs](../tests/KOKONote.Tests/PersistenceTests.cs)
- [tests/KOKONote.Tests/SaveCoordinatorTests.cs](../tests/KOKONote.Tests/SaveCoordinatorTests.cs)
- [tests/KOKONote.Tests/SecurityAndGeometryTests.cs](../tests/KOKONote.Tests/SecurityAndGeometryTests.cs)
- [tests/KOKONote.Tests/TestSupport.cs](../tests/KOKONote.Tests/TestSupport.cs)
- [tests/KOKONote.Tests/WindowsUiTests.cs](../tests/KOKONote.Tests/WindowsUiTests.cs)
- [tests/KOKONote.Tests/packages.lock.json](../tests/KOKONote.Tests/packages.lock.json)
- [tests/KOKONote.Tests/packages.windows.lock.json](../tests/KOKONote.Tests/packages.windows.lock.json)

## 文書

- [docs/architecture.md](architecture.md)
- [docs/security.md](security.md)
- [docs/performance.md](performance.md)
- [docs/decisions.md](decisions.md)
- [docs/testing.md](testing.md)
- [docs/validation.md](validation.md)
- [docs/fault-table.md](fault-table.md)
- [docs/changes.md](changes.md)

## macOS対応の追加

- [docs/macos.md](macos.md)
- [scripts/build-macos.sh](../scripts/build-macos.sh)
- [scripts/verify-macos.sh](../scripts/verify-macos.sh)
- [macos/Package.swift](../macos/Package.swift)
- [macos/Info.plist](../macos/Info.plist)
- [macos/Sources/KOKONoteMacCore/Workspace.swift](../macos/Sources/KOKONoteMacCore/Workspace.swift)
- [macos/Sources/KOKONoteMacCore/Encryption.swift](../macos/Sources/KOKONoteMacCore/Encryption.swift)
- [macos/Sources/KOKONoteMacCore/SqliteStore.swift](../macos/Sources/KOKONoteMacCore/SqliteStore.swift)
- [macos/Sources/KOKONoteMacCore/SecureLocation.swift](../macos/Sources/KOKONoteMacCore/SecureLocation.swift)
- [macos/Sources/KOKONoteMac/main.swift](../macos/Sources/KOKONoteMac/main.swift)
- [macos/Sources/KOKONoteMac/AppDelegate.swift](../macos/Sources/KOKONoteMac/AppDelegate.swift)
- [macos/Sources/KOKONoteMac/EditorWindow.swift](../macos/Sources/KOKONoteMac/EditorWindow.swift)
- [macos/Sources/KOKONoteMac/Hotkey.swift](../macos/Sources/KOKONoteMac/Hotkey.swift)
- [macos/Sources/KOKONoteMac/PlainEditor.swift](../macos/Sources/KOKONoteMac/PlainEditor.swift)
- [macos/Sources/KOKONoteMac/NotePicker.swift](../macos/Sources/KOKONoteMac/NotePicker.swift)
- [macos/Sources/KOKONoteMac/DesktopSelfTest.swift](../macos/Sources/KOKONoteMac/DesktopSelfTest.swift)
- [macos/Tests/KOKONoteMacTests/CoreTests.swift](../macos/Tests/KOKONoteMacTests/CoreTests.swift)

Mac版追加時にREADME、.gitignore、docs/decisions.md、docs/validation.md、docs/fault-table.md、本一覧も更新しています。Mac版追加時点ではWindows版のC#ソースは変更していません。

`bin/`、`obj/`、`TestResults/`、`artifacts/` はGit対象外の生成物です。Windows x64の配布フォルダーは `artifacts/win-x64/`、圧縮版は `artifacts/KOKONote-2.0-win-x64.zip` に生成しています。

## 修飾キーでのショートカット案内

- `src/KOKONote.App/MainForm.cs`: Ctrl押下中の案内・キー解放／非表示／フォーカス離脱で解除。入力は消費せず、保存状態は別表示。
- `tests/KOKONote.Tests/WindowsUiTests.cs`: キーを消費しないこと、保存状態・選択・失敗表示を保持するWindows専用テストを追加。
- `macos/Sources/KOKONoteMac/EditorWindow.swift`: 自アプリのflagsChangedを使い⌘押下中に案内。監視の解除とフォーカス離脱も処理。
- `macos/Sources/KOKONoteMac/DesktopSelfTest.swift`: 案内表示・保存中の状態保持・キー解放・非表示時の解除を追加し、24項目へ更新。
- `README.md`、`docs/macos.md`、`docs/testing.md`、`docs/validation.md`、本一覧: 操作・検証記録を更新。

この変更では新たな再発防止項目はありません。V2-004のIME保護とV2-008の実機検証範囲の区別を継続しています。

## Windows操作仕様の統一

- `src/KOKONote.App/MainForm.cs`: 名前変更Ctrl+R（F2互換）、Redo Ctrl+Shift+Z（Ctrl+Y互換）、案内と削除メニュー名をMac版に対応。追加キーもIME変換中は抑制。
- `tests/KOKONote.Tests/WindowsUiTests.cs`: Ctrl+Rのメニュー登録とCtrl+Shift+ZによるネイティブRedoのテストを追加（Windows専用・この環境ではコンパイル確認のみ）。
- `docs/shortcuts.md`: OS別のキー対応・案内表示条件・IME・削除操作の共通仕様を新規作成。
- `README.md`、`docs/macos.md`、`docs/testing.md`、`docs/validation.md`、本一覧: 仕様と検証範囲を更新。

今回は新たな再発防止項目はありません。既存のV2-004とV2-008を適用しています。
