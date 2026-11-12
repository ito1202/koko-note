# 性能目標と測定

2026-11-12時点のWindows実測値は**ありません**。開発環境はmacOS ARM64です。Windows GUIを実行していないため、目標を達成したとは報告しません。

| 指標 | 目標 | Release x64実測 |
|---|---|---|
| 非表示待機中CPU | ほぼ0% | 未測定 |
| 常駐メモリ | 30MB以下（ストレッチ） | 未測定 |
| Private Bytes | 実測して報告 | 未測定 |
| Working Set | 実測して報告 | 未測定 |
| 起動から使用可能まで | 実測して報告 | 未測定 |
| 初回表示 | 100ms以内を目標 | 未測定 |
| 2回目以降の表示 | 100ms以内 | 未測定 |
| ノート切り替え | 50ms以内 | 未測定 |

## Windows実測手順

1. Windows 11 x64でReleaseをpublishします。デバッガを接続しません。既に起動しているKOKO Noteはトレイから終了してください。
2. 使用端末のCPU、RAM、Windowsビルド、.NETバージョン、電源モード、モニター／DPI、DBのノート数・合計文字数を別に記録します。通常データと、10ノート×10,000字・100ノート×100,000字等を分けて測ります。
3. 次を実行します。出力先は新規ファイル名にしてください。既存ファイルは上書きしません。

```powershell
dotnet publish src/KOKONote.App -c Release -r win-x64 --self-contained false -o artifacts/win-x64
Start-Process .\artifacts\win-x64\KOKONote.App.exe -ArgumentList '--measure', "$PWD\artifacts\performance-01.json" -Wait
```

4. 起動後、**20秒以上非表示のまま操作せず待ちます**。初期化後5秒の待機に続く10秒間で、CPU時間差、Private Bytes、Working Setを1回だけサンプリングします。この間に表示・切り替え等があれば `ValidHiddenSample=false` になります。途中で終了した場合は `Idle=null` になります。
5. Ctrl+Shift+Spaceで初回表示します。以後、表示／非表示を10回以上繰り返し、Ctrl+Tab／Ctrl+Shift+Tabで複数ノートを切り替えます。他アプリに隠れた状態からの復帰も試します。初めて開くタブと既に開いたタブを比較してください。
6. トレイの「終了」で終了するとJSONが生成されます。ノート内容・タイトルは含みません。値が未記録ならnull／空配列のままです。

`StartupToReadyMs` はOSのProcess.StartTimeからDB初期化と初期保存の完了までです。`FirstShowMs` と `SubsequentShowMs` は表示処理入口から同期 `Update()` まで、`NoteSwitchMs` はタブDeselectingからエディター作成・Update・状態取得までです。キーの物理押下からメッセージ到着するまでの遅延、IME保留時間、DWMで実際に画面へ提示されるまでの遅延は含みません。物理キーからの体感遅延はWindows Performance Recorder等で別途確認します。トレイ表示とホットキー表示は同じ経路で測ります。

`Idle.CpuPercentOfMachine` は全論理コア容量を100%としたCPU使用率、`CpuPercentOfOneCore` は1コアを100%とした使用率です。メモリはbytesで出力します。30MB目標との比較では **Private BytesとWorking Setを別々に**記載し、必要ならMiB（÷1,048,576）も併記します。終了直前のメモリは編集コントロール作成後の参考値として別フィールドへ記録します。

同一条件で5プロセス以上測り、初回表示とウォーム表示の中央値・p95・最大、ノート切り替えの中央値・p95、メモリの範囲を報告してください。30MBを超えた場合もその値を記載し、達成したと扱わないでください。

## 実装上の方針と未解決点

通常稼働はイベント駆動です。500msタイマーは編集があったときだけ起動し、一度のTickで停止します。診断モード以外に待機サンプリングはありません。フォームと訪問済みタブのコントロールを保持し、通常のホットキー表示・ノート切り替えではDB読込や復号を行いません。

SQLiteのAsync APIは[内部で同期実行される](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/async)ため、暗号化・DB書込をUI外へ移しています。すべてのノートをメモリに読み込む方式と、Undoを保持する編集コントロールはデータ量とともにメモリを消費します。WinForms／.NET自体のコストもあるため30MBは未達となる可能性があります。大規模データでのメモリ、初回復号時間、検索中のUI応答性、保存要求のキューサイズはWindowsで評価が必要です。
