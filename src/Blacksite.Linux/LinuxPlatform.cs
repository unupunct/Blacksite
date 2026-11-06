using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Blacksite.Core.Configuration;
using Blacksite.Core.Services;

namespace Blacksite.Linux;

public sealed class LinuxPlatform : IPlatformServices
{
    readonly Window _w;
    readonly AppSettings _s;
    readonly string _dataDir;
    TrayIcon? _tray;
    TimeSpan _cpuLast; DateTime _cpuAt = DateTime.UtcNow; double _cpu;

    public LinuxPlatform(Window w, AppSettings s, string dataDir) { _w = w; _s = s; _dataDir = dataDir; }

    public string PlatformName => "Linux";
    public string DeviceModel => $"Linux PC / {Environment.ProcessorCount}-core {RuntimeInformation.ProcessArchitecture}";
    public string OsVersion => RuntimeInformation.OSDescription;
    public bool IsDesktop => true;
    public bool IsTouchFirst => false;
    public string DataDirectory => _dataDir;
    public bool IsFullscreen => _w.WindowState == WindowState.FullScreen;

    public IReadOnlyList<MonitorInfo> Monitors
    {
        get
        {
            var l = new List<MonitorInfo>();
            var all = _w.Screens.All;
            for (int i = 0; i < all.Count; i++)
                l.Add(new MonitorInfo(i, $"Display {i + 1}", all[i].Bounds.Width, all[i].Bounds.Height, all[i].Scaling, all[i].IsPrimary));
            return l;
        }
    }

    Avalonia.Platform.Screen? ScreenAt(int index)
    {
        var all = _w.Screens.All;
        if (all.Count == 0) return null;
        return all[Math.Clamp(index, 0, all.Count - 1)];
    }

    public void MoveToMonitor(int monitorIndex)
    {
        var sc = ScreenAt(monitorIndex); if (sc == null) return;
        bool fs = _w.WindowState == WindowState.FullScreen, max = _w.WindowState == WindowState.Maximized;
        if (fs || max) _w.WindowState = WindowState.Normal;
        var wa = sc.WorkingArea;
        double scale = sc.Scaling;
        int w = (int)(_w.Width * scale), h = (int)(_w.Height * scale);
        if (double.IsNaN(_w.Width) || w <= 0) { w = wa.Width * 2 / 3; h = wa.Height * 2 / 3; }
        _w.Position = new PixelPoint(wa.X + Math.Max(0, (wa.Width - w) / 2), wa.Y + Math.Max(0, (wa.Height - h) / 2));
        if (fs) _w.WindowState = WindowState.FullScreen; else if (max) _w.WindowState = WindowState.Maximized;
    }

    public void ApplyWindowMode(WindowMode mode, int monitorIndex)
    {
        _w.WindowDecorations = _s.NoBorders && mode == WindowMode.Windowed ? WindowDecorations.None : WindowDecorations.Full;
        switch (mode)
        {
            case WindowMode.Windowed:
                _w.WindowState = WindowState.Normal;
                if (_s.WindowWidth >= 640) _w.Width = _s.WindowWidth;
                if (_s.WindowHeight >= 400) _w.Height = _s.WindowHeight;
                if (_s.WindowX >= 0 && _s.WindowY >= 0 && ScreenAt(monitorIndex) is { } sc0 && sc0.Bounds.Contains(new PixelPoint((int)_s.WindowX, (int)_s.WindowY)))
                    _w.Position = new PixelPoint((int)_s.WindowX, (int)_s.WindowY);
                else MoveToMonitor(monitorIndex);
                break;
            case WindowMode.Maximized:
                _w.WindowState = WindowState.Normal; MoveToMonitor(monitorIndex); _w.WindowState = WindowState.Maximized; break;
            default:
                // Borderless fullscreen on the chosen monitor. "Exclusive" maps to the same mode: Windows 10/11 promotes
                // borderless fullscreen windows to independent flip, which gives the benefits of exclusive mode without mode switches.
                _w.WindowState = WindowState.Normal; MoveToMonitor(monitorIndex); _w.WindowState = WindowState.FullScreen; break;
        }
    }

