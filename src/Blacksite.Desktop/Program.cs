using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Blacksite.Core.Audio;
using Blacksite.Core.Configuration;
using Blacksite.UI.Shell;

namespace Blacksite.Desktop;

public sealed record LaunchOptions(string? Preset, int? Monitor, bool Fullscreen, bool Instance, bool Startup, bool Portable);

public static class Program
{
    public static LaunchOptions Options = new(null, null, false, false, false, false);

    [STAThread]
    public static void Main(string[] args)
    {
        string? preset = null; int? mon = null; bool fs = false, inst = false, startup = false, portable = false;
        for (int i = 0; i < args.Length; i++)
            switch (args[i])
            {
                case "--preset" when i + 1 < args.Length: preset = args[++i]; break;
                case "--monitor" when i + 1 < args.Length && int.TryParse(args[i + 1], out var m): mon = m; i++; break;
                case "--fullscreen": fs = true; break;
                case "--instance": inst = true; break;
                case "--startup": startup = true; break;
                case "--portable": portable = true; break;
            }
        string exeDir = AppContext.BaseDirectory;
        if (File.Exists(Path.Combine(exeDir, "portable.flag"))) portable = true;
        Options = new LaunchOptions(preset, mon, fs, inst, startup, portable);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<DesktopApp>().UsePlatformDetect().LogToTrace();
}

public sealed class DesktopApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = ThemeVariant.Dark;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desk)
            desk.MainWindow = new MainWindow(Program.Options);
        base.OnFrameworkInitializationCompleted();
    }
}

public sealed class MainWindow : Window
{
    readonly BlacksiteView _view;
    readonly AppSettings _settings;
    readonly ConfigStore _store;
    readonly WindowsPlatform _platform;
    readonly LaunchOptions _o;
    readonly WindowsAudioOutput _audioOut = new();

    public MainWindow(LaunchOptions o)
    {
        _o = o;
        Title = "BLACKSITE – Tactical Interface Simulator";
        MinWidth = 640; MinHeight = 400;
        Background = Brushes.Black;
        try { Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Blacksite.UI/Assets/logo.png"))); } catch { }

        string dir = o.Portable ? Path.Combine(AppContext.BaseDirectory, "Data")
                                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BLACKSITE");
        Directory.CreateDirectory(dir);
        var main = new ConfigStore(dir);
        if (o.Instance)
        {   // secondary windows get their own config copy so the main window's settings are never clobbered
            var idir = Path.Combine(dir, "instances", Environment.ProcessId.ToString());
            Directory.CreateDirectory(idir);
            _settings = main.Load();
            _store = new ConfigStore(idir);
            dir = idir;
        }
        else { _store = main; _settings = main.Load(); }

        if (o.Monitor.HasValue) _settings.MonitorIndex = o.Monitor.Value;
        if (o.Fullscreen) _settings.WindowMode = WindowMode.BorderlessFullscreen;
        else if (o.Instance) _settings.WindowMode = WindowMode.Windowed;
        if (o.Startup && _settings.StartFullscreen) _settings.WindowMode = WindowMode.BorderlessFullscreen;

        Width = Math.Max(640, _settings.WindowWidth); Height = Math.Max(400, _settings.WindowHeight);
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        _platform = new WindowsPlatform(this, _settings, dir);
        var audio = new AudioEngine(_settings) { Speech = new WindowsSpeech() };
        var ctx = new BsContext(_settings, _store, _platform, audio) { StartPresetOverride = o.Preset };
        _view = new BlacksiteView(ctx);
        Content = _view;
        audio.Attach(_audioOut);
        Task.Run(audio.Warm);

        Opened += (_, _) =>
        {
            _platform.ApplyWindowMode(_settings.WindowMode, _settings.MonitorIndex);
            _platform.SetAlwaysOnTop(_settings.AlwaysOnTop);
            if (_settings.StartMinimized && o.Startup) WindowState = WindowState.Minimized;
            if (!o.Instance && _settings.RestoreMonitorConfig && _settings.CommandCenter.Count > 1)
                foreach (var a in _settings.CommandCenter.Where(x => x.MonitorIndex != _settings.MonitorIndex))
                    _platform.OpenNewInstance(a.PresetId, a.MonitorIndex, a.Fullscreen);
            _view.Focus();
        };
        PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty) _view.Paused = WindowState == WindowState.Minimized;
        };
        Closing += (_, _) => SaveGeometry();
    }

    void SaveGeometry()
    {
        if (WindowState == WindowState.Normal)
        {
            _settings.WindowWidth = Width; _settings.WindowHeight = Height;
            _settings.WindowX = Position.X; _settings.WindowY = Position.Y;
        }
        _settings.WindowMode = WindowState switch { WindowState.FullScreen => _settings.WindowMode == WindowMode.ExclusiveFullscreen ? WindowMode.ExclusiveFullscreen : WindowMode.BorderlessFullscreen, WindowState.Maximized => WindowMode.Maximized, _ => WindowMode.Windowed };
        _store.Save(_settings);
        _audioOut.Dispose();
    }
}
