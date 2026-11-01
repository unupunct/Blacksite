using Avalonia;
using Blacksite.Core.Audio;
using Blacksite.Core.Configuration;
using Blacksite.Core.Presets;
using Blacksite.Core.Themes;
using Blacksite.UI.Rendering;

namespace Blacksite.UI.Shell.Overlays;

public sealed class SettingsOverlay : Overlay
{
    enum RK { Header, Toggle, Slider, Choice, Action, Info, Text }

    sealed class Row
    {
        public RK K; public string Label = "";
        public Func<bool>? GetB; public Action<bool>? SetB;
        public Func<double>? GetD; public Action<double>? SetD; public double Min, Max; public string Fmt = "0.00";
        public string[]? Opts; public Func<int>? GetI; public Action<int>? SetI;
        public Func<string>? Val; public Action? Run; public string Btn = "";
    }

    static readonly string[] Tabs = { "DISPLAY", "AUDIO", "SIMULATION", "MODES", "SYSTEM", "CONTROLS", "DATA", "ABOUT" };
    static readonly (string name, string? hex)[] Colors =
    {
        ("PRESET DEFAULT", null), ("CYAN", "#00DCFF"), ("ELECTRIC BLUE", "#2878FF"), ("GREEN", "#28FF5A"), ("EMERALD", "#00EB96"),
        ("AMBER", "#FFAA20"), ("ORANGE", "#FF8C14"), ("RED", "#FF3030"), ("MAGENTA", "#FF40D0"), ("VIOLET", "#AA6EFF"), ("WHITE", "#E8F4FF"), ("OLIVE", "#96AF46"),
    };
    static readonly (string name, string? hex)[] Backgrounds =
    {
        ("PRESET DEFAULT", null), ("OBSIDIAN", "#030305"), ("DEEP NAVY", "#02081A"), ("CHARCOAL", "#0C0D0F"), ("MIDNIGHT GREEN", "#02100A"), ("DEEP RED", "#0E0204"), ("VIOLET NIGHT", "#07031A"),
    };

    int _tab, _built = -1;
    bool _dirty;
    string? _confirm;      // pending consent dialog id
    string _status = "";
    readonly List<Row> _rows = new();

    public SettingsOverlay(BlacksiteView v) : base(v) { }

    AppSettings S => C.Settings;

    void Changed() { C.Save(); V.ApplySettings(); V.RefreshPalette(); }
    void Live() { V.RefreshPalette(); }

    // --------------------------------------------------------------------- row factories
    Row Hdr(string t) => new() { K = RK.Header, Label = t };
    Row Tog(string l, Func<bool> g, Action<bool> s) => new() { K = RK.Toggle, Label = l, GetB = g, SetB = v => { s(v); Changed(); } };
    Row Sld(string l, double min, double max, Func<double> g, Action<double> s, string fmt = "0.00") =>
        new() { K = RK.Slider, Label = l, Min = min, Max = max, GetD = g, SetD = v => { s(v); Live(); }, Fmt = fmt };
    Row Cho(string l, string[] opts, Func<int> g, Action<int> s) => new() { K = RK.Choice, Label = l, Opts = opts, GetI = g, SetI = i => { s(i); Changed(); } };
    Row Act(string l, string btn, Action run) => new() { K = RK.Action, Label = l, Btn = btn, Run = run };
    Row Inf(string l, Func<string> v) => new() { K = RK.Info, Label = l, Val = v };
    Row Txt(string t) => new() { K = RK.Text, Label = t };

    static int IndexOf((string name, string? hex)[] list, string? hex)
    {
        for (int i = 0; i < list.Length; i++) if (string.Equals(list[i].hex, hex, StringComparison.OrdinalIgnoreCase)) return i;
        return 0;
    }

    void Build()
    {
        _rows.Clear();
        switch (_tab)
        {
            case 0: BuildDisplay(); break;
            case 1: BuildAudio(); break;
            case 2: BuildSim(); break;
            case 3: BuildModes(); break;
            case 4: BuildSystem(); break;
            case 5: BuildControls(); break;
            case 6: BuildData(); break;
            default: BuildAbout(); break;
        }
    }

