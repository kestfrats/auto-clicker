namespace AutoClicker.Core;

public sealed class ClickEngine : IDisposable
{
    private readonly IInputInjector _inputInjector;
    private readonly object _gate = new();
    private CancellationTokenSource? _clickCancellation;
    private Task? _clickTask;

    public ClickEngine(IInputInjector inputInjector)
    {
        _inputInjector = inputInjector;
    }

    public bool IsRunning { get; private set; }
    public string? LastError { get; private set; }

    public event EventHandler? StateChanged;

    public void Toggle(AppSettings settings)
    {
        if (IsRunning)
        {
            Stop();
        }
        else
        {
            Start(settings);
        }
    }

    public void Start(AppSettings settings)
    {
        lock (_gate)
        {
            if (IsRunning)
            {
                return;
            }

            var activeSettings = settings.CloneNormalized();
            LastError = null;
            _clickCancellation = new CancellationTokenSource();
            IsRunning = true;
            _clickTask = Task.Run(() => ClickLoopAsync(activeSettings, _clickCancellation.Token));
        }

        OnStateChanged();
    }

    public void Stop()
    {
        CancellationTokenSource? cancellation;

        lock (_gate)
        {
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;
            cancellation = _clickCancellation;
            _clickCancellation = null;
        }

        cancellation?.Cancel();
        OnStateChanged();
    }

    private async Task ClickLoopAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                _inputInjector.Click();
                await Task.Delay(settings.IntervalMs, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Stop();
        }
    }

    private void OnStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        Stop();
        _clickCancellation?.Dispose();
    }
}
