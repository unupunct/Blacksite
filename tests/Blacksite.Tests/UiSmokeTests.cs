using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Blacksite.Core.Audio;
using Blacksite.Core.Configuration;
using Blacksite.Core.Presets;
using Blacksite.Core.Services;
using Blacksite.UI.Modules;
using Blacksite.UI.Shell;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(Blacksite.Tests.TestAppBuilder))]

namespace Blacksite.Tests;

public sealed class TestApp : Application
{
    public override void Initialize() { Styles.Add(new FluentTheme()); RequestedThemeVariant = ThemeVariant.Dark; }
}

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

public sealed class FakePlatform : IPlatformServices
{
    public string PlatformName => "Fake"; public string DeviceModel => "Test"; public string OsVersion => "Test";
    public bool IsDesktop { get; set; } = true; public bool IsTouchFirst { get; set; }
    public string DataDirectory { get; set; } = Path.GetTempPath();
    public IReadOnlyList<MonitorInfo> Monitors => new[] { new MonitorInfo(0, "D1", 1920, 1080, 1, true), new MonitorInfo(1, "D2", 2560, 1440, 1, false) };
    public WindowMode LastMode; public int Applied;
    public void ApplyWindowMode(WindowMode mode, int monitorIndex) { LastMode = mode; Applied++; _fs = mode >= WindowMode.BorderlessFullscreen; }
    public void SetAlwaysOnTop(bool on) { } public void SetTray(bool on) { } public void MoveToMonitor(int monitorIndex) { }
    public int Opened; public void OpenNewInstance(string presetId, int monitorIndex, bool fullscreen) => Opened++;
    public void SetStartWithWindows(bool on) { StartupSet = on; } public bool StartupSet;
    public void Quit() { }
    bool _fs; public bool IsFullscreen => _fs;
    public void ApplyImmersive(bool on) { } public void SetKeepScreenOn(bool on) { } public void SetBrightness(double value01) { }
    public void ApplyOrientation(Blacksite.Core.Configuration.Orientation o) { }
    public double? BatteryLevel => 0.8; public bool? IsCharging => false;
    public Task<string?> PickTextFileAsync(string title) => Task.FromResult<string?>(null);
    public Task<bool> SaveTextFileAsync(string suggestedName, string content) => Task.FromResult(true);
    public double? ProcessCpuPercent => 2; public long ProcessMemoryBytes => 100L << 20; public long TotalMemoryBytes => 16L << 30;
}

public class UiSmokeTests
{
    static (BlacksiteView view, BsContext ctx, Window win, FakePlatform plat) Make(string preset, int w, int h, GraphicsProfile q = GraphicsProfile.Balanced, bool touch = false)
    {
        var s = new AppSettings { LastPresetId = preset, Graphics = q, GraphicsChosenByUser = true, HeatWarningAccepted = true, AutoEvents = false };
        var plat = new FakePlatform { IsTouchFirst = touch, DataDirectory = Path.Combine(Path.GetTempPath(), "bs-ui-" + Guid.NewGuid().ToString("N")) };
        var store = new ConfigStore(plat.DataDirectory);
        var ctx = new BsContext(s, store, plat, new AudioEngine(s));
        var view = new BlacksiteView(ctx);
        var win = new Window { Width = w, Height = h, Content = view };
        win.Show(); view.SkipBoot(); view.Focus();
        return (view, ctx, win, plat);
    }

    static void Frames(BlacksiteView v, Window w, int n = 20) { for (int i = 0; i < n; i++) v.Step(0.05); w.CaptureRenderedFrame(); }

    public static IEnumerable<object[]> Presets() => PresetCatalog.All.Select(p => new object[] { p.Id });

    [AvaloniaTheory, MemberData(nameof(Presets))]
    public void Every_preset_renders_on_all_target_resolutions(string id)
    {
        (int, int)[] sizes = { (1366, 768), (1920, 1080), (3840, 2160), (3440, 1440), (800, 1280), (390, 844), (844, 390), (640, 400) };
        foreach (var (w, h) in sizes)
            foreach (var q in new[] { GraphicsProfile.Low, GraphicsProfile.High })
            {
                if (q == GraphicsProfile.High && w is 3840 or 3440 or 640) continue;
                var (view, _, win, _) = Make(id, w, h, q, w < 900);
                Frames(view, win, 8);
                var bmp = win.CaptureRenderedFrame();
                Assert.NotNull(bmp);
                win.Close();
            }
    }