    void BuildDisplay()
    {
        var rec = HardwareProbe.Recommend(C.Platform);
        _rows.Add(Hdr("GRAPHICS"));
        _rows.Add(Cho("Graphics profile", new[] { "LOW", "BALANCED", "HIGH" }, () => (int)S.Graphics, i => { S.Graphics = (GraphicsProfile)i; S.GraphicsChosenByUser = true; }));
        _rows.Add(Inf("Recommended for this device", () => rec.ToString().ToUpper()));
        _rows.Add(Hdr("COLOUR"));
        _rows.Add(Cho("Primary accent", Colors.Select(c => c.name).ToArray(), () => IndexOf(Colors, S.PrimaryOverride), i => S.PrimaryOverride = Colors[i].hex));
        _rows.Add(Cho("Secondary accent", Colors.Select(c => c.name).ToArray(), () => IndexOf(Colors, S.SecondaryOverride), i => S.SecondaryOverride = Colors[i].hex));
        _rows.Add(Cho("Background theme", Backgrounds.Select(c => c.name).ToArray(), () => IndexOf(Backgrounds, S.BackgroundOverride), i => S.BackgroundOverride = Backgrounds[i].hex));
        _rows.Add(Sld("Interface brightness", 0.3, 1.3, () => S.InterfaceBrightness, v => S.InterfaceBrightness = v));
        _rows.Add(Sld("HUD opacity", 0.3, 1.0, () => S.HudOpacity, v => S.HudOpacity = v));
        _rows.Add(Hdr("LAYOUT"));
        _rows.Add(Sld("HUD scale", 0.7, 1.6, () => S.HudScale, v => S.HudScale = v));
        _rows.Add(Sld("Font size", 0.8, 1.5, () => S.FontScale, v => S.FontScale = v));
        _rows.Add(Cho("UI density", new[] { "COMPACT", "NORMAL", "RELAXED" }, () => S.UiDensity, i => S.UiDensity = i));
        _rows.Add(Tog("Show HUD", () => S.ShowHud, v => S.ShowHud = v));
        _rows.Add(Tog("Show diagnostics", () => S.ShowDiagnostics, v => S.ShowDiagnostics = v));
        _rows.Add(Hdr("ANIMATION & EFFECTS"));
        _rows.Add(Sld("Animation speed", 0.25, 2.0, () => S.AnimationSpeed, v => S.AnimationSpeed = v));
        _rows.Add(Sld("Animation intensity", 0.0, 2.0, () => S.AnimationIntensity, v => S.AnimationIntensity = v));
        _rows.Add(Tog("Hologram effects (projector, shimmer)", () => S.Hologram, v => S.Hologram = v));
        _rows.Add(Tog("Glow effects", () => S.Glow, v => S.Glow = v));
        _rows.Add(Tog("Scanlines", () => S.Scanlines, v => S.Scanlines = v));
        _rows.Add(Tog("CRT vignette", () => S.Crt, v => S.Crt = v));
        _rows.Add(Tog("Glitch effects", () => S.Glitch, v => S.Glitch = v));
        _rows.Add(Tog("Screen flicker", () => S.Flicker, v => S.Flicker = v));
        _rows.Add(Tog("Reduced motion", () => S.ReducedMotion, v => S.ReducedMotion = v));
    }

