using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace Sf6Guard {
 public sealed class Device {
  public string Id {get;set;}
  public string Parent {get;set;}
  public string Usage {get;set;}
  public string Name {get;set;}
  public string State {get;set;}
  public string Class {get;set;}
  public bool Leaf {get;set;}
  public uint Node {get;set;}
 }
 public static class Policy {
  public const int MaxDevices=128;
  public static bool Usb(string id) {return Regex.IsMatch(id??"", @"^USB\\VID_[0-9A-F]{4}&PID_[0-9A-F]{4}\\[^\\\r\n]+$",RegexOptions.IgnoreCase);}
  public static bool Hid(Device d) {
   if(d==null || !Usb(d.Parent))return false;
   var h=Regex.Match(d.Id??"", @"^HID\\VID_([0-9A-F]{4})&PID_([0-9A-F]{4})(?:&[0-9A-Z_]+)*\\[0-9A-Z&_-]+$",RegexOptions.IgnoreCase);
   return h.Success && d.Parent.StartsWith("USB\\VID_"+h.Groups[1].Value+"&PID_"+h.Groups[2].Value+"\\",StringComparison.OrdinalIgnoreCase);
  }
  public static string Usage(Device d) {
   var matches=Regex.Matches(d.Usage??"", @"UP:([0-9A-F]{4})_U:([0-9A-F]{4})(?=$|[\0\s])",RegexOptions.IgnoreCase);
   var all=matches.Cast<Match>().Select(m=>(m.Groups[1].Value+":"+m.Groups[2].Value).ToUpperInvariant()).Distinct().ToArray();
   return all.Length==1?all[0]:"";
  }
  public static string Auxiliary(Device d) {
   if(!Hid(d) || !d.Leaf || !String.Equals(d.Class,"HIDClass",StringComparison.OrdinalIgnoreCase))return "";
   if(Regex.IsMatch(d.Usage??"",@"HID_DEVICE_SYSTEM_(KEYBOARD|MOUSE|GAME)",RegexOptions.IgnoreCase))return "";
   string u=Usage(d);
   if(u=="000C:0001")return "多媒體鍵";
   if(u=="0001:0080")return "系統控制鍵";
   // Opaque vendor collections are never generalized to other models.
   if(Regex.IsMatch(d.Id,@"^HID\\VID_36B0&PID_3002&MI_01\\",RegexOptions.IgnoreCase) && u=="FF60:0061")return "已知型號設定介面";
   if(Regex.IsMatch(d.Id,@"^HID\\VID_36B0&PID_3002&MI_02&Col05\\",RegexOptions.IgnoreCase) && u=="FF00:0001")return "已知型號輔助介面";
   return "";
  }
  public static bool Allowed(Device d) {return d!=null && Auxiliary(d)!="";}
  public static bool Legacy(Device d) {
   if(d==null || d.Parent!=@"USB\VID_36B0&PID_3002\19971217")return false;
   string[] roles={"MI_01","MI_02&Col02","MI_02&Col03","MI_02&Col05"};
   string[] usages={"FF60:0061","0001:0080","000C:0001","FF00:0001"};
   for(int i=0;i<roles.Length;i++)if(Regex.IsMatch(d.Id??"", @"^HID\\VID_36B0&PID_3002&"+roles[i]+@"\\[0-9A-F&]+$",RegexOptions.IgnoreCase)&&Usage(d)==usages[i])return true;
   return false;
  }
  public static bool Same(Device a,Device b) {return a!=null&&b!=null&&String.Equals(a.Id,b.Id,StringComparison.OrdinalIgnoreCase)&&String.Equals(a.Parent,b.Parent,StringComparison.OrdinalIgnoreCase)&&Usage(a)==Usage(b);}
  public static void Validate(List<Device> targets) {
   if(targets==null || targets.Count==0 || targets.Count>MaxDevices || targets.Any(d=>!Allowed(d)||d.State!="Enabled") || targets.Select(d=>d.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=targets.Count)
    throw new InvalidOperationException("目標介面已變更，請重新檢查。");
  }
 }
 public interface IBackend {
  List<Device> List();
  bool GameRunning();
  void Disable(Device d);
  void Enable(Device d);
 }
 public sealed class Entry {
  public Device Device {get;set;}
  public bool WasEnabled {get;set;}
  public bool RestoreNeeded {get;set;}
 }
 public sealed class Journal {
  public int Version {get;set;}
  public string Session {get;set;}
  public string Phase {get;set;}
  public string Message {get;set;}
  public string Updated {get;set;}
  public int GuardianPid {get;set;}
  public ScanReport Report {get;set;}
  public List<Entry> Entries {get;set;}
  public Journal() {Entries=new List<Entry>();Phase="Idle";Message="";}
 }
 public interface IJournalStore {void Save(Journal j);}
 public sealed class Store : IJournalStore {
  public readonly string DirectoryPath;
  public string JournalPath {get{return Path.Combine(DirectoryPath,"session.json");}}
  public Store(string path){DirectoryPath=path;}
  public void Save(Journal j) {
   Directory.CreateDirectory(DirectoryPath);j.Updated=DateTimeOffset.Now.ToString("o");
   string temp=JournalPath+".tmp";byte[] data=System.Text.Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(j));
   using(var f=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){f.Write(data,0,data.Length);f.Flush(true);}
   if(File.Exists(JournalPath))File.Replace(temp,JournalPath,JournalPath+".bak");else File.Move(temp,JournalPath);
  }
  public Journal Load() {
   if(!File.Exists(JournalPath))return null;
   using(var f=new FileStream(JournalPath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
   using(var r=new StreamReader(f))return new JavaScriptSerializer().Deserialize<Journal>(r.ReadToEnd());
  }
  public void Log(string s){Directory.CreateDirectory(DirectoryPath);File.AppendAllText(Path.Combine(DirectoryPath,"guard.log"),DateTimeOffset.Now.ToString("o")+" "+s+Environment.NewLine);}
 }
 public sealed class Engine {
  readonly IBackend backend;readonly IJournalStore store;
  public Engine(IBackend b,IJournalStore s){backend=b;store=s;}
  public static void Stable(List<Device> before,List<Device> now) {
   if(before.Count!=now.Count || before.Any(d=>!now.Any(n=>Policy.Same(d,n)&&d.State==n.State&&d.Leaf==n.Leaf&&d.Class==n.Class)))
    throw new InvalidOperationException("USB 清單已變更，請重新檢查。");
  }
  public void Prepare(Journal j,List<Device> targets,List<Device> baseline,Func<bool> cancelled=null) {
   if(backend.GameRunning())throw new InvalidOperationException("請先關閉 SF6。");
   Policy.Validate(targets);Stable(baseline,backend.List());
   if(targets.Any(d=>!baseline.Any(n=>Policy.Same(d,n)&&Policy.Allowed(n)&&n.State=="Enabled")))throw new InvalidOperationException("目標不在本次清單中。");
   j.Version=2;j.Entries=targets.Select(d=>new Entry{Device=d,WasEnabled=true}).ToList();
   j.Phase="Preparing";j.Message="正在準備防護";store.Save(j);
   try {
    foreach(var e in j.Entries) {
     if(cancelled!=null&&cancelled())throw new OperationCanceledException("已取消防護。");
     if(backend.GameRunning())throw new InvalidOperationException("SF6 已啟動，停止後續變更。");
     e.RestoreNeeded=true;store.Save(j);
     backend.Disable(e.Device);
     var now=backend.List().FirstOrDefault(d=>Policy.Same(d,e.Device));
     if(now==null||!Policy.Allowed(now)||now.State!="Disabled")throw new InvalidOperationException("停用結果未通過驗證。");
    }
    var after=backend.List();
    if(j.Entries.Any(e=>!after.Any(n=>Policy.Same(n,e.Device)&&Policy.Allowed(n)&&n.State=="Disabled")))throw new InvalidOperationException("防護範圍已變更。");
    j.Phase="Ready";j.Message="已暫停 "+j.Entries.Count+" 個異常輔助介面";store.Save(j);
   }catch(Exception error){if(!backend.GameRunning())Restore(j);j.Phase=j.Entries.Any(e=>e.RestoreNeeded)?"RecoveryNeeded":"Failed";j.Message=error.Message;store.Save(j);throw;}
  }
  public bool Covered(Journal j) {var now=backend.List();return j.Entries.Count>0&&j.Entries.All(e=>now.Any(n=>Policy.Same(n,e.Device)&&Policy.Allowed(n)&&n.State=="Disabled"));}
  public void Restore(Journal j) {
   if(j.Entries==null||j.Entries.Count>Policy.MaxDevices||j.Entries.Any(e=>e==null||e.Device==null||!(j.Version==2?Policy.Allowed(e.Device):Policy.Legacy(e.Device))||(!e.WasEnabled&&e.RestoreNeeded))||j.Entries.Select(e=>e.Device.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=j.Entries.Count)
    throw new InvalidOperationException("還原紀錄不符合允許範圍。");
   if(backend.GameRunning())throw new InvalidOperationException("請先結束 SF6 再還原。");
   j.Phase="Restoring";j.Message="正在還原";store.Save(j);var errors=new List<string>();
   foreach(var e in j.Entries.Where(e=>e.WasEnabled&&e.RestoreNeeded))try {
    if(backend.GameRunning())throw new InvalidOperationException("SF6 已啟動，暫停還原。");
    var current=backend.List().FirstOrDefault(d=>Policy.Same(d,e.Device));
    if(current==null)throw new InvalidOperationException("請將待還原裝置插回原埠。");
    if(!Policy.Allowed(current))throw new InvalidOperationException("介面用途已變更。");
    if(current.State=="Disabled")backend.Enable(current);else if(current.State!="Enabled")throw new InvalidOperationException("介面狀態異常。");
    current=backend.List().FirstOrDefault(d=>Policy.Same(d,e.Device));
    if(current==null||current.State!="Enabled")throw new InvalidOperationException("還原驗證失敗。");
    e.RestoreNeeded=false;store.Save(j);
   }catch(Exception ex){errors.Add(ex.Message);}
   j.Phase=j.Entries.Any(e=>e.RestoreNeeded)?"RecoveryNeeded":"Restored";j.Message=errors.Count>0?String.Join("；",errors.Distinct()):"本次變更已還原";store.Save(j);
  }
 }
 public sealed class Lifetime {
  readonly DateTime deadline;bool seen;DateTime? gone;
  public Lifetime(DateTime now){deadline=now.AddMinutes(3);}
  public string Step(DateTime now,bool running,bool cancel){if(running){seen=true;gone=null;return "Active";}if(!seen)return cancel||now>=deadline?"Restore":"Waiting";if(!gone.HasValue)gone=now;return (now-gone.Value).TotalSeconds>=8?"Restore":"Active";}
 }
}
