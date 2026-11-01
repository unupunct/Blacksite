using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Blacksite.Core.Configuration;
using Blacksite.Core.Events;
using Blacksite.Core.Presets;
using Blacksite.Core.Themes;
using Blacksite.UI.Modules;
using Blacksite.UI.Rendering;
using Blacksite.UI.Shell.Overlays;

namespace Blacksite.UI.Shell;

/// <summary>
/// The whole BLACKSITE interface is one custom-drawn control: a render loop, a unified input
/// pipeline (tap / double-tap / long-press / drag / swipe / pinch / wheel / keys) and the shell chrome.
/// </summary>
public sealed partial class BlacksiteView : Control
{
    public BsContext Ctx { get; }
    readonly Gfx _g = new();
    readonly DispatcherTimer _timer;
    readonly Stopwatch _clock = Stopwatch.StartNew();
    double _last, _simAcc, _animTime;
    public ModuleBase Module { get; private set; } = null!;
    public string PresetId { get; private set; } = "space";
    CustomPreset? _custom;
    Palette _pal = PresetCatalog.All[0].Palette;
    public Overlay? Active { get; private set; }

    // diagnostics
    public double Fps { get; private set; }
    public double FrameMs { get; private set; }
    double _fpsAcc; int _fpsFrames;

    // activity / ambient
    double _idle;
    double _rotateTimer;
    double _bootT = 0;
    bool _booting = true;
    bool _paused;
    public bool Paused
    {
        get => _paused;
        set { _paused = value; Ctx.Audio.Suspended = value && !Ctx.Settings.ContinuousBackgroundAudio; }
    }
    bool _lastTouch;
    double _glitch;
    readonly Random _rng = new();

    public BlacksiteView(BsContext ctx)
    {
        Ctx = ctx;
        Focusable = true;
        ClipToBounds = true;
        ctx.ToastHandler = (m, w) => AddToast(m, w);
        ctx.SwitchPreset = id => SwitchTo(id, true);
        ctx.OpenOverlay = OpenOverlay;
        ctx.Events.Started += OnEventStarted;
        GestureRecognizers.Add(new PinchGestureRecognizer());
        AddHandler(PinchEvent, OnPinch);
        AddHandler(PinchEndedEvent, (_, _) => _lastPinch = 1);

        var s = ctx.Settings;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        if (!s.GraphicsChosenByUser) s.Graphics = HardwareProbe.Recommend(ctx.Platform);
        string start = ctx.StartPresetOverride ?? (s.RestoreLastPreset ? s.LastPresetId : "space");
        SwitchTo(start, false);
        if (s.StartInAmbient) s.AmbientMode = true;
        if (!ctx.Platform.IsDesktop && !s.HeatWarningAccepted) OpenOverlay("heat");

        _timer.Tick += (_, _) => Tick();
        _timer.Start();
        ApplySettings();
        ctx.Audio.Play("boot");
    }

    // ----------------------------------------------------------------- settings application
    public void ApplySettings()
    {
        var s = Ctx.Settings;
        Ctx.Events.Auto = s.AutoEvents;
        Ctx.Events.Frequency = s.EventFrequency;
        Ctx.Audio.SyncAmbient();
        int fps = (_batterySaver ? GraphicsProfile.Low : s.Graphics) switch { GraphicsProfile.Low => 30, GraphicsProfile.Balanced => 60, _ => 0 };   // 0 = every display refresh
        if (s.AmbientMode) fps = fps == 0 ? 30 : Math.Min(fps, 30);
        if (s.ReducedMotion) fps = fps == 0 ? 30 : Math.Min(fps, 30);
        _minFrame = fps == 0 ? 0 : 1.0 / fps;
        _timer.Interval = TimeSpan.FromMilliseconds(fps == 0 ? 8 : 1000.0 / fps);
        Ctx.Platform.SetKeepScreenOn(s.KeepScreenOn || s.AmbientMode);
        Ctx.Platform.SetBrightness(s.ScreenBrightness);
        Ctx.Platform.ApplyOrientation(s.Orientation);
        Ctx.Platform.SetAlwaysOnTop(s.AlwaysOnTop);
        Ctx.Platform.SetTray(s.TrayIcon);
    }

