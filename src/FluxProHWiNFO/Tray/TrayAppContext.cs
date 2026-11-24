using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using FluxProHWiNFO.Core;
using FluxProHWiNFO.HWiNFO;
using FluxProHWiNFO.Logging;
using FluxProHWiNFO.Startup;
using Microsoft.Win32;

namespace FluxProHWiNFO.Tray;

/// <summary>The tray icon and its menu. All display work happens on <see cref="DisplayWorker"/>'s thread.</summary>
internal sealed class TrayAppContext : ApplicationContext
{
    private const int MaxTooltipLength = 127;

    private readonly DisplayWorker _worker;
    private readonly TrayIcons _icons = new();
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu = new();
    private readonly SynchronizationContext _ui;

    private readonly ToolStripMenuItem _cpuItem = InfoItem();
    private readonly ToolStripMenuItem _cpuSourceItem = InfoItem();
    private readonly ToolStripMenuItem _gpuItem = InfoItem();
    private readonly ToolStripMenuItem _gpuSourceItem = InfoItem();
    private readonly ToolStripMenuItem _hwinfoItem = InfoItem();
    private readonly ToolStripMenuItem _displayItem = InfoItem();
    private readonly ToolStripMenuItem _configErrorItem = InfoItem();
    private readonly ToolStripMenuItem _autostartItem;

    private WorkerStatus? _status;
    private bool _exiting;

    public TrayAppContext(DisplayWorker worker)
    {
        _worker = worker;
        _ui = new WindowsFormsSynchronizationContext();

        _autostartItem = new ToolStripMenuItem("Start with Windows", null, (_, _) => ToggleAutostart());
        _menu.Items.AddRange(new ToolStripItem[]
        {
            _cpuItem, _cpuSourceItem, _gpuItem, _gpuSourceItem,
            new ToolStripSeparator(),
            _hwinfoItem, _displayItem, _configErrorItem,
            new ToolStripSeparator(),
            new ToolStripMenuItem("Open config", null, (_, _) => OpenFile(_worker.ConfigPath)),
            new ToolStripMenuItem("Reload config", null, (_, _) => _worker.ReloadConfig()),
            new ToolStripMenuItem("Open log", null, (_, _) => OpenFile(Log.FilePath)),
            _autostartItem,
            new ToolStripSeparator(),
            new ToolStripMenuItem("Exit", null, (_, _) => Exit()),
        });
        _menu.Opening += (_, _) => RefreshMenu();
        _menu.ShowItemToolTips = true;

        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = _menu,
            Icon = _icons.Error,
            Text = $"{AppInfo.Name}: starting",
            Visible = true,
        };

        RemoveDuplicateAutostart();

        _worker.StatusChanged += OnStatusChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionEnding += OnSessionEnding;

