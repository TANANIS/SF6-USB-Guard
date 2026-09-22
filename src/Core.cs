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
  public uint Node {get;set;}
 }
 public static class Policy {
  public const string Receiver = @"USB\VID_36B0&PID_3002\19971217";
  public static readonly string[] Roles={"MI_01","MI_02&Col02","MI_02&Col03","MI_02&Col05"};
  public static readonly string[] Usages={"FF60_U:0061","0001_U:0080","000C_U:0001","FF00_U:0001"};
  public static readonly string[] Labels={"廠商設定介面","系統控制鍵","音量／多媒體鍵","廠商輔助介面"};
  public static int Role(string id) {
   if(id==null) return -1;
   for(int i=0;i<Roles.Length;i++)
    if(Regex.IsMatch(id,@"^HID\\VID_36B0&PID_3002&"+Roles[i]+@"\\[0-9A-F&]+$",RegexOptions.IgnoreCase)) return i;
   return -1;
  }
  public static bool Allowed(Device d) {
   int i=Role(d.Id);
   return i>=0 && String.Equals(d.Parent,Receiver,StringComparison.OrdinalIgnoreCase)
    && (d.Usage??"").IndexOf("UP:"+Usages[i],StringComparison.OrdinalIgnoreCase)>=0;
  }
  public static void Validate(List<Device> targets) {
   if(targets.Count!=4 || targets.Any(d=>!Allowed(d)) || targets.Select(d=>Role(d.Id)).Distinct().Count()!=4)
    throw new InvalidOperationException("未找到完整且相符的四個輔助介面；不會停用任何裝置。");
   if(targets.Any(d=>d.State!="Enabled" && d.State!="Disabled"))
    throw new InvalidOperationException("裝置狀態異常；先中止防護，不變更裝置。");
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
  public string Session {get;set;}
  public string Phase {get;set;}
  public string Message {get;set;}
  public string Updated {get;set;}
  public int GuardianPid {get;set;}
  public List<Entry> Entries {get;set;}
  public Journal() { Entries=new List<Entry>(); Phase="Idle"; Message=""; }
 }
 public interface IJournalStore { void Save(Journal j); }
 public sealed class Store : IJournalStore {
  public readonly string DirectoryPath;
  public string JournalPath {get {return Path.Combine(DirectoryPath,"session.json");}}
  public Store(string path) {DirectoryPath=path;}
  public void Save(Journal j) {
   Directory.CreateDirectory(DirectoryPath);
   j.Updated=DateTimeOffset.Now.ToString("o");
   string temp=JournalPath+".tmp";
   byte[] data=System.Text.Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(j));
   using(var f=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)) {f.Write(data,0,data.Length);f.Flush(true);}
   if(File.Exists(JournalPath)) File.Replace(temp,JournalPath,JournalPath+".bak");
   else File.Move(temp,JournalPath);
  }
  public Journal Load() {
   if(!File.Exists(JournalPath)) return null;
   using(var file=new FileStream(JournalPath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
   using(var reader=new StreamReader(file))return new JavaScriptSerializer().Deserialize<Journal>(reader.ReadToEnd());
  }
  public void Log(string s) {
   Directory.CreateDirectory(DirectoryPath);
   File.AppendAllText(Path.Combine(DirectoryPath,"guard.log"),DateTimeOffset.Now.ToString("o")+" "+s+Environment.NewLine);
  }
 }
 public sealed class Engine {
  readonly IBackend backend; readonly IJournalStore store;
  public Engine(IBackend b,IJournalStore s) {backend=b;store=s;}
  public void Prepare(Journal j) {
   if(backend.GameRunning()) throw new InvalidOperationException("請先關閉快打旋風 6，再啟用防護。");
   var targets=backend.List().Where(Policy.Allowed).ToList(); Policy.Validate(targets);
   j.Entries=targets.Select(d=>new Entry {Device=d,WasEnabled=d.State=="Enabled"}).ToList();
   j.Phase="Preparing"; j.Message="正在準備輔助介面防護"; store.Save(j);
   try {
    foreach(var e in j.Entries.Where(e=>e.WasEnabled)) {
     if(backend.GameRunning()) throw new InvalidOperationException("準備期間偵測到遊戲啟動，已停止後續變更。");
     e.RestoreNeeded=true; store.Save(j); // Durable intent must precede every mutation.
     backend.Disable(e.Device);
     var now=backend.List().FirstOrDefault(d=>String.Equals(d.Id,e.Device.Id,StringComparison.OrdinalIgnoreCase));
     if(now==null || !Policy.Allowed(now) || now.State!="Disabled") throw new InvalidOperationException("停用結果未通過驗證。");
    }
    var after=backend.List().Where(Policy.Allowed).ToList(); Policy.Validate(after);
    if(after.Any(d=>d.State!="Disabled")) throw new InvalidOperationException("防護介面未全部停用。");
    j.Phase="Ready";j.Message="防護已準備，等待啟動遊戲";store.Save(j);
   } catch(Exception error) {
    if(!backend.GameRunning()) Restore(j);
    j.Phase=j.Entries.Any(e=>e.RestoreNeeded)?"RecoveryNeeded":"Failed";
    j.Message="準備失敗："+error.Message;store.Save(j);throw;
   }
  }
  public void Restore(Journal j) {
   if(j.Entries==null || j.Entries.Any(e=>e.Device==null || !Policy.Allowed(e.Device)) || j.Entries.Count>4
      || j.Entries.Select(e=>e.Device.Id.ToUpperInvariant()).Distinct().Count()!=j.Entries.Count)
    throw new InvalidOperationException("還原紀錄不符合允許的裝置範圍，已拒絕操作。");
   if(backend.GameRunning()) throw new InvalidOperationException("遊戲仍在執行，請關閉遊戲後還原。");
   j.Phase="Restoring";j.Message="正在還原原本啟用的介面";store.Save(j);
   var errors=new List<string>();
   foreach(var e in j.Entries.Where(e=>e.WasEnabled && e.RestoreNeeded)) {
    try {
     if(backend.GameRunning()) throw new InvalidOperationException("遊戲已啟動，暫停還原。");
     var current=backend.List().FirstOrDefault(d=>String.Equals(d.Id,e.Device.Id,StringComparison.OrdinalIgnoreCase));
     if(current==null) throw new InvalidOperationException("接收器未連接或已換埠；請插回原埠後重試還原。");
     if(!Policy.Allowed(current)) throw new InvalidOperationException("裝置識別已變更。");
     if(current.State=="Disabled") backend.Enable(current);
     else if(current.State!="Enabled") throw new InvalidOperationException("裝置狀態異常。");
     current=backend.List().FirstOrDefault(d=>String.Equals(d.Id,e.Device.Id,StringComparison.OrdinalIgnoreCase));
     if(current==null || current.State!="Enabled") throw new InvalidOperationException("還原結果未通過驗證。");
     e.RestoreNeeded=false;store.Save(j);
    } catch(Exception ex) {errors.Add(ex.Message);}
   }
   j.Phase=j.Entries.Any(e=>e.RestoreNeeded)?"RecoveryNeeded":"Restored";
   j.Message=errors.Count>0?String.Join("；",errors.Distinct()):"已還原本次變更的介面";store.Save(j);
  }
 }
 public sealed class Lifetime {
  readonly DateTime deadline;bool seen;DateTime? gone;
  public Lifetime(DateTime now){deadline=now.AddMinutes(3);}
  public string Step(DateTime now,bool running,bool cancel) {
   if(running){seen=true;gone=null;return "Active";}
   if(!seen)return cancel||now>=deadline?"Restore":"Waiting";
   if(!gone.HasValue)gone=now;
   return (now-gone.Value).TotalSeconds>=8?"Restore":"Active";
  }
 }
}
