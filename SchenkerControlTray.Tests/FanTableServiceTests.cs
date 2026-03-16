namespace SchenkerControlTray.Tests;

public class FanTableServiceTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 0)]
    [InlineData(3, 2)]
    [InlineData(4, 3)]
    public void TryMapOemMode_MapsKnownModes(int oemMode, int expectedMode)
    {
        var actual = FanTableService.TryMapOemMode(oemMode);
        Assert.True(actual.HasValue);
        Assert.Equal(expectedMode, (int)actual.Value);
    }

    [Fact]
    public void BuildSummary_IncludesUsefulPowerInfo()
    {
        var profile = new UserProfileFile
        {
            CPU = new CpuProfileSettings { PL1 = 45 },
            GPU = new GpuProfileSettings
            {
                ConfigurableTGPSwitch = 1,
                ConfigurableTGPTarget = 150,
                DynamicBoostSwitch = 1,
                DynamicBoost = 25,
            },
            FAN = new FanSettings { TableName = "M1T2" },
        };

        var summary = FanTableService.BuildSummary(profile);
        Assert.Equal("45W CPU · 150W GPU +25W boost · M1T2", summary);
    }

    [Fact]
    public void BuildSuggestedName_UsesUsefulDefaultForGamingBoost()
    {
        var profile = new UserProfileFile
        {
            CPU = new CpuProfileSettings { PL1 = 45 },
            GPU = new GpuProfileSettings
            {
                DynamicBoostSwitch = 1,
                DynamicBoost = 25,
            },
            FAN = new FanSettings { TableName = "M1T1" },
        };

        var name = FanTableService.BuildSuggestedName(ProfileMode.Enthusiast, 0, profile);
        Assert.Equal("Gaming Boost", name);
    }

    [Fact]
    public void BuildSuggestedName_UsesBatterySaverForReducedBalancedProfile()
    {
        var profile = new UserProfileFile
        {
            CPU = new CpuProfileSettings { PL1 = 15 },
            GPU = new GpuProfileSettings(),
            FAN = new FanSettings { TableName = "M2T3" },
        };

        var name = FanTableService.BuildSuggestedName(ProfileMode.Balanced, 2, profile);
        Assert.Equal("Battery Saver", name);
    }

    [Fact]
    public void FriendlyName_PrefersAliasOverOtherNames()
    {
        var profile = new ProfileDefinition
        {
            Mode = ProfileMode.Balanced,
            ProfileIndex = 1,
            ProfileName = "Mode2_Profile2",
            CustomizeName = "2",
            AliasName = "My Quiet Mode",
            SuggestedName = "Quiet (Alt Curve)",
            TableName = "M2T2",
            Summary = "15W CPU · M2T2",
        };

        Assert.Equal("My Quiet Mode", profile.FriendlyName);
    }
}
