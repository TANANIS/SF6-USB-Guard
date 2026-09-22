using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Sf6Guard {
 sealed class Card : Panel {
  public Card(){DoubleBuffered=true;BackColor=Color.Transparent;}
  protected override void OnPaint(PaintEventArgs e){
   e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
   var r=new Rectangle(1,1,Width-3,Height-3);int d=24;
   using(var path=new GraphicsPath()){
    path.AddArc(r.X,r.Y,d,d,180,90);path.AddArc(r.Right-d,r.Y,d,d,270,90);
    path.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);path.AddArc(r.X,r.Bottom-d,d,d,90,90);path.CloseFigure();
    using(var fill=new SolidBrush(Color.White))e.Graphics.FillPath(fill,path);
    using(var border=new Pen(Color.FromArgb(224,231,237)))e.Graphics.DrawPath(border,path);
   }
   base.OnPaint(e);
  }
 }
 public sealed class MainForm : Form {
  public const string SupportUrl="https://buymeacoffee.com/tananis";
  readonly bool preview;readonly WindowsBackend backend=new WindowsBackend();
  Label status,detail,summary;Panel indicator;Button check,start,restore;ComboBox language;System.Windows.Forms.Timer timer;
  List<Device> snapshot=new List<Device>();ScanReport report;
  bool busy;volatile bool cancel;string phase="";string lastError="";
  public MainForm(bool mock) {
   preview=mock;Text="SF6 USB Guard";ClientSize=new Size(760,500);FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;
   AutoScaleMode=AutoScaleMode.Dpi;Font=new Font("Microsoft JhengHei UI",10);BackColor=Color.FromArgb(241,245,248);StartPosition=FormStartPosition.CenterScreen;
   var header=new Panel{Location=new Point(0,0),Size=new Size(760,104),BackColor=Color.FromArgb(21,34,48)};Controls.Add(header);
   var mark=new Label{Text="SF",Location=new Point(28,26),Size=new Size(46,46),TextAlign=ContentAlignment.MiddleCenter,BackColor=Color.FromArgb(25,128,125),ForeColor=Color.White,Font=new Font("Segoe UI",17,FontStyle.Bold)};header.Controls.Add(mark);
   header.Controls.Add(new Label{Text="SF6 USB Guard",Location=new Point(89,21),AutoSize=true,ForeColor=Color.White,Font=new Font("Segoe UI",23,FontStyle.Bold)});
   var caption=new Label{Location=new Point(92,66),Size=new Size(470,24),ForeColor=Color.FromArgb(169,192,208),Font=new Font(Font.FontFamily,9)};SetText(caption,"開局前檢查，結束後還原。");header.Controls.Add(caption);
   language=new ComboBox{Location=new Point(589,33),Size=new Size(143,28),DropDownStyle=ComboBoxStyle.DropDownList,FlatStyle=FlatStyle.Flat,Font=new Font("Segoe UI",10)};
   language.Items.AddRange(new object[]{"English (US)","繁體中文"});language.SelectedIndex=L.English?0:1;header.Controls.Add(language);
   language.SelectedIndexChanged+=delegate{
    L.Set(language.SelectedIndex==0?"en-US":"zh-TW");TranslateControls(this);Populate(snapshot);
    try{L.Save();}catch(Exception ex){Failure(ex);}
   };
   var card=new Card{Location=new Point(24,124),Size=new Size(712,204)};Controls.Add(card);
   summary=LabelAt(card,"正在讀取 USB…",22,20,667,24,10,Color.FromArgb(105,121,136));
   indicator=new Panel{Location=new Point(24,63),Size=new Size(5,31),BackColor=Color.FromArgb(128,145,158)};card.Controls.Add(indicator);
   status=LabelAt(card,"尚未啟用",42,57,645,38,20,Color.FromArgb(26,52,68));
   detail=LabelAt(card,"先關閉 SF6，再開始防護。",23,108,662,49,10,Color.FromArgb(71,91,106));
   var separator=new Panel{Location=new Point(24,161),Size=new Size(663,1),BackColor=Color.FromArgb(232,237,241)};card.Controls.Add(separator);
   LabelAt(card,"僅暫停異常輔助介面，可能影響多媒體鍵。",24,174,664,23,9,Color.FromArgb(113,101,70));
   start=ButtonAt(this,"檢查並啟動 SF6",24,350,342,52,StartGuard);start.BackColor=Color.FromArgb(22,117,114);start.ForeColor=Color.White;start.Font=new Font(Font.FontFamily,11,FontStyle.Bold);start.FlatAppearance.BorderSize=0;
   check=ButtonAt(this,"重新檢查",380,350,171,52,CheckHealth);
   restore=ButtonAt(this,"還原",565,350,171,52,Restore);
   var details=ButtonAt(this,"詳細資訊",24,437,130,35,ShowDetails);details.BackColor=BackColor;details.FlatAppearance.BorderSize=0;
   LabelAt(this,"v0.2",171,447,100,20,9,Color.FromArgb(123,138,152));
   var support=ButtonAt(this,"請支持我繼續更新",526,437,210,35,delegate{Open(SupportUrl);});support.BackColor=BackColor;support.FlatAppearance.BorderSize=0;support.ForeColor=Color.FromArgb(22,117,114);
   if(preview){ShowInTaskbar=false;Opacity=0;StartPosition=FormStartPosition.Manual;Location=new Point(-30000,-30000);}
   Shown+=async delegate{if(!preview)await CaptureInventory();};
   if(!preview){timer=new System.Windows.Forms.Timer{Interval=1000};timer.Tick+=delegate{Tick();};timer.Start();}
   FormClosing+=delegate(object sender,FormClosingEventArgs e){if(busy){e.Cancel=true;SetStatus("處理中","請等待檢查完成，或按「取消」。");}};
   FormClosed+=delegate{cancel=true;if(timer!=null)timer.Dispose();};
  }
  static void SetText(Control control,string text){control.Tag=text;control.Text=L.Text(text);}
  static void TranslateControls(Control parent){foreach(Control control in parent.Controls){if(control.Tag is string)control.Text=L.Text((string)control.Tag);TranslateControls(control);}}
  Label LabelAt(Control parent,string text,int x,int y,int w,int h,int size,Color color){var l=new Label{Location=new Point(x,y),Size=new Size(w,h),ForeColor=color,BackColor=Color.Transparent,Font=new Font(Font.FontFamily,size,size>=20?FontStyle.Bold:FontStyle.Regular)};SetText(l,text);parent.Controls.Add(l);return l;}
  Button ButtonAt(Control parent,string text,int x,int y,int w,int h,Action action){var b=new Button{Location=new Point(x,y),Size=new Size(w,h),BackColor=Color.White,ForeColor=Color.FromArgb(45,67,81),FlatStyle=FlatStyle.Flat,Cursor=Cursors.Hand,UseMnemonic=false};SetText(b,text);b.FlatAppearance.BorderColor=Color.FromArgb(211,222,230);b.Click+=delegate{action();};parent.Controls.Add(b);return b;}
  void Open(string target){try{Process.Start(new ProcessStartInfo(target){UseShellExecute=true});}catch(Exception ex){Failure(ex);}}
  void SetStatus(string title,string text){SetText(status,title);SetText(detail,text);indicator.BackColor=title=="防護中"||title=="防護已就緒"?Color.FromArgb(22,150,126):title=="未完成"||title=="需要還原"?Color.FromArgb(199,134,55):Color.FromArgb(128,145,158);}
  void Failure(Exception ex){lastError=ex is AggregateException?((AggregateException)ex).Flatten().InnerExceptions[0].Message:ex.Message;SetStatus("未完成",L.Text(lastError).Length>100?"請查看「詳細資訊」中的原因。":lastError);}
  void Populate(List<Device> all){snapshot=all;SetText(summary,all.Count(d=>Policy.Usb(d.Id))+" 個 USB 裝置  ·  "+all.Count(Policy.Hid)+" 個 HID 介面");}
  async Task CaptureInventory(){Busy(true);try{Populate(await Task.Run(()=>backend.List()));}catch(Exception ex){Failure(ex);}finally{Busy(false);}}
  void Tick(){
   try{
    var j=Program.Data.Load();bool owner=Program.GuardianActive(),game=backend.GameRunning();
    bool pending=j!=null&&j.Entries.Any(e=>e.RestoreNeeded);
    if(owner&&j==null){start.Enabled=check.Enabled=restore.Enabled=false;SetStatus("其他版本正在防護","請先透過原版本完成還原。");return;}
    if(!busy){start.Enabled=!owner&&!pending&&!game;check.Enabled=!owner&&!pending&&!game;restore.Enabled=(owner||pending)&&!game;}
    if(j!=null&&(owner||pending||phase!=j.Phase)){
     if(j.Report!=null)report=j.Report;
     if(owner)SetStatus(j.Phase=="Active"?"防護中":j.Phase=="Ready"?"防護已就緒":j.Phase=="Scanning"?"檢查中":"處理中",Short(j.Message));
     else if(pending)SetStatus("需要還原","先結束 SF6，再按「還原」。");
     else if(j.Phase=="Restored")SetStatus("已還原","本次變更已還原。");
     else if(j.Phase=="NoChanges")SetStatus("未啟用防護","未發現可處理的異常，未更動 USB。");
     else if(j.Phase=="Failed"){lastError=j.Message;SetStatus("未啟用",Short(j.Message));}
     phase=j.Phase;
    }else if(!busy&&game&&!owner&&!pending)SetStatus("SF6 正在執行",j!=null&&j.Phase=="NoChanges"?"未發現可處理的異常，未更動 USB。":"先結束遊戲，再開始防護。");
   }catch(Exception ex){start.Enabled=check.Enabled=false;if(!busy)restore.Enabled=false;Failure(ex);}
  }
  static string Short(string text){return L.Text(text).Length>145?"請查看「詳細資訊」。":text;}
  void Busy(bool value){busy=value;start.Enabled=check.Enabled=language.Enabled=!value;restore.Enabled=value;SetText(restore,value?"取消":"還原");if(value)cancel=false;else Tick();}
  async void CheckHealth(){
   if(busy||backend.GameRunning()||Program.GuardianActive())return;
   Busy(true);lastError="";SetStatus("檢查中","正在檢查目前 USB，不會停用裝置。");
   try{
    report=await Task.Run(()=>new Scanner(backend,new ProcessProbe()).Run(()=>cancel||Program.GuardianActive(),delegate(int done,int total){BeginInvoke(new Action(()=>SetStatus("檢查中",done+" / "+total+" 個 HID 介面")));}));
    Populate(report.Snapshot);int count=report.Targets().Count;
    SetStatus(count>0?"找到 "+count+" 個可處理介面":"未找到可處理介面",count>0?"按「檢查並啟動 SF6」重新確認並套用。":"未套用防護；詳細結果可在下方查看。");
   }catch(Exception ex){Failure(ex);}finally{Busy(false);}
  }
  Process Elevated(string mode){return Process.Start(new ProcessStartInfo(Application.ExecutablePath,mode+" --lang="+L.Language){UseShellExecute=true,Verb="runas",WorkingDirectory=Program.Root,WindowStyle=ProcessWindowStyle.Hidden});}
  public static bool CanLaunch(string readyPhase,bool cancelled,bool gameRunning){return !cancelled&&!gameRunning&&(readyPhase=="Ready"||readyPhase=="NoChanges");}
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
      if(CanLaunch(j.Phase,cancel,backend.GameRunning())){ready=j;break;}
      if(j.Phase=="NoChanges"&&cancel){SetStatus("已取消","未啟動 SF6。");return;}
      if(j.Phase=="Failed"||j.Phase=="RecoveryNeeded")throw new InvalidOperationException(j.Message);
      SetStatus(j.Phase=="Scanning"?"檢查中":"準備防護",Short(j.Message));
     }
     if(worker.HasExited)throw new InvalidOperationException("防護程序已結束，請查看詳細資訊。");
    }
    if(ready==null)throw new InvalidOperationException("準備逾時，未啟動 SF6。");
    if(!CanLaunch(ready.Phase,cancel,backend.GameRunning())){File.WriteAllText(Program.Signal(ready.Session),"cancel");SetStatus("已取消","未啟動 SF6。");return;}
    if(ready.Phase=="Ready"&&!Program.GuardianActive())throw new InvalidOperationException("防護程序已結束，請查看詳細資訊。");
    Process.Start(new ProcessStartInfo("steam://rungameid/1364780"){UseShellExecute=true});
    SetStatus("等待 SF6 啟動",ready.Phase=="NoChanges"?"檢查完成，未更動 USB；正在啟動 SF6。":"3 分鐘未啟動會自動還原。");
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
   using(var f=new Form{Text=L.Text("裝置與詳細資訊"),Size=new Size(960,650),StartPosition=FormStartPosition.CenterParent,Font=Font}){
    var grid=new ListView{Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,HideSelection=false};
    grid.Columns.Add(L.Text("裝置／介面"),320);grid.Columns.Add(L.Text("用途"),250);grid.Columns.Add(L.Text("檢查結果"),340);
    foreach(var d in snapshot){
     var item=report==null?null:report.Items.FirstOrDefault(x=>Policy.Same(x.Device,d));
     string result=L.Text(item==null?"未查詢":item.Note);
     if(item!=null&&item.First!=null)result+=" ("+L.Result(item.First.Kind)+", "+item.First.Milliseconds+" ms)";
     var row=new ListViewItem(d.Name??d.Id);row.SubItems.Add(L.Text(Policy.Usb(d.Id)?"USB 裝置":Policy.Allowed(d)?Policy.Auxiliary(d):"保留／不自動停用"));row.SubItems.Add(result);row.Tag=d;grid.Items.Add(row);
    }
    var info=new TextBox{Dock=DockStyle.Bottom,Height=152,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Text=L.Text("僅在同一輔助介面連續兩次查詢過慢或逾時時停用。主要輸入與未知用途介面不自動停用。\r\n不能保證所有 USB 變更都不凍結；新插入或換埠的裝置需要結束遊戲後重新檢查。\r\n"+(lastError==""?"點選裝置查看識別資訊。":"最近訊息："+lastError))};
    grid.SelectedIndexChanged+=delegate{if(grid.SelectedItems.Count>0){var d=(Device)grid.SelectedItems[0].Tag;info.Text=(d.Name??"")+"\r\n"+d.Id+"\r\n"+L.Text("父裝置：")+d.Parent+"\r\nUsage: "+Policy.Usage(d)+"  "+L.Text("狀態：")+L.State(d.State)+"\r\n"+L.Text("最近訊息：")+L.Text(lastError);}};
    var footer=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=44};
    var log=new Button{Text=L.Text("開啟紀錄"),Width=140,Height=32};log.Click+=delegate{Directory.CreateDirectory(Program.Data.DirectoryPath);Open(Program.Data.DirectoryPath);};footer.Controls.Add(log);
    var guide=new Button{Text=L.Text("使用說明"),Width=140,Height=32};guide.Click+=delegate{Open(Path.Combine(Program.Root,L.English?"USAGE.md":"使用說明.md"));};footer.Controls.Add(guide);
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
