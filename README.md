# SF6 USB Guard

在《快打旋風 6》開局前檢查 USB HID 查詢，暫停符合條件的異常輔助介面，遊戲結束後還原。

![主畫面（模擬資料）](docs/preview.png)

## v0.2

- **自動抓取目前 USB**：開啟視窗先讀取裝置清單，不切換 USB。
- **不限單一接收器**：檢查目前 USB 裝置下的 HID 介面；連續兩次查詢過慢或逾時，且符合用途範圍時，才會暫停。
- **保留主要輸入**：不自動停用鍵盤、滑鼠、控制器、USB 父裝置、Hub 或未知用途介面。
- **精簡介面**：主畫面顯示狀態與操作，裝置清單、查詢結果及原因放在「詳細資訊」。
- **支持更新**：按「請支持我繼續更新」開啟 [Buy Me a Coffee](https://buymeacoffee.com/tananis)，不影響任何功能。

這是實驗性緩解工具，並非所有 USB 斷線的通用修復。它不會阻止遊戲接收 USB 變更通知；實際遊戲防凍結效果尚未驗證。

## 使用

1. **Code → Download ZIP**，完整解壓縮，保留兩個 EXE 與說明檔在同一資料夾。
2. 關閉 SF6，開啟 SF6-USB-Guard.exe。
3. 按「防護並啟動 SF6」，允許管理員權限。程式重新檢查目前裝置，成功暫停目標後才啟動 Steam 遊戲。
4. 第一次請使用離線訓練模式。防護期間可能暫停音量／多媒體鍵、系統控制鍵或已知型號的設定功能。
5. 結束遊戲後約 8 秒自動還原。若沒有可處理的異常介面，會顯示未啟用；此時可自行啟動 SF6。

「重新檢查」只查詢、不停用。舊版仍在防護時，先讓舊版完成還原並關閉主視窗，再使用新版。更多行為、限制與復原方式見[使用說明](使用說明.md)。

## 建置與驗證

Windows x64、.NET Framework，以及 Visual Studio C++ Build Tools / Windows SDK：

```powershell
.\build.ps1
$test = Start-Process .\SF6-USB-Guard.exe -ArgumentList '--self-test', 'test-results.txt' -Wait -PassThru
Get-Content .\test-results.txt
if ($test.ExitCode -ne 0) { throw 'Self-tests failed' }
```

模擬測試不操作真實 USB。[驗證結果](驗證結果.txt)包含動態範圍、查詢確認、主要輸入排除、取消、回復、競態與舊版還原相容性。兩個執行檔的雜湊見 [SHA256.txt](SHA256.txt)。

## 資料與實作

完整 C# / C++ 原始碼在 src/。執行時的清單、日誌和還原紀錄保存在同資料夾的 Data/，不納入 Git，也不會上傳。防護或待還原期間請保留該資料夾。

不注入遊戲、不替換 DLL、不安裝驅動、不設自動開機啟動。程式沒有數位簽章。

用途判定依 [Microsoft HID collection 說明](https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/top-level-collections)與裝置識別；停用使用 [CM_Disable_DevNode](https://learn.microsoft.com/en-us/windows/win32/api/cfgmgr32/nf-cfgmgr32-cm_disable_devnode)，不設定永久停用旗標。