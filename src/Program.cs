using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Security.Principal;
using System.Windows.Forms;
using System.Web.Script.Serialization;

namespace Sf6Guard {
 static class Program {
  public static readonly string Root=AppDomain.CurrentDomain.BaseDirectory;
  public static readonly Store Data=new Store(Path.Combine(Root,"Data"));
  public static string Signal(string session) {Guid id;if(!Guid.TryParseExact(session,"N",out id))throw new InvalidOperationException("無效工作階段。");return Path.Combine(Data.DirectoryPath,"cancel-"+id.ToString("N"));}
  public static bool GuardianActive() {try{using(var m=Mutex.OpenExisting(@"Local\Sf6UsbGuard-DeviceOwner-v1")){try{bool free=m.WaitOne(0);if(free)m.ReleaseMutex();return !free;}catch(AbandonedMutexException){m.ReleaseMutex();return false;}}}catch(WaitHandleCannotBeOpenedException){return false;}}
  [STAThread] static int Main(string[] args) {
   try {
    if(args.Length>0 && args[0]=="--self-test") return Tests.Run(args.Length>1?args[1]:Path.Combine(Root,"test-results.txt"));
    if(args.Length>0 && args[0]=="--inventory") {File.WriteAllText(args[1],new JavaScriptSerializer().Serialize(new WindowsBackend().List()));return 0;}
    if(args.Length>0 && (args[0]=="--guard" || args[0]=="--restore"))return RunGuardian(args[0]=="--restore");
    Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
    if(args.Length>0 && args[0]=="--preview") {using(var f=new MainForm(true)){f.RenderPreview(args[1]);}return 0;}
    bool created;using(var mutex=new Mutex(true,@"Local\Sf6UsbGuard-UI-v1",out created)) {
     if(!created) {MessageBox.Show("SF6 USB Guard 已經開啟。","SF6 USB Guard");return 0;}
     Application.Run(new MainForm(false));mutex.ReleaseMutex();
    }
    return 0;
   }catch(Exception ex) {try{Data.Log(ex.ToString());}catch{} MessageBox.Show(ex.Message,"SF6 USB Guard",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}
  }
  static bool IsAdmin() {return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);}
  static int RunGuardian(bool recovery) {
   if(!IsAdmin())throw new InvalidOperationException("防護操作需要系統管理員權限。請由主視窗按鈕啟動。");
   using(var mutex=new Mutex(false,@"Local\Sf6UsbGuard-DeviceOwner-v1")) {
    bool held=false;try{held=mutex.WaitOne(0);}catch(AbandonedMutexException){held=true;}
    if(!held)throw new InvalidOperationException("已有防護工作執行中，請回到原本視窗。");
    var backend=new WindowsBackend();var engine=new Engine(backend,Data);Journal j=null;
    try {
     var previous=Data.Load();
     if(recovery) {if(previous==null)return 0;engine.Restore(previous);Data.Log("Recovery: "+previous.Phase+" "+previous.Message);return previous.Phase=="Restored"?0:2;}
     if(previous!=null && previous.Entries.Any(e=>e.RestoreNeeded))throw new InvalidOperationException("上次還有待還原的介面，請先按還原。");
     j=new Journal {Session=Guid.NewGuid().ToString("N"),GuardianPid=Process.GetCurrentProcess().Id};
     Data.Log("Preparing session "+j.Session);
     engine.Prepare(j);
     var lifetime=new Lifetime(DateTime.UtcNow);DateTime check=DateTime.UtcNow;
     while(true) {
      bool running=backend.GameRunning();
      string action=lifetime.Step(DateTime.UtcNow,running,File.Exists(Signal(j.Session)));
      if(action=="Restore")break;
      if(action=="Active") {
       if(j.Phase!="Active"){j.Phase="Active";j.Message="防護中；遊戲結束後自動還原";Data.Save(j);Data.Log("Game detected");}
       if(DateTime.UtcNow>=check && running) {
        var current=backend.List().Where(Policy.Allowed).ToList();
        bool covered=current.Count==4 && current.All(d=>d.State=="Disabled") && current.All(d=>j.Entries.Any(e=>e.Device.Id.Equals(d.Id,StringComparison.OrdinalIgnoreCase)));
        string message=covered?"防護中；遊戲結束後自動還原":"裝置已變動，防護範圍不完整；本局不再切換裝置，結束後請重開防護";
        if(message!=j.Message){j.Message=message;Data.Save(j);Data.Log(message);}
        check=DateTime.UtcNow.AddSeconds(5);
       }
      }
      Thread.Sleep(500);
     }
     engine.Restore(j);Data.Log(j.Phase+": "+j.Message);return j.Phase=="Restored"?0:2;
    }catch(Exception ex) {
     Data.Log(ex.ToString());
     if(j!=null) {
      // Do not race a running game with a rollback. Leave a durable recovery record.
      if(!backend.GameRunning() && j.Entries.Any(e=>e.RestoreNeeded)) {try{engine.Restore(j);}catch(Exception re){Data.Log(re.ToString());}}
      j.Phase=j.Entries.Any(e=>e.RestoreNeeded)?"RecoveryNeeded":"Failed";j.Message=ex.Message;Data.Save(j);
     }else {MessageBox.Show(ex.Message,"SF6 USB Guard",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
     return 1;
    }finally{mutex.ReleaseMutex();}
   }
  }
 }
}
