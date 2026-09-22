using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Sf6Guard {
 public sealed class WindowsBackend : IBackend {
  [StructLayout(LayoutKind.Sequential)] struct Info {public uint Size; public Guid Class; public uint Node; public IntPtr Reserved;}
  [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr SetupDiGetClassDevs(ref Guid guid,string e,IntPtr h,uint flags);
  [DllImport("setupapi.dll",SetLastError=true)] static extern bool SetupDiEnumDeviceInfo(IntPtr h,uint index,ref Info i);
  [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool SetupDiGetDeviceInstanceId(IntPtr h,ref Info i,StringBuilder b,uint size,out uint required);
  [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool SetupDiGetDeviceRegistryProperty(IntPtr h,ref Info i,uint prop,out uint type,byte[] b,uint size,out uint required);
  [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr h);
  [DllImport("cfgmgr32.dll")] static extern uint CM_Get_Parent(out uint parent,uint node,uint flags);
  [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)] static extern uint CM_Get_Device_ID(uint node,StringBuilder b,uint length,uint flags);
  [DllImport("cfgmgr32.dll")] static extern uint CM_Get_DevNode_Status(out uint status,out uint problem,uint node,uint flags);
  [DllImport("cfgmgr32.dll")] static extern uint CM_Disable_DevNode(uint node,uint flags);
  [DllImport("cfgmgr32.dll")] static extern uint CM_Enable_DevNode(uint node,uint flags);
  static string Property(IntPtr h,ref Info i,uint key) {
   byte[] b=new byte[8192];uint t,n;
   return SetupDiGetDeviceRegistryProperty(h,ref i,key,out t,b,(uint)b.Length,out n)?Encoding.Unicode.GetString(b,0,(int)Math.Min(n,(uint)b.Length)).TrimEnd('\0'):"";
  }
  static string Receiver(uint node) {
   for(int i=0;i<8;i++) {uint p;if(CM_Get_Parent(out p,node,0)!=0)break;var b=new StringBuilder(1024);
    if(CM_Get_Device_ID(p,b,1024,0)!=0)break;
    if(String.Equals(b.ToString(),Policy.Receiver,StringComparison.OrdinalIgnoreCase))return b.ToString();node=p;}
   return "";
  }
  public List<Device> List() {
   Guid g=new Guid("745a17a0-74d3-11d0-b6fe-00a0c90f57da");
   IntPtr h=SetupDiGetClassDevs(ref g,null,IntPtr.Zero,2);
   if(h==new IntPtr(-1))throw new InvalidOperationException("無法讀取 HID 裝置清單。");
   var result=new List<Device>();
   try { for(uint index=0;;index++) {
    var i=new Info();i.Size=(uint)Marshal.SizeOf(typeof(Info));
    if(!SetupDiEnumDeviceInfo(h,index,ref i)) {if(Marshal.GetLastWin32Error()!=259)throw new InvalidOperationException("裝置列舉中斷。");break;}
    var id=new StringBuilder(1024);uint n;if(!SetupDiGetDeviceInstanceId(h,ref i,id,1024,out n))continue;
    if(Policy.Role(id.ToString())<0)continue;
    uint status,problem;uint code=CM_Get_DevNode_Status(out status,out problem,i.Node,0);
    string state=code!=0?"Unknown":problem==22?"Disabled":problem==0 && (status&8)!=0?"Enabled":"Problem";
    result.Add(new Device {Id=id.ToString(),Parent=Receiver(i.Node),Usage=Property(h,ref i,1),Name=Property(h,ref i,0),State=state,Node=i.Node});
   }}finally{SetupDiDestroyDeviceInfoList(h);}
   return result;
  }
  public bool GameRunning() {var p=Process.GetProcessesByName("StreetFighter6");bool yes=p.Length>0;foreach(var x in p)x.Dispose();return yes;}
  Device Fresh(Device d) {
   var now=List().FirstOrDefault(x=>String.Equals(x.Id,d.Id,StringComparison.OrdinalIgnoreCase));
   if(now==null || !Policy.Allowed(now))throw new InvalidOperationException("目標已移除或不符合限定範圍。");return now;
  }
  public void Disable(Device d) {var now=Fresh(d);if(now.State!="Enabled")throw new InvalidOperationException("停用前狀態已改變。");uint r=CM_Disable_DevNode(now.Node,4);if(r!=0)throw new InvalidOperationException("Windows 拒絕暫停介面，代碼 0x"+r.ToString("X"));}
  public void Enable(Device d) {var now=Fresh(d);uint r=CM_Enable_DevNode(now.Node,0);if(r!=0)throw new InvalidOperationException("Windows 無法還原介面，代碼 0x"+r.ToString("X"));}
 }
}
