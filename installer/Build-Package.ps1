param(
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64',
    [string]$Version = '1.0.0'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$projectDir = Join-Path $repoRoot 'SchenkerControlTray'
$installerProject = Join-Path $repoRoot 'installer\SchenkerControlTray.Installer\SchenkerControlTray.Installer.wixproj'
$publishDir = Join-Path $projectDir "bin\$Configuration\net8.0-windows\$Runtime\publish"
$distDir = Join-Path $repoRoot "dist\SchenkerControlTray-$Runtime"
$zipPath = Join-Path $repoRoot "dist\SchenkerControlTray-$Runtime.zip"
$msiBuildPath = Join-Path $repoRoot "installer\SchenkerControlTray.Installer\bin\$Configuration\SchenkerControlTray.Installer.msi"
$msiDistPath = Join-Path $repoRoot "dist\SchenkerControlTray-$Runtime-$Version.msi"

Push-Location $projectDir
try {
    dotnet publish -c $Configuration -r $Runtime --self-contained false -p:Version=$Version
}
finally {
    Pop-Location
}

Push-Location (Split-Path $installerProject)
try {
    dotnet build $installerProject -c $Configuration -p:ProductVersion=$Version
}
finally {
    Pop-Location
}

if (Test-Path $distDir) {
    Remove-Item $distDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $distDir | Out-Null
Copy-Item -Path (Join-Path $publishDir '*') -Destination $distDir -Recurse -Force
Copy-Item -Path (Join-Path $PSScriptRoot 'Install.ps1') -Destination $distDir -Force
Copy-Item -Path (Join-Path $PSScriptRoot 'Uninstall.ps1') -Destination $distDir -Force
Copy-Item -Path $msiBuildPath -Destination $distDir -Force

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}
Compress-Archive -Path (Join-Path $distDir '*') -DestinationPath $zipPath

Copy-Item -Path $msiBuildPath -Destination $msiDistPath -Force

Write-Host "Publish: $publishDir"
Write-Host "Package: $distDir"
Write-Host "Zip:     $zipPath"
Write-Host "MSI:     $msiDistPath"