    void BuildAudio()
    {
        _rows.Add(Hdr("VOLUME"));
        _rows.Add(Sld("Master volume", 0, 1, () => S.MasterVolume, v => S.MasterVolume = v, "0%"));
        _rows.Add(Sld("Interface sounds", 0, 1, () => S.UiVolume, v => S.UiVolume = v, "0%"));
        _rows.Add(Sld("Ambient volume", 0, 1, () => S.AmbientVolume, v => S.AmbientVolume = v, "0%"));
        _rows.Add(Sld("Alarm volume", 0, 1, () => S.AlarmVolume, v => S.AlarmVolume = v, "0%"));
        _rows.Add(Sld("Voice volume", 0, 1, () => S.VoiceVolume, v => S.VoiceVolume = v, "0%"));
        _rows.Add(Sld("Cinematic event volume", 0, 1, () => S.EventVolume, v => S.EventVolume = v, "0%"));
        _rows.Add(Hdr("MUTE INDIVIDUAL CATEGORIES"));
        _rows.Add(Tog("Mute interface sounds", () => S.MuteUi, v => S.MuteUi = v));
        _rows.Add(Tog("Mute ambient loops", () => S.MuteAmbientCat, v => { S.MuteAmbientCat = v; }));
        _rows.Add(Tog("Mute alarms", () => S.MuteAlarm, v => S.MuteAlarm = v));
        _rows.Add(Tog("Mute cinematic event sounds", () => S.MuteEvent, v => S.MuteEvent = v));
        _rows.Add(Hdr("BEHAVIOUR"));
        _rows.Add(Tog("Mute all", () => S.Muted, v => { S.Muted = v; C.Audio.SyncAmbient(); }));
        _rows.Add(Tog("Ambient background loop", () => S.AmbientAudio, v => S.AmbientAudio = v));
        _rows.Add(Tog("Randomised ambient events", () => S.RandomAmbientEvents, v => S.RandomAmbientEvents = v));
        _rows.Add(Tog("Continuous background audio", () => S.ContinuousBackgroundAudio, v => S.ContinuousBackgroundAudio = v));
        var devs = new List<string> { "SYSTEM DEFAULT" }; devs.AddRange(C.Audio.Output.Devices);
        if (C.Platform.IsDesktop)
            _rows.Add(Cho("Output device", devs.ToArray(), () => Math.Max(0, devs.IndexOf(S.AudioDevice ?? "SYSTEM DEFAULT")),
                i => { S.AudioDevice = i == 0 ? null : devs[i]; C.Audio.Attach(C.Audio.Output); }));
        _rows.Add(Inf("Audio status", () => C.Audio.Status));
        _rows.Add(Hdr("TEST"));
        _rows.Add(Act("Interface sounds", "PLAY", () => { C.Audio.Play("confirm"); C.Audio.Play("ping"); }));
        _rows.Add(Act("Alarm", "PLAY", () => C.Audio.Play("alarm", SoundCategory.Alarm)));
        _rows.Add(Act("Voice announcement", "SPEAK", () => C.Audio.Say("Good evening. All systems operational.")));
    }

    void BuildSim()
    {
        _rows.Add(Hdr("SIMULATION"));
        _rows.Add(Sld("Update interval (ms)", 50, 500, () => S.UpdateIntervalMs, v => S.UpdateIntervalMs = Math.Round(v / 10) * 10, "0"));
        _rows.Add(Hdr("CINEMATIC EVENTS"));
        _rows.Add(Tog("Random events", () => S.AutoEvents, v => S.AutoEvents = v));
        _rows.Add(Sld("Event frequency", 0.1, 3.0, () => S.EventFrequency, v => S.EventFrequency = v));
        _rows.Add(Sld("Random event intensity", 0.0, 2.0, () => S.EventIntensity, v => S.EventIntensity = v));
        _rows.Add(Txt("All telemetry, tracks and events are generated locally and are entirely fictional."));
    }

