using AutoClicker.Core;

namespace AutoClicker.Tests;

public sealed class CoreTests
{
    [Fact]
    public void AppSettings_Normalize_ClampsIntervalAndSeparatesHotkeys()
    {
        var settings = new AppSettings
        {
            IntervalMs = 0,
            ToggleHotkey = HotkeyKey.F6,
            EmergencyStopHotkey = HotkeyKey.F6
        };

        settings.Normalize();

        Assert.Equal(AppSettings.MinIntervalMs, settings.IntervalMs);
        Assert.Equal(HotkeyKey.F6, settings.ToggleHotkey);
        Assert.NotEqual(settings.ToggleHotkey, settings.EmergencyStopHotkey);
    }

    [Fact]
    public void AppSettings_Normalize_SnapsCustomIntervalToNearestPreset()
    {
        var settings = new AppSettings { IntervalMs = 250 };

        settings.Normalize();

        Assert.Equal(200, settings.IntervalMs);
    }

    [Fact]
    public async Task SettingsStore_SaveAndLoad_RoundTripsSettings()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "settings.json");
        var store = new SettingsStore(settingsPath);
        var settings = new AppSettings
        {
            IntervalMs = 200,
            ToggleHotkey = HotkeyKey.F5,
            EmergencyStopHotkey = HotkeyKey.F9
        };

        await store.SaveAsync(settings);
        var loaded = await store.LoadAsync();

        Assert.Equal(200, loaded.IntervalMs);
        Assert.Equal(HotkeyKey.F5, loaded.ToggleHotkey);
        Assert.Equal(HotkeyKey.F9, loaded.EmergencyStopHotkey);
    }

    [Fact]
    public async Task ClickEngine_StartAndStop_TransitionsAndClicks()
    {
        var injector = new RecordingInputInjector();
        using var engine = new ClickEngine(injector);
        var settings = new AppSettings { IntervalMs = 20 };

        engine.Start(settings);
        await Task.Delay(55);
        engine.Stop();
        var clicksAfterStop = injector.Clicks.Count;
        await Task.Delay(40);

        Assert.False(engine.IsRunning);
        Assert.True(clicksAfterStop >= 1);
        Assert.Equal(clicksAfterStop, injector.Clicks.Count);
    }

    [Fact]
    public void HotkeyService_Register_RejectsDuplicateHotkeys()
    {
        using var service = new HotkeyService(() => { }, () => { });

        var ex = Assert.Throws<InvalidOperationException>(() => service.Register(HotkeyKey.F6, HotkeyKey.F6));

        Assert.Contains("must be different", ex.Message);
    }

    private sealed class RecordingInputInjector : IInputInjector
    {
        public List<int> Clicks { get; } = [];

        public void Click()
        {
            Clicks.Add(1);
        }
    }
}
