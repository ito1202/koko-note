# 開発中に検出した再発防止項目

既存版の `../koko-note/docs/fault-table/fault-table.md` を作業前に読み、FT-0002（生成と復元を分離）、FT-0004（復号失敗時の原本保全）を適用しました。既存版のファイルを変更せず、新版の追加項目をここへ記録します。

| ID | 事象 | 根本原因 | 防止策 |
|---|---|---|---|
| V2-001 | NuGet復元がNU1903で失敗 | 初期のMicrosoft.Data.Sqlite 10.0.0が脆弱性のあるSQLitePCLRawを参照 | 初期復元から監査を有効にし、警告を抑止せず修正版へ更新。lockファイルとReleaseゲートを維持 |
| V2-002 | WAL存在検査が失敗 | 接続をOpenしただけでは読み取りスナップショットを保持せず、最終書込接続のcloseでWALがチェックポイント・削除された | 実SELECTを実行する読み取りトランザクションでWALを保持し、その存在をassertしてから走査 |
| V2-003 | WinForms解析WFO1000でビルド失敗 | Form上の実行時専用setterにデザイナーのシリアル化指定がなかった | 実行時専用プロパティにDesignerSerializationVisibility.Hiddenを明示。Windows向けクロスビルドも継続 |
| V2-004 | IME未確定文字が保存スナップショットへ混ざる可能性をレビューで検出 | TextChangedを変換中もWorkspaceに反映すると、既に待機中の非同期保存が未確定文字を撮影し得る | 変換中はエディター内だけに保持し、CompositionEndedで確定状態をWorkspaceへ反映。完了コールバックまで切り替え保留を解除しない |
| V2-005 | 通常の閉じる操作のキャンセルがOS終了まで拒否し得るとレビューで検出 | FormClosingのCloseReasonを区別しなかった | WindowsShutDownは独立して扱い、未保存判定と非同期保存の保留はWM_QUERYENDSESSIONで行う |
| V2-006 | MacのSwiftテストでXCTestが見つからなかった | 端末にはCommand Line ToolsのみがありXcode本体がなかった | 同梱のSwift Testingを使い、--disable-xctest --enable-swift-testingを指定。テスト用ディレクトリを作成してからSwiftPMの検証を始める |
| V2-007 | Macの保存・再読込でDateの厳密一致が失敗 | Dateの内部エポックとUnixエポック間の浮動小数変換で微小な誤差が生じた | モデルの作成・更新時刻をUnixミリ秒精度に正規化し、保存・再読込の一致をテストする |
| V2-008 | 実機のUI操作ツールが接続タイムアウトした | UI接続を確立できず、画面を観測できなかった | 自アプリ内のAppKit統合テストと実プロセス・DBの確認へ切り替え、目視・物理キー・実IME変換を未確認として明示する |
