namespace SchenkerControlTray.Tests;

public class FanCurveRulesTests
{
    [Fact]
    public void ValidatePoints_AcceptsValidPoints()
    {
        var points = new[]
        {
            new FanPoint { ID = 0, UpT = 40, DownT = 35, Duty = 20 },
            new FanPoint { ID = 1, UpT = 60, DownT = 55, Duty = 45 },
        };

        FanCurveRules.ValidatePoints(points, "CPU");
    }

    [Fact]
    public void ValidatePoints_RejectsDutyAbove100()
    {
        var points = new[]
        {
            new FanPoint { ID = 3, UpT = 70, DownT = 65, Duty = 101 },
        };

        var ex = Assert.Throws<InvalidOperationException>(() => FanCurveRules.ValidatePoints(points, "GPU"));
        Assert.Contains("GPU point 3: Duty must be between 0 and 100.", ex.Message);
    }
}
