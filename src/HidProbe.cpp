#include <windows.h>
#include <setupapi.h>
#include <hidsdi.h>
#include <cfgmgr32.h>
#include <cstdio>
#include <string>
#include <vector>
#include <thread>
#include <atomic>
#include <iostream>
#include <tlhelp32.h>
std::atomic<bool> querying{false},done{false};
std::atomic<ULONGLONG> querySince{0};
bool GameRunning() {
 HANDLE snapshot=CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS,0);
 if(snapshot==INVALID_HANDLE_VALUE)return true;
 PROCESSENTRY32W entry={sizeof(entry)};bool running=false;
 if(!Process32FirstW(snapshot,&entry))running=true;
 else do{if(_wcsicmp(entry.szExeFile,L"StreetFighter6.exe")==0){running=true;break;}}while(Process32NextW(snapshot,&entry));
 CloseHandle(snapshot);return running;
}
void Query(std::wstring path) {
 if(GameRunning()){done=true;return;}
 HANDLE h=CreateFileW(path.c_str(),0,FILE_SHARE_READ|FILE_SHARE_WRITE,nullptr,OPEN_EXISTING,FILE_FLAG_OVERLAPPED,nullptr);
 if(h==INVALID_HANDLE_VALUE){printf("OPEN_FAILED\n");fflush(stdout);done=true;return;}
 if(GameRunning()){CloseHandle(h);done=true;return;}
 auto start=GetTickCount64();querySince=start;
 printf("QUERY\n");fflush(stdout);querying=true;
 wchar_t name[260]={};
 BOOL ok=HidD_GetProductString(h,name,sizeof(name));DWORD error=ok?0:GetLastError();
 printf("RESULT\t%llu\t%d\t%lu\n",GetTickCount64()-start,ok,error);fflush(stdout);
 done=true;CloseHandle(h);
}
int wmain() {
 if(GameRunning())return 2;
 std::wstring target;std::getline(std::wcin,target);
 if(target.size()>1024||target.rfind(L"HID\\",0)!=0)return 2;
 GUID guid;HidD_GetHidGuid(&guid);
 HDEVINFO list=SetupDiGetClassDevsW(&guid,nullptr,nullptr,DIGCF_PRESENT|DIGCF_DEVICEINTERFACE);
 if(list==INVALID_HANDLE_VALUE)return 2;
 std::wstring path;
 for(DWORD i=0;;i++) {
  SP_DEVICE_INTERFACE_DATA item={sizeof(item)};
  if(!SetupDiEnumDeviceInterfaces(list,nullptr,&guid,i,&item))break;
  DWORD size=0;SetupDiGetDeviceInterfaceDetailW(list,&item,nullptr,0,&size,nullptr);
  if(size<sizeof(SP_DEVICE_INTERFACE_DETAIL_DATA_W)||size>65536)continue;
  std::vector<BYTE> buffer(size);auto detail=(SP_DEVICE_INTERFACE_DETAIL_DATA_W*)buffer.data();detail->cbSize=sizeof(*detail);
  SP_DEVINFO_DATA info={sizeof(info)};wchar_t id[1024]={};
  if(!SetupDiGetDeviceInterfaceDetailW(list,&item,detail,size,nullptr,&info)||!SetupDiGetDeviceInstanceIdW(list,&info,id,1024,nullptr)||_wcsicmp(id,target.c_str())!=0)continue;
  // Never query Bluetooth/virtual HID accidentally, even with a supplied instance ID.
  DEVINST node=info.DevInst;bool usb=false;
  for(int depth=0;depth<16;depth++){DEVINST parent;wchar_t parentId[1024]={};if(CM_Get_Parent(&parent,node,0)!=CR_SUCCESS||CM_Get_Device_IDW(parent,parentId,1024,0)!=CR_SUCCESS)break;if(wcsncmp(parentId,L"USB\\VID_",8)==0){usb=true;break;}node=parent;}
  if(usb)path=detail->DevicePath;
  break;
 }
 SetupDiDestroyDeviceInfoList(list);
 if(path.empty())return 2;
 std::thread(Query,path).detach();
 auto end=GetTickCount64()+6000;auto nextGameCheck=GetTickCount64();
 while(!done&&GetTickCount64()<end){auto now=GetTickCount64();if(now>=nextGameCheck){if(GameRunning())ExitProcess(2);nextGameCheck=now+200;}if(querying&&now-querySince>=2500)break;Sleep(20);}
 if(done)ExitProcess(0);
 if(querying){printf("TIMEOUT\n");fflush(stdout);ExitProcess(3);}
 ExitProcess(2);
}