    // ----------------------------------------------------------------- presets
    public void SwitchTo(string id, bool announce)
    {
        var list = Ctx.AllPresets();
        var entry = list.FirstOrDefault(x => x.id == id);
        if (entry.id == null) entry = list[0];
        PresetId = entry.id; _custom = entry.custom;
        var info = entry.info; _info = info;
        _pal = Ctx.PaletteFor(info, entry.custom);
        Module = ModuleFactory.Create(info.Id);
        Module.Init(Ctx, info);
        Module.Enter();
        var sp = info.Sound;
        if (entry.custom?.SoundProfile != null && Enum.TryParse<SoundProfile>(entry.custom.SoundProfile, out var cs)) sp = cs;
        Ctx.Audio.SetProfile(sp);
        Ctx.Settings.LastPresetId = PresetId;
        Ctx.Events.CurrentPreset = info.Id;
        _glitch = 1;
        if (announce)
        {
            Ctx.Audio.Play("whoosh", Core.Audio.SoundCategory.Ui, 0.5f);
            AddToast("PRESET // " + entry.name, false);
            Ctx.Save();
        }
        _rotateTimer = 0;
    }

    public void SwitchByOffset(int d)
    {
        var l = Ctx.AllPresets(); int i = Math.Max(0, l.ToList().FindIndex(x => x.id == PresetId));
        SwitchTo(l[(i + d + l.Count) % l.Count].id, true);
    }

    public void OpenOverlay(string name)
    {
        Active = name switch
        {
            "presets" => new PresetManagerOverlay(this),
            "settings" => new SettingsOverlay(this),
            "events" => new EventsOverlay(this),
            "heat" => new HeatWarningOverlay(this),
            "menu" => new MenuOverlay(this, _menuPoint, _menuRadial),
            _ => null,
        };
        if (Active != null) Ctx.Audio.Play("beep");
    }

    public void CloseOverlay() { Active = null; Ctx.Save(); }

    public void Invoke(string id) => Activate(id);

    PresetInfo _info = PresetCatalog.All[0];
    public void RefreshPalette() => _pal = Ctx.PaletteFor(_info, _custom);

    public void ToggleAmbient()
    {
        var s = Ctx.Settings; s.AmbientMode = !s.AmbientMode;
        _idle = 0; Active = null;
        ApplySettings(); Ctx.Save();
        AddToast(s.AmbientMode ? "AMBIENT MODE ACTIVE — TOUCH TO EXIT" : "INTERACTIVE MODE", false);
        Ctx.Audio.Play("confirm");
    }

    public void ToggleMute() { var s = Ctx.Settings; s.Muted = !s.Muted; Ctx.Audio.SyncAmbient(); AddToast(s.Muted ? "AUDIO MUTED" : "AUDIO ON", false); Ctx.Save(); if (!s.Muted) Ctx.Audio.Play("confirm"); }

    public void ToggleFullscreen()
    {
        var s = Ctx.Settings;
        s.WindowMode = Ctx.Platform.IsFullscreen ? WindowMode.Windowed : WindowMode.BorderlessFullscreen;
        Ctx.Platform.ApplyWindowMode(s.WindowMode, s.MonitorIndex);
        AddToast(Ctx.Platform.IsFullscreen ? "FULLSCREEN" : "WINDOWED", false); Ctx.Save();
    }