    void BuildModes()
    {
        _rows.Add(Hdr("OPERATING MODE"));
        _rows.Add(Tog("Ambient (cinematic) mode", () => S.AmbientMode, v => S.AmbientMode = v));
        _rows.Add(Tog("Automatic preset rotation", () => S.AutoRotate, v => S.AutoRotate = v));
        _rows.Add(Sld("Rotation interval (s)", 10, 300, () => S.AutoRotateSeconds, v => S.AutoRotateSeconds = (int)Math.Round(v / 5) * 5, "0"));
        string[] mins = { "OFF", "1 MIN", "2 MIN", "5 MIN", "10 MIN", "30 MIN" }; int[] mv = { 0, 1, 2, 5, 10, 30 };
        _rows.Add(Cho("Screen saver after", mins, () => Math.Max(0, Array.IndexOf(mv, S.ScreenSaverMinutes)), i => S.ScreenSaverMinutes = mv[i]));
        _rows.Add(Tog("Restore last preset on start", () => S.RestoreLastPreset, v => S.RestoreLastPreset = v));
        _rows.Add(Tog("Start in ambient mode", () => S.StartInAmbient, v => S.StartInAmbient = v));
        if (!C.Platform.IsDesktop)
        {
            _rows.Add(Hdr("ANDROID WALL DISPLAY"));
            _rows.Add(Tog("Keep screen on", () => S.KeepScreenOn, v => S.KeepScreenOn = v));
            _rows.Add(Sld("Screen brightness (0 = system)", 0, 1, () => Math.Max(0, S.ScreenBrightness), v => S.ScreenBrightness = v < 0.03 ? -1 : v, "0%"));
            _rows.Add(Cho("Orientation", new[] { "AUTO", "LANDSCAPE", "PORTRAIT" }, () => (int)S.Orientation, i => S.Orientation = (Orientation)i));
            _rows.Add(Tog("Battery-aware (reduce load < 20%)", () => S.BatteryAware, v => S.BatteryAware = v));
            _rows.Add(Inf("Battery", () => C.Platform.BatteryLevel is { } b ? $"{b * 100:0}%{(C.Platform.IsCharging == true ? " CHARGING" : "")}" : "N/A"));
            _rows.Add(Tog("Start on device boot (best effort)", () => S.StartWithWindows, v => { S.StartWithWindows = v; C.Platform.SetStartWithWindows(v); }));
            _rows.Add(Act("Operating notice", "SHOW", () => V.OpenOverlay("heat")));
        }
    }

