# 必須テストとWindows受け入れ手順

Windows GUIを実行できない環境で実行成功と扱わないことを原則にします。以下の「Core成功」はmacOS上の実SQLiteとテスト用暗号化での確認であり、DPAPIの代用結果をDPAPI成功とは呼びません。最新の件数・コマンドは[validation.md](validation.md)に記載します。

| # | 要件 | 自動テスト／検証 | 現環境の分類 |
|---|---|---|---|
| 1 | 新規・保存・再読込 | CreateUnicodeAndEmojiReload | Core成功 |
| 2 | 日本語・絵文字 | 同上、RealDpapiDatabaseRoundTrip | Core成功、実DPAPI未実行 |
| 3 | 連続入力の自動保存 | RepeatedEditsAndRapidSwitchesPersistLatestGeneration / DebounceWaitsUntilContinuousTypingStops | Core成功、GUIの500ms未実行 |
| 4 | 高速切り替え | 同上、TabsWrapInBothDirections | 整合性成功、時間未測定 |
| 5 | 保存失敗の保持 | FailedSaveRetainsGenerationAndRetryWritesLatest / EncryptionFailureLeavesCommittedContentAndDirtyMemoryIntact | Core成功、終了ダイアログ未実行 |
| 6 | DPAPI | DpapiRoundTripAndTamperAndBinding / RealDpapiDatabaseRoundTrip | Windows専用、skip |
| 7 | 改ざん・復号失敗 | CorruptionFailsClosedWithoutReplacingCiphertext、DPAPI改ざん試験 | Core成功、実DPAPI未実行 |
| 8 | SQLite transaction失敗 | TransactionFailureRollsBackEveryNoteAndRetainsMemory（2件目のUPDATEでRAISE ABORT） | Core成功 |
| 9 | 削除の確認 | DeleteRequiresConfirmationAndCloseDoesNotDelete | Core成功、GUI確認画面未実行 |
| 10 | 再起動後の状態 | SessionAndClosedNotesSurviveRestart / HiddenWindowPreservesSelectionAndNativeUndo | Core成功、実画面未実行 |
| 11 | ホットキー登録失敗 | HotkeyConflictIsReportedAndUnregisterReleasesKey / HotkeyTransitions | 判断成功、Win32未実行 |
| 12 | 多重起動 | InstanceLeaseRejectsDuplicateAndReleasesOnDispose | Mutexロジック成功、Windows別プロセス未実行 |
| 13 | 日本語IME | 以下の実操作シナリオ | Windows手動、未実行 |
| 14 | 画面外位置復元 | OffscreenAndOversizedWindowsRestoreInsideWorkArea / NegativeMonitorCoordinatesArePreserved | Core成功、マルチDPI実画面未実行 |
| 15 | 非表示CPU | --measureの10秒非表示サンプル | Windows実測、未実行 |

## Windows自動テスト

実デスクトップにログインしたWindows 11 x64環境で、他のKOKO Noteを終了して実行します。

```powershell
dotnet restore KOKONote.sln --locked-mode
dotnet build KOKONote.sln -c Release --no-restore
dotnet test KOKONote.sln -c Release --no-build --logger 'trx;LogFileName=windows.trx' --results-directory TestResults
```

WindowsではDPAPI3件とGUI6件が加わります（他OSの26件からDPAPIのskipがなくなり、計32件）。GUIテストはSTAスレッドと実メッセージループを使います。IME境界の1件は自アプリへ合成した開始・終了通知で保留処理を検証するもので、実IME変換の検証ではありません。ホットキーはRegisterHotKeyで競合させ、他プロセスへの入力注入は行いません。既に外部アプリが同じキーを使っている場合は競合の確認のみになり、キーの解放後再登録までの分岐は実行されません。デスクトップのないCI環境でGUIの成功を推定しないでください。

## Windows手動チェック（全て未実行）

実データを使わない専用Windowsユーザーで検証し、結果・OSビルド・IME・DPIを記録します。

1. 起動時にウィンドウが見えずトレイに常駐する。ホットキーで表示・前面復帰・非表示、×で非表示、トレイから再表示・終了が動く。別exe起動が2個目の常駐を作らない。
2. 日本語・改行・タブ・結合文字・絵文字を入力し、500ms後に保存済みとなる。1秒以内の連続入力中は保存を連発せず、停止後に最後の内容を保存する。
3. 新規、タブ10枚、Ctrl+Tab／逆順、名前変更、検索、Ctrl+W、閉じたノートの再オープン、削除「いいえ」「はい」、Undo／Redo、プレーンテキスト貼り付けを確認する。
4. Microsoft日本語IMEで「にほんご」を入力し変換候補を開いたまま、ホットキー、タブクリック、Ctrl+Tab、Ctrl+N/S/W/L/R、Ctrl+Shift+Z、F2、×、トレイ終了を試す。未確定文字が消えず、切り替え・非表示は定義どおり保留／抑制される。確定・取消の両方を試す。候補一覧、予測入力、再変換、複数文節、英数直接入力も確認する。
5. 長文でカーソルとスクロールを動かし、タブ切り替え・非表示・再起動後に復元される。再起動ではUndo履歴は復元しない。
6. 100%と150%／200%のモニター間で移動する。片方を外して再起動してもタイトルバーが作業領域内に表示される。最大化状態からの再起動も確認する。
7. 専用ユーザーで `icacls "$env:LOCALAPPDATA\KOKONote"` と各ファイルの権限を確認し、現在ユーザーとSYSTEMに限定されることを検証する。別ユーザーからアクセス・DPAPI復号できないことを確認する。
8. 別のSQLiteツールで書き込みロックを保持する等、安全に解除できる障害を発生させる。編集後に保存失敗が明示され、内容が残る。トレイ終了で再試行／キャンセルが選べ、キャンセル後に編集できる。障害解除・Ctrl+Sで保存できる。ディスク容量不足のテストは隔離した試験環境で行う。
9. 正常終了後にDB全体をバックアップし、試験用コピーの暗号文を改ざん／別ユーザーへコピーする。起動が失敗し、暗号文を空の内容に置き換えない。元の試験バックアップを戻すと元のユーザーで開ける。
10. 未保存入力直後にWindows終了を要求し、いったん終了が拒否され保存される動作を確認する。IME変換中・保存ロック中も確認する。強制終了試験は直前の未保存文字が失われる可能性を前提にし、次回起動で最後にコミットしたDBが読めることを確認する。
11. [性能手順](performance.md)でRelease x64の全指標を採取し、目標との差を記録する。
