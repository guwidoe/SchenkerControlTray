using System.ComponentModel;
using System.Text.Json.Serialization;

namespace SchenkerControlTray;

internal sealed class FanStatus
{
    public string? OperatingMode { get; set; }
    public string? GamingProfileIndex { get; set; }
    public string? OfficeProfileIndex { get; set; }
    public string? TurboProfileIndex { get; set; }
    public string? CustomProfileIndex { get; set; }
    public string? FanBoostEnable { get; set; }
    public string? ProfileName { get; set; }
    public string? FAN_FanSwitchSpeed { get; set; }
    public string? FAN_TableName { get; set; }
    public bool OcSupport { get; set; }
    public bool IsNvGpu { get; set; }
    public string? TurboModeOption { get; set; }
    public bool FanBoostBtnSupport { get; set; }
    public string? PowerMode { get; set; }

    [JsonIgnore]
    public ProfileMode CurrentMode => ProfileModeExtensions.FromOperatingMode(OperatingMode);

    [JsonIgnore]
    public int CurrentProfileIndex => CurrentMode switch
    {
        ProfileMode.Balanced => ParseIndex(OfficeProfileIndex),
        ProfileMode.Enthusiast => ParseIndex(GamingProfileIndex),
        ProfileMode.Turbo => ParseIndex(TurboProfileIndex),
        ProfileMode.Custom => ParseIndex(CustomProfileIndex),
        _ => 0,
    };

    [JsonIgnore]
    public bool TurboSupported => TurboModeOption != "0";

    private static int ParseIndex(string? value) => int.TryParse(value, out var index) ? index : 0;
}

internal sealed class TrayStatus
{
    public string? OperatingMode { get; set; }
    public string? FanBoostEnable { get; set; }
}

internal sealed class SupportInfo
{
    public int ServiceReady { get; set; }
    public int FanSettingsSupport { get; set; }
    public int RamFan1p5Support { get; set; }
    public int OcSettingsSupport { get; set; }
    public bool FanBoostBtnSupport { get; set; }
    public bool IsNvGpu { get; set; }
    public bool IsAMDPlatform { get; set; }
}

internal sealed class StatusSnapshot
{
    public FanStatus? FanStatus { get; init; }
    public TrayStatus? TrayStatus { get; init; }
    public SupportInfo? SupportInfo { get; init; }
}

internal sealed class FanPoint : INotifyPropertyChanged
{
    private int _upT;
    private int _downT;
    private int _duty;

    public int ID { get; set; }

    public int UpT
    {
        get => _upT;
        set
        {
            if (_upT == value) return;
            _upT = value;
            OnPropertyChanged(nameof(UpT));
        }
    }

    public int DownT
    {
        get => _downT;
        set
        {
            if (_downT == value) return;
            _downT = value;
            OnPropertyChanged(nameof(DownT));
        }
    }

    public int Duty
    {
        get => _duty;
        set
        {
            if (_duty == value) return;
            _duty = value;
            OnPropertyChanged(nameof(Duty));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public FanPoint Clone() => new()
    {
        ID = ID,
        UpT = UpT,
        DownT = DownT,
        Duty = Duty,
    };

    private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

internal sealed class FanTable
{
    public bool Activated { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool FanControlRespective { get; set; }
    public int CpuTemp_DefaultMaxLevel { get; set; }
    public int GpuTemp_DefaultMaxLevel { get; set; }
    public List<FanPoint> CPU { get; set; } = [];
    public List<FanPoint> GPU { get; set; } = [];

    public FanTable Clone() => new()
    {
        Activated = Activated,
        Name = Name,
        FanControlRespective = FanControlRespective,
        CpuTemp_DefaultMaxLevel = CpuTemp_DefaultMaxLevel,
        GpuTemp_DefaultMaxLevel = GpuTemp_DefaultMaxLevel,
        CPU = CPU.Select(p => p.Clone()).ToList(),
        GPU = GPU.Select(p => p.Clone()).ToList(),
    };
}

internal sealed class ProfileDefinition
{
    public ProfileMode Mode { get; init; }
    public int ProfileIndex { get; init; }
    public string ProfileName { get; init; } = string.Empty;
    public string TableName { get; init; } = string.Empty;

    public string DisplayName => $"{Mode.DisplayName()} · Profile {ProfileIndex + 1} · {TableName}";

    public override string ToString() => DisplayName;
}

internal sealed class UserProfileFile
{
    public string Name { get; set; } = string.Empty;
    public FanSettings FAN { get; set; } = new();
}

internal sealed class FanSettings
{
    public string TableName { get; set; } = string.Empty;
}
