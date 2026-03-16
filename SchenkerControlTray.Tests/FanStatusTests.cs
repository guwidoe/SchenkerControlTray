namespace SchenkerControlTray.Tests;

public class FanStatusTests
{
    [Fact]
    public void CurrentProfileIndex_UsesProfileFieldForCurrentMode()
    {
        var status = new FanStatus
        {
            OperatingMode = "1",
            GamingProfileIndex = "2",
            OfficeProfileIndex = "0",
            TurboProfileIndex = "5",
            CustomProfileIndex = "7",
        };

        Assert.Equal(ProfileMode.Enthusiast, status.CurrentMode);
        Assert.Equal(2, status.CurrentProfileIndex);
    }

    [Theory]
    [InlineData("0", false)]
    [InlineData("1", true)]
    [InlineData(null, true)]
    public void TurboSupported_DependsOnTurboModeOption(string? rawValue, bool expected)
    {
        var status = new FanStatus { TurboModeOption = rawValue };
        Assert.Equal(expected, status.TurboSupported);
    }
}