    public void SetAlwaysOnTop(bool on) => _w.Topmost = on;

    public void SetTray(bool on)
    {
        if (on && _tray == null)
        {
            var menu = new NativeMenu();
            var show = new NativeMenuItem("Show BLACKSITE"); show.Click += (_, _) => { _w.Show(); _w.WindowState = WindowState.Normal; _w.Activate(); };
            var quit = new NativeMenuItem("Quit"); quit.Click += (_, _) => Quit();
            menu.Items.Add(show); menu.Items.Add(quit);
            _tray = new TrayIcon { ToolTipText = "BLACKSITE", Menu = menu, Icon = _w.Icon, IsVisible = true };
            _tray.Clicked += (_, _) => { _w.Show(); _w.WindowState = WindowState.Normal; _w.Activate(); };
        }
        else if (_tray != null) _tray.IsVisible = on;
    }

    public void OpenNewInstance(string presetId, int monitorIndex, bool fullscreen)
    {
        try
        {
            var exe = Environment.ProcessPath!;
            var args = $"--instance --preset {presetId} --monitor {monitorIndex}" + (fullscreen ? " --fullscreen" : "");
            Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false });
        }
        catch { }
    }

    public void SetStartWithWindows(bool on)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "autostart");
            var f = Path.Combine(dir, "blacksite.desktop");
            if (on)
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(f, $"[Desktop Entry]\nType=Application\nName=BLACKSITE\nExec=\"{Environment.ProcessPath}\" --startup\nTerminal=false\nX-GNOME-Autostart-enabled=true\n");
            }
            else if (File.Exists(f)) File.Delete(f);
        }
        catch { }
    }

    public void Quit() { _tray?.Dispose(); _w.Close(); Environment.Exit(0); }

    // mobile no-ops
    public void ApplyImmersive(bool on) { }
    public void SetKeepScreenOn(bool on) { }
    public void SetBrightness(double value01) { }
    public void ApplyOrientation(Orientation o) { }
    public double? BatteryLevel
    {
        get
        {
            try { foreach (var d in Directory.GetDirectories("/sys/class/power_supply", "BAT*")) if (double.TryParse(File.ReadAllText(Path.Combine(d, "capacity")).Trim(), out var v)) return v / 100; } catch { }
            return null;
        }
    }
    public bool? IsCharging
    {
        get
        {
            try { foreach (var d in Directory.GetDirectories("/sys/class/power_supply", "BAT*")) return File.ReadAllText(Path.Combine(d, "status")).Trim() != "Discharging"; } catch { }
            return null;
        }
    }

    public async Task<string?> PickTextFileAsync(string title)
    {
        var files = await _w.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title, AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("JSON") { Patterns = new[] { "*.json" } } },
        });
        if (files.Count == 0) return null;
        await using var s = await files[0].OpenReadAsync();
        using var r = new StreamReader(s);
        return await r.ReadToEndAsync();
    }

    public async Task<bool> SaveTextFileAsync(string suggestedName, string content)
    {
        var f = await _w.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = suggestedName, DefaultExtension = "json",
            FileTypeChoices = new[] { new FilePickerFileType("JSON") { Patterns = new[] { "*.json" } } },
        });
        if (f == null) return false;
        await using var s = await f.OpenWriteAsync();
        s.SetLength(0);
        await using var w = new StreamWriter(s);
        await w.WriteAsync(content);
        return true;
    }

    public double? ProcessCpuPercent
    {
        get
        {
            var now = DateTime.UtcNow;
            if ((now - _cpuAt).TotalSeconds >= 1)
            {
                var p = Process.GetCurrentProcess();
                var t = p.TotalProcessorTime;
                _cpu = (t - _cpuLast).TotalSeconds / ((now - _cpuAt).TotalSeconds * Environment.ProcessorCount) * 100;
                _cpuLast = t; _cpuAt = now;
            }
            return _cpu;
        }
    }

    public long ProcessMemoryBytes => Process.GetCurrentProcess().WorkingSet64;
    public long TotalMemoryBytes => GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
}
