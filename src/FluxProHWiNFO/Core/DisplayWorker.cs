using System.Diagnostics;
using FluxProHWiNFO.Config;
using FluxProHWiNFO.Display;
using FluxProHWiNFO.HWiNFO;
using FluxProHWiNFO.Logging;

namespace FluxProHWiNFO.Core;

public sealed record WorkerStatus(
    Readings Readings,
    bool DeviceConnected,
    string? DeviceError,
    bool Fahrenheit,
    string? ConfigError);

/// <summary>
/// Background loop: read HWiNFO, encode, write to the display, about once a second.
/// The display goes dark ~2 s after the last write, so it keeps writing even when there is
/// nothing to show (dashes). It never throws: device loss, HWiNFO restarts and bad config are
/// logged once per change and retried.
/// </summary>
public sealed class DisplayWorker : IDisposable
{
    public const int DeviceRetryMs = 3000;
    public const int HwinfoCheckMs = 5000;
    public const int ConfigCheckMs = 2000;

    private readonly string _configPath;
    private readonly VsbReader _reader = new();
    private readonly ManualResetEventSlim _stop = new();
    private Thread? _thread;

    private volatile bool _reopenRequested;
    private volatile bool _configReloadRequested;

    private volatile AppConfig _config = new();
    private DateTime _configStamp = DateTime.MinValue;
    private string? _configError;
    private long _nextConfigCheck;

    private bool _hwinfoRunning;
    private long _nextHwinfoCheck;

    private FluxDisplayDevice? _display;
    private long _nextDeviceAttempt;
    private string? _deviceError;

    private volatile WorkerStatus? _status;

    // What has already been logged, so the log only records changes.
    private HwinfoStatus? _loggedHwinfo;
    private string? _loggedCpu = "\0";
    private string? _loggedGpu = "\0";
    private bool? _loggedDevice;
    private string? _loggedError;

    public DisplayWorker(string configPath) => _configPath = configPath;

    public event Action<WorkerStatus>? StatusChanged;

    public string ConfigPath => _configPath;
    public AppConfig Config => _config;
    public WorkerStatus? Status => _status;

    public void Start()
    {
        if (_thread is not null)
            return;
        _thread = new Thread(Run) { IsBackground = true, Name = "DisplayWorker" };
        _thread.Start();
    }

    /// <summary>Close and reopen the device on the next tick (e.g. after resume from sleep).</summary>
    public void RequestReopen() => _reopenRequested = true;

    public void ReloadConfig() => _configReloadRequested = true;

    /// <summary>Stops the loop and sends a final blank frame. Safe to call more than once.</summary>
    public void Stop()
    {
        _stop.Set();
        _thread?.Join(TimeSpan.FromSeconds(3));
    }

    public void Dispose()
    {
        Stop();
        _stop.Dispose();
    }

    private void Run()
    {
        Log.Info("Display worker started");
        while (!_stop.IsSet)
        {
            int interval = AppConfig.MaxIntervalMs;
            try
            {
                interval = Tick();
            }
            catch (Exception ex)
            {
                LogErrorOnce($"Unexpected error in display loop: {ex}");
            }
            _stop.Wait(interval);
        }
        Shutdown();
    }

    private int Tick()
    {
        long now = Environment.TickCount64;

        if (_configReloadRequested || now >= _nextConfigCheck)
        {
            bool force = _configReloadRequested;
            _configReloadRequested = false;
            _nextConfigCheck = now + ConfigCheckMs;
            MaybeReloadConfig(force);
        }

        if (now >= _nextHwinfoCheck)
        {
            _nextHwinfoCheck = now + HwinfoCheckMs;
            _hwinfoRunning = IsHwinfoRunning();
        }

        VsbSnapshot? snapshot = null;
        if (_hwinfoRunning)
        {
            try
            {
                snapshot = _reader.Read();
            }
            catch (Exception ex)
            {
                LogErrorOnce($"Reading {RegistryVsbSource.DisplayPath} failed: {ex.Message}");
            }
        }

        var config = _config;
        var readings = ReadingResolver.Resolve(snapshot, _hwinfoRunning, config);
        LogReadingChanges(readings, config);

        SendToDisplay(PayloadEncoder.Encode(readings.CpuDisplay, readings.GpuDisplay), now);

        Publish(new WorkerStatus(readings, _display is not null, _deviceError, config.Fahrenheit, _configError));
        return config.EffectiveIntervalMs;
    }

    // ---- display ----

    private void SendToDisplay(byte[] payload, long now)
    {
        if (_reopenRequested)
        {
            _reopenRequested = false;
            if (_display is not null)
                Log.Info("Reopening the display");
            CloseDisplay();
            _nextDeviceAttempt = now;
        }

        if (_display is null && now >= _nextDeviceAttempt)
            TryOpenDisplay(now);
        if (_display is null)
            return;

        try
        {
            _display.Send(payload);
            _deviceError = null;
        }
        catch (Exception ex)
        {
            _deviceError = $"write failed: {ex.Message}";
            LogDevice(false, $"Display {_deviceError}; retrying every {DeviceRetryMs / 1000} s");
            CloseDisplay();
            _nextDeviceAttempt = now + DeviceRetryMs;
        }
    }