    // ----------------------------------------------------------------- tick
    // ---- display-synchronised frame loop (falls back to the timer when not attached, e.g. headless tests)
    bool _attached; double _minFrame, _lastFrame;
    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e) { base.OnAttachedToVisualTree(e); _attached = true; _timer.Stop(); RequestFrame(); }
    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e) { _attached = false; _timer.Start(); base.OnDetachedFromVisualTree(e); }
    void RequestFrame() { if (!_attached) return; var tl = TopLevel.GetTopLevel(this); if (tl == null) { _timer.Start(); return; } tl.RequestAnimationFrame(OnFrame); }
    void OnFrame(TimeSpan _)
    {
        if (!_attached) return;
        double now = _clock.Elapsed.TotalSeconds;
        if (now - _lastFrame >= _minFrame - 0.003) { _lastFrame = now; Tick(); }
        RequestFrame();
    }

    public void SkipBoot() => _booting = false;

    /// <summary>Advances the view by a fixed step without the wall clock (tests and headless snapshots).</summary>
    public void Step(double dt) { _manualDt = dt; Tick(); _manualDt = 0; }
    double _manualDt, _batteryAt;
    bool _batterySaver;

    void Tick()
    {
        double now = _clock.Elapsed.TotalSeconds;
        double dt = _manualDt > 0 ? _manualDt : Math.Min(0.25, now - _last); _last = now;
        if (Paused) return;
        var s = Ctx.Settings;
        CheckLongPress();
        _idle += dt;
        double baseDt = dt * s.AnimationSpeed * (s.ReducedMotion ? 0.35 : 1);
        double sdt = Ctx.AnimFrozen ? 0 : baseDt;               // animation clock (VIS dock: ANIM freeze)
        Ctx.SimDt = Ctx.DataPaused ? 0 : baseDt;                // data clock (VIS dock: DATA pause)
        _animTime += sdt;

        // simulation at configurable interval (slower in ambient/low)
        var effQ = _batterySaver ? GraphicsProfile.Low : s.Graphics;
        double interval = s.UpdateIntervalMs / 1000.0 * (effQ == GraphicsProfile.High ? 0.33 : effQ == GraphicsProfile.Balanced ? 0.5 : 1.0);   // finer data steps = smoother gauges and graphs
        interval = Math.Max(interval, effQ == GraphicsProfile.Low ? 0.1 : 0.016);
        if (s.AmbientMode) interval *= 1.5;
        _simAcc += Ctx.SimDt;
        if (_simAcc >= interval)
        {
            Ctx.Sim.Step(_simAcc, true, s.EventIntensity);
            _simAcc = 0;
        }
        Ctx.Events.Auto = s.AutoEvents && (s.AmbientMode || s.AutoEvents);
        Ctx.Events.Update(dt);
        Ctx.Audio.Update(dt);
        UpdateCrisis(dt);
        if (!_booting) Module.Update(sdt);

        if (_booting) { _bootT += dt; if (_bootT > 4.2) _booting = false; }
        if (s.BatteryAware && !Ctx.Platform.IsDesktop && now - _batteryAt > 30)
        {
            _batteryAt = now;
            bool low = Ctx.Platform.BatteryLevel is < 0.2 && Ctx.Platform.IsCharging != true;
            if (low != _batterySaver) { _batterySaver = low; ApplySettings(); AddToast(low ? "BATTERY LOW — REDUCED GRAPHICS" : "BATTERY OK — GRAPHICS RESTORED", low); }
        }
        _glitch = Math.Max(0, _glitch - dt * 1.4);

        if (s.AutoRotate)
        {
            _rotateTimer += dt;
            if (_rotateTimer > s.AutoRotateSeconds) SwitchByOffset(1);
        }

        // fps
        _fpsAcc += dt; _fpsFrames++;
        if (_fpsAcc >= 0.5) { Fps = _fpsFrames / _fpsAcc; FrameMs = 1000 * _fpsAcc / _fpsFrames; _fpsAcc = 0; _fpsFrames = 0; }

        for (int i = _toasts.Count - 1; i >= 0; i--) if (now - _toasts[i].t > 3.2) _toasts.RemoveAt(i);
        InvalidateVisual();
    }

    // ----------------------------------------------------------------- render
    public override void Render(DrawingContext dc)
    {
        var b = new Rect(Bounds.Size);
        if (b.Width < 10 || b.Height < 10) return;
        var s = Ctx.Settings;
        var g = _g;
        g.Begin(dc, b);
        using var shake = CrisisActive ? dc.PushTransform(CrisisShake()) : default;
        g.S = s; g.P = _pal;
        g.Time = _animTime; g.Dt = 0.016;
        g.Quality = _batterySaver ? GraphicsProfile.Low : s.Graphics; g.Reduced = s.ReducedMotion;
        g.Touch = _lastTouch || Ctx.Platform.IsTouchFirst;
        g.PixelScale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        g.Brightness = s.InterfaceBrightness * (_saver ? 0.35 : 1);
        g.HudOpacity = s.HudOpacity;
        g.Pointer = _pointer; g.PointerIsDown = _down; g.PressedId = _pressedId; g.HoverId = _hoverId;
        double sc = Math.Clamp(Math.Sqrt(b.Width * b.Height) / 1100.0, 0.75, 2.6) * s.HudScale;
        if (g.Touch) sc = Math.Max(sc, 0.95);
        g.Scale = sc;

        dc.DrawRectangle(g.Br(_pal.Background), null, b);

        if (_booting) { DrawBoot(g, b); g.AddHit("boot:skip", b); return; }

        bool ambient = s.AmbientMode;
        double topH = s.ShowHud && !ambient ? g.U(g.Touch ? 48 : 40) : 0;
        double botH = s.ShowHud && !ambient ? g.U(g.Touch ? 52 : 44) : 0;
        double tickH = topH > 0 && b.Width > g.U(700) && !Module.NoTicker ? g.U(15) : 0;
        var content = new Rect(0, topH + tickH, b.Width, Math.Max(10, b.Height - topH - tickH - botH));

        Module.Draw(g, content);
        DrawAtmosphere(g, content);
        if (s.ShowHud && !ambient) { DrawTopBar(g, new Rect(0, 0, b.Width, topH)); if (tickH > 0) DrawTicker(g, new Rect(0, topH, b.Width, tickH)); if (botH > 0) DrawActionBar(g, new Rect(0, b.Height - botH, b.Width, botH)); }
        if (ambient) DrawAmbientHud(g, b);
        DrawEventFx(g, b);
        if (s.ShowDiagnostics && s.ShowHud) DrawDiagnostics(g, b, topH);
        DrawToasts(g, b, topH);
        if (_dock && s.ShowHud && !ambient) DrawDock(g, b, topH + tickH);
        DrawCrisis(g, b);
        g.PostEffects(b, Math.Max(_glitch, EventGlitch()));
        Active?.Draw(g, b);
        if (_saver) DrawSaver(g, b);
        if (_radialVisible) { /* drawn by MenuOverlay */ }
    }

    public Gfx GfxForOverlays => _g;

    double EventGlitch()
    {
        var ev = Ctx.Events.Primary;
        if (ev == null) return 0;
        return ev.Def.Effect == EventEffect.Glitch ? 0.6 * ev.Fade * Ctx.Settings.AnimationIntensity : 0;
    }

}

public static class HardwareProbe
{
    public static GraphicsProfile Recommend(Blacksite.Core.Services.IPlatformServices p)
    {
        double gb = p.TotalMemoryBytes / 1073741824.0;
        int cores = Environment.ProcessorCount;
        if (!p.IsDesktop) return gb < 3.5 || cores <= 4 ? GraphicsProfile.Low : gb >= 7 && cores >= 8 ? GraphicsProfile.High : GraphicsProfile.Balanced;
        if (gb < 6 || cores <= 2) return GraphicsProfile.Low;
        return gb >= 12 && cores >= 8 ? GraphicsProfile.High : GraphicsProfile.Balanced;
    }
}
