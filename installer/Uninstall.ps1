param(
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'SchenkerControlTray\App')
)

$ErrorActionPreference = 'Stop'

Get-Process SchenkerControlTray -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
Remove-ItemProperty -Path $runKey -Name 'SchenkerControlTray' -ErrorAction SilentlyContinue

$shortcutPath = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Schenker Control Tray.lnk'
Remove-Item -Path $shortcutPath -Force -ErrorAction SilentlyContinue

if (Test-Path $InstallDir) {
    Remove-Item -Path $InstallDir -Recurse -Force
}

Write-Host 'Uninstall complete.'
