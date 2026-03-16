namespace SchenkerControlTray;

internal enum ProfileMode
{
    Balanced = 0,
    Enthusiast = 1,
    Turbo = 2,
    Custom = 3,
}

internal static class ProfileModeExtensions
{
    public static string DisplayName(this ProfileMode mode) => mode switch
    {
        ProfileMode.Balanced => "Balanced",
        ProfileMode.Enthusiast => "Enthusiast",
        ProfileMode.Turbo => "Turbo",
        ProfileMode.Custom => "Custom",
        _ => mode.ToString(),
    };

    public static string ActionName(this ProfileMode mode) => mode switch
    {
        ProfileMode.Balanced => "OPERATING_OFFICE_MODE",
        ProfileMode.Enthusiast => "OPERATING_GAMING_MODE",
        ProfileMode.Turbo => "OPERATING_TURBO_MODE",
        ProfileMode.Custom => "OPERATING_CUSTOM_MODE",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    public static ProfileMode FromOperatingMode(string? value)
    {
        return value switch
        {
            "0" => ProfileMode.Balanced,
            "1" => ProfileMode.Enthusiast,
            "2" => ProfileMode.Turbo,
            "3" => ProfileMode.Custom,
            _ => ProfileMode.Balanced,
        };
    }
}