    void BuildSystem()
    {
        var p = C.Platform;
        if (!p.IsDesktop)
        {
            _rows.Add(Hdr("DEVICE"));
            _rows.Add(Inf("Device", () => p.DeviceModel));
            _rows.Add(Inf("Android", () => p.OsVersion));
            _rows.Add(Txt("Immersive fullscreen is always on. Auto-start after boot depends on manufacturer restrictions and is not guaranteed."));
            return;
        }
        var mons = p.Monitors;
        string[] mn = mons.Select(m => $"{m.Index + 1}: {m.Width}x{m.Height}{(m.Primary ? " (MAIN)" : "")}").ToArray();
        if (mn.Length == 0) mn = new[] { "1: DEFAULT" };
        _rows.Add(Hdr("WINDOW"));
        _rows.Add(Cho("Window mode", new[] { "WINDOWED", "MAXIMIZED", "BORDERLESS FULLSCREEN", "EXCLUSIVE FULLSCREEN" }, () => (int)S.WindowMode,
            i => { S.WindowMode = (WindowMode)i; p.ApplyWindowMode(S.WindowMode, S.MonitorIndex); }));
        _rows.Add(Cho("Display monitor", mn, () => Math.Clamp(S.MonitorIndex, 0, mn.Length - 1), i => { S.MonitorIndex = i; p.MoveToMonitor(i); }));
        string[] sizes = { "1280 x 720", "1366 x 768", "1600 x 900", "1920 x 1080", "2560 x 1440", "3840 x 2160" };
        int[][] dims = { new[] { 1280, 720 }, new[] { 1366, 768 }, new[] { 1600, 900 }, new[] { 1920, 1080 }, new[] { 2560, 1440 }, new[] { 3840, 2160 } };
        _rows.Add(Cho("Windowed size", sizes, () => Math.Max(0, Array.FindIndex(dims, d => d[0] == (int)S.WindowWidth && d[1] == (int)S.WindowHeight)),
            i => { S.WindowWidth = dims[i][0]; S.WindowHeight = dims[i][1]; S.WindowMode = WindowMode.Windowed; p.ApplyWindowMode(WindowMode.Windowed, S.MonitorIndex); }));
        _rows.Add(Tog("Disable window borders (windowed)", () => S.NoBorders, v => { S.NoBorders = v; p.ApplyWindowMode(S.WindowMode, S.MonitorIndex); }));
        _rows.Add(Tog("Always on top", () => S.AlwaysOnTop, v => S.AlwaysOnTop = v));
        _rows.Add(Tog("System tray icon", () => S.TrayIcon, v => S.TrayIcon = v));
        _rows.Add(Hdr("STARTUP (REQUIRES CONSENT)"));
        _rows.Add(Tog("Start BLACKSITE with Windows", () => S.StartWithWindows, v =>
        {
            if (v) { _confirm = "startup"; } else { S.StartWithWindows = false; p.SetStartWithWindows(false); _status = "WINDOWS STARTUP DISABLED"; }
        }));
        _rows.Add(Tog("Start in fullscreen", () => S.StartFullscreen, v => S.StartFullscreen = v));
        _rows.Add(Tog("Start minimized", () => S.StartMinimized, v => S.StartMinimized = v));
        _rows.Add(Tog("Restore monitor configuration", () => S.RestoreMonitorConfig, v => S.RestoreMonitorConfig = v));
        _rows.Add(Hdr("MULTI-MONITOR COMMAND CENTER"));
        var ids = new List<string> { "OFF" }; ids.AddRange(PresetCatalog.All.Select(x => x.Name));
        for (int m = 0; m < mons.Count; m++)
        {
            int mi = m;
            _rows.Add(Cho($"Monitor {m + 1} preset", ids.ToArray(),
                () => { var a = S.CommandCenter.FirstOrDefault(x => x.MonitorIndex == mi); return a == null ? 0 : 1 + PresetCatalog.All.ToList().FindIndex(x => x.Id == a.PresetId); },
                i =>
                {
                    S.CommandCenter.RemoveAll(x => x.MonitorIndex == mi);
                    if (i > 0) S.CommandCenter.Add(new MonitorAssignment { MonitorIndex = mi, PresetId = PresetCatalog.All[i - 1].Id });
                }));
        }
        _rows.Add(Act("Launch configured command center", "LAUNCH", () =>
        {
            int n = 0;
            foreach (var a in S.CommandCenter.OrderBy(x => x.MonitorIndex))
            {
                if (a.MonitorIndex == S.MonitorIndex) { V.SwitchTo(a.PresetId, true); continue; }
                p.OpenNewInstance(a.PresetId, a.MonitorIndex, a.Fullscreen); n++;
            }
            _status = n > 0 ? $"OPENED {n} ADDITIONAL WINDOW(S)" : "NOTHING TO LAUNCH — ASSIGN PRESETS TO MONITORS";
        }));
        _rows.Add(Act("Open this preset on next monitor", "OPEN", () =>
        {
            int next = mons.Count == 0 ? 0 : (S.MonitorIndex + 1) % mons.Count;
            p.OpenNewInstance(V.PresetId, next, true); _status = "NEW WINDOW OPENED";
        }));
    }

    void BuildControls()
    {
        _rows.Add(Txt("Select REBIND, then press the new key (Esc cancels). ESC closes overlays / exits fullscreen."));
        string[] order = { "Fullscreen", "Escape", "Ambient", "Action", "Mute", "Hud", "Settings", "Diagnostics", "Presets", "Events", "Visual",
            "Preset1", "Preset2", "Preset3", "Preset4", "Preset5", "Preset6", "Preset7", "Preset8", "Preset9", "Preset10", "Preset11", "Preset12", "Preset13", "Preset14", "Preset15" };
        foreach (var k in order)
        {
            var key = k;
            string label = key.StartsWith("Preset") && key.Length > 6 ? "Switch to preset " + key[6..] : key == "Action" ? "Trigger selected action" : key switch
            {
                "Fullscreen" => "Toggle fullscreen", "Escape" => "Exit fullscreen / close overlay", "Ambient" => "Toggle ambient mode", "Mute" => "Mute / unmute",
                "Hud" => "Toggle HUD", "Settings" => "Open settings", "Diagnostics" => "Toggle diagnostics", "Presets" => "Preset manager", "Events" => "Event panel", _ => key,
            };
            _rows.Add(Act(label + "  [" + (S.Shortcuts.TryGetValue(key, out var v) ? v : "-") + "]", V.RebindTarget == key ? "PRESS KEY…" : "REBIND", () => V.RebindTarget = key));
        }
        _rows.Add(Act("Restore default shortcuts", "RESET", () => { S.Shortcuts = AppSettings.DefaultShortcuts(); _status = "SHORTCUTS RESET"; }));
    }

