# Сборка MRID6 Helper встроенным Roslyn (VS BuildTools) под .NET Framework 4.8. Ничего не скачивает.
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$csc  = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe'
$fw   = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$src  = Join-Path $root 'src'
$out  = Join-Path $root 'build'
New-Item -ItemType Directory -Force $out | Out-Null
& $csc /nologo /target:winexe /platform:x64 /optimize+ /langversion:latest /codepage:65001 /utf8output `
    /out:"$out\Mrid6Helper.exe" /win32manifest:"$src\app.manifest" /win32icon:"$root\assets\mrid6.ico" `
    /r:"$fw\mscorlib.dll" /r:"$fw\System.dll" /r:"$fw\System.Core.dll" /r:"$fw\System.Drawing.dll" `
    /r:"$fw\System.Windows.Forms.dll" /r:"$fw\System.Management.dll" `
    "$src\Mi.cs" "$src\MainForm.cs" "$src\Program.cs" "$src\FanCurve.cs" "$src\AutoStart.cs" "$src\AssemblyInfo.cs"
if ($LASTEXITCODE -ne 0) { throw "csc failed: $LASTEXITCODE" }
Get-FileHash "$out\Mrid6Helper.exe" -Algorithm SHA256 | ForEach-Object { "sha256 $($_.Hash)  Mrid6Helper.exe ($((Get-Item $_.Path).Length) bytes)" }
