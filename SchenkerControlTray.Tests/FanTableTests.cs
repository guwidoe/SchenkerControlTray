namespace SchenkerControlTray.Tests;

public class FanTableTests
{
    [Fact]
    public void Clone_CreatesDeepCopy()
    {
        var original = new FanTable
        {
            Activated = true,
            Name = "M2T1",
            FanControlRespective = true,
            CpuTemp_DefaultMaxLevel = 9,
            GpuTemp_DefaultMaxLevel = 8,
            CPU = [new FanPoint { ID = 0, UpT = 45, DownT = 40, Duty = 25 }],
            GPU = [new FanPoint { ID = 1, UpT = 60, DownT = 55, Duty = 50 }],
        };

        var clone = original.Clone();
        clone.Name = "Changed";
        clone.CPU[0].Duty = 99;
        clone.GPU[0].UpT = 80;

        Assert.Equal("M2T1", original.Name);
        Assert.Equal(25, original.CPU[0].Duty);
        Assert.Equal(60, original.GPU[0].UpT);
    }
}
