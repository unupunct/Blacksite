using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Blacksite.Core.Audio;
using Blacksite.Core.Configuration;
using Blacksite.Core.Services;
using Blacksite.UI.Shell;

// Headless renderer used for visual verification and automated tests.
// usage: Snap <outDir> <preset|all> [width] [height] [seconds] [overlay] [graphics]
public sealed class SnapApp : Application
{
    public override void Initialize() { Styles.Add(new FluentTheme()); RequestedThemeVariant = ThemeVariant.Dark; }
}

public sealed class HeadlessPlatform : IPlatformServices
{
    public string PlatformName => "Headless";
    public string DeviceModel => "Test";
    public string OsVersion => "Test";
    public bool IsDesktop { get; set; } = true;
    public bool IsTouchFirst { get; set; }
    public string DataDirectory { get; set; } = Path.GetTempPath();
    public IReadOnlyList<MonitorInfo> Monitors => new[] { new MonitorInfo(0, "D1", 1920, 1080, 1, true), new MonitorInfo(1, "D2", 1920, 1080, 1, false) };
    public void ApplyWindowMode(WindowMode mode, int monitorIndex) { }
    public void SetAlwaysOnTop(bool on) { }
    public void SetTray(bool on) { }
    public void MoveToMonitor(int monitorIndex) { }
    public void OpenNewInstance(string presetId, int monitorIndex, bool fullscreen) { }
    public void SetStartWithWindows(bool on) { }
    public void Quit() { }
    public bool IsFullscreen => false;
    public void ApplyImmersive(bool on) { }
    public void SetKeepScreenOn(bool on) { }
    public void SetBrightness(double value01) { }
    public void ApplyOrientation(Orientation o) { }
    public double? BatteryLevel => 0.8;
    public bool? IsCharging => false;
    public Task<string?> PickTextFileAsync(string title) => Task.FromResult<string?>(null);
    public Task<bool> SaveTextFileAsync(string suggestedName, string content) => Task.FromResult(true);
    public double? ProcessCpuPercent => 3;
    public long ProcessMemoryBytes => 120L << 20;
    public long TotalMemoryBytes => 16L << 30;
}

public static class Program
{
    public static void Main(string[] args)
    {
        string outDir = args.Length > 0 ? args[0] : "snaps";
        string preset = args.Length > 1 ? args[1] : "space";
        int w = args.Length > 2 ? int.Parse(args[2]) : 1600, h = args.Length > 3 ? int.Parse(args[3]) : 900;
        double secs = args.Length > 4 ? double.Parse(args[4]) : 6;
        string overlay = args.Length > 5 ? args[5] : "";
        string gfx = args.Length > 6 ? args[6] : "high";
        int post = args.Length > 7 ? int.Parse(args[7]) : 20;
        Directory.CreateDirectory(outDir);

        AppBuilder.Configure<SnapApp>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var ids = preset == "all" ? Blacksite.Core.Presets.PresetCatalog.All.Select(p => p.Id).ToArray() : preset.Split(',');
        foreach (var id in ids)
        {
            var s = new AppSettings { LastPresetId = id, Graphics = Enum.Parse<GraphicsProfile>(gfx, true), GraphicsChosenByUser = true, HeatWarningAccepted = true };
            s.AutoEvents = false;
            var plat = new HeadlessPlatform { IsTouchFirst = w < 900 };
            var store = new ConfigStore(Path.Combine(Path.GetTempPath(), "bs-snap"));
            var audio = new AudioEngine(s);
            var ctx = new BsContext(s, store, plat, audio);
            var view = new BlacksiteView(ctx);
            var win = new Window { Width = w, Height = h, Content = view };
            win.Show();
            view.SkipBoot();
            for (double t = 0; t < secs; t += 0.05) { view.Step(0.05); }
            Thread.Sleep(3000); for (int i = 0; i < 10; i++) view.Step(0.05);
            if (overlay.Length > 0) { foreach (var o in overlay.Split(',')) { if (o.StartsWith("ev=")) ctx.Events.Trigger(o[3..]); else if (o.StartsWith("click=")) view.Invoke(o[6..]); else view.OpenOverlay(o); } for (int i = 0; i < post; i++) view.Step(0.05); }
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            var bmp = win.CaptureRenderedFrame();
            string file = Path.Combine(outDir, $"{id}{(overlay.Length > 0 ? "-" + overlay.Replace(',', '_').Replace('=', '-').Replace(':', '-') : "")}-{w}x{h}.png");
            bmp?.Save(file);
            Console.WriteLine(bmp == null ? "capture failed " + id : "wrote " + file);
            win.Close();
        }
    }
}
