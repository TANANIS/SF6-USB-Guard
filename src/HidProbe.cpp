#include <windows.h>
#include <setupapi.h>
#include <hidsdi.h>
#include <cfgmgr32.h>
#include <cstdio>
#include <string>
#include <vector>
#include <thread>
#include <mutex>
#include <atomic>
struct Entry { std::wstring path; std::atomic<bool> done{false}; };
std::mutex logMutex;
void Query(Entry* e) {
 auto start=GetTickCount64();
 HANDLE h=CreateFileW(e->path.c_str(),GENERIC_READ|GENERIC_WRITE,FILE_SHARE_READ|FILE_SHARE_WRITE,nullptr,OPEN_EXISTING,FILE_FLAG_OVERLAPPED,nullptr);
 DWORD error=GetLastError(); BOOL ok=FALSE; wchar_t name[260]={};
 if(h!=INVALID_HANDLE_VALUE) { ok=HidD_GetProductString(h,name,sizeof(name)); error=ok?0:GetLastError(); CloseHandle(h); }
 {std::lock_guard<std::mutex> lock(logMutex);
 wprintf(L"RESULT ms=%llu ok=%d error=%lu path=%ls product=%ls\n",GetTickCount64()-start,ok,error,e->path.c_str(),name); fflush(stdout);}
 e->done=true;
}
int main() {
 const unsigned timeout=10000;
 GUID guid; HidD_GetHidGuid(&guid);
 HDEVINFO list=SetupDiGetClassDevsW(&guid,nullptr,nullptr,DIGCF_PRESENT|DIGCF_DEVICEINTERFACE);
 if(list==INVALID_HANDLE_VALUE) return 2;
 std::vector<Entry*> entries;
 for(DWORD i=0;;i++) {
  SP_DEVICE_INTERFACE_DATA item={sizeof(item)};
  if(!SetupDiEnumDeviceInterfaces(list,nullptr,&guid,i,&item)) break;
  DWORD size=0; SetupDiGetDeviceInterfaceDetailW(list,&item,nullptr,0,&size,nullptr);
  std::vector<BYTE> buffer(size); auto detail=(SP_DEVICE_INTERFACE_DETAIL_DATA_W*)buffer.data(); detail->cbSize=sizeof(*detail);
  SP_DEVINFO_DATA info={sizeof(info)};
  if(SetupDiGetDeviceInterfaceDetailW(list,&item,detail,size,nullptr,&info)) {
   auto e=new Entry; e->path=detail->DevicePath;
   bool scope=false; DEVINST node=info.DevInst;
   for(int depth=0;depth<8;depth++) { DEVINST parent; wchar_t id[1024]={}; if(CM_Get_Parent(&parent,node,0)!=CR_SUCCESS || CM_Get_Device_IDW(parent,id,1024,0)!=CR_SUCCESS) break; if(_wcsicmp(id,L"USB\\VID_36B0&PID_3002\\19971217")==0) {scope=true;break;} node=parent; }
   bool role=e->path.find(L"&mi_01#")!=std::wstring::npos || e->path.find(L"&mi_02&col02#")!=std::wstring::npos || e->path.find(L"&mi_02&col03#")!=std::wstring::npos || e->path.find(L"&mi_02&col05#")!=std::wstring::npos;
   if(!scope || !role || e->path.find(L"vid_36b0&pid_3002")==std::wstring::npos) {delete e; continue;}
   entries.push_back(e);
  }
 }
 SetupDiDestroyDeviceInfoList(list);
 for(auto e:entries) std::thread(Query,e).detach();
 auto deadline=GetTickCount64()+timeout;
 for(;;) {bool done=true; for(auto e:entries) if(!e->done) done=false; if(done||GetTickCount64()>=deadline) break; Sleep(50);}
 {std::lock_guard<std::mutex> lock(logMutex); for(auto e:entries) if(!e->done) wprintf(L"PENDING_OVER_%uMS path=%ls\n",timeout,e->path.c_str()); fflush(stdout);}
 ExitProcess(0);
}
