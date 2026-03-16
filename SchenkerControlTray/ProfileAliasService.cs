using System.Text.Json;

namespace SchenkerControlTray;

internal sealed class ProfileAliasService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public string? GetAlias(ProfileMode mode, int profileIndex)
    {
        var aliases = LoadAliases();
        return aliases.TryGetValue(BuildKey(mode, profileIndex), out var alias) ? alias : null;
    }

    public void SaveAlias(ProfileMode mode, int profileIndex, string? alias)
    {
        var aliases = LoadAliases();
        var key = BuildKey(mode, profileIndex);
        var normalized = Normalize(alias);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            aliases.Remove(key);
        }
        else
        {
            aliases[key] = normalized;
        }

        Directory.CreateDirectory(AppPaths.SettingsDirectory);
        File.WriteAllText(AppPaths.ProfileAliasesPath, JsonSerializer.Serialize(aliases, JsonOptions));
    }

    internal static string BuildKey(ProfileMode mode, int profileIndex) => $"{(int)mode}:{profileIndex}";

    private static Dictionary<string, string> LoadAliases()
    {
        try
        {
            if (!File.Exists(AppPaths.ProfileAliasesPath))
            {
                return [];
            }

            var aliases = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(AppPaths.ProfileAliasesPath), JsonOptions);
            return aliases ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string? Normalize(string? alias) => string.IsNullOrWhiteSpace(alias) ? null : alias.Trim();
}
