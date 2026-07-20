using AutoClicker.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AutoClicker;

/// <summary>
/// The main content page displayed inside the application window.
/// Add your UI logic, event handlers, and data binding here.
/// </summary>
public sealed partial class MainPage : Page
{
    private const double DesignWidth = 740;
    private const double DesignHeight = 452;
    private const double CompactBreakpoint = 720;
    private const double CompactMinWidth = 320;

    private readonly SettingsStore _settingsStore = new();
    private readonly ClickEngine _clickEngine = new(new InputInjector());
    private readonly HotkeyService _hotkeyService;

    private AppSettings _settings = new();
    private bool _syncingUi;
    private string? _statusMessage;

    public MainPage()
    {
        InitializeComponent();

        _hotkeyService = new HotkeyService(OnToggleHotkeyPressed, OnEmergencyStopHotkeyPressed);
        _clickEngine.StateChanged += ClickEngine_StateChanged;

        Loaded += MainPage_Loaded;
        Unloaded += MainPage_Unloaded;
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        ToggleHotkeyBox.ItemsSource = Enum.GetValues<HotkeyKey>();
        EmergencyHotkeyBox.ItemsSource = Enum.GetValues<HotkeyKey>();

        _settings = await _settingsStore.LoadAsync();
        ApplySettingsToUi();
        RegisterHotkeys();
        UpdateReadouts();
        UpdateStatus();
        ApplyResponsiveLayout(ActualWidth);
    }