    void BuildData()
    {
        _rows.Add(Hdr("CONFIGURATION (BLACKSITE_CONFIG.json)"));
        _rows.Add(Act("Export all settings", "EXPORT", async () => await Do(async () =>
        {
            var ok = await C.Platform.SaveTextFileAsync("BLACKSITE_CONFIG.json", ConfigStore.Serialize(S, C.Platform.PlatformName));
            _status = ok ? "SETTINGS EXPORTED" : "EXPORT CANCELLED";
        })));
        _rows.Add(Act("Import settings", "IMPORT", async () => await Do(async () =>
        {
            var t = await C.Platform.PickTextFileAsync("Import BLACKSITE_CONFIG.json");
            if (t == null) { _status = "IMPORT CANCELLED"; return; }
            Replace(ConfigStore.Parse(t)); _status = "SETTINGS IMPORTED";
        })));
        _rows.Add(Hdr("CUSTOM PRESETS & SEQUENCES"));
        _rows.Add(Inf("Stored", () => $"{S.CustomPresets.Count} PRESETS / {S.Sequences.Count} SEQUENCES"));
        _rows.Add(Act("Export presets & sequences", "EXPORT", async () => await Do(async () =>
        {
            var ok = await C.Platform.SaveTextFileAsync("BLACKSITE_PRESETS.json", ConfigStore.ExportPresets(S));
            _status = ok ? "PRESETS EXPORTED" : "EXPORT CANCELLED";
        })));
        _rows.Add(Act("Import presets & sequences", "IMPORT", async () => await Do(async () =>
        {
            var t = await C.Platform.PickTextFileAsync("Import BLACKSITE_PRESETS.json");
            if (t == null) { _status = "IMPORT CANCELLED"; return; }
            int n = ConfigStore.ImportPresets(S, t); C.Save(); _status = $"IMPORTED {n} ITEM(S)";
        })));
        _rows.Add(Hdr("BACKUP"));
        _rows.Add(Act("Backup configuration now", "BACKUP", () =>
        {
            try
            {
                var f = System.IO.Path.Combine(C.Platform.DataDirectory, $"BLACKSITE_CONFIG.{DateTime.Now:yyyyMMdd-HHmmss}.bak.json");
                System.IO.File.WriteAllText(f, ConfigStore.Serialize(S, C.Platform.PlatformName)); _status = "BACKUP SAVED";
            }
            catch (Exception e) { _status = "BACKUP FAILED: " + e.Message; }
        }));
        _rows.Add(Act("Restore previous configuration", "RESTORE", () =>
        {
            try
            {
                var f = C.Store.BackupPath;
                if (!System.IO.File.Exists(f)) { _status = "NO PREVIOUS CONFIGURATION"; return; }
                Replace(ConfigStore.Parse(System.IO.File.ReadAllText(f))); _status = "PREVIOUS CONFIGURATION RESTORED";
            }
            catch (Exception e) { _status = "RESTORE FAILED: " + e.Message; }
        }));
        _rows.Add(Act("Reset everything to defaults", "RESET", () => { Replace(new AppSettings()); _status = "DEFAULTS RESTORED"; }));
        _rows.Add(Inf("Data folder", () => C.Platform.DataDirectory));
    }

    void BuildAbout()
    {
        _rows.Add(Hdr("BLACKSITE — TACTICAL INTERFACE SIMULATOR"));
        _rows.Add(Inf("Version", () => "1.0.0"));
        _rows.Add(Inf("Platform", () => C.Platform.PlatformName));
        _rows.Add(Inf("Device", () => C.Platform.DeviceModel));
        _rows.Add(Inf("OS", () => C.Platform.OsVersion));
        _rows.Add(Txt("BLACKSITE is an entertainment application. Every reading, track, camera feed, threat, network and mission shown is procedurally generated and fictional. It performs no real scanning, tracking, surveillance, attack or security changes and never accesses cameras or the network."));
        _rows.Add(Txt(HeatWarningOverlay.Text));
        _rows.Add(Txt("Sounds are synthesised on the device; no third-party audio or music is bundled."));
    }

