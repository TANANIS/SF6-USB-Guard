@echo off
call "%SF6_GUARD_VC%" >nul
if errorlevel 1 exit /b 1
pushd "%~dp0"
cl /nologo /EHsc /MT /O2 /W4 HidProbe.cpp /Fo:"%SF6_GUARD_BUILD%\HidProbe.obj" /Fe:"..\HidProbe.exe" /link hid.lib setupapi.lib cfgmgr32.lib
set "SF6_GUARD_RESULT=%errorlevel%"
popd
exit /b %SF6_GUARD_RESULT%
