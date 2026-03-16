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

    private readonly ProfileAliasService _aliasService;

    public FanTableService()
        : this(new ProfileAliasService())
    {
    }

    internal FanTableService(ProfileAliasService aliasService)
    {
        _aliasService = aliasService;
    }

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

            var oemModeValue = int.Parse(match.Groups["mode"].Value);
            var mode = TryMapOemMode(oemModeValue);
            if (mode is null)
            {
                continue;
            }

            var profileIndex = int.Parse(match.Groups["profile"].Value) - 1;
            profiles.Add(new ProfileDefinition
            {
                Mode = mode.Value,
                ProfileIndex = profileIndex,
                ProfileName = userProfile.Name,
                CustomizeName = userProfile.CustomizeName,
                AliasName = _aliasService.GetAlias(mode.Value, profileIndex),
                SuggestedName = BuildSuggestedName(mode.Value, profileIndex, userProfile),
                TableName = userProfile.FAN.TableName,
                Summary = BuildSummary(userProfile),
            });
        }

        return profiles
            .OrderBy(p => p.Mode)
            .ThenBy(p => p.ProfileIndex)
            .ToList();
    }

    public void SaveProfileAlias(ProfileMode mode, int profileIndex, string? alias)
        => _aliasService.SaveAlias(mode, profileIndex, alias);

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

    internal static ProfileMode? TryMapOemMode(int oemMode)
        => oemMode switch
        {
            1 => ProfileMode.Enthusiast,
            2 => ProfileMode.Balanced,
            3 => ProfileMode.Turbo,
            4 => ProfileMode.Custom,
            _ => null,
        };

    internal static string BuildSummary(UserProfileFile profile)
    {
        var parts = new List<string>();

        var cpuWatts = profile.CPU.PL1 > 0 ? profile.CPU.PL1 : profile.CPU.AmdSPL;
        if (cpuWatts > 0)
        {
            parts.Add($"{cpuWatts}W CPU");
        }

        var gpuPart = BuildGpuSummary(profile.GPU);
        if (!string.IsNullOrWhiteSpace(gpuPart))
        {
            parts.Add(gpuPart);
        }

        if (!string.IsNullOrWhiteSpace(profile.FAN.TableName))
        {
            parts.Add(profile.FAN.TableName);
        }

        return string.Join(" · ", parts);
    }

    internal static string? BuildFriendlyProfileName(ProfileDefinition profile)
    {
        var alias = profile.AliasName?.Trim();
        if (!string.IsNullOrWhiteSpace(alias))
        {
            return alias;
        }

        var trimmed = profile.CustomizeName?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmed) &&
            trimmed != (profile.ProfileIndex + 1).ToString() &&
            !string.Equals(trimmed, profile.ProfileName, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        if (!string.IsNullOrWhiteSpace(profile.SuggestedName))
        {
            return profile.SuggestedName;
        }

        return null;
    }

    internal static string BuildSuggestedName(ProfileMode mode, int profileIndex, UserProfileFile profile)
    {
        var reducedGpu = IsReducedGpu(profile.GPU);

        return mode switch
        {
            ProfileMode.Balanced when reducedGpu => "Battery Saver",
            ProfileMode.Balanced when profileIndex == 0 => "Quiet",
            ProfileMode.Balanced when profileIndex == 1 => "Quiet (Alt Curve)",
            ProfileMode.Balanced => "Balanced",

            ProfileMode.Enthusiast when reducedGpu => "Gaming Eco GPU",
            ProfileMode.Enthusiast when profile.GPU.ConfigurableTGPSwitch == 1 && profile.GPU.ConfigurableTGPTarget >= 150 => "Gaming dGPU Max",
            ProfileMode.Enthusiast when profile.GPU.DynamicBoostSwitch == 1 && profile.GPU.DynamicBoost > 0 => "Gaming Boost",
            ProfileMode.Enthusiast => "Gaming",

            ProfileMode.Turbo when profile.GPU.CoreClockOffset != 0 => "Turbo OC",
            ProfileMode.Turbo => "Turbo",

            ProfileMode.Custom => $"Custom {profileIndex + 1}",
            _ => $"Profile {profileIndex + 1}",
        };
    }

    private static bool IsReducedGpu(GpuProfileSettings gpu)
        => gpu.ConfigurableTGPSwitch == 0 &&
           gpu.ConfigurableTGPTarget == 0 &&
           gpu.DynamicBoostSwitch == 0 &&
           gpu.DynamicBoost == 0 &&
           gpu.CoreClockOffset == 0;

    private static string? BuildGpuSummary(GpuProfileSettings gpu)
    {
        var gpuBits = new List<string>();

        if (gpu.ConfigurableTGPSwitch == 1 && gpu.ConfigurableTGPTarget > 0)
        {
            gpuBits.Add($"{gpu.ConfigurableTGPTarget}W GPU");
        }

        if (gpu.DynamicBoostSwitch == 1 && gpu.DynamicBoost > 0)
        {
            gpuBits.Add($"+{gpu.DynamicBoost}W boost");
        }

        if (gpu.CoreClockOffset != 0)
        {
            gpuBits.Add($"{gpu.CoreClockOffset:+#;-#;0} MHz core");
        }

        if (gpuBits.Count == 0 && IsReducedGpu(gpu))
        {
            gpuBits.Add("reduced GPU");
        }

        return gpuBits.Count == 0 ? null : string.Join(" ", gpuBits);
    }
}
