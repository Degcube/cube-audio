using System.Diagnostics;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using CubeAudio.Audio;
using CubeAudio.Boost;
using CubeAudio.Shell;
using Microsoft.Win32;

namespace CubeAudio;

public partial class MainWindow : Window
{
    private const double MeterFloorDb = -60;
    private const double MeterFallPerTick = 1.5;

    private static readonly TimeSpan SampleLength = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PeakHold = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan ReopenDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan EffectStartup = TimeSpan.FromSeconds(2);

    private readonly BoostSettings _settings = new(Registry.LocalMachine);
    private readonly EndpointEffects _effects = new(Registry.LocalMachine);
    private readonly DispatcherTimer _timer;
    private Microphone? _microphone;
    private MicrophoneMonitor? _monitor;
    private BoostState _state;
    private bool _loading;
    private bool _busy;
    private bool _monitorSettled;
    private double _levelDb = MeterFloorDb;
    private double _peakDb = MeterFloorDb;
    private DateTime _peakAt;
    private DateTime _reopenAt;
    private DateTime _monitorOpenedAt;

    public MainWindow()
    {
        InitializeComponent();

        MicrophoneButton.ContextMenuOpening += (_, _) => FillMicrophoneMenu();

        Select(PickMicrophone());

        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Render, OnTick, Dispatcher);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        NativeMethods.UseDarkTitleBar(new WindowInteropHelper(this).Handle, 0x12, 0x12, 0x14);
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _monitor?.Dispose();

