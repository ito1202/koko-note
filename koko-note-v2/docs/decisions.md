# 設計判断

| 判断 | 理由・制約 |
|---|---|
| 既存版と別の `koko-note-v2` | 既存のAvalonia実装・ユーザー変更を破壊しない。名前・データ移行を推測しない |
| .NET 10 / WinForms / x64 | ユーザー指定。WebView・Electron・クラウドは不使用 |
| Core内に小さいSQLite実装 | UIと永続化を分離しつつプロジェクト数・抽象化を増やさない |
| ORMなし | Microsoft.Data.Sqliteだけでトランザクションとパラメーターを明示 |
| DPAPIをP/Invoke | CurrentUserの保護を利用し、ProtectedData用NuGetを追加しない |
| タイトルも暗号化 | ノート名にも機密情報が入る。平文 `Title` 列は持たない |
| plaintextの全文索引なし | メモリ上で大文字小文字を無視した部分一致検索。大規模データには未最適化 |
| WAL / synchronous FULL | 原子性・耐久性を優先。SQLiteに渡る文字列自体を事前暗号化 |
| Semaphoreと編集世代 | 保存の追い越しと、未保存の編集を保存済みにする誤表示を防ぐ |
| 一度作ったフォームとエディターを保持 | 高速表示とUndoを優先。メモリ30MBはストレッチ目標のまま |
| Ctrl+WはIsArchived | 閉じる操作を削除と分離。閉じたノートは一覧に残す |
| 復号失敗で起動中止 | 鍵違い・破損を空ノート扱いして上書きしない。自動復旧DBを作らない |
| ホットキー固定・登録再試行のみ | MVPの設定は小さく維持。カスタマイズUIは将来課題 |
| フォントはPlainEditorで設定 | 将来の設定差し替え地点。本文に書式を埋め込まない |
| 未保存時のOS終了をいったん拒否 | UIスレッドで長時間同期保存せず、未保存のまま静かに終了しない。強制終了は保証外 |

## 依存関係とライセンス

直接の本番NuGet依存はMicrosoft.Data.Sqliteのみです。ライセンスは実際に復元したパッケージのnuspecから確認しています。全推移依存とハッシュは `packages*.lock.json` に記録します。

| パッケージ | バージョン | 用途 | ライセンス |
|---|---|---|---|
| Microsoft.Data.Sqlite | 10.0.15 | 直接依存、SQLiteアクセス | MIT |
| Microsoft.Data.Sqlite.Core | 10.0.15 | ADO.NETプロバイダー | MIT |
| SQLitePCLRaw.bundle_e_sqlite3 / core / provider.e_sqlite3 / lib.e_sqlite3 | 2.1.13 | SQLiteバインディング・ネイティブバイナリ | Apache-2.0（SQLiteエンジン自体はpublic domain） |
| Microsoft.NET.Test.Sdk | 17.14.1 | テスト専用、VSTest | MIT |
| Microsoft.CodeCoverage / Microsoft.TestPlatform.TestHost / Microsoft.TestPlatform.ObjectModel | 17.14.1 | テスト基盤の推移依存 | MIT |
| Newtonsoft.Json | 13.0.3 | テストホストの推移依存 | MIT |
| xunit / xunit.core / xunit.assert / xunit.extensibility.core / xunit.extensibility.execution | 2.9.3 | テスト専用、xUnit v2 | Apache-2.0 |
| xunit.analyzers | 1.18.0 | テストの静的解析 | Apache-2.0 |
| xunit.abstractions | 2.0.3 | テスト専用、xUnit抽象化 | Apache-2.0 |
| xunit.runner.visualstudio | 3.1.4 | テスト専用、VSTestアダプター | Apache-2.0 |

Windows Desktop参照パック、.NET SDK、.NET実行環境はプラットフォーム依存です。配布するSQLiteバイナリのライセンス・NOTICEを含む第三者通知は `THIRD-PARTY-NOTICES.txt` にまとめます。テスト用ライブラリはアプリのpublish出力に含めません。

Microsoft.Data.Sqlite 10.0.0での初回復元は既知脆弱性のNU1903で失敗しました。監査を無効にせず10.0.15へ更新しました。Windows専用テストと他OSのテストでTFMが変わるため、テストのlockファイルを `packages.windows.lock.json` と `packages.lock.json` に分離しています。

## MVPに含めないもの

ホットキーカスタマイズ、フォント設定画面、自動起動登録、インストーラー、コード署名、バックアップUI、別端末向けエクスポート、旧版からの移行、ノートのドラッグ並べ替え、クリップボード監視、入力注入、外部送信、AI機能は実装していません。

## 残課題

Windows実機でのGUI／IME／DPAPI／ACL検証と性能測定が最優先です。その結果に応じてタブ数・大容量本文のメモリ、検索のUI負荷、保存リクエストの集約、Windowsの前面制御制約、ログオフ時の動作を改善します。既存データを変換する移行が必要になった場合は、Backup APIによるバックアップと移行失敗のテストを追加してからバージョンを上げます。

## macOS対応の追加判断

ユーザーがこのMacで直接動く版を希望したため、Windows版とは別にSwift／AppKit版を追加しました。既存Avalonia版への切り戻しや、WinFormsの互換レイヤーは使いません。OSネイティブUIと追加パッケージ不要の構成を優先し、Workspace・保存制御・SQLite処理はSwiftで独立実装し、同様の整合性テストを用意しました。両実装の仕様がずれないよう、機能変更時には両方のテストが必要です。

DPAPIはmacOSで利用できないため、Mac版はCryptoKitのAES-256-GCMとlogin Keychainを使用します。データと鍵はWindows版と共有・自動移行しません。Mac版の本番依存はOS付属のAppKit、Carbon、Security、CryptoKit、SQLiteのみで、外部Swift Package依存はありません。Swift Testingはツールチェーンに同梱される開発専用ライブラリです。

作業ブランチは `codex/koko-note-v2` です。ローカルの未コミット変更を保持してmainから切り替えました。この文書の記載だけでGitHubへpush済みとは扱いません。