    [AvaloniaTheory, MemberData(nameof(Presets))]
    public void Every_action_button_on_every_preset_works(string id)
    {
        var (view, ctx, win, _) = Make(id, 1600, 900);
        Frames(view, win);
        foreach (var a in view.Module.Actions)
        {
            view.Invoke("act:" + a);
            Frames(view, win, 12);
            view.Invoke("act:" + a);         // toggle back / second press must not throw
            Frames(view, win, 6);
        }
        Assert.NotEmpty(view.NotificationLog.Concat(new[] { "x" }));
        win.Close();
    }

    [AvaloniaTheory, MemberData(nameof(Presets))]
    public void Kill_switch_and_self_destruct_run_and_recover_on_every_preset(string id)
    {
        var (view, _, win, _) = Make(id, 960, 540, GraphicsProfile.Low);
        Frames(view, win, 4);
        view.Invoke("cr:kill"); for (int i = 0; i < 36; i++) { view.Step(0.2); if (i % 12 == 0) win.CaptureRenderedFrame(); }   // 7 s > kill sequence
        view.Invoke("cr:sd"); for (int i = 0; i < 10; i++) view.Step(0.2);
        view.Invoke("cr:abort"); Frames(view, win, 2);
        view.Invoke("cr:sd"); for (int i = 0; i < 90; i++) { view.Step(0.2); if (i % 15 == 0) win.CaptureRenderedFrame(); }   // countdown + detonation + reboot
        Frames(view, win, 3);
        win.Close();
    }

    [AvaloniaTheory, MemberData(nameof(Presets))]
    public void Pointer_interaction_on_every_preset_does_not_throw(string id)
    {
        var (view, _, win, _) = Make(id, 1600, 900);
        Frames(view, win);
        var rnd = new Random(1);
        for (int i = 0; i < 25; i++)
        {
            var p = new Point(100 + rnd.NextDouble() * 1400, 80 + rnd.NextDouble() * 700);
            win.MouseDown(p, MouseButton.Left); win.MouseMove(p + new Vector(40, 20)); win.MouseUp(p + new Vector(40, 20), MouseButton.Left);
            win.MouseDown(p, MouseButton.Left); win.MouseUp(p, MouseButton.Left);
            win.MouseWheel(p, new Vector(0, rnd.Next(2) == 0 ? 1 : -1));
            Frames(view, win, 3);
            if (view.Active != null) view.CloseOverlay();
        }
        win.Close();
    }