        base.OnClosed(e);
    }

    private Microphone? PickMicrophone()
    {
        var microphones = Microphone.All();
        var defaultId = Microphone.DefaultId();

        return microphones.FirstOrDefault(microphone => _effects.IsAttached(microphone.Endpoint))
            ?? microphones.FirstOrDefault(microphone => microphone.Id == defaultId)
            ?? microphones.FirstOrDefault();
    }

    private void Select(Microphone? microphone)
    {
        _microphone = microphone;
        MicrophoneName.Text = microphone?.Name ?? "No microphone";

        OpenMonitor();
        RefreshState();
        ShowStateHint();
    }

    private void OpenMonitor()
    {
        _monitor?.Dispose();
        _monitor = null;
        _monitorSettled = false;
        _reopenAt = DateTime.UtcNow + ReopenDelay;

        if (_microphone is null)
            return;

        try
        {
            _monitor = new MicrophoneMonitor(_microphone);
            _monitorOpenedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Opening the microphone failed: {ex.Message}");
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;

        if (_monitor is null or { Stopped: true } && now >= _reopenAt && !_busy)
            OpenMonitor();

        if (!_monitorSettled && !_busy && _monitor is { Stopped: false } && now - _monitorOpenedAt > EffectStartup)
        {
            _monitorSettled = true;
            RefreshState();

            if (_state == BoostState.NeedsRepair)
                ShowStateHint();
        }

        var level = Math.Max(Calibration.ToDb(_monitor?.TakePeak() ?? 0), MeterFloorDb);

        _levelDb = Math.Max(level, _levelDb - MeterFallPerTick);

        if (level >= _peakDb || now - _peakAt > PeakHold)
        {
            _peakDb = level >= _peakDb ? level : _levelDb;
            _peakAt = now;
        }

        Level.Show(_levelDb, _peakDb);
        LevelText.Text = _peakDb <= MeterFloorDb ? "" : _peakDb >= -1.5 ? "limiting" : FormattableString.Invariant($"{_peakDb:0} dB");
    }

    private void RefreshState()
    {
        var status = _microphone is null
            ? new BoostStatus(BoostState.Unsupported, "No microphone found")
            : BoostInstallation.GetStatus(_microphone.Endpoint);

        if (status.State == BoostState.Active && _monitorSettled && !BoostInstallation.EffectIsRunning)
            status = new BoostStatus(BoostState.NeedsRepair, "Windows is not running the boost effect on this microphone");

        _state = status.State;

        StateText.Text = status.Text;
        StateDot.Fill = (Brush)FindResource(_state switch
        {
            BoostState.Active => "Primary",
            BoostState.NeedsRepair => "Danger",
            _ => "Text25"
        });

        InstallButton.Visibility = _state == BoostState.Unsupported ? Visibility.Collapsed : Visibility.Visible;
        InstallButton.Style = (Style)FindResource(_state == BoostState.Active ? typeof(Button) : (object)"PrimaryButton");
        InstallButton.Content = _state switch
        {
            BoostState.Active => "Remove",
            BoostState.NeedsRepair => "Repair",
            _ => "Enable"
        };

        _loading = true;
        GainSlider.Value = _settings.GainDb;
        EnabledToggle.IsChecked = _settings.Enabled;
        _loading = false;

        ShowGain();
        RefreshControls();
    }

    private void RefreshControls()
    {
        var active = _state == BoostState.Active;

        BoostPanel.IsEnabled = active && !_busy;
        BoostPanel.Opacity = active ? 1 : 0.4;
        CalibrateButton.IsEnabled = active && !_busy;
        TestButton.IsEnabled = _microphone is not null && !_busy;
        InstallButton.IsEnabled = !_busy;
        MicrophoneButton.IsEnabled = !_busy;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        RefreshControls();
    }

    private void ShowGain()
    {
        var on = EnabledToggle.IsChecked == true;

        GainText.Text = on ? FormattableString.Invariant($"+{GainSlider.Value:0.#} dB") : "Off";
        GainText.Foreground = (Brush)FindResource(on ? "Text" : "Text50");
    }

    private void ShowStateHint()
    {
        Hint.Text = _state switch
        {
            BoostState.NotInstalled => "Enable asks for admin rights once and restarts Windows audio for a moment, so running calls and recordings get interrupted.",
            BoostState.NeedsRepair => "Repair asks for admin rights and restarts Windows audio for a moment.",
            _ => ""
        };
    }

    private void OnGainChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading || !IsInitialized)
            return;

        Save(() => _settings.GainDb = e.NewValue);
        ShowGain();
    }

    private void OnEnabledClick(object sender, RoutedEventArgs e)
    {
        Save(() => _settings.Enabled = EnabledToggle.IsChecked == true);
        ShowGain();
    }

    private bool Save(Action change)
    {
        try
        {
            change();
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or SecurityException or IOException)
        {
            Hint.Text = $"Saving failed: {ex.Message}";
            return false;
        }
    }

    private void OnMicrophoneClick(object sender, RoutedEventArgs e)
    {
        FillMicrophoneMenu();

        MicrophoneMenu.PlacementTarget = MicrophoneButton;
        MicrophoneMenu.IsOpen = true;
    }

    private void FillMicrophoneMenu()
    {
        MicrophoneMenu.Items.Clear();

        foreach (var microphone in Microphone.All())
        {
            var item = new MenuItem
            {
                Header = _effects.IsAttached(microphone.Endpoint) ?$"{microphone.Name}  ·  boosted" : microphone.Name,
                IsChecked = microphone.Id == _microphone?.Id
            };

            item.Click += (_, _) => Select(microphone);
            MicrophoneMenu.Items.Add(item);
        }
    }

    private async void OnInstallClick(object sender, RoutedEventArgs e)
    {
        if (_microphone is null)
            return;

        var command = _state == BoostState.Active ? ElevatedCommands.Detach : ElevatedCommands.Attach;
        var endpoint = _microphone.Endpoint;

        SetBusy(true);
        Hint.Text = "Waiting for Windows…";

        var done = await Task.Run(() => Elevation.Run(command, endpoint));

        for (var attempt = 0; attempt < 8; attempt++)
        {
            OpenMonitor();

            if (_monitor is not null)
                break;

            await Task.Delay(500);
        }

        SetBusy(false);
        RefreshState();

        if (_state == BoostState.Active && _monitor is null)
            Hint.Text = "The microphone doesn't open with the boost attached. Click Remove to get it back the way it was.";
        else if (done && _state == BoostState.Active)
            Hint.Text = "Boost is on. Use Auto-calibrate, or move the slider until your voice peaks in the brighter part of the level bar.";
        else
            ShowStateHint();
    }

    private async void OnCalibrateClick(object sender, RoutedEventArgs e)
    {
        var monitor = _monitor;

        if (monitor is null)
            return;

        var wasEnabled = _settings.Enabled;

        if (!Save(() => _settings.Enabled = false))
            return;

        SetBusy(true);

        try
        {
            await Task.Delay(300);

            var samples = await RecordWithCountdown(monitor, "Talk in your normal voice");
            var result = Calibration.Analyze(samples, monitor.SampleRate);

            if (result.Heard)
            {
                _settings.GainDb = result.GainDb;
                _settings.Enabled = true;

                Hint.Text = FormattableString.Invariant($"Set to +{result.GainDb:0.#} dB, your voice came in at {result.VoiceDb:0} dB.")
                    + (result.GainDb >= BoostSettings.MaxGainDb ? " That is the maximum." : "");
            }
            else
            {
                _settings.Enabled = wasEnabled;
                Hint.Text = "Didn't hear a voice, nothing changed. Check that the microphone isn't muted and try again.";
            }
        }
        catch (Exception ex)
        {
            Hint.Text = ex.Message;
            Save(() => _settings.Enabled = wasEnabled);
        }

        SetBusy(false);
        RefreshState();
    }

    private async void OnTestClick(object sender, RoutedEventArgs e)
    {
        var monitor = _monitor;

        if (monitor is null)
        {
            Hint.Text = "The microphone is not available right now.";
            return;
        }

        SetBusy(true);

        try
        {
            var samples = await RecordWithCountdown(monitor, "Recording, say something");

            using var player = RecordingPlayer.TryCreate(samples, monitor.SampleRate)
                ?? throw new InvalidOperationException("No speakers or headphones found.");

            var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            player.Finished += () => finished.TrySetResult();

            Hint.Text = "Playing it back, this is how others hear you";
            player.Play();

            await Task.WhenAny(finished.Task, Task.Delay(SampleLength + TimeSpan.FromSeconds(2)));

            Hint.Text = "";
        }
        catch (Exception ex)
        {
            Hint.Text = ex.Message;
        }

        SetBusy(false);
    }

    private async Task<float[]> RecordWithCountdown(MicrophoneMonitor monitor, string text)
    {
        var recording = monitor.Record(SampleLength);

        for (var left = (int)SampleLength.TotalSeconds; left > 0 && !recording.IsCompleted; left--)
        {
            Hint.Text = $"{text}… {left}";
            await Task.WhenAny(recording, Task.Delay(1000));
        }

        return await recording;
    }
}
