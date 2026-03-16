# SchenkerControlTray

Small tray app for SCHENKER / XMG Control Center systems.

## License

MIT. See [LICENSE](LICENSE).

## Features

- tray app for quick profile switching
- per-mode subprofiles
- startup toggle: **Start with Windows**
- fan curve editor with:
  - live CPU/GPU charts
  - point sliders for temperature and duty
  - direct grid editing
  - JSON backup on every save
- MSI installer build
- optional PowerShell install/uninstall scripts
- unit tests for core non-UI logic

## Reverse-engineering notes

This machine uses the Uniwill / Tongfang `ControlCenter3` stack.

Relevant pieces:

- broker: `127.0.0.1:13688`
- service: `C:\Program Files\OEM\Control Center\UniwillService\MyControlCenter\GCUService.exe`
- control topic: `Fan/Control`
- status topic: `Fan/Status`

Working actions:

- `GETSTATUS`
- `OPERATING_OFFICE_MODE`
- `OPERATING_GAMING_MODE`
- `OPERATING_TURBO_MODE`
- `OPERATING_CUSTOM_MODE`

Per-mode subprofiles work via:

```json
{
  "Action": "OPERATING_GAMING_MODE",
  "ProfileIndex": 1
}
```

`ProfileIndex` is zero-based.

## Fan curves

The app edits the same fan table JSON files used by Control Center:

- `C:\Program Files\OEM\Control Center\UniwillService\MyControlCenter\UserFanTables`

Profile-to-table mapping comes from:

- `C:\Program Files\OEM\Control Center\UniwillService\MyControlCenter\UserPofiles`

Backups are written to:

- `%LocalAppData%\SchenkerControlTray\Backups`

## Build

```powershell
cd C:\Repositories\SchenkerControlTray
dotnet build SchenkerControlTray.sln
```

## Test

```powershell
cd C:\Repositories\SchenkerControlTray
dotnet test SchenkerControlTray.sln
```

## Publish app

```powershell
cd C:\Repositories\SchenkerControlTray\SchenkerControlTray
dotnet publish -c Release -r win-x64 --self-contained false
```

## Build MSI + package

Files involved:

- `installer\SchenkerControlTray.Installer\SchenkerControlTray.Installer.wixproj`
- `installer\SchenkerControlTray.Installer\Package.wxs`
- `installer\Build-Package.ps1`

Build everything:

```powershell
cd C:\Repositories\SchenkerControlTray\installer
.\Build-Package.ps1
```

Optional version override:

```powershell
.\Build-Package.ps1 -Version 1.0.6
```

Outputs:

- folder package: `dist\SchenkerControlTray-win-x64`
- zip package: `dist\SchenkerControlTray-win-x64.zip`
- MSI: `dist\SchenkerControlTray-win-x64-1.0.5.msi`

You can also build the installer directly after publishing:

```powershell
cd C:\Repositories\SchenkerControlTray
dotnet build .\installer\SchenkerControlTray.Installer\SchenkerControlTray.Installer.wixproj -c Release -p:ProductVersion=1.0.5
```

## PowerShell install scripts

Scripts live in:

- `installer\Install.ps1`
- `installer\Uninstall.ps1`

Install:

```powershell
cd C:\Repositories\SchenkerControlTray\installer
.\Install.ps1 -EnableStartup
```

Uninstall:

```powershell
cd C:\Repositories\SchenkerControlTray\installer
.\Uninstall.ps1
```

## Run manually

```powershell
cd C:\Repositories\SchenkerControlTray\SchenkerControlTray\bin\Debug\net8.0-windows
.\SchenkerControlTray.exe
```
