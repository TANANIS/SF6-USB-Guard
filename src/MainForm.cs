using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Sf6Guard {
 public sealed class MainForm : Form {
  public const string SupportUrl="https://buymeacoffee.com/tananis";
  readonly bool preview;readonly WindowsBackend backend=new WindowsBackend();
  Label status,detail,summary;Button check,start,restore;System.Windows.Forms.Timer timer;
  List<Device> snapshot=new List<Device>();ScanReport report;
  bool busy;volatile bool cancel;string phase="";string lastError="";
  public MainForm(bool mock) {
   preview=mock;Text="SF6 USB Guard";ClientSize=new Size(620,440);FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;
   AutoScaleMode=AutoScaleMode.Dpi;Font=new Font("Microsoft JhengHei UI",10);BackColor=Color.FromArgb(246,248,250);StartPosition=FormStartPosition.CenterScreen;
   var header=new Panel{Dock=DockStyle.Top,Height=100,BackColor=Color.FromArgb(24,34,53)};Controls.Add(header);
   header.Controls.Add(new Label{Text="SF6 USB Guard",Font=new Font(Font.FontFamily,23,FontStyle.Bold),ForeColor=Color.White,AutoSize=true,Location=new Point(26,17)});
   header.Controls.Add(new Label{Text="開局前檢查，結束後還原。",ForeColor=Color.FromArgb(186,207,222),AutoSize=true,Location=new Point(29,65)});
   summary=LabelAt("正在讀取 USB…",28,119,564,24,10,Color.FromArgb(89,103,119));
   status=LabelAt("尚未啟用",26,159,567,38,20,Color.FromArgb(28,74,86));
   detail=LabelAt("先關閉 SF6，再開始防護。",28,210,564,44,10,Color.FromArgb(64,78,94));
   LabelAt("僅暫停異常輔助介面，可能影響多媒體鍵。",28,266,564,24,10,Color.FromArgb(115,82,27));
   start=ButtonAt("防護並啟動 SF6",28,301,246,46,StartGuard);start.BackColor=Color.FromArgb(25,104,105);start.ForeColor=Color.White;
   check=ButtonAt("重新檢查",286,301,138,46,CheckHealth);
   restore=ButtonAt("還原",436,301,156,46,Restore);
   ButtonAt("詳細資訊",28,371,126,36,ShowDetails);
   ButtonAt("請支持我繼續更新",414,371,178,36,delegate{Open(SupportUrl);});
   LabelAt("v0.2",170,380,237,24,9,Color.FromArgb(101,112,128));
   if(preview){ShowInTaskbar=false;Opacity=0;StartPosition=FormStartPosition.Manual;Location=new Point(-30000,-30000);}
   Shown+=async delegate{if(!preview)await CaptureInventory();};
   if(!preview){timer=new System.Windows.Forms.Timer{Interval=1000};timer.Tick+=delegate{Tick();};timer.Start();}
   FormClosing+=delegate(object sender,FormClosingEventArgs e){if(busy){e.Cancel=true;SetStatus("處理中","請等待檢查完成，或按「取消」。");}};
   FormClosed+=delegate{cancel=true;if(timer!=null)timer.Dispose();};
  }
  Label LabelAt(string text,int x,int y,int w,int h,int size,Color color){var l=new Label{Text=text,Location=new Point(x,y),Size=new Size(w,h),ForeColor=color,Font=new Font(Font.FontFamily,size,size>=20?FontStyle.Bold:FontStyle.Regular)};Controls.Add(l);return l;}
  Button ButtonAt(string text,int x,int y,int w,int h,Action action){var b=new Button{Text=text,Location=new Point(x,y),Size=new Size(w,h),BackColor=Color.White,FlatStyle=FlatStyle.Flat,Cursor=Cursors.Hand};b.FlatAppearance.BorderColor=Color.FromArgb(210,218,226);b.Click+=delegate{action();};Controls.Add(b);return b;}
  void Open(string target){try{Process.Start(new ProcessStartInfo(target){UseShellExecute=true});}catch(Exception ex){Failure(ex);}}
  void SetStatus(string title,string text){status.Text=title;detail.Text=text;}
  void Failure(Exception ex){lastError=ex is AggregateException?((AggregateException)ex).Flatten().InnerExceptions[0].Message:ex.Message;SetStatus("未完成",lastError.Length>65?"請查看「詳細資訊」中的原因。":lastError);}
  void Populate(List<Device> all){snapshot=all;summary.Text=all.Count(d=>Policy.Usb(d.Id))+" 個 USB 裝置  ·  "+all.Count(Policy.Hid)+" 個 HID 介面";}
  async Task CaptureInventory(){Busy(true);try{Populate(await Task.Run(()=>backend.List()));}catch(Exception ex){Failure(ex);}finally{Busy(false);}}
  void Tick(){
   try{
    var j=Program.Data.Load();bool owner=Program.GuardianActive(),game=backend.GameRunning();
    bool pending=j!=null&&j.Entries.Any(e=>e.RestoreNeeded);
    if(owner&&j==null){start.Enabled=check.Enabled=restore.Enabled=false;SetStatus("其他版本正在防護","請先透過原版本完成還原。" );return;}
    if(!busy){start.Enabled=!owner&&!pending&&!game;check.Enabled=!owner&&!pending&&!game;restore.Enabled=(owner||pending)&&!game;}
    if(j!=null&&(owner||pending||phase!=j.Phase)){
     if(j.Report!=null)report=j.Report;
     if(owner)SetStatus(j.Phase=="Active"?"防護中":j.Phase=="Ready"?"防護已就緒":j.Phase=="Scanning"?"檢查中":"處理中",Short(j.Message));
     else if(pending)SetStatus("需要還原","先結束 SF6，再按「還原」。");
     else if(j.Phase=="Restored")SetStatus("已還原","本次變更已還原。");
     else if(j.Phase=="NoChanges")SetStatus("未啟用防護","沒有可自動處理的異常介面。");
     else if(j.Phase=="Failed"){lastError=j.Message;SetStatus("未啟用",Short(j.Message));}
     phase=j.Phase;
    }else if(!busy&&game&&!owner&&!pending)SetStatus("SF6 正在執行","先結束遊戲，再開始防護。");
   }catch(Exception ex){start.Enabled=check.Enabled=false;if(!busy)restore.Enabled=false;Failure(ex);}
  }
  static string Short(string text){return (text??"").Length>65?"請查看「詳細資訊」。":text;}
  void Busy(bool value){busy=value;start.Enabled=check.Enabled=!value;restore.Enabled=value;restore.Text=value?"取消":"還原";if(value)cancel=false;else Tick();}
  async void CheckHealth(){
   if(busy||backend.GameRunning()||Program.GuardianActive())return;
   Busy(true);lastError="";SetStatus("檢查中","正在檢查目前 USB，不會停用裝置。");
   try{
    report=await Task.Run(()=>new Scanner(backend,new ProcessProbe()).Run(()=>cancel||Program.GuardianActive(),delegate(int done,int total){BeginInvoke(new Action(()=>SetStatus("檢查中",done+" / "+total+" 個 HID 介面")));}));
    Populate(report.Snapshot);
    int count=report.Targets().Count;
    SetStatus(count>0?"找到 "+count+" 個可處理介面":"未找到可處理介面",count>0?"按「防護並啟動 SF6」重新確認並套用。":"未套用防護；詳細結果可在下方查看。");
   }catch(Exception ex){Failure(ex);}finally{Busy(false);}
  }
  Process Elevated(string mode){return Process.Start(new ProcessStartInfo(Application.ExecutablePath,mode){UseShellExecute=true,Verb="runas",WorkingDirectory=Program.Root,WindowStyle=ProcessWindowStyle.Hidden});}
  async void StartGuard(){
   if(busy||backend.GameRunning()||Program.GuardianActive())return;
   Busy(true);lastError="";SetStatus("準備防護","允許管理員權限後開始檢查。");
   Process worker=null;string session=null;
   try{
    var before=Program.Data.Load();string old=before==null?null:before.Session;
    worker=Elevated("--guard");DateTime end=DateTime.UtcNow.AddSeconds(120);Journal ready=null;
    while(DateTime.UtcNow<end){
     await Task.Delay(200);Journal j=null;try{j=Program.Data.Load();}catch(IOException){}
     if(j!=null&&j.Session!=old){
      session=j.Session;if(j.Report!=null){report=j.Report;Populate(report.Snapshot);}
      if(cancel)File.WriteAllText(Program.Signal(session),"cancel");
      if(j.Phase=="Ready"){ready=j;break;}
      if(j.Phase=="NoChanges"){SetStatus("未啟用防護","沒有可自動處理的異常介面；可自行啟動 SF6。");return;}
      if(j.Phase=="Failed"||j.Phase=="RecoveryNeeded")throw new InvalidOperationException(j.Message);
      SetStatus(j.Phase=="Scanning"?"檢查中":"準備防護",Short(j.Message));
     }
     if(worker.HasExited)throw new InvalidOperationException("防護程序已結束，請查看詳細資訊。");
    }
    if(ready==null)throw new InvalidOperationException("準備逾時，未啟動 SF6。");
    if(cancel){File.WriteAllText(Program.Signal(ready.Session),"cancel");SetStatus("已取消","等待還原本次變更。");return;}
    Process.Start(new ProcessStartInfo("steam://rungameid/1364780"){UseShellExecute=true});
    SetStatus("等待 SF6 啟動","3 分鐘未啟動會自動還原。");
   }catch(Exception ex){Failure(ex);if(session!=null)try{File.WriteAllText(Program.Signal(session),"cancel");}catch{}}
   finally{if(worker!=null)worker.Dispose();Busy(false);}
  }
  async void Restore(){
   if(busy){cancel=true;SetStatus("正在取消","請稍候。");return;}
   if(backend.GameRunning())return;
   Busy(true);
   try{
    if(Program.GuardianActive()){var j=Program.Data.Load();if(j!=null)File.WriteAllText(Program.Signal(j.Session),"cancel");}
    else using(var p=Elevated("--restore")){await Task.Run(()=>p.WaitForExit());}
   }catch(Exception ex){Failure(ex);}finally{Busy(false);}
  }
  void ShowDetails(){
   using(var f=new Form{Text="裝置與詳細資訊",Size=new Size(860,620),StartPosition=FormStartPosition.CenterParent,Font=Font}){
    var grid=new ListView{Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,HideSelection=false};
    grid.Columns.Add("裝置／介面",330);grid.Columns.Add("用途",170);grid.Columns.Add("檢查結果",290);
    foreach(var d in snapshot){
     var item=report==null?null:report.Items.FirstOrDefault(x=>Policy.Same(x.Device,d));
     string result=item==null?"未查詢":item.Note;
     if(item!=null&&item.First!=null)result+=" ("+item.First.Kind+", "+item.First.Milliseconds+" ms)";
     var row=new ListViewItem(d.Name??d.Id);row.SubItems.Add(Policy.Usb(d.Id)?"USB 裝置":Policy.Allowed(d)?Policy.Auxiliary(d):"保留／不自動停用");row.SubItems.Add(result);row.Tag=d;grid.Items.Add(row);
    }
    var info=new TextBox{Dock=DockStyle.Bottom,Height=142,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Text="僅在同一輔助介面連續兩次查詢過慢或逾時時停用。主要輸入與未知用途介面不自動停用。\r\n不能保證所有 USB 變更都不凍結；新插入或換埠的裝置需要結束遊戲後重新檢查。\r\n"+(lastError==""?"點選裝置查看識別資訊。":"最近訊息："+lastError)};
    grid.SelectedIndexChanged+=delegate{if(grid.SelectedItems.Count>0){var d=(Device)grid.SelectedItems[0].Tag;info.Text=(d.Name??"")+"\r\n"+d.Id+"\r\n父裝置："+d.Parent+"\r\nUsage："+Policy.Usage(d)+"  狀態："+d.State+"\r\n最近訊息："+lastError;}};
    var footer=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=44};
    var log=new Button{Text="開啟紀錄",Width=120,Height=32};log.Click+=delegate{Directory.CreateDirectory(Program.Data.DirectoryPath);Open(Program.Data.DirectoryPath);};footer.Controls.Add(log);
    var guide=new Button{Text="使用說明",Width=120,Height=32};guide.Click+=delegate{Open(Path.Combine(Program.Root,"使用說明.md"));};footer.Controls.Add(guide);
    f.Controls.Add(grid);f.Controls.Add(info);f.Controls.Add(footer);f.ShowDialog(this);
   }
  }
  public void RenderPreview(string file){
   Populate(Tests.Devices());SetStatus("尚未啟用 · 示意畫面","USB 清單已就緒。此預覽使用模擬資料。");
   Show();Application.DoEvents();using(var bmp=new Bitmap(Width,Height)){DrawToBitmap(bmp,new Rectangle(0,0,Width,Height));bmp.Save(file);}Hide();
  }
  protected override bool ShowWithoutActivation{get{return preview;}}
 }
}
