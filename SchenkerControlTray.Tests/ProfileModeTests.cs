namespace SchenkerControlTray.Tests;

public class ProfileModeTests
{
    [Theory]
    [InlineData("0", 0)]
    [InlineData("1", 1)]
    [InlineData("2", 2)]
    [InlineData("3", 3)]
    [InlineData(null, 0)]
    [InlineData("unknown", 0)]
    public void FromOperatingMode_MapsExpectedValues(string? rawValue, int expected)
    {
        var actual = ProfileModeExtensions.FromOperatingMode(rawValue);
        Assert.Equal(expected, (int)actual);
    }

    [Theory]
    [InlineData(0, "OPERATING_OFFICE_MODE")]
    [InlineData(1, "OPERATING_GAMING_MODE")]
    [InlineData(2, "OPERATING_TURBO_MODE")]
    [InlineData(3, "OPERATING_CUSTOM_MODE")]
    public void ActionName_ReturnsExpectedCommand(int modeValue, string expected)
    {
        var mode = (ProfileMode)modeValue;
        Assert.Equal(expected, mode.ActionName());
    }
}
