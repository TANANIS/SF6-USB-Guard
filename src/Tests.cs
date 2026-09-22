using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace Sf6Guard {
 static class Tests {
  public static List<Device> Devices() {
   return Policy.Roles.Select((role,i)=>new Device {Id=@"HID\VID_36B0&PID_3002&"+role+@"\8&ABCDE&0&000"+i,Parent=Policy.Receiver,Usage="HID\\VID_36B0&UP:"+Policy.Usages[i],Name=Policy.Labels[i],State="Enabled"}).ToList();
  }
  sealed class Fake : IBackend {
   public List<Device> Items=Devices();public bool Running;public int FailAt=-1;public int DisableCalls;public int EnableCalls;public string EnableFailure="";public Action BeforeDisable;
   public List<Device> List(){return Items;}
   public bool GameRunning(){return Running;}
   public void Disable(Device d){DisableCalls++;if(BeforeDisable!=null)BeforeDisable();if(DisableCalls==FailAt)throw new Exception("simulated disable refusal");d.State="Disabled";}
   public void Enable(Device d){EnableCalls++;if(d.Id==EnableFailure)throw new Exception("simulated restore refusal");d.State="Enabled";}
  }
  sealed class MemoryStore : IJournalStore {
   public Journal Last; public int Writes; public int FailAt=-1;
   public void Save(Journal j){Writes++;if(Writes==FailAt)throw new IOException("simulated journal failure");Last=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Journal>(new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(j));}
  }
  static void Assert(bool value,string message){if(!value)throw new Exception(message);}
  static void MustThrow(Action a){bool threw=false;try{a();}catch{threw=true;}Assert(threw,"expected refusal");}
  public static int Run(string file) {
   var lines=new List<string>();int failed=0;
   Action<string,Action> test=delegate(string name,Action action){try{action();lines.Add("PASS "+name);}catch(Exception e){failed++;lines.Add("FAIL "+name+": "+e.Message);}};
   test("exact four intended roles accepted",delegate{Policy.Validate(Devices());});
   test("primary keyboard and mouse cannot match policy",delegate{
    foreach(string role in new[]{"MI_00","MI_02&Col01","MI_02&Col04"}){var d=Devices()[0];d.Id=@"HID\VID_36B0&PID_3002&"+role+@"\8&AB&0&0000";Assert(!Policy.Allowed(d),role);}
   });
   test("USB receiver parent and hub cannot match policy",delegate{foreach(string id in new[]{Policy.Receiver,@"USB\VID_1A86&PID_8098\6&123&0&1"}){var d=Devices()[0];d.Id=id;Assert(!Policy.Allowed(d),id);}});
   test("other receiver serial and Logitech excluded",delegate{var d=Devices()[0];d.Parent=Policy.Receiver+"9";Assert(!Policy.Allowed(d),"wrong serial");d=Devices()[0];d.Id=d.Id.Replace("36B0","046D");Assert(!Policy.Allowed(d),"Logitech");});
   test("changed HID usage and instance injection rejected",delegate{var d=Devices()[0];d.Usage="HID_DEVICE_SYSTEM_KEYBOARD";Assert(!Policy.Allowed(d),"usage");d=Devices()[0];d.Id+="\" /force";Assert(!Policy.Allowed(d),"injection");});
   test("missing and duplicate roles fail closed",delegate{var d=Devices();d.RemoveAt(3);MustThrow(delegate{Policy.Validate(d);});d=Devices();d[3]=d[0];MustThrow(delegate{Policy.Validate(d);});});
   test("game running blocks all changes",delegate{var b=new Fake{Running=true};MustThrow(delegate{new Engine(b,new MemoryStore()).Prepare(new Journal());});Assert(b.DisableCalls==0,"changed");});
   test("unknown device state blocks all changes",delegate{var b=new Fake();b.Items[2].State="Unknown";MustThrow(delegate{new Engine(b,new MemoryStore()).Prepare(new Journal());});Assert(b.DisableCalls==0,"changed");});
   test("intent is durable before native disable",delegate{var b=new Fake();var s=new MemoryStore();b.BeforeDisable=delegate{Assert(s.Last.Entries.Count(e=>e.RestoreNeeded)==b.DisableCalls,"intent missing");};new Engine(b,s).Prepare(new Journal());});
   test("complete apply and restore verified",delegate{var b=new Fake();var s=new MemoryStore();var j=new Journal();var e=new Engine(b,s);e.Prepare(j);Assert(j.Phase=="Ready"&&b.Items.All(d=>d.State=="Disabled"),"apply");e.Restore(j);Assert(j.Phase=="Restored"&&b.Items.All(d=>d.State=="Enabled")&&!j.Entries.Any(x=>x.RestoreNeeded),"restore");});
   test("pre-disabled state preserved",delegate{var b=new Fake();b.Items[2].State="Disabled";var j=new Journal();var e=new Engine(b,new MemoryStore());e.Prepare(j);e.Restore(j);Assert(b.DisableCalls==3&&b.EnableCalls==3&&b.Items[2].State=="Disabled","changed original disabled state");});
   test("partial apply failure rolls back previous changes",delegate{var b=new Fake{FailAt=3};var j=new Journal();MustThrow(delegate{new Engine(b,new MemoryStore()).Prepare(j);});Assert(b.Items.All(x=>x.State=="Enabled")&&!j.Entries.Any(x=>x.RestoreNeeded),"rollback incomplete");});
   test("journal write failure occurs before disable",delegate{var b=new Fake();MustThrow(delegate{new Engine(b,new MemoryStore{FailAt=2}).Prepare(new Journal());});Assert(b.DisableCalls==0,"write-ahead broken");});
   test("recovery uses persisted intent after interrupted apply",delegate{var b=new Fake();var j=new Journal();j.Entries.Add(new Entry{Device=b.Items[0],WasEnabled=true,RestoreNeeded=true});b.Items[0].State="Disabled";new Engine(b,new MemoryStore()).Restore(j);Assert(b.Items[0].State=="Enabled"&&!j.Entries[0].RestoreNeeded,"recovery");});
   test("disappeared device retains recovery record",delegate{var b=new Fake();var j=new Journal();var e=new Engine(b,new MemoryStore());e.Prepare(j);b.Items.RemoveAt(0);e.Restore(j);Assert(j.Phase=="RecoveryNeeded"&&j.Entries[0].RestoreNeeded,"lost recovery");});
   test("restore failure keeps only failed entries pending",delegate{var b=new Fake();var j=new Journal();var e=new Engine(b,new MemoryStore());e.Prepare(j);b.EnableFailure=b.Items[1].Id;e.Restore(j);Assert(j.Entries.Count(x=>x.RestoreNeeded)==1&&j.Phase=="RecoveryNeeded","lost recovery");});
   test("tampered restore target rejected",delegate{var b=new Fake();var j=new Journal();j.Entries.Add(new Entry{Device=new Device{Id=Policy.Receiver,Parent=Policy.Receiver},WasEnabled=true,RestoreNeeded=true});MustThrow(delegate{new Engine(b,new MemoryStore()).Restore(j);});Assert(b.EnableCalls==0,"out of scope enable");});
   test("restore blocked during active game",delegate{var b=new Fake();var j=new Journal();var e=new Engine(b,new MemoryStore());e.Prepare(j);b.Running=true;MustThrow(delegate{e.Restore(j);});Assert(b.EnableCalls==0&&j.Entries.All(x=>x.RestoreNeeded),"unsafe restore");});
   test("game starts midway: no further disable or rollback",delegate{var b=new Fake();b.BeforeDisable=delegate{b.Running=true;};var j=new Journal();MustThrow(delegate{new Engine(b,new MemoryStore()).Prepare(j);});Assert(b.DisableCalls==1&&b.EnableCalls==0&&j.Phase=="RecoveryNeeded","race guard failed");});
   test("atomic journal round trip",delegate{string dir=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(file)),"test-state-"+Guid.NewGuid().ToString("N"));var s=new Store(dir);var j=new Journal{Session=Guid.NewGuid().ToString("N"),Phase="Preparing"};s.Save(j);j.Phase="Ready";s.Save(j);Assert(s.Load().Phase=="Ready"&&File.Exists(s.JournalPath+".bak"),"journal persistence");});
   test("no game: three minute timeout restores",delegate{var t=DateTime.UtcNow;var l=new Lifetime(t);Assert(l.Step(t.AddSeconds(179),false,false)=="Waiting","early restore");Assert(l.Step(t.AddSeconds(180),false,false)=="Restore","timeout");});
   test("cancel before game restores",delegate{var t=DateTime.UtcNow;Assert(new Lifetime(t).Step(t,false,true)=="Restore","cancel");});
   test("cancel never restores during game",delegate{var t=DateTime.UtcNow;Assert(new Lifetime(t).Step(t,true,true)=="Active","active cancellation");});
   test("game exit grace period then automatic restore",delegate{var t=DateTime.UtcNow;var l=new Lifetime(t);l.Step(t,true,false);Assert(l.Step(t.AddSeconds(2),false,false)=="Active","no grace");Assert(l.Step(t.AddSeconds(9),false,false)=="Active","short grace");Assert(l.Step(t.AddSeconds(10),false,false)=="Restore","exit not restored");});
   test("game restart during grace postpones restore",delegate{var t=DateTime.UtcNow;var l=new Lifetime(t);l.Step(t,true,false);l.Step(t.AddSeconds(1),false,false);l.Step(t.AddSeconds(7),true,false);Assert(l.Step(t.AddSeconds(12),false,false)=="Active","restart ignored");Assert(l.Step(t.AddSeconds(20),false,false)=="Restore","restart exit");});
   test("long game is not subject to launch timeout",delegate{var t=DateTime.UtcNow;var l=new Lifetime(t);l.Step(t,true,false);Assert(l.Step(t.AddHours(8),true,false)=="Active","timed out active game");});
   lines.Add("TOTAL "+(lines.Count-failed)+" passed, "+failed+" failed. Fake device backend only; no live device mutations.");File.WriteAllLines(file,lines);return failed==0?0:1;
  }
 }
}