    private void TryOpenDisplay(long now)
    {
        try
        {
            var device = FluxDisplayDevice.FindWritable();
            if (device is null)
            {
                _deviceError = "not found";
                _nextDeviceAttempt = now + DeviceRetryMs;
                LogDevice(false, $"Display not found (USB VID {FluxDisplayDevice.VendorId:X4} PID {FluxDisplayDevice.ProductId:X4}); retrying every {DeviceRetryMs / 1000} s");
                return;
            }

            _display = FluxDisplayDevice.Open(device, ReportFraming.ReportIdZeroPadded);
            _deviceError = null;
            LogDevice(true, $"Display connected: {device.DevicePath}, output report {_display.ReportLength} bytes, framing {HidReportBuilder.CliName(_display.Framing)}");
        }
        catch (Exception ex)
        {
            CloseDisplay();
            _deviceError = $"open failed: {ex.Message}";
            _nextDeviceAttempt = now + DeviceRetryMs;
            LogDevice(false, $"Display {_deviceError}; retrying every {DeviceRetryMs / 1000} s");
        }
    }

    private void CloseDisplay()
    {
        try { _display?.Dispose(); }
        catch (Exception) { /* already gone */ }
        _display = null;
    }

    private void Shutdown()
    {
        if (_display is not null)
        {
            try { _display.Send(PayloadEncoder.EncodeBlank()); }
            catch (Exception) { /* exiting anyway */ }
            CloseDisplay();
        }
        Log.Info("Display worker stopped");
    }

    // ---- HWiNFO and config ----

    private static bool IsHwinfoRunning()
    {
        var processes = Process.GetProcessesByName("HWiNFO64");
        foreach (var p in processes)
            p.Dispose();
        return processes.Length > 0;
    }

    private void MaybeReloadConfig(bool force)
    {
        DateTime stamp = File.Exists(_configPath) ? File.GetLastWriteTimeUtc(_configPath) : DateTime.MinValue;
        if (!force && stamp == _configStamp && stamp != DateTime.MinValue)
            return;

        var (config, error) = ConfigStore.LoadOrCreate(_configPath);
        if (config is null)
        {
            // Keep the previous settings, and retry until the file parses (an editor may be mid-save).
            if (error != _configError)
                Log.Warn($"Config {_configPath} not loaded, keeping previous settings: {error}");
            _configError = error;
            return;
        }

        _configStamp = File.Exists(_configPath) ? File.GetLastWriteTimeUtc(_configPath) : DateTime.MinValue;
        _configError = null;
        _config = config;
        _loggedCpu = _loggedGpu = "\0"; // re-log matches under the new settings

        Log.Info($"Config loaded from {_configPath}: CPU {config.Cpu.ToSelector()}, GPU {config.Gpu.ToSelector()}, " +
                 $"interval {config.EffectiveIntervalMs} ms, unit {(config.Fahrenheit ? "°F" : "°C")}");
        if (config.PollIntervalMs != config.EffectiveIntervalMs)
            Log.Warn($"pollIntervalMs {config.PollIntervalMs} is outside {AppConfig.MinIntervalMs}–{AppConfig.MaxIntervalMs}; using {config.EffectiveIntervalMs} " +
                     "(the display goes dark if it gets no data for about 2 s)");
        if (config.Fahrenheit)
            Log.Info("Fahrenheit: the display has two integer digits, so values above 99.9 °F show as 99.9");
    }

    // ---- status and logging ----

    private void Publish(WorkerStatus status)
    {
        if (Equals(status, _status))
            return;
        _status = status;
        try { StatusChanged?.Invoke(status); }
        catch (Exception ex) { LogErrorOnce($"Status listener failed: {ex.Message}"); }
    }

    private void LogReadingChanges(Readings readings, AppConfig config)
    {
        if (_loggedHwinfo != readings.Status)
        {
            _loggedHwinfo = readings.Status;
            switch (readings.Status)
            {
                case HwinfoStatus.Ok: Log.Info("HWiNFO: reading sensor values"); break;
                case HwinfoStatus.NotRunning: Log.Warn("HWiNFO64 is not running; the display shows dashes until it starts"); break;
                case HwinfoStatus.KeyMissing: Log.Warn($"{RegistryVsbSource.DisplayPath} not found; turn on Gadget reporting in HWiNFO's Sensor Settings"); break;
                case HwinfoStatus.ReadError: Log.Warn("HWiNFO registry values could not be read; the display shows dashes"); break;
            }
        }

        if (readings.Status != HwinfoStatus.Ok)
        {
            _loggedCpu = _loggedGpu = "\0";
            return;
        }
        LogMatchChange("CPU", readings.Cpu, config.Cpu, ref _loggedCpu);
        LogMatchChange("GPU", readings.Gpu, config.Gpu, ref _loggedGpu);
    }

    private static void LogMatchChange(string name, SensorMatch? match, SensorConfig config, ref string? logged)
    {
        // Ignore the index: HWiNFO renumbers entries, which isn't worth a log line.
        string? key = match is null ? null : $"{match.Entry.Label}|{match.Entry.Sensor}";
        if (key == logged)
            return;
        logged = key;

        if (match is null)
            Log.Warn($"{name}: no HWiNFO value matches {config.ToSelector()}; shows dashes");
        else
            Log.Info($"{name}: \"{match.Entry.Label}\" from \"{match.Entry.Sensor}\"");
    }

    private void LogDevice(bool connected, string message)
    {
        if (_loggedDevice == connected)
            return;
        _loggedDevice = connected;
        if (connected) Log.Info(message); else Log.Warn(message);
    }

    private void LogErrorOnce(string message)
    {
        if (message == _loggedError)
            return;
        _loggedError = message;
        Log.Error(message);
    }
}
