using System.Text.Json;
using System.Text.RegularExpressions;

namespace SchenkerControlTray;

internal sealed class FanTableService
{
    private static readonly Regex UserProfileRegex = new(@"^Mode(?<mode>\d+)_Profile(?<profile>\d+)\.json$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public IReadOnlyList<ProfileDefinition> GetProfileDefinitions()
    {
        if (!Directory.Exists(AppPaths.UserProfilesDirectory))
        {
            return Array.Empty<ProfileDefinition>();
        }

        var profiles = new List<ProfileDefinition>();
        foreach (var path in Directory.EnumerateFiles(AppPaths.UserProfilesDirectory, "Mode*_Profile*.json"))
        {
            var fileName = Path.GetFileName(path);
            var match = UserProfileRegex.Match(fileName);
            if (!match.Success)
            {
                continue;
            }

            var userProfile = JsonSerializer.Deserialize<UserProfileFile>(File.ReadAllText(path), JsonOptions);
            if (userProfile is null)
            {
                continue;
            }

            var modeValue = int.Parse(match.Groups["mode"].Value) - 1;
            var profileIndex = int.Parse(match.Groups["profile"].Value) - 1;
            if (modeValue < 0 || modeValue > 3)
            {
                continue;
            }

            profiles.Add(new ProfileDefinition
            {
                Mode = (ProfileMode)modeValue,
                ProfileIndex = profileIndex,
                ProfileName = userProfile.Name,
                TableName = userProfile.FAN.TableName,
            });
        }

        return profiles
            .OrderBy(p => p.Mode)
            .ThenBy(p => p.ProfileIndex)
            .ToList();
    }

    public FanTable LoadFanTable(string tableName)
    {
        var path = GetFanTablePath(tableName);
        var fanTable = JsonSerializer.Deserialize<FanTable>(File.ReadAllText(path), JsonOptions);
        return fanTable ?? throw new InvalidOperationException($"Failed to load fan table: {tableName}");
    }

    public string SaveFanTable(FanTable table)
    {
        Directory.CreateDirectory(AppPaths.BackupDirectory);

        var targetPath = GetFanTablePath(table.Name);
        var backupPath = Path.Combine(
            AppPaths.BackupDirectory,
            $"{table.Name}-{DateTime.Now:yyyyMMdd-HHmmss}.json");

        File.Copy(targetPath, backupPath, overwrite: true);
        File.WriteAllText(targetPath, JsonSerializer.Serialize(table, JsonOptions));

        return backupPath;
    }

    public string GetFanTablePath(string tableName)
    {
        var path = Path.Combine(AppPaths.UserFanTablesDirectory, $"{tableName}.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Fan table not found: {tableName}", path);
        }

        return path;
    }
}
