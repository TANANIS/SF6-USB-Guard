# SF6 USB Guard

針對《快打旋風 6》因 USB 裝置變更觸發 HID 掃描而凍結的實驗性 Windows 工具。

**目前為 0.1 實驗版：26 項模擬測試通過，實際停用介面後的遊戲效果尚未驗證。它不是所有 USB 斷線的通用修復。**

## 原理與適用範圍

本次診斷發現，USB 變更後，遊戲會等待另一顆 `VID_36B0 / PID_3002` 接收器的 HID 產品名稱查詢。此工具在遊戲啟動前暫停該接收器的四個輔助 HID 介面，嘗試讓後續掃描避開這些介面；遊戲結束後自動還原本次變更。

此版本綁定特定接收器的完整父裝置識別碼、介面與 HID Usage，屬於個別診斷的實作，其他電腦可能不符合條件。完整限定範圍見[使用說明](使用說明.md)。主要鍵盤、滑鼠介面與 USB Hub 不在停用清單內；**多媒體鍵、系統控制鍵與廠商設定功能可能在防護期間暫停**。

## 使用

1. 使用 GitHub 的 **Code → Download ZIP** 下載並完整解壓縮，將兩個 EXE 保留於同一資料夾。
2. 先關閉 SF6，再開啟 `SF6-USB-Guard.exe`。開啟視窗本身只讀取裝置清單。
3. 按「啟用防護並啟動 SF6」，允許管理員權限。只有四個介面全部暫停並驗證成功，才會透過 Steam 啟動遊戲。
4. 第一次請在離線訓練模式確認操作與 USB 變更後的表現。「防護中」代表指定介面已暫停，並非遊戲效果已獲驗證。
5. 關閉遊戲後約 8 秒還原；若 3 分鐘內未啟動遊戲，也會還原。中斷後可重新開啟程式，於遊戲關閉時按「還原／結束防護」。

防護或待還原期間，請保留程式資料夾與 `Data/`，並參閱[完整使用與復原說明](使用說明.md)。程式為免安裝 Windows x64 版本，使用 .NET Framework，沒有數位簽章。

## 建置與測試

需要 Windows、.NET Framework C# 編譯器、Visual Studio C++ Build Tools 與 Windows SDK。

```powershell
.\build.ps1
$test = Start-Process .\SF6-USB-Guard.exe -ArgumentList '--self-test', 'test-results.txt' -Wait -PassThru
Get-Content .\test-results.txt
if ($test.ExitCode -ne 0) { throw 'Self-tests failed' }
```

模擬測試使用假裝置後端，不停用真實 USB。已編譯檔案的測試紀錄見[驗證結果](驗證結果.txt)，雜湊見 [SHA256.txt](SHA256.txt)。目前未驗證真實停用及遊戲內效果。

## 檔案與資料

- `src/`、`build.ps1`：C# 主程式、C++ 查詢工具、建置腳本與模擬測試。
- `SF6-USB-Guard.exe`、`HidProbe.exe`：可直接使用的已編譯檔案。
- `Data/`：執行時的本機還原狀態與日誌，不納入版本控制。

沒有遊戲注入、DLL 替換、驅動安裝、自動開機啟動或背景上傳。儲存庫不包含個人診斷快照、記憶體傾印、原始裝置清單或本機還原紀錄。