        if (_worker.Status is { } initial)
            Apply(initial);
    }

    private static ToolStripMenuItem InfoItem() => new() { Enabled = false };

    // Raised on the worker thread.
    private void OnStatusChanged(WorkerStatus status) => _ui.Post(_ => Apply(status), null);

    private void Apply(WorkerStatus status)
    {
        if (_exiting)
            return;
        _status = status;
        _notifyIcon.Icon = _icons.For(Severity(status));
        _notifyIcon.Text = Truncate(Tooltip(status), MaxTooltipLength);
        if (_menu.Visible)
            RefreshMenu();
    }

    private static TraySeverity Severity(WorkerStatus s)
    {
        if (!s.DeviceConnected)
            return TraySeverity.Error;
        bool allGood = s.Readings.Status == HwinfoStatus.Ok && s.Readings.Cpu is not null && s.Readings.Gpu is not null && s.ConfigError is null;
        return allGood ? TraySeverity.Ok : TraySeverity.Warning;
    }

    private static string Tooltip(WorkerStatus s)
    {
        string unit = s.Fahrenheit ? "°F" : "°C";
        string text = $"{AppInfo.Name}\nCPU {Format(s.Readings.CpuDisplay, unit)}   GPU {Format(s.Readings.GpuDisplay, unit)}";
        string? problem = !s.DeviceConnected ? $"Display {s.DeviceError ?? "not connected"}"
            : s.Readings.Status != HwinfoStatus.Ok ? HwinfoText(s.Readings.Status)
            : s.ConfigError is not null ? "Config error (see menu)"
            : null;
        return problem is null ? text : $"{text}\n{problem}";
    }

    private void RefreshMenu()
    {
        var config = _worker.Config;
        var s = _status;
        string unit = config.Fahrenheit ? "°F" : "°C";

        _cpuItem.Text = $"CPU  {Format(s?.Readings.CpuDisplay, unit)}";
        _gpuItem.Text = $"GPU  {Format(s?.Readings.GpuDisplay, unit)}";
        _cpuSourceItem.Text = SourceText(s, s?.Readings.Cpu, config.Cpu.ToSelector());
        _gpuSourceItem.Text = SourceText(s, s?.Readings.Gpu, config.Gpu.ToSelector());

        _hwinfoItem.Text = s is null ? "HWiNFO: checking…" : $"HWiNFO: {HwinfoText(s.Readings.Status)}";
        _displayItem.Text = s is null ? "Display: connecting…"
            : s.DeviceConnected ? "Display: connected"
            : $"Display: {s.DeviceError ?? "not connected"} (retrying)";

        _configErrorItem.Visible = s?.ConfigError is not null;
        _configErrorItem.Text = $"Config error, using previous settings: {Truncate(s?.ConfigError ?? "", 80)}";

        RefreshAutostartItem();
    }

    private void RefreshAutostartItem()
    {
        bool allUsers;
        try { allUsers = Autostart.IsEnabledForAllUsers(); }
        catch (Exception) { allUsers = false; }

        if (allUsers)
        {
            // Set by an "all users" install; changing it needs admin rights, so the toggle is locked.
            _autostartItem.Text = "Start with Windows: on for all users";
            _autostartItem.ToolTipText = "Set by the installer. To change it, run Setup again and untick Start with Windows.";
            _autostartItem.Checked = true;
            _autostartItem.Enabled = false;
            return;
        }

        _autostartItem.Text = "Start with Windows";
        _autostartItem.ToolTipText = null;
        _autostartItem.Enabled = true;
        try { _autostartItem.Checked = Autostart.IsEnabled(); }
        catch (Exception) { _autostartItem.Checked = false; }
    }

    private static string SourceText(WorkerStatus? s, SensorMatch? match, SensorSelector selector)
    {
        if (match is not null)
            return $"    {match.Entry.Label}  ·  {match.Entry.Sensor}";
        if (s is null || s.Readings.Status != HwinfoStatus.Ok)
            return "    —";
        return $"    no match for {selector}";
    }

    private static string HwinfoText(HwinfoStatus status) => status switch
    {
        HwinfoStatus.Ok => "OK",
        HwinfoStatus.NotRunning => "not running",
        HwinfoStatus.KeyMissing => "Gadget reporting is off",
        _ => "can't read values",
    };

    private static string Format(double? value, string unit) =>
        value is double v ? $"{v.ToString("0.0", CultureInfo.CurrentCulture)} {unit}" : "--";

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..(max - 1)] + "…";

    /// <summary>
    /// An all-users entry makes this user's own entry redundant. Remove it, so sign-in doesn't start two
    /// copies (the second would exit, but an entry from 0.1.0 has no --background and would show a message box).
    /// </summary>
    private static void RemoveDuplicateAutostart()
    {
        try
        {
            if (Autostart.IsEnabledForAllUsers() && Autostart.HasUserEntry())
            {
                Autostart.Disable();
                Log.Info("Start with Windows is on for all users; removed this user's duplicate entry");
            }
        }
        catch (Exception ex)
        {
            Log.Error("Checking Start with Windows entries failed", ex);
        }
    }

    private void ToggleAutostart()
    {
        try
        {
            if (Autostart.IsEnabledForAllUsers())
                return; // the menu item is disabled in this state; nothing to toggle per user
            if (Autostart.IsEnabled())
            {
                Autostart.Disable();
                Log.Info("Start with Windows: off");
            }
            else
            {
                Autostart.Enable();
                Log.Info($"Start with Windows: on ({Autostart.Command})");
            }
        }
        catch (Exception ex)
        {
            Log.Error("Changing Start with Windows failed", ex);
            MessageBox.Show($"Couldn't change Start with Windows:\n{ex.Message}", AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static void OpenFile(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            MessageBox.Show($"File not found:\n{path}", AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();
        }
        catch (Win32Exception)
        {
            // No program is associated with the extension.
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{path}\"") { UseShellExecute = true })?.Dispose();
        }
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            Log.Info("Resumed from sleep");
            _worker.RequestReopen();
        }
    }

    private void OnSessionEnding(object? sender, SessionEndingEventArgs e)
    {
        Log.Info($"Windows session ending ({e.Reason})");
        _worker.Stop();
    }

    private void Exit()
    {
        if (_exiting)
            return;
        _exiting = true;
        Log.Info("Exit chosen from the tray menu");
        _notifyIcon.Visible = false;
        _worker.Stop();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _exiting = true;
            _worker.StatusChanged -= OnStatusChanged;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.SessionEnding -= OnSessionEnding;
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _menu.Dispose();
            _icons.Dispose();
        }
        base.Dispose(disposing);
    }
}
