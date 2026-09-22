using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace Sf6Guard {
 public static class L {
  public static string Language {get;private set;}
  public static bool English {get{return Language!="zh-TW";}}
  static string preference;
  static readonly Dictionary<string,string> Messages=new Dictionary<string,string>{
   {"開局前檢查，結束後還原。","Check before playing. Restore when you're done."},
   {"正在讀取 USB…","Reading USB devices..."},
   {"尚未啟用","Not active"},
   {"先關閉 SF6，再開始防護。","Close SF6 before starting protection."},
   {"僅暫停異常輔助介面，可能影響多媒體鍵。","Pauses affected auxiliary interfaces; media keys may be unavailable."},
   {"防護並啟動 SF6","Protect & launch SF6"},
   {"檢查並啟動 SF6","Check & launch SF6"},
   {"按「檢查並啟動 SF6」重新確認並套用。","Click Check & launch SF6 to recheck and apply."},
   {"未發現可處理的異常，未更動 USB。","No eligible affected interfaces found. USB devices were left unchanged."},
   {"未啟動 SF6。","SF6 was not launched."},
   {"檢查完成，未更動 USB；正在啟動 SF6。","Check complete. USB devices unchanged; launching SF6."},
   {"重新檢查","Check again"},
   {"還原","Restore"},
   {"詳細資訊","Details"},
   {"請支持我繼續更新","Support future updates"},
   {"處理中","Working"},
   {"請等待檢查完成，或按「取消」。","Wait for the check to finish, or click Cancel."},
   {"未完成","Not completed"},
   {"請查看「詳細資訊」中的原因。","See Details for the error."},
   {"其他版本正在防護","Another version is active"},
   {"請先透過原版本完成還原。","Restore your devices using the original version first."},
   {"防護中","Protection active"},
   {"防護已就緒","Protection ready"},
   {"檢查中","Checking"},
   {"需要還原","Restore needed"},
   {"先結束 SF6，再按「還原」。","Close SF6, then click Restore."},
   {"已還原","Restored"},
   {"本次變更已還原。","Changes from this session have been restored."},
   {"未啟用防護","Protection not active"},
   {"沒有可自動處理的異常介面。","No eligible affected interfaces were found."},
   {"未啟用","Not active"},
   {"SF6 正在執行","SF6 is running"},
   {"先結束遊戲，再開始防護。","Close the game before starting protection."},
   {"請查看「詳細資訊」。","See Details."},
   {"取消","Cancel"},
   {"正在檢查目前 USB，不會停用裝置。","Checking connected USB devices. No devices will be disabled."},
   {"未找到可處理介面","No eligible interfaces found"},
   {"找到 ","Found "},
   {" 個可處理介面"," eligible interfaces"},
   {"按「防護並啟動 SF6」重新確認並套用。","Click Protect & launch SF6 to recheck and apply."},
   {"未套用防護；詳細結果可在下方查看。","Protection is not active. Open Details for the results."},
   {"準備防護","Preparing protection"},
   {"允許管理員權限後開始檢查。","Allow administrator access to begin the check."},
   {"沒有可自動處理的異常介面；可自行啟動 SF6。","No eligible affected interfaces found. You can launch SF6 yourself."},
   {"防護程序已結束，請查看詳細資訊。","The protection process has stopped. See Details."},
   {"準備逾時，未啟動 SF6。","Preparation timed out. SF6 was not launched."},
   {"已取消","Cancelled"},
   {"等待還原本次變更。","Waiting to restore changes from this session."},
   {"等待 SF6 啟動","Waiting for SF6"},
   {"3 分鐘未啟動會自動還原。","Devices will be restored if SF6 does not start within 3 minutes."},
   {"正在取消","Cancelling"},
   {"請稍候。","Please wait."},
   {"裝置與詳細資訊","Devices and details"},
   {"裝置／介面","Device / interface"},
   {"用途","Purpose"},
   {"檢查結果","Check result"},
   {"未查詢","Not queried"},
   {"USB 裝置","USB device"},
   {"保留／不自動停用","Kept / not disabled automatically"},
   {"僅在同一輔助介面連續兩次查詢過慢或逾時時停用。主要輸入與未知用途介面不自動停用。\r\n不能保證所有 USB 變更都不凍結；新插入或換埠的裝置需要結束遊戲後重新檢查。\r\n","Only eligible auxiliary interfaces with two slow or timed-out queries are disabled. Primary inputs and unknown interfaces are kept.\r\nThis cannot prevent every USB-related freeze. Close the game and recheck after reconnecting devices or changing ports.\r\n"},
   {"點選裝置查看識別資訊。","Select a device to view its identifiers."},
   {"最近訊息：","Last message: "},
   {"父裝置：","Parent: "},
   {"狀態：","State: "},
   {"開啟紀錄","Open logs"},
   {"使用說明","Usage guide"},
   {"尚未啟用 · 示意畫面","Not active · Preview"},
   {"USB 清單已就緒。此預覽使用模擬資料。","USB inventory is ready. This preview uses sample devices."},
   {" 個 USB 裝置  ·  "," USB devices  ·  "},
   {" 個 HID 介面"," HID interfaces"},
   {"多媒體鍵","Media keys"},
   {"系統控制鍵","System controls"},
   {"已知型號設定介面","Known-model settings"},
   {"已知型號輔助介面","Known-model auxiliary"},
   {"目標介面已變更，請重新檢查。","A target interface changed. Run the check again."},
   {"USB 清單已變更，請重新檢查。","The USB inventory changed. Run the check again."},
   {"請先關閉 SF6。","Close SF6 first."},
   {"目標不在本次清單中。","A target is not in this session's inventory."},
   {"正在準備防護","Preparing protection"},
   {"已取消防護。","Protection was cancelled."},
   {"SF6 已啟動，停止後續變更。","SF6 started. Further changes have stopped."},
   {"停用結果未通過驗證。","Could not verify that the interface was disabled."},
   {"防護範圍已變更。","The protected interfaces changed."},
   {"已暫停 ","Paused "},
   {" 個異常輔助介面"," affected auxiliary interfaces"},
   {"還原紀錄不符合允許範圍。","The restore record contains interfaces outside the allowed scope."},
   {"請先結束 SF6 再還原。","Close SF6 before restoring devices."},
   {"正在還原","Restoring"},
   {"SF6 已啟動，暫停還原。","SF6 started. Restoration has paused."},
   {"請將待還原裝置插回原埠。","Reconnect the device to its original port, then try restoring again."},
   {"介面用途已變更。","The interface's purpose changed."},
   {"介面狀態異常。","The interface is in an unexpected state."},
   {"還原驗證失敗。","Could not verify restoration."},
   {"本次變更已還原","Changes from this session have been restored"},
   {"無法讀取 USB 清單。","Could not read the USB inventory."},
   {"裝置列舉中斷。","Device enumeration was interrupted."},
   {"裝置識別讀取失敗。","Could not read a device identifier."},
   {"目標已移除或用途改變。","The target was removed or its purpose changed."},
   {"SF6 已啟動。","SF6 has started."},
   {"介面狀態已變更。","The interface state changed."},
   {"Windows 拒絕暫停介面：0x","Windows refused to pause the interface: 0x"},
   {"Windows 拒絕還原介面：0x","Windows refused to restore the interface: 0x"},
   {"無效工作階段。","Invalid session."},
   {"SF6 USB Guard 已經開啟。","SF6 USB Guard is already open."},
   {"防護操作需要系統管理員權限。請由主視窗按鈕啟動。","Protection requires administrator access. Start it from the main window."},
   {"已有防護工作執行中，請回到原本視窗。","Protection is already running. Return to the original window."},
   {"上次還有待還原的介面，請先按還原。","Some interfaces still need restoration. Click Restore first."},
   {"正在檢查 USB","Checking USB devices"},
   {"未找到可自動處理的異常介面","No eligible affected interfaces were found"},
   {"防護中；遊戲結束後自動還原","Protection is active. Devices will be restored after the game exits."},
   {" 個介面；遊戲結束後還原"," interfaces; they will be restored after the game exits"},
   {"裝置已變更；本局防護範圍不完整","Devices changed. Protection is incomplete for this session."},
   {"檢查已取消或 SF6 已啟動。","The check was cancelled or SF6 started."},
   {"HID 數量超出本版檢查上限。","The number of HID interfaces exceeds this version's limit."},
   {"檢查已取消、逾時或 SF6 已啟動。","The check was cancelled, timed out, or SF6 started."},
   {"保留","Kept"},
   {"將暫停：","Will pause: "},
   {"回覆過慢，保留","Slow response; kept"},
   {"正常","OK"},
   {"未確認，保留","Unconfirmed; kept"},
   {"原本停用，保留","Already disabled; kept"},
   {"狀態異常，保留","Unexpected state; kept"},
   {"已啟用","Enabled"},
   {"已停用","Disabled"},
   {"狀態不明","Unknown state"},
   {"狀態異常","Unexpected state"},
   {"回覆過慢","Slow response"},
   {"查詢逾時","Query timed out"},
   {"查詢失敗","Query failed"},
   {"未確認","Unconfirmed"}
  };
  static readonly string[] Keys=Messages.Keys.OrderByDescending(s=>s.Length).ToArray();
  public static void Load(string path,string[] args){
   preference=path;Language="en-US";
   try{if(File.Exists(path)){string saved=File.ReadAllText(path).Trim();if(saved=="en-US"||saved=="zh-TW")Set(saved);}}catch(IOException){}catch(UnauthorizedAccessException){}
   foreach(string arg in args)if(arg.StartsWith("--lang=",StringComparison.Ordinal))Set(arg.Substring(7));
  }
  public static void Set(string language){if(language!="en-US"&&language!="zh-TW")throw new ArgumentException("Unsupported language.");Language=language;}
  public static void Save(){
   if(String.IsNullOrEmpty(preference))return;
   Directory.CreateDirectory(Path.GetDirectoryName(preference));
   File.WriteAllText(preference,Language);
  }
  // UI renders canonical messages from both live operations and saved recovery records.
  // Longest-first matching keeps full sentences intact, including older journal text.
  public static string Text(string value){
   if(value==null)return "";if(!English)return value;
   var count=System.Text.RegularExpressions.Regex.Match(value,@"^(\d+) 個 USB 裝置  ·  (\d+) 個 HID 介面$");
   if(count.Success){string usb=count.Groups[1].Value,hid=count.Groups[2].Value;return usb+(usb=="1"?" USB device":" USB devices")+"  ·  "+hid+(hid=="1"?" HID interface":" HID interfaces");}
   string translated;if(Messages.TryGetValue(value,out translated))return translated;
   foreach(string key in Keys)if(value.Contains(key))value=value.Replace(key,Messages[key]);
   return value.Replace("；","; ");
  }
  public static string State(string state){return Text(state=="Enabled"?"已啟用":state=="Disabled"?"已停用":state=="Unknown"?"狀態不明":"狀態異常");}
  public static string Result(string kind){return Text(kind=="OK"?"正常":kind=="Slow"?"回覆過慢":kind=="Timeout"?"查詢逾時":kind=="Failed"?"查詢失敗":"未確認");}
 }
}
