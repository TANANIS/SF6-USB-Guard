using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Sf6Guard {
 public sealed class MainForm : Form {
  readonly bool preview; readonly WindowsBackend backend=new WindowsBackend();
  Label status,detail; ListView list; Button check,start,restore; System.Windows.Forms.Timer timer;
  bool busy;string phase="";
  public MainForm(bool mock) {
   preview=mock;Text="SF6 USB Guard · 已知接收器防護";ClientSize=new Size(820,710);MinimumSize=Size;MaximumSize=Size;
   AutoScaleMode=AutoScaleMode.Dpi;Font=new Font("Microsoft JhengHei UI",10);BackColor=Color.FromArgb(244,246,249);StartPosition=FormStartPosition.CenterScreen;
   try{Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch{}
   var header=new Panel {Location=new Point(0,0),Size=new Size(820,112),BackColor=Color.FromArgb(24,34,53)};Controls.Add(header);
   var title=new Label {Text="SF6 USB Guard",Font=new Font(Font.FontFamily,24,FontStyle.Bold),ForeColor=Color.White,AutoSize=true,Location=new Point(28,20)};header.Controls.Add(title);
   header.Controls.Add(new Label {Text="針對已知接收器查詢逾時的可逆防護",AutoSize=true,ForeColor=Color.FromArgb(184,207,227),Location=new Point(31,76)});
   AddLabel("已知接收器防護  ·  初版，遊戲效果待驗證",28,130,760,25,FontStyle.Bold,Color.FromArgb(120,79,16));
   status=AddLabel("尚未啟用",28,168,760,34,FontStyle.Bold,Color.FromArgb(27,70,91));status.Font=new Font(Font.FontFamily,17,FontStyle.Bold);
   detail=AddLabel("開啟本程式不會自動切換 USB。請先關閉遊戲，再啟用防護。",29,210,758,55,FontStyle.Regular,Color.FromArgb(65,76,91));
   var box=new Panel {Location=new Point(28,273),Size=new Size(764,98),BackColor=Color.FromArgb(255,243,218)};Controls.Add(box);
   box.Controls.Add(new Label {Text="啟用時會暫停下列 4 個輔助介面，保留鍵盤主要輸入。\r\n音量／多媒體鍵、系統控制键及廠商設定功能可能暫停，遊戲結束後還原。\r\n這是針對已定位問題的防護，無法保證所有 USB 插拔都零卡頓。",Location=new Point(14,12),Size=new Size(735,78),ForeColor=Color.FromArgb(99,69,23)});
   list=new ListView {Location=new Point(28,387),Size=new Size(764,145),View=View.Details,FullRowSelect=true,GridLines=false,HeaderStyle=ColumnHeaderStyle.Nonclickable};
   list.Columns.Add("限定的接收器輔助介面",260);list.Columns.Add("識別",220);list.Columns.Add("目前狀態",265);Controls.Add(list);
   check=ButtonAt("只檢查接收器",28,551,190,delegate{CheckHealth();});
   start=ButtonAt("啟用防護並啟動 SF6",230,551,330,delegate{StartGuard();});start.BackColor=Color.FromArgb(29,105,108);start.ForeColor=Color.White;
   restore=ButtonAt("還原／結束防護",573,551,219,delegate{Restore();});
   ButtonAt("開啟紀錄資料夾",28,616,190,delegate{Directory.CreateDirectory(Program.Data.DirectoryPath);Process.Start(new ProcessStartInfo(Program.Data.DirectoryPath){UseShellExecute=true});});
   AddLabel("關閉視窗後，已啟動的防護仍會等遊戲結束再還原。\r\n不自動開機啟動、不修改遊戲；接收器換埠後請重新啟用防護。",235,616,557,64,FontStyle.Regular,Color.FromArgb(87,99,116));
   if(preview){ShowInTaskbar=false;Opacity=0;StartPosition=FormStartPosition.Manual;Location=new Point(-30000,-30000);}
   Shown+=delegate{if(!preview){RefreshInventory();Tick();}};
   if(!preview){timer=new System.Windows.Forms.Timer {Interval=1500};timer.Tick+=delegate{Tick();};timer.Start();}
   FormClosed+=delegate{if(timer!=null)timer.Dispose();};
  }
  Label AddLabel(string s,int x,int y,int w,int h,FontStyle style,Color color) {var l=new Label{Text=s,Location=new Point(x,y),Size=new Size(w,h),ForeColor=color,Font=new Font(Font.FontFamily,10,style)};Controls.Add(l);return l;}
  Button ButtonAt(string s,int x,int y,int w,Action click) {var b=new Button{Text=s,Location=new Point(x,y),Size=new Size(w,46),FlatStyle=FlatStyle.Flat,BackColor=Color.White,Cursor=Cursors.Hand};b.FlatAppearance.BorderColor=Color.FromArgb(204,212,222);b.Click+=delegate{click();};Controls.Add(b);return b;}
  void SetStatus(string a,string b){status.Text=a;detail.Text=b;}
  void RefreshInventory() {
   try {Populate(backend.List());}catch(Exception ex){SetStatus("無法讀取裝置",ex.Message);}
  }
  void Populate(List<Device> all) {
   list.Items.Clear();for(int i=0;i<4;i++) {
    var d=all.FirstOrDefault(x=>Policy.Allowed(x) && Policy.Role(x.Id)==i);
    var item=new ListViewItem(Policy.Labels[i]);item.SubItems.Add(Policy.Roles[i]);item.SubItems.Add(d==null?"未找到":d.State=="Enabled"?"已啟用":d.State=="Disabled"?"已暫停":"狀態異常");list.Items.Add(item);
   }
  }
  void Tick() {
   if(busy)return;
   try {
    var j=Program.Data.Load();bool owner=Program.GuardianActive();bool game=backend.GameRunning();
    bool pending=j!=null && j.Entries.Any(e=>e.RestoreNeeded);
    start.Enabled=!owner && !pending && !game;check.Enabled=!owner && !game;restore.Enabled=(owner||pending) && !game;
    if(j!=null && (owner||pending||phase!=j.Phase)) {
     if(owner)SetStatus(j.Phase=="Active"?"防護中":j.Phase=="Ready"?"防護已準備":"處理中",j.Message);
     else if(pending)SetStatus("有介面待還原",j.Message+"。請關閉遊戲並按還原。");
     else if(j.Phase=="Restored")SetStatus("已還原",j.Message);
     else if(j.Phase=="Failed")SetStatus("未啟用",j.Message);
     if(phase!=j.Phase){RefreshInventory();phase=j.Phase;}
    } else if(game && !owner && !pending)SetStatus("遊戲已開啟，尚未防護","請先關閉遊戲，再由這裡啟用防護；對戰中不切換裝置。");
   }catch(Exception ex){start.Enabled=false;check.Enabled=false;restore.Enabled=false;SetStatus("請檢查還原紀錄",ex.Message+"。不要刪除 Data 資料夾。");}
  }
  void Busy(bool value){busy=value;check.Enabled=start.Enabled=restore.Enabled=!value;if(!value)Tick();}
  async void CheckHealth() {
   if(backend.GameRunning()||Program.GuardianActive())return;
   Busy(true);SetStatus("檢查中","只查詢已知接收器；不停用或重新啟動裝置。");
   try {
    var devices=backend.List().Where(Policy.Allowed).ToList();Policy.Validate(devices);Populate(devices);
    if(devices.Any(d=>d.State=="Disabled"))throw new InvalidOperationException("有介面已停用，無法完成健康檢查。請先處理還原或既有設定。");
    string exe=Path.Combine(Program.Root,"HidProbe.exe");
    string text=await Task.Run(delegate {
     var p=Process.Start(new ProcessStartInfo(exe){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true});
     var read=p.StandardOutput.ReadToEndAsync();var err=p.StandardError.ReadToEndAsync();
     DateTime deadline=DateTime.UtcNow.AddSeconds(15);
     while(!p.WaitForExit(200)) {
      if(backend.GameRunning()||DateTime.UtcNow>=deadline){p.Kill();p.WaitForExit();p.Dispose();throw new InvalidOperationException("遊戲已啟動或檢查逾時，已結束檢查工具。");}
     }
     string output=read.Result+err.Result;p.Dispose();return output;
    });
    Program.Data.Log("Probe:\r\n"+text);
    int success=0;foreach(string line in text.Split('\n'))if(line.Contains("ok=1") && line.IndexOf("vid_36b0&pid_3002",StringComparison.OrdinalIgnoreCase)>=0)success++;
    if(text.Contains("PENDING_") || success!=4)SetStatus("接收器查詢異常","已記錄查詢結果。可於開遊戲前啟用四介面防護，效果仍需在訓練模式確認。");
    else SetStatus("目前回覆正常","四個輔助介面均成功回覆；本次檢查不保證後續不再逾時。尚未啟用防護。");
   }catch(Exception ex){SetStatus("檢查未完成",ex.Message);}finally{Busy(false);}
  }
  Process Elevated(string mode) {return Process.Start(new ProcessStartInfo(Application.ExecutablePath,mode){UseShellExecute=true,Verb="runas",WorkingDirectory=Program.Root,WindowStyle=ProcessWindowStyle.Hidden});}
  async void StartGuard() {
   if(backend.GameRunning()||Program.GuardianActive())return;
   Busy(true);SetStatus("準備防護","Windows 將要求管理員權限；成功後才啟動遊戲。");
   Process worker=null;
   try {
    var before=Program.Data.Load();string old=before==null?null:before.Session;
    worker=Elevated("--guard");DateTime deadline=DateTime.UtcNow.AddSeconds(90);Journal ready=null;
    while(DateTime.UtcNow<deadline) {
     await Task.Delay(200);Journal j=null;try{j=Program.Data.Load();}catch(IOException){}
     if(j!=null && j.Session!=old) {
      if(j.Phase=="Ready"){ready=j;break;}
      if(j.Phase=="Failed"||j.Phase=="RecoveryNeeded")throw new InvalidOperationException(j.Message);
     }
     if(worker.HasExited)throw new InvalidOperationException("防護程序已結束，請查看紀錄。");
    }
    if(ready==null)throw new InvalidOperationException("準備尚未完成；不啟動遊戲，請查看防護狀態。");
    Process.Start(new ProcessStartInfo("steam://rungameid/1364780"){UseShellExecute=true});
    SetStatus("已送出啟動要求","等待 Steam 啟動遊戲；3 分鐘未啟動會自動還原。");
   }catch(System.ComponentModel.Win32Exception ex){SetStatus("未啟動",ex.NativeErrorCode==1223?"已取消管理員權限要求。":ex.Message);CancelWaiting();}
    catch(Exception ex){SetStatus("未啟動",ex.Message);CancelWaiting();}
   finally{if(worker!=null)worker.Dispose();Busy(false);RefreshInventory();}
  }
  void CancelWaiting(){try{var j=Program.Data.Load();if(j!=null && Program.GuardianActive())File.WriteAllText(Program.Signal(j.Session),"cancel");}catch{}}
  async void Restore() {
   if(backend.GameRunning()){SetStatus("請先結束遊戲","還原會造成裝置變更，遊戲結束後才進行。");return;}
   Busy(true);
   try {
    if(Program.GuardianActive()){CancelWaiting();SetStatus("正在結束防護","守護程序將還原本次變更的介面。");}
    else {using(var p=Elevated("--restore")){await Task.Run(delegate{p.WaitForExit();});}RefreshInventory();}
   }catch(Exception ex){SetStatus("還原未完成",ex.Message);}finally{Busy(false);}
  }
  public void RenderPreview(string file) {
   Populate(Tests.Devices());SetStatus("尚未啟用 · 示意畫面","四個目標介面已辨識。此預覽使用模擬資料，沒有變更任何 USB 裝置。");
   Show();Application.DoEvents();
   using(var bmp=new Bitmap(Width,Height)){DrawToBitmap(bmp,new Rectangle(0,0,Width,Height));bmp.Save(file);}Hide();
  }
  protected override bool ShowWithoutActivation {get{return preview;}}
 }
}
