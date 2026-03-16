namespace SchenkerControlTray.Tests;

public class FanPointTests
{
    [Fact]
    public void SettingProperty_RaisesPropertyChanged()
    {
        var point = new FanPoint();
        var changed = new List<string?>();
        point.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        point.UpT = 50;
        point.DownT = 45;
        point.Duty = 30;

        Assert.Equal(new[] { nameof(FanPoint.UpT), nameof(FanPoint.DownT), nameof(FanPoint.Duty) }, changed);
    }
}
