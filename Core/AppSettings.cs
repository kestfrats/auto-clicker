namespace AutoClicker.Core;

public sealed class AppSettings
{
    public const int MinIntervalMs = 5;
    public const int MaxIntervalMs = 2000;
    public static readonly int[] PresetIntervalsMs = [1000, 500, 200, 100, 67, 50, 33, 20, 13, 10, 7, 5];

    public int IntervalMs { get; set; } = 100;
    public HotkeyKey ToggleHotkey { get; set; } = HotkeyKey.F6;
    public HotkeyKey EmergencyStopHotkey { get; set; } = HotkeyKey.F8;

    public void Normalize()
    {
        IntervalMs = SnapToPresetInterval(ClampInterval(IntervalMs));

        if (!Enum.IsDefined(ToggleHotkey))
        {
            ToggleHotkey = HotkeyKey.F6;
        }

        if (!Enum.IsDefined(EmergencyStopHotkey) || EmergencyStopHotkey == ToggleHotkey)
        {
            EmergencyStopHotkey = ToggleHotkey == HotkeyKey.F8 ? HotkeyKey.F6 : HotkeyKey.F8;
        }
    }

    public AppSettings CloneNormalized()
    {
        var clone = new AppSettings
        {
            IntervalMs = IntervalMs,
            ToggleHotkey = ToggleHotkey,
            EmergencyStopHotkey = EmergencyStopHotkey
        };
        clone.Normalize();
        return clone;
    }

    public static int ClampInterval(int intervalMs)
    {
        return Math.Clamp(intervalMs, MinIntervalMs, MaxIntervalMs);
    }

    public static int SnapToPresetInterval(int intervalMs)
    {
        return PresetIntervalsMs
            .OrderBy(preset => Math.Abs(preset - intervalMs))
            .ThenBy(preset => preset)
            .First();
    }
}
