namespace SchenkerControlTray;

internal static class AppPaths
{
    public const string GcuServicePath = @"C:\Program Files\OEM\Control Center\UniwillService\MyControlCenter\GCUService.exe";
    public const string UserProfilesDirectory = @"C:\Program Files\OEM\Control Center\UniwillService\MyControlCenter\UserPofiles";
    public const string UserFanTablesDirectory = @"C:\Program Files\OEM\Control Center\UniwillService\MyControlCenter\UserFanTables";
    public const string ControlCenterAppId = @"shell:AppsFolder\ControlCenter3_h329z55cwnj8g!App";

    public static string BackupDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SchenkerControlTray",
        "Backups");

    public static string SettingsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SchenkerControlTray");

    public static string ProfileAliasesPath => Path.Combine(SettingsDirectory, "ProfileAliases.json");
}
