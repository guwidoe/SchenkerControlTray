namespace SchenkerControlTray;

internal static class FanCurveRules
{
    public static void ValidatePoints(IEnumerable<FanPoint> points, string label)
    {
        foreach (var point in points)
        {
            if (point.UpT is < 0 or > 255)
            {
                throw new InvalidOperationException($"{label} point {point.ID}: UpT must be between 0 and 255.");
            }

            if (point.DownT is < 0 or > 255)
            {
                throw new InvalidOperationException($"{label} point {point.ID}: DownT must be between 0 and 255.");
            }

            if (point.Duty is < 0 or > 100)
            {
                throw new InvalidOperationException($"{label} point {point.ID}: Duty must be between 0 and 100.");
            }
        }
    }
}
