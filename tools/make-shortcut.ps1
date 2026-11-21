# Ярлык «MRID6 Helper» на рабочем столе. Запускает не сам exe (на нём Windows рисует щит UAC), а задачу планировщика
# «Mrid6Helper-Open» с повышенными правами — без запроса и без щита. Задача создаётся приложением при запуске (или ключом --install-open-task).
param([string]$Exe = (Join-Path (Split-Path $PSScriptRoot) 'build\Mrid6Helper.exe'))
$desk = [Environment]::GetFolderPath('Desktop')
$lnk  = Join-Path $desk 'MRID6 Helper.lnk'
$ws   = New-Object -ComObject WScript.Shell
$s = $ws.CreateShortcut($lnk)
$s.TargetPath       = Join-Path $env:SystemRoot 'System32\schtasks.exe'
$s.Arguments        = '/Run /TN Mrid6Helper-Open'
$s.WorkingDirectory = Split-Path $Exe
$s.WindowStyle      = 7                      # свёрнуто: без мелькания консоли
$s.IconLocation     = "$Exe,0"
$s.Description      = 'MRID6 Helper — управление ноутбуком MECHREVO Jiaolong MRID6'
$s.Save()
# убрать флаг «Запускать от имени администратора» (бит 0x20 в байте 0x15), если он был
$b = [IO.File]::ReadAllBytes($lnk); $b[0x15] = $b[0x15] -band (-bnot 0x20); [IO.File]::WriteAllBytes($lnk, $b)
"ярлык: $lnk"
