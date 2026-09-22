$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$source=Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object FullName
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /warn:4 "/out:$root\SF6-USB-Guard.exe" "/win32manifest:$root\src\app.manifest" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.Core.dll $source
if($LASTEXITCODE -ne 0){throw 'C# build failed'}
Write-Output 'Built SF6-USB-Guard.exe'
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if(-not (Test-Path -LiteralPath $vswhere)){throw 'Install Visual Studio C++ Build Tools to build HidProbe.exe'}
$vsInstall=& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if(-not $vsInstall){throw 'Visual Studio C++ toolchain not found'}
$env:SF6_GUARD_VC=Join-Path $vsInstall 'VC\Auxiliary\Build\vcvars64.bat'
$env:SF6_GUARD_BUILD=Join-Path $env:TEMP ('Sf6UsbGuardBuild-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $env:SF6_GUARD_BUILD | Out-Null
& (Join-Path $root 'src\build-probe.cmd')
if($LASTEXITCODE -ne 0){throw 'HidProbe C++ build failed'}
Write-Output 'Built HidProbe.exe'