    void Replace(AppSettings n)
    {
        var s = S;
        foreach (var pr in typeof(AppSettings).GetProperties()) if (pr.CanWrite) pr.SetValue(s, pr.GetValue(n));
        s.HeatWarningAccepted = true;
        Changed();
        C.Audio.SyncAmbient();
    }

    async Task Do(Func<Task> a)
    {
        try { await a(); } catch (Exception e) { _status = "ERROR: " + e.Message; }
        C.Save();
    }

    // --------------------------------------------------------------------- draw
    public override void Draw(Gfx g, Rect b)
    {
        var inner = Frame(g, b, "SETTINGS", 860, 640);
        bool narrow = inner.Width < g.U(640);
        double th = g.U(g.Touch ? 34 : 28);
        // tabs (wrap into two rows on narrow screens)
        int perRow = narrow ? 4 : Tabs.Length;
        double tw = (inner.Width - g.U(4) * (perRow - 1)) / perRow;
        int rowsN = (Tabs.Length + perRow - 1) / perRow;
        for (int i = 0; i < Tabs.Length; i++)
            g.Button("set:tab:" + i, new Rect(inner.X + (i % perRow) * (tw + g.U(4)), inner.Y + (i / perRow) * (th + g.U(3)), tw, th), Tabs[i], i == _tab, true, null, narrow ? 8.5 : 10);
        double top = inner.Y + rowsN * (th + g.U(3)) + g.U(6);
        double statusH = g.U(18);
        var list = new Rect(inner.X, top, inner.Width - g.U(8), inner.Bottom - top - statusH);
        if (_built != _tab || _dirty || _tab == 5 || _tab == 1) { Build(); _built = _tab; _dirty = false; }

        double rh = g.U(g.Touch ? 44 : 36);
        double y = 0;
        BeginList(g, list);
        using (g.Clip(list))
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                double h = row.K switch { RK.Header => g.U(30), RK.Slider => rh + g.U(8), RK.Text => TextHeight(g, row.Label, list.Width), _ => rh };
                double ry = list.Y + y - Scroll;
                if (ry + h > list.Y - 4 && ry < list.Bottom + 4) DrawRow(g, row, i, new Rect(list.X, ry, list.Width, h), list);
                y += h;
            }
        }
        ContentHeight = y + g.U(6);
        DrawScrollbar(g);
        if (_status.Length > 0) g.Text(_status, inner.X, inner.Bottom - statusH + g.U(2), 9, g.P.Primary, Align.Left, true);
        if (_confirm != null) DrawConsent(g, b);
    }

    double TextHeight(Gfx g, string t, double w)
    {
        double lh = g.U(15.5), cur = 0; int lines = 1;
        foreach (var word in t.Split(' '))
        {
            double ww = g.TextWidth(word + " ", 10.5 * g.Scale * g.S.FontScale);
            if (cur + ww > w) { lines++; cur = ww; } else cur += ww;
        }
        return lines * lh + g.U(10);
    }

    void DrawRow(Gfx g, Row row, int idx, Rect r, Rect clip)
    {
        double pad = g.U(4);
        switch (row.K)
        {
            case RK.Header:
                g.Text(row.Label, r.X, r.Y + g.U(12), 10, g.P.Primary, Align.Left, true);
                g.Line(r.X, r.Bottom - g.U(3), r.Right, r.Bottom - g.U(3), g.P.Primary, 1, 0.35);
                break;
            case RK.Text:
                HeatWarningOverlay.Wrap(g, row.Label, r.X, r.Y + g.U(2), r.Width, 10.5);
                break;
            case RK.Info:
                g.TextIn(r, row.Label, 11, g.P.Text, Align.Left, false, 0.8);
                g.TextIn(r, row.Val!(), 11, g.P.Primary, Align.Right, true);
                break;
            case RK.Toggle:
                g.Toggle("set:t:" + idx, r.Deflate(new Thickness(pad, 2)), row.Label, row.GetB!());
                break;
            case RK.Slider:
                {
                    double v = row.GetD!();
                    double n = (v - row.Min) / (row.Max - row.Min);
                    string txt = row.Fmt == "0%" ? $"{v * 100:0}%" : v.ToString(row.Fmt);
                    g.Slider("set:s:" + idx, new Rect(r.X + pad, r.Y + g.U(2), r.Width - pad * 2, r.Height - g.U(2)), n, row.Label, txt);
                    break;
                }
            case RK.Choice:
                {
                    g.TextIn(new Rect(r.X, r.Y, r.Width * 0.5, r.Height), row.Label, 11, g.P.Text, Align.Left);
                    var br = new Rect(r.X + r.Width * 0.46, r.Y + g.U(3), r.Width * 0.54, r.Height - g.U(6));
                    int i = Math.Clamp(row.GetI!(), 0, row.Opts!.Length - 1);
                    g.Button("set:c:" + idx, br, "< " + row.Opts[i] + " >", false, true, null, 10);
                    break;
                }
            case RK.Action:
                {
                    g.TextIn(new Rect(r.X, r.Y, r.Width - g.U(130), r.Height), row.Label, 11, g.P.Text, Align.Left);
                    g.Button("set:a:" + idx, new Rect(r.Right - g.U(124), r.Y + g.U(3), g.U(120), r.Height - g.U(6)), row.Btn, false, true, null, 10);
                    break;
                }
        }
    }

    void DrawConsent(Gfx g, Rect b)
    {
        g.Fill(b, 0xFF000000, 0.6); g.AddHit("set:blocker", b);
        double w = Math.Min(b.Width - g.U(20), g.U(460)), h = g.U(190);
        var r = new Rect(b.Center.X - w / 2, b.Center.Y - h / 2, w, h);
        var inner = g.Panel(r, "CONSENT REQUIRED", g.P.Warning);
        HeatWarningOverlay.Wrap(g, "BLACKSITE will register itself to launch automatically when you sign in to Windows (current user only, no administrator rights). You can turn this off here at any time.", inner.X, inner.Y + g.U(4), inner.Width, 11);
        g.Button("set:consent:yes", new Rect(inner.X, inner.Bottom - g.U(40), inner.Width / 2 - g.U(4), g.U(36)), "ALLOW", true, true, g.P.Warning);
        g.Button("set:consent:no", new Rect(inner.Center.X + g.U(4), inner.Bottom - g.U(40), inner.Width / 2 - g.U(4), g.U(36)), "CANCEL");
    }

    // --------------------------------------------------------------------- input
    public override void OnButton(string id)
    {
        base.OnButton(id);
        if (!id.StartsWith("set:")) return;
        _dirty = true;
        var p = id.Split(':');
        switch (p[1])
        {
            case "tab": _tab = int.Parse(p[2]); Scroll = 0; _status = ""; break;
            case "t": { var r = RowAt(p[2]); if (r?.K == RK.Toggle) r.SetB!(!r.GetB!()); break; }
            case "c": { var r = RowAt(p[2]); if (r?.K == RK.Choice) r.SetI!((r.GetI!() + 1) % r.Opts!.Length); break; }
            case "a": { var r = RowAt(p[2]); if (r?.K == RK.Action) r.Run!(); break; }
            case "consent":
                if (p[2] == "yes") { S.StartWithWindows = true; C.Platform.SetStartWithWindows(true); _status = "WINDOWS STARTUP ENABLED"; }
                _confirm = null; C.Save(); break;
        }
    }

    Row? RowAt(string s) => int.TryParse(s, out int i) && i >= 0 && i < _rows.Count ? _rows[i] : null;

    public override void OnSlider(string id, double v)
    {
        var p = id.Split(':');
        if (p.Length > 2 && p[1] == "s" && int.TryParse(p[2], out int i) && i < _rows.Count)
        {
            var r = _rows[i];
            r.SetD!(r.Min + (r.Max - r.Min) * v);
        }
    }

    public override void Tap(Point p, string? regionId) { if (regionId == "ov:blocker") V.CloseOverlay(); }
}