    private void MainPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _clickEngine.StateChanged -= ClickEngine_StateChanged;
        _clickEngine.Dispose();
        _hotkeyService.Dispose();
    }

    private void HotkeyBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingUi)
        {
            return;
        }

        PullSettingsFromUi();
        RegisterHotkeys();
        UpdateReadouts();
        _ = SaveSettingsAsync();
    }

    private void ApplySettingsToUi()
    {
        _syncingUi = true;
        try
        {
            ToggleHotkeyBox.SelectedItem = _settings.ToggleHotkey;
            EmergencyHotkeyBox.SelectedItem = _settings.EmergencyStopHotkey;
            SelectMatchingCpsPreset(_settings.IntervalMs);
            UpdateTimingSummary();
            UpdateReadouts();
        }
        finally
        {
            _syncingUi = false;
        }
    }

    private void PullSettingsFromUi()
    {
        if (ToggleHotkeyBox.SelectedItem is HotkeyKey toggleHotkey)
        {
            _settings.ToggleHotkey = toggleHotkey;
        }

        if (EmergencyHotkeyBox.SelectedItem is HotkeyKey emergencyStopHotkey)
        {
            _settings.EmergencyStopHotkey = emergencyStopHotkey;
        }
    }

    private void RegisterHotkeys()
    {
        if (_settings.ToggleHotkey == _settings.EmergencyStopHotkey)
        {
            _statusMessage = "Hotkeys must differ.";
            UpdateStatus();
            return;
        }

        try
        {
            _hotkeyService.Register(_settings.ToggleHotkey, _settings.EmergencyStopHotkey);
            _statusMessage = null;
            UpdateReadouts();
            UpdateStatus();
        }
        catch (Exception ex)
        {
            _statusMessage = ex.Message;
            UpdateStatus();
        }
    }

    private void SelectMatchingCpsPreset(int intervalMs)
    {
        foreach (var button in GetPresetButtons())
        {
            var isSelected = TryGetPresetInterval(button, out var presetInterval) && presetInterval == intervalMs;
            button.Background = (Microsoft.UI.Xaml.Media.Brush)Resources[isSelected ? "AmberBrush" : "CarbonBrush"];
            button.BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Resources[isSelected ? "NavGoldBrush" : "ChromeIndigoBrush"];
            button.BorderThickness = isSelected ? new Thickness(2, 1, 2, 5) : new Thickness(1, 1, 1, 3);
            button.Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources[isSelected ? "InkBrush" : "CanvasSoftBrush"];
            button.Resources["ButtonBackgroundPointerOver"] = (Microsoft.UI.Xaml.Media.Brush)Resources[isSelected ? "AmberBrush" : "CarbonHoverBrush"];
            button.Resources["ButtonBackgroundPressed"] = (Microsoft.UI.Xaml.Media.Brush)Resources[isSelected ? "NavGoldBrush" : "AmberBrush"];
            button.Resources["ButtonBorderBrushPointerOver"] = (Microsoft.UI.Xaml.Media.Brush)Resources[isSelected ? "NavGoldBrush" : "CanvasSoftBrush"];
            button.Resources["ButtonBorderBrushPressed"] = (Microsoft.UI.Xaml.Media.Brush)Resources["NavGoldBrush"];
            button.Resources["ButtonForegroundPointerOver"] = (Microsoft.UI.Xaml.Media.Brush)Resources[isSelected ? "InkBrush" : "CanvasSoftBrush"];
            button.Resources["ButtonForegroundPressed"] = (Microsoft.UI.Xaml.Media.Brush)Resources["InkBrush"];
        }
    }

    private void CpsPresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || !TryGetPresetInterval(button, out var interval))
        {
            return;
        }

        _settings.IntervalMs = AppSettings.ClampInterval(interval);
        SelectMatchingCpsPreset(_settings.IntervalMs);
        UpdateTimingSummary();
        UpdateReadouts();
        _ = SaveSettingsAsync();
    }

    private void UpdateTimingSummary()
    {
        var timingNote = _settings.IntervalMs < 20 ? " / TURBO LIMITS MAY APPLY" : string.Empty;
        SelectedCpsText.Text = FormatPresetCps(_settings.IntervalMs);
        SelectedIntervalText.Text = $"{_settings.IntervalMs} MS / LEFT CLICK{timingNote}";
    }

    private void UpdateReadouts()
    {
        var cps = 1000d / Math.Max(AppSettings.MinIntervalMs, _settings.IntervalMs);
        var litSegmentBrush = (Microsoft.UI.Xaml.Media.Brush)Resources[_clickEngine.IsRunning ? "SignalBrush" : "CanvasSoftBrush"];

        var litSegments = cps switch
        {
            >= 150 => 6,
            >= 100 => 5,
            >= 50 => 4,
            >= 20 => 3,
            >= 10 => 2,
            _ => 1
        };

        var segments = new[] { SpeedSegment1, SpeedSegment2, SpeedSegment3, SpeedSegment4, SpeedSegment5, SpeedSegment6 };
        for (var index = 0; index < segments.Length; index++)
        {
            segments[index].Fill = index < litSegments ? litSegmentBrush : (Microsoft.UI.Xaml.Media.Brush)Resources["GaugeOffBrush"];
            segments[index].Opacity = index < litSegments ? (_clickEngine.IsRunning ? 1 : 0.76) : 0.5;
        }

        StatusToggleHotkeyText.Text = _settings.ToggleHotkey.ToString().ToUpperInvariant();
        StatusStopHotkeyText.Text = _settings.EmergencyStopHotkey.ToString().ToUpperInvariant();
    }

    private void UpdateStatus()
    {
        UpdateReadouts();
        SelectedCpsText.Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources[_clickEngine.IsRunning ? "SignalBrush" : "AmberBrush"];
        SelectedIntervalText.Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources[_clickEngine.IsRunning ? "SignalBrush" : "CanvasSoftBrush"];

        if (_clickEngine.IsRunning)
        {
            SetStatusWord("CLICKING", 30, true);
            StatusText.Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["SignalBrush"];
            StatusPlate.Background = (Microsoft.UI.Xaml.Media.Brush)Resources["CarbonBrush"];
            StatusPlate.BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Resources["SignalBrush"];
            StatusDetailText.Text = _statusMessage ?? $"Left click every {_settings.IntervalMs} ms. Press {_settings.EmergencyStopHotkey} to stop.";
            return;
        }

        SetStatusWord("IDLE", 40, false);
        StatusText.Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["CanvasSoftBrush"];
        StatusPlate.Background = (Microsoft.UI.Xaml.Media.Brush)Resources["CarbonBrush"];
        StatusPlate.BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Resources["ChromeIndigoBrush"];
        StatusDetailText.Text = _statusMessage ?? _clickEngine.LastError ?? $"{_settings.ToggleHotkey} starts clicking. {_settings.EmergencyStopHotkey} stops everything.";
    }

    private void SetStatusWord(string text, double fontSize, bool showOutline)
    {
        var statusTextBlocks = new[] { StatusText, StatusOutlineLeftText, StatusOutlineRightText, StatusOutlineTopText, StatusDropText };
        foreach (var textBlock in statusTextBlocks)
        {
            textBlock.Text = text;
            textBlock.FontSize = fontSize;
        }

        StatusOutlineLeftText.Opacity = showOutline ? 1 : 0;
        StatusOutlineRightText.Opacity = showOutline ? 1 : 0;
        StatusOutlineTopText.Opacity = showOutline ? 1 : 0;
        StatusDropText.Opacity = showOutline ? 0.48 : 0;
    }

    private void PageRoot_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyResponsiveLayout(e.NewSize.Width);
    }

    private void ApplyResponsiveLayout(double availablePageWidth)
    {
        if (availablePageWidth <= 0)
        {
            return;
        }

        var isCompact = availablePageWidth < CompactBreakpoint;
        PageRoot.Padding = isCompact ? new Thickness(10) : new Thickness(18);

        var horizontalPadding = PageRoot.Padding.Left + PageRoot.Padding.Right;
        var shellWidth = Math.Min(DesignWidth, Math.Max(CompactMinWidth, availablePageWidth - horizontalPadding));

        ContentShell.Width = shellWidth;
        ChromeFrame.Width = shellWidth;

        if (isCompact)
        {
            ContentShell.VerticalAlignment = VerticalAlignment.Top;
            ChromeFrame.Height = double.NaN;
            ChromeFrame.MinHeight = 0;
            CornerChrome.Visibility = Visibility.Collapsed;

            MainContentGrid.ColumnSpacing = 0;
            MainContentGrid.RowSpacing = 12;

            StatusColumn.Width = new GridLength(1, GridUnitType.Star);
            DividerColumn.Width = new GridLength(0);
            ControlsColumn.Width = new GridLength(0);

            Grid.SetColumn(StatusPanel, 0);
            Grid.SetRow(StatusPanel, 0);

            Grid.SetColumn(MainDivider, 0);
            Grid.SetRow(MainDivider, 1);
            MainDivider.Height = 1;
            MainDivider.HorizontalAlignment = HorizontalAlignment.Stretch;
            MainDivider.VerticalAlignment = VerticalAlignment.Center;

            Grid.SetColumn(ControlsPanel, 0);
            Grid.SetRow(ControlsPanel, 2);
            return;
        }

        ContentShell.VerticalAlignment = VerticalAlignment.Center;
        ChromeFrame.Height = DesignHeight;
        ChromeFrame.MinHeight = DesignHeight;
        CornerChrome.Visibility = Visibility.Visible;
        CornerChrome.Width = DesignWidth;
        CornerChrome.Height = DesignHeight;

        MainContentGrid.ColumnSpacing = 12;
        MainContentGrid.RowSpacing = 0;

        StatusColumn.Width = new GridLength(260);
        DividerColumn.Width = new GridLength(1);
        ControlsColumn.Width = new GridLength(1, GridUnitType.Star);

        Grid.SetColumn(StatusPanel, 0);
        Grid.SetRow(StatusPanel, 0);

        Grid.SetColumn(MainDivider, 1);
        Grid.SetRow(MainDivider, 0);
        MainDivider.Height = double.NaN;
        MainDivider.HorizontalAlignment = HorizontalAlignment.Stretch;
        MainDivider.VerticalAlignment = VerticalAlignment.Stretch;

        Grid.SetColumn(ControlsPanel, 2);
        Grid.SetRow(ControlsPanel, 0);
    }

    private void ClickEngine_StateChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(UpdateStatus);
    }

    private void OnToggleHotkeyPressed()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            PullSettingsFromUi();
            _clickEngine.Toggle(_settings);
            UpdateStatus();
        });
    }

    private void OnEmergencyStopHotkeyPressed()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _clickEngine.Stop();
            UpdateStatus();
        });
    }

    private Button[] GetPresetButtons()
    {
        return
        [
            Cps1Button,
            Cps2Button,
            Cps5Button,
            Cps10Button,
            Cps15Button,
            Cps20Button,
            Cps30Button,
            Cps50Button,
            Cps75Button,
            Cps100Button,
            Cps150Button,
            Cps200Button
        ];
    }

    private static bool TryGetPresetInterval(Button button, out int interval)
    {
        return int.TryParse(button.Tag?.ToString(), out interval);
    }

    private static string FormatPresetCps(int intervalMs)
    {
        return intervalMs switch
        {
            1000 => "1",
            500 => "2",
            200 => "5",
            100 => "10",
            67 => "15",
            50 => "20",
            33 => "30",
            20 => "50",
            13 => "75",
            10 => "100",
            7 => "150",
            5 => "200",
            _ => $"{1000d / Math.Max(AppSettings.MinIntervalMs, intervalMs):0.##}"
        };
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            await _settingsStore.SaveAsync(_settings);
        }
        catch (Exception ex)
        {
            _statusMessage = $"Settings save failed: {ex.Message}";
            UpdateStatus();
        }
    }

}
