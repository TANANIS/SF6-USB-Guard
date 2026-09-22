using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Web.Script.Serialization;
namespace Sf6Guard {
 static class Tests {
  static Device Make(string vid,string pid,string serial,string role,string usage){return new Device{Id="HID\\VID_"+vid+"&PID_"+pid+"&"+role+"\\8&ABCDE&0&0000",Parent="USB\\VID_"+vid+"&PID_"+pid+"\\"+serial,Usage="HID_DEVICE_UP:"+usage,Class="HIDClass",Leaf=true,State="Enabled",Name="測試輔助介面"};}
  public static List<Device> Devices(){return new List<Device>{
   Make("36B0","3002","19971217","MI_01","FF60_U:0061"),
   Make("36B0","3002","19971217","MI_02&Col02","0001_U:0080"),
   Make("36B0","3002","19971217","MI_02&Col03","000C_U:0001"),
   Make("36B0","3002","19971217","MI_02&Col05","FF00_U:0001"),
   Make("1234","5678","SECOND","MI_01&Col03","000C_U:0001"),
   Make("1234","5678","SECOND","MI_00","0001_U:0006"),
   new Device{Id=@"USB\VID_1234&PID_5678\SECOND",Parent=@"USB\VID_1234&PID_5678\SECOND",Name="示意 USB 接收器",Class="USB",State="Enabled"}
  };}
  static T Copy<T>(T o){var s=new JavaScriptSerializer();return s.Deserialize<T>(s.Serialize(o));}
  sealed class Fake : IBackend {
   public List<Device> Items=Devices();public bool Running;public int FailAt=-1,DisableCalls,EnableCalls;public string EnableFailure="";public Action BeforeDisable;
   public List<Device> List(){return Copy(Items);}
   public bool GameRunning(){return Running;}
   public void Disable(Device d){DisableCalls++;if(BeforeDisable!=null)BeforeDisable();if(DisableCalls==FailAt)throw new Exception("disable refusal");Items.First(x=>x.Id==d.Id).State="Disabled";}
   public void Enable(Device d){EnableCalls++;if(d.Id==EnableFailure)throw new Exception("restore refusal");Items.First(x=>x.Id==d.Id).State="Enabled";}
  }
  sealed class FakeProbe:IProbe {
   public Func<Device,ProbeResult> Result=d=>new ProbeResult{Kind="Slow",Milliseconds=1800};
   public ProbeResult Query(Device d,Func<bool> stop){if(stop())throw new OperationCanceledException();return Result(d);}
  }
  sealed class MemoryStore:IJournalStore {
   public Journal Last;public int Writes;public int FailAt=-1;
   public void Save(Journal j){Writes++;if(Writes==FailAt)throw new IOException("journal failure");Last=Copy(j);}
  }
  static void Assert(bool v,string m){if(!v)throw new Exception(m);}
  static void MustThrow(Action a){bool threw=false;try{a();}catch{threw=true;}Assert(threw,"expected refusal");}
  static List<Device> Targets(Fake b){return b.List().Where(d=>Policy.Allowed(d)&&d.State=="Enabled").ToList();}
  static void Prepare(Engine e,Fake b,Journal j){e.Prepare(j,Targets(b),b.List());}
  static ProbeResult Slow(){return new ProbeResult{Kind="Slow",Milliseconds=1800};}
  public static int Run(string file) {
   var lines=new List<string>();int failed=0;
   Action<string,Action> test=delegate(string name,Action action){try{action();lines.Add("PASS "+name);}catch(Exception ex){failed++;lines.Add("FAIL "+name+": "+ex.Message);}};
   test("multiple vendors and serials supported",delegate{Assert(Targets(new Fake()).Count==5,"scope");var d=Devices()[0];d.Parent=d.Parent.Replace("19971217","ANOTHER");Assert(Policy.Allowed(d),"serial locked");});
   test("primary keyboard mouse controller touch and unknown vendor preserved",delegate{foreach(string usage in new[]{"0001_U:0006","0001_U:0002","0001_U:0004","0001_U:0005","000D_U:0004","FF00_U:0001"}){var d=Devices()[4];d.Usage="HID_DEVICE_UP:"+usage;Assert(!Policy.Allowed(d),usage);}});
   test("USB parents and hubs never selected",delegate{var d=Devices()[0];d.Id=d.Parent;Assert(!Policy.Allowed(d),"parent");d.Id=@"USB\ROOT_HUB30\4&AB";Assert(!Policy.Allowed(d),"hub");});
   test("mixed usage ambiguous descriptor rejected",delegate{var d=Devices()[4];d.Usage+="\0HID_DEVICE_UP:0001_U:0006";Assert(!Policy.Allowed(d),"mixed");});
   test("primary compatible IDs override auxiliary usage",delegate{foreach(string role in new[]{"KEYBOARD","MOUSE","GAME"}){var d=Devices()[4];d.Usage+="\0HID_DEVICE_SYSTEM_"+role;Assert(!Policy.Allowed(d),role);}});
   test("duplicate compatible usage is not ambiguous",delegate{var d=Devices()[4];d.Usage+="\0"+d.Usage;Assert(Policy.Allowed(d),"duplicate usage");});
   test("nonleaf and wrong class rejected",delegate{var d=Devices()[4];d.Leaf=false;Assert(!Policy.Allowed(d),"parent collection");d.Leaf=true;d.Class="Keyboard";Assert(!Policy.Allowed(d),"keyboard class");});
   test("mismatched USB ancestry rejected",delegate{var d=Devices()[4];d.Parent=Devices()[0].Parent;Assert(!Policy.Allowed(d),"parent mismatch");d.Parent=@"BTH\VID_1234&PID_5678\OTHER";Assert(!Policy.Allowed(d),"bluetooth");});
   test("malformed instance and usage rejected",delegate{var d=Devices()[4];d.Id+="\" /force";Assert(!Policy.Allowed(d),"malformed ID");d=Devices()[4];d.Usage="HID_DEVICE_UP:000C_U:00010";Assert(!Policy.Allowed(d),"usage suffix");});
   test("two slow results required",delegate{var d=Devices()[4];Assert(Scanner.Select(d,Slow(),Slow()),"slow pair");Assert(!Scanner.Select(d,Slow(),new ProbeResult{Kind="OK"}),"intermittent");Assert(!Scanner.Select(d,Slow(),null),"unconfirmed");});
   test("fast failures and denied access never trigger disabling",delegate{var d=Devices()[4];Assert(!Scanner.Select(d,new ProbeResult{Kind="Failed",Error=5},new ProbeResult{Kind="Failed",Error=5}),"failed query");});
   test("slow primary input still cannot be selected",delegate{Assert(!Scanner.Select(Devices()[5],Slow(),Slow()),"keyboard");});
   test("already disabled auxiliary left alone",delegate{var b=new Fake();b.Items[0].State="Disabled";var report=new Scanner(b,new FakeProbe()).Run(()=>false,(n,t)=>{});Assert(report.Targets().Count==4,"disabled selected");var e=new Engine(b,new MemoryStore());var j=new Journal();Prepare(e,b,j);e.Restore(j);Assert(b.Items[0].State=="Disabled"&&b.EnableCalls==4,"original changed");});
   test("scan covers USB HID and reports other USB without querying it",delegate{var b=new Fake();int calls=0;var p=new FakeProbe{Result=d=>{System.Threading.Interlocked.Increment(ref calls);return Slow();}};var report=new Scanner(b,p).Run(()=>false,(n,t)=>{});Assert(report.Snapshot.Count==7&&report.Items.Count==6&&report.Targets().Count==5&&calls==11,"coverage");});
   test("no anomalies produces no targets",delegate{var r=new Scanner(new Fake(),new FakeProbe{Result=d=>new ProbeResult{Kind="OK"}}).Run(()=>false,(n,t)=>{});Assert(r.Targets().Count==0,"normal selected");});
   test("scan cancelled before any query",delegate{int calls=0;var p=new FakeProbe{Result=d=>{calls++;return Slow();}};MustThrow(()=>new Scanner(new Fake(),p).Run(()=>true,(n,t)=>{}));Assert(calls==0,"queried after cancel");});
   test("running game blocks scan",delegate{MustThrow(()=>new Scanner(new Fake{Running=true},new FakeProbe()).Run(()=>false,(n,t)=>{}));});
   test("game starts during scan aborts plan",delegate{var b=new Fake();var p=new FakeProbe{Result=d=>{b.Running=true;return Slow();}};MustThrow(()=>new Scanner(b,p).Run(()=>false,(n,t)=>{}));Assert(b.DisableCalls==0,"mutated");});
   test("scan inventory change aborts plan",delegate{var b=new Fake();bool changed=false;MustThrow(()=>new Scanner(b,new FakeProbe()).Run(()=>false,(n,t)=>{if(!changed){changed=true;b.Items.RemoveAt(b.Items.Count-1);}}));});
   test("protocol accepts measured slow response",delegate{Assert(ProcessProbe.Parse("QUERY\nRESULT\t1700\t0\t995\n",0).Kind=="Slow","slow");});
   test("protocol distinguishes timeout from tool failure",delegate{Assert(ProcessProbe.Parse("QUERY\nTIMEOUT\n",3).Kind=="Timeout","timeout");Assert(ProcessProbe.Parse("TIMEOUT\n",3).Kind=="Unknown","unproven query");Assert(ProcessProbe.Parse("QUERY\n",2).Kind=="Unknown","tool failure");});
   test("protocol fast access failure is not a slow device",delegate{Assert(ProcessProbe.Parse("QUERY\nRESULT\t1\t0\t5\n",0).Kind=="Failed","access");Assert(ProcessProbe.Parse("OPEN_FAILED\n",0).Kind=="Unknown","open");});
   test("malformed helper output rejected",delegate{Assert(ProcessProbe.Parse("QUERY\nRESULT\tbad\t1\t0\n",0).Kind=="Unknown","bad ms");Assert(ProcessProbe.Parse("RESULT\t1700\t1\t0\n",0).Kind=="Unknown","missing query");});
   test("running game blocks apply",delegate{var b=new Fake{Running=true};MustThrow(()=>Prepare(new Engine(b,new MemoryStore()),b,new Journal()));Assert(b.DisableCalls==0,"mutation");});
   test("empty duplicate and unknown-state targets rejected",delegate{MustThrow(()=>Policy.Validate(new List<Device>()));var d=Devices()[4];MustThrow(()=>Policy.Validate(new List<Device>{d,d}));d.State="Unknown";MustThrow(()=>Policy.Validate(new List<Device>{d}));});
   test("new USB or changed parent aborts stale plan",delegate{var b=new Fake();var before=b.List();b.Items.RemoveAt(6);MustThrow(()=>new Engine(b,new MemoryStore()).Prepare(new Journal(),Targets(b),before));Assert(b.DisableCalls==0,"stale plan");});
   test("targets must belong to captured inventory",delegate{var b=new Fake();var targets=Targets(b);targets[0].Id+="F";MustThrow(()=>new Engine(b,new MemoryStore()).Prepare(new Journal(),targets,b.List()));Assert(b.DisableCalls==0,"foreign target");});
   test("durable intent before each change",delegate{var b=new Fake();var s=new MemoryStore();b.BeforeDisable=()=>Assert(s.Last.Entries.Count(e=>e.RestoreNeeded)==b.DisableCalls,"no durable intent");Prepare(new Engine(b,s),b,new Journal());});
   test("apply and restore only selected auxiliaries",delegate{var b=new Fake();var e=new Engine(b,new MemoryStore());var j=new Journal();Prepare(e,b,j);Assert(e.Covered(j)&&b.Items[5].State=="Enabled"&&b.Items[6].State=="Enabled","coverage");e.Restore(j);Assert(j.Phase=="Restored"&&b.Items.All(d=>d.State=="Enabled"),"restore");});
   test("partial failure rolls back",delegate{var b=new Fake{FailAt=3};var j=new Journal();MustThrow(()=>Prepare(new Engine(b,new MemoryStore()),b,j));Assert(b.Items.All(d=>d.State=="Enabled")&&!j.Entries.Any(e=>e.RestoreNeeded),"rollback");});
   test("cancellation during apply rolls back and stops further changes",delegate{var b=new Fake();var e=new Engine(b,new MemoryStore());var j=new Journal();MustThrow(()=>e.Prepare(j,Targets(b),b.List(),()=>b.DisableCalls>0));Assert(b.DisableCalls==1&&b.Items.All(d=>d.State=="Enabled"),"cancellation");});
   test("journal failure prevents native change",delegate{var b=new Fake();MustThrow(()=>Prepare(new Engine(b,new MemoryStore{FailAt=2}),b,new Journal()));Assert(b.DisableCalls==0,"write ahead");});
   test("missing device preserves recovery intent",delegate{var b=new Fake();var j=new Journal();var e=new Engine(b,new MemoryStore());Prepare(e,b,j);b.Items.RemoveAt(0);e.Restore(j);Assert(j.Phase=="RecoveryNeeded"&&j.Entries[0].RestoreNeeded,"lost intent");});
   test("failed enable preserves only unresolved entries",delegate{var b=new Fake();var j=new Journal();var e=new Engine(b,new MemoryStore());Prepare(e,b,j);b.EnableFailure=b.Items[1].Id;e.Restore(j);Assert(j.Entries.Count(x=>x.RestoreNeeded)==1,"lost intent");});
   test("tampered restore cannot enable USB parent",delegate{var b=new Fake();var j=new Journal{Version=2};j.Entries.Add(new Entry{Device=b.Items[6],WasEnabled=true,RestoreNeeded=true});MustThrow(()=>new Engine(b,new MemoryStore()).Restore(j));Assert(b.EnableCalls==0,"parent enabled");});
   test("changed usage prevents restore to different device type",delegate{var b=new Fake();var j=new Journal();var e=new Engine(b,new MemoryStore());Prepare(e,b,j);b.Items[0].Usage="HID_DEVICE_UP:0001_U:0006";e.Restore(j);Assert(j.Entries[0].RestoreNeeded,"changed usage accepted");});
   test("v0.1 pending journal can restore original known receiver",delegate{var b=new Fake();var old=Copy(b.Items[0]);old.Class=null;old.Leaf=false;b.Items[0].State="Disabled";var j=new Journal();j.Entries.Add(new Entry{Device=old,WasEnabled=true,RestoreNeeded=true});new Engine(b,new MemoryStore()).Restore(j);Assert(j.Phase=="Restored"&&b.EnableCalls==1,"legacy recovery");});
   test("legacy journal cannot broaden scope",delegate{var b=new Fake();var j=new Journal();j.Entries.Add(new Entry{Device=b.Items[4],WasEnabled=true,RestoreNeeded=true});MustThrow(()=>new Engine(b,new MemoryStore()).Restore(j));});
   test("game starts during apply leaves recoverable state",delegate{var b=new Fake();b.BeforeDisable=()=>b.Running=true;var j=new Journal();MustThrow(()=>Prepare(new Engine(b,new MemoryStore()),b,j));Assert(b.DisableCalls==1&&b.EnableCalls==0&&j.Phase=="RecoveryNeeded","race");});
   test("restore blocked while game running",delegate{var b=new Fake();var j=new Journal();var e=new Engine(b,new MemoryStore());Prepare(e,b,j);b.Running=true;MustThrow(()=>e.Restore(j));Assert(b.EnableCalls==0,"live restore");});
   test("coverage detects unplug and reenable",delegate{var b=new Fake();var j=new Journal();var e=new Engine(b,new MemoryStore());Prepare(e,b,j);b.Items[0].State="Enabled";Assert(!e.Covered(j),"reenabled");b.Items.RemoveAt(0);Assert(!e.Covered(j),"unplug");});
   test("atomic journal round trip",delegate{var s=new Store(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(file)),"test-state-"+Guid.NewGuid().ToString("N")));var j=new Journal{Version=2,Phase="Preparing"};s.Save(j);j.Phase="Ready";s.Save(j);Assert(s.Load().Phase=="Ready"&&File.Exists(s.JournalPath+".bak"),"persistence");});
   test("launch timeout and cancellation restore",delegate{var t=DateTime.UtcNow;var l=new Lifetime(t);Assert(l.Step(t.AddSeconds(179),false,false)=="Waiting","early");Assert(l.Step(t.AddSeconds(180),false,false)=="Restore","deadline");Assert(new Lifetime(t).Step(t,false,true)=="Restore","cancel");});
   test("active game ignores cancellation and launch deadline",delegate{var t=DateTime.UtcNow;var l=new Lifetime(t);Assert(l.Step(t,true,true)=="Active","cancel");Assert(l.Step(t.AddHours(8),true,false)=="Active","deadline");});
   test("exit grace and game restart",delegate{var t=DateTime.UtcNow;var l=new Lifetime(t);l.Step(t,true,false);l.Step(t.AddSeconds(1),false,false);Assert(l.Step(t.AddSeconds(8),false,false)=="Active","grace");l.Step(t.AddSeconds(8),true,false);Assert(l.Step(t.AddSeconds(20),false,false)=="Active","restart");Assert(l.Step(t.AddSeconds(28),false,false)=="Restore","restore");});
   test("successful checks launch with or without eligible targets",delegate{Assert(MainForm.CanLaunch("Ready",false,false),"protected launch");Assert(MainForm.CanLaunch("NoChanges",false,false),"normal launch");});
   test("failed cancelled or incomplete checks cannot launch",delegate{foreach(string phase in new[]{"Scanning","Preparing","Failed","RecoveryNeeded","Restored"})Assert(!MainForm.CanLaunch(phase,false,false),phase);Assert(!MainForm.CanLaunch("Ready",true,false),"cancelled");Assert(!MainForm.CanLaunch("NoChanges",false,true),"already running");});
   test("English translates dynamic status and persisted recovery messages",delegate{L.Set("en-US");Assert(L.Text("已暫停 3 個異常輔助介面")=="Paused 3 affected auxiliary interfaces","dynamic");Assert(L.Text("請將待還原裝置插回原埠。").StartsWith("Reconnect"),"recovery");Assert(L.Text("檢查並啟動 SF6")=="Check & launch SF6","launch label");});
   test("Traditional Chinese restores original messages",delegate{L.Set("zh-TW");Assert(L.Text("請支持我繼續更新")=="請支持我繼續更新","support");Assert(L.Text("已暫停 3 個異常輔助介面")=="已暫停 3 個異常輔助介面","dynamic");});
   test("language preference persists and command line overrides it",delegate{string path=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(file)),"language-test-"+Guid.NewGuid().ToString("N")+".txt");L.Load(path,new string[0]);Assert(L.English,"default language");L.Set("zh-TW");L.Save();L.Load(path,new string[0]);Assert(!L.English,"saved language");L.Load(path,new[]{"--lang=en-US"});Assert(L.English,"override");File.WriteAllText(path,"invalid");L.Load(path,new string[0]);Assert(L.English,"invalid preference fallback");});
   lines.Add("TOTAL "+(lines.Count-failed)+" passed, "+failed+" failed. Fake device backend only; no live device mutations.");File.WriteAllLines(file,lines);return failed==0?0:1;
  }
 }
}
