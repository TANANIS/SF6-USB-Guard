using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Sf6Guard {
 public sealed class ProbeResult {
  public string Kind {get;set;}
  public long Milliseconds {get;set;}
  public int Error {get;set;}
  public bool Slow {get{return Kind=="Timeout"||Kind=="Slow";}}
 }
 public sealed class ScanItem {
  public Device Device {get;set;}
  public ProbeResult First {get;set;}
  public ProbeResult Second {get;set;}
  public bool Selected {get;set;}
  public string Note {get;set;}
 }
 public sealed class ScanReport {
  public string Captured {get;set;}
  public List<Device> Snapshot {get;set;}
  public List<ScanItem> Items {get;set;}
  public ScanReport(){Snapshot=new List<Device>();Items=new List<ScanItem>();}
  public List<Device> Targets(){return Items.Where(x=>x.Selected).Select(x=>x.Device).ToList();}
 }
 public interface IProbe {ProbeResult Query(Device device,Func<bool> stop);}
 public sealed class ProcessProbe : IProbe {
  public static ProbeResult Parse(string output,int exitCode) {
   var lines=output.Replace("\r","").Split('\n');
   if(exitCode==3&&lines.Contains("QUERY")&&lines.Contains("TIMEOUT"))return new ProbeResult{Kind="Timeout",Milliseconds=2500};
   if(exitCode!=0)return new ProbeResult{Kind="Unknown"};
   foreach(var line in lines)if(line.StartsWith("RESULT\t",StringComparison.Ordinal)) {
    var p=line.Split('\t');long ms;int ok,error;
    if(p.Length!=4||!long.TryParse(p[1],out ms)||ms<0||ms>60000||!int.TryParse(p[2],out ok)||(ok!=0&&ok!=1)||!int.TryParse(p[3],out error)||error<0||!lines.Contains("QUERY"))return new ProbeResult{Kind="Unknown"};
    return new ProbeResult{Kind=ms>=1500?"Slow":ok==1?"OK":"Failed",Milliseconds=ms,Error=error};
   }
   return new ProbeResult{Kind="Unknown"};
  }
  public ProbeResult Query(Device device,Func<bool> stop) {
   if(!Policy.Hid(device))return new ProbeResult{Kind="Unknown"};
   if(stop())throw new OperationCanceledException("檢查已取消或 SF6 已啟動。");
   using(var p=new Process()) {
    p.StartInfo=new ProcessStartInfo(Path.Combine(Program.Root,"HidProbe.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
    p.Start();var read=p.StandardOutput.ReadToEndAsync();var err=p.StandardError.ReadToEndAsync();
    try {
     p.StandardInput.WriteLine(device.Id);p.StandardInput.Close();
     DateTime end=DateTime.UtcNow.AddSeconds(8);
     while(!p.WaitForExit(100)){
      if(stop())throw new OperationCanceledException("檢查已取消或 SF6 已啟動。");
      if(DateTime.UtcNow>=end){try{p.Kill();}catch{}return new ProbeResult{Kind="Unknown"};}
     }
     if(stop())throw new OperationCanceledException("檢查已取消或 SF6 已啟動。");
     return Parse(read.Result,p.ExitCode);
    }finally{try{if(!p.HasExited)p.Kill();}catch{} GC.KeepAlive(err);}
   }
  }
 }
 public sealed class Scanner {
  readonly IBackend backend;readonly IProbe probe;
  public Scanner(IBackend b,IProbe p){backend=b;probe=p;}
  public static bool Select(Device d,ProbeResult first,ProbeResult second){return d.State=="Enabled"&&Policy.Allowed(d)&&first!=null&&second!=null&&first.Slow&&second.Slow;}
  public ScanReport Run(Func<bool> cancelled,Action<int,int> progress) {
   DateTime deadline=DateTime.UtcNow.AddSeconds(90);
   Func<bool> stop=delegate{return cancelled()||backend.GameRunning()||DateTime.UtcNow>=deadline;};
   if(stop())throw new OperationCanceledException("請先關閉 SF6。");
   var report=new ScanReport{Captured=DateTimeOffset.Now.ToString("o"),Snapshot=backend.List()};
   var devices=report.Snapshot.Where(Policy.Hid).ToList();
   if(devices.Count>Policy.MaxDevices)throw new InvalidOperationException("HID 數量超出本版檢查上限。");
   var items=new ScanItem[devices.Count];int complete=0;
   Parallel.For(0,devices.Count,new ParallelOptions{MaxDegreeOfParallelism=4},index=>{
    if(stop())throw new OperationCanceledException("檢查已取消、逾時或 SF6 已啟動。");
    var d=devices[index];var item=new ScanItem{Device=d,Note="保留"};
    if(d.State=="Enabled") {
     item.First=probe.Query(d,stop);
     if(item.First.Slow&&Policy.Allowed(d))item.Second=probe.Query(d,stop);
     item.Selected=Select(d,item.First,item.Second);
     item.Note=item.Selected?"將暫停："+Policy.Auxiliary(d):item.First.Slow?"回覆過慢，保留":item.First.Kind=="OK"?"正常":"未確認，保留";
    }else item.Note=d.State=="Disabled"?"原本停用，保留":"狀態異常，保留";
    items[index]=item;int done=Interlocked.Increment(ref complete);lock(items){progress(done,devices.Count);}
   });
   if(stop())throw new OperationCanceledException("檢查已取消、逾時或 SF6 已啟動。");
   Engine.Stable(report.Snapshot,backend.List());report.Items=items.ToList();return report;
  }
 }
}