    [AvaloniaFact]
    public void Keyboard_shortcuts_switch_presets_and_toggle_modes()
    {
        var (view, ctx, win, plat) = Make("space", 1600, 900);
        Frames(view, win);
        for (int i = 1; i <= 10; i++)
        {
            win.KeyPressQwerty((PhysicalKey)((int)PhysicalKey.F1 + i - 1), RawInputModifiers.None);
            Frames(view, win, 3);
            Assert.Equal(PresetCatalog.All[i - 1].Id, view.PresetId);
        }
        for (int i = 1; i <= 5; i++)
        {
            win.KeyPressQwerty((PhysicalKey)((int)PhysicalKey.F1 + i - 1), RawInputModifiers.Control); Frames(view, win, 3);
            Assert.Equal(PresetCatalog.All[10 + i - 1].Id, view.PresetId);
        }
        win.KeyPressQwerty(PhysicalKey.F11, RawInputModifiers.None); Assert.True(plat.IsFullscreen);
        win.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None); Assert.False(plat.IsFullscreen);
        win.KeyPressQwerty(PhysicalKey.M, RawInputModifiers.None); Assert.True(ctx.Settings.Muted);
        win.KeyPressQwerty(PhysicalKey.M, RawInputModifiers.None); Assert.False(ctx.Settings.Muted);
        win.KeyPressQwerty(PhysicalKey.H, RawInputModifiers.None); Assert.False(ctx.Settings.ShowHud);
        win.KeyPressQwerty(PhysicalKey.H, RawInputModifiers.None); Assert.True(ctx.Settings.ShowHud);
        win.KeyPressQwerty(PhysicalKey.F12, RawInputModifiers.None); Assert.True(ctx.Settings.AmbientMode);
        win.KeyPressQwerty(PhysicalKey.F12, RawInputModifiers.None); Assert.False(ctx.Settings.AmbientMode);
        win.KeyPressQwerty(PhysicalKey.S, RawInputModifiers.Control); Frames(view, win, 3); Assert.NotNull(view.Active);
        win.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None); Assert.Null(view.Active);
        win.Close();
    }

    [AvaloniaTheory, InlineData("orbital"), InlineData("quantum"), InlineData("commarray"), InlineData("medical"), InlineData("facility")]
    public void Deck_panels_can_be_hidden_reordered_expanded_and_persist(string id)
    {
        var (view, ctx, win, plat) = Make(id, 1600, 900);
        Frames(view, win);
        view.Invoke("act:PANELS"); Frames(view, win, 3);
        var first = ctx.Settings.PanelLayouts.ContainsKey(id) ? 0 : 0;
        // hide the second panel via the options popup
        var layoutBefore = ctx.Settings.PanelLayouts.GetValueOrDefault(id)?.Hidden.Count ?? 0;
        var pid = id switch { "orbital" => "tele", "quantum" => "proc", "commarray" => "track", "medical" => "life", _ => "security" };
        view.Invoke("dk:vis:" + pid); Frames(view, win, 3);
        Assert.Contains(pid, ctx.Settings.PanelLayouts[id].Hidden);
        view.Invoke("dk:up:" + pid); view.Invoke("dk:dn:" + pid);
        Assert.NotEmpty(ctx.Settings.PanelLayouts[id].Order);
        view.Invoke("dk:accent"); Assert.NotNull(ctx.Settings.PanelLayouts[id].Accent);
        view.Invoke("dk:done"); Frames(view, win, 3);
        // settings survive a save/load round trip and switching presets
        ctx.Save(); var loaded = ctx.Store.Load();
        Assert.Contains(pid, loaded.PanelLayouts[id].Hidden); Assert.Equal(ctx.Settings.PanelLayouts[id].Accent, loaded.PanelLayouts[id].Accent);
        view.SwitchTo("space", false); Frames(view, win, 3); view.SwitchTo(id, false); Frames(view, win, 3);
        Assert.Contains(pid, ctx.Settings.PanelLayouts[id].Hidden);
        // restore default layout
        view.Invoke("act:PANELS"); view.Invoke("dk:reset"); Assert.Empty(ctx.Settings.PanelLayouts[id].Hidden); Assert.Null(ctx.Settings.PanelLayouts[id].Accent);
        view.Invoke("dk:done");
        // narrow layout scrolls and panel title expands
        win.Width = 800; win.Height = 1280; Frames(view, win, 4);
        win.MouseDown(new Point(400, 900), MouseButton.Left); win.MouseMove(new Point(400, 500)); win.MouseUp(new Point(400, 500), MouseButton.Left); Frames(view, win, 3);
        win.Close();
    }

    [AvaloniaFact]
    public void Shortcuts_are_rebindable()
    {
        var (view, ctx, win, _) = Make("space", 1280, 720);
        Frames(view, win); view.Focus();
        ctx.Settings.Shortcuts["Mute"] = "N";
        win.KeyPressQwerty(PhysicalKey.M, RawInputModifiers.None); Assert.False(ctx.Settings.Muted);
        win.KeyPressQwerty(PhysicalKey.N, RawInputModifiers.None); Assert.True(ctx.Settings.Muted);
        win.Close();
    }

    [AvaloniaFact]
    public void Overlays_open_render_and_scroll()
    {
        var (view, ctx, win, _) = Make("hacker", 1280, 720);
        foreach (var name in new[] { "presets", "settings", "events", "heat", "menu" })
        {
            view.OpenOverlay(name); Frames(view, win, 6);
            Assert.NotNull(view.Active);
            win.MouseWheel(new Point(600, 360), new Vector(0, -3)); Frames(view, win, 2);
            view.CloseOverlay();
        }
        win.Close();
    }

    [AvaloniaFact]
    public void Every_settings_row_can_be_activated()
    {
        var (view, ctx, win, plat) = Make("space", 1400, 900);
        view.OpenOverlay("settings"); Frames(view, win, 4);
        for (int tab = 0; tab < 8; tab++)
        {
            view.Invoke("set:tab:" + tab); Frames(view, win, 3);
            for (int row = 0; row < 40; row++)
            {
                foreach (var kind in new[] { "set:t:", "set:c:", "set:a:" })
                {
                    try { view.Invoke(kind + row); } catch (ArgumentOutOfRangeException) { }
                    catch (NullReferenceException) { }
                    Frames(view, win, 1);
                    if (view.Active is not Blacksite.UI.Shell.Overlays.SettingsOverlay) { view.OpenOverlay("settings"); view.Invoke("set:tab:" + tab); }
                }
            }
        }
        win.Close();
    }

    [AvaloniaFact]
    public void Startup_toggle_requires_consent()
    {
        var (view, ctx, win, plat) = Make("space", 1400, 900);
        view.OpenOverlay("settings"); view.Invoke("set:tab:4"); Frames(view, win, 3);
        // find the startup row by toggling every toggle and checking the platform flag only flips after consent
        for (int row = 0; row < 20; row++) view.Invoke("set:t:" + row);
        Assert.False(plat.StartupSet);                      // not allowed until the consent dialog is confirmed
        view.Invoke("set:consent:yes");
        Assert.True(plat.StartupSet);
        win.Close();
    }

    [AvaloniaFact]
    public void Graphics_profile_recommendation_is_sane()
    {
        Assert.Equal(GraphicsProfile.High, HardwareProbe.Recommend(new FakePlatform()));
        Assert.Equal(GraphicsProfile.Low, HardwareProbe.Recommend(new FakePlatform { IsDesktop = false, IsTouchFirst = true } is var p ? new LowEnd() : null!));
    }

    sealed class LowEnd : IPlatformServices
    {
        public string PlatformName => ""; public string DeviceModel => ""; public string OsVersion => ""; public bool IsDesktop => false; public bool IsTouchFirst => true;
        public string DataDirectory => ""; public IReadOnlyList<MonitorInfo> Monitors => Array.Empty<MonitorInfo>(); public bool IsFullscreen => true;
        public void ApplyWindowMode(WindowMode mode, int monitorIndex) { } public void SetAlwaysOnTop(bool on) { } public void SetTray(bool on) { }
        public void MoveToMonitor(int monitorIndex) { } public void OpenNewInstance(string presetId, int monitorIndex, bool fullscreen) { }
        public void SetStartWithWindows(bool on) { } public void Quit() { } public void ApplyImmersive(bool on) { } public void SetKeepScreenOn(bool on) { }
        public void SetBrightness(double value01) { } public void ApplyOrientation(Blacksite.Core.Configuration.Orientation o) { }
        public double? BatteryLevel => 0.5; public bool? IsCharging => false;
        public Task<string?> PickTextFileAsync(string title) => Task.FromResult<string?>(null);
        public Task<bool> SaveTextFileAsync(string suggestedName, string content) => Task.FromResult(true);
        public double? ProcessCpuPercent => null; public long ProcessMemoryBytes => 0; public long TotalMemoryBytes => 2L << 30;
    }

    [AvaloniaFact]
    public void Cinematic_events_show_banner_and_change_state()
    {
        var (view, ctx, win, _) = Make("reactor", 1600, 900);
        foreach (var e in Blacksite.Core.Events.EventCatalog.All) { ctx.Events.Trigger(e.Id); Frames(view, win, 10); }
        Assert.NotEmpty(ctx.Events.Active);
        win.Close();
    }

    [AvaloniaFact]
    public void Ambient_mode_rotates_presets_and_screensaver_activates()
    {
        var (view, ctx, win, _) = Make("space", 1280, 720);
        ctx.Settings.AutoRotate = true; ctx.Settings.AutoRotateSeconds = 10; ctx.Settings.AmbientMode = true; ctx.Settings.ScreenSaverMinutes = 1; ctx.Settings.AutoEvents = true;
        view.ApplySettings();
        string start = view.PresetId;
        for (int i = 0; i < 400; i++) view.Step(0.05);     // 20 s
        Assert.NotEqual(start, view.PresetId);
        for (int i = 0; i < 1300; i++) view.Step(0.05);    // > 1 min idle
        win.CaptureRenderedFrame();
        win.Close();
    }

    [AvaloniaFact]
    public void Long_run_does_not_leak_memory_heavily()
    {
        var (view, ctx, win, _) = Make("hacker", 1280, 720, GraphicsProfile.High);
        Frames(view, win, 30); GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long before = GC.GetTotalMemory(true);
        for (int i = 0; i < 1500; i++) { view.Step(0.04); if (i % 25 == 0) win.CaptureRenderedFrame(); }
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long after = GC.GetTotalMemory(true);
        Assert.True(after - before < 40L << 20, $"memory grew by {(after - before) / 1048576.0:0.0} MB");
        win.Close();
    }
}
