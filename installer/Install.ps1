param(
    [string]$PublishDir = (Join-Path $PSScriptRoot '..\SchenkerControlTray\bin\Release\net8.0-windows\win-x64\publish'),
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'SchenkerControlTray\App'),
    [switch]$EnableStartup,
    [bool]$LaunchAfterInstall = $true
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $PublishDir)) {
    throw "Publish directory not found: $PublishDir`nRun .\Build-Package.ps1 first or pass -PublishDir."
}

Write-Host "Installing from: $PublishDir"
Write-Host "Installing to:   $InstallDir"

Get-Process SchenkerControlTray -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
Copy-Item -Path (Join-Path $PublishDir '*') -Destination $InstallDir -Recurse -Force

$exePath = Join-Path $InstallDir 'SchenkerControlTray.exe'
if (-not (Test-Path $exePath)) {
    throw "Installed exe not found: $exePath"
}

$programsDir = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
$shortcutPath = Join-Path $programsDir 'Schenker Control Tray.lnk'
$wshell = New-Object -ComObject WScript.Shell
$shortcut = $wshell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exePath
$shortcut.WorkingDirectory = $InstallDir
$shortcut.IconLocation = "$exePath,0"
$shortcut.Description = 'Schenker Control Tray'
$shortcut.Save()

$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
if ($EnableStartup) {
    Set-ItemProperty -Path $runKey -Name 'SchenkerControlTray' -Value ('"' + $exePath + '"')
}

if ($LaunchAfterInstall) {
    Start-Process -FilePath $exePath
}

Write-Host 'Install complete.'
Write-Host "Start menu shortcut: $shortcutPath"
if ($EnableStartup) {
    Write-Host 'Startup enabled.'
}
