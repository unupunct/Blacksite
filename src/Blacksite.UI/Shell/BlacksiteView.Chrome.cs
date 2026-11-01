using Avalonia;
using Blacksite.Core.Audio;
using Blacksite.Core.Configuration;
using Blacksite.Core.Events;
using Blacksite.Core.Themes;
using Blacksite.UI.Rendering;

namespace Blacksite.UI.Shell;

public sealed partial class BlacksiteView
{
    readonly List<(string text, bool warn, double t)> _toasts = new();
    readonly List<string> _notifLog = new();
    public IReadOnlyList<string> NotificationLog => _notifLog;
    bool _saver;

    public void AddToast(string msg, bool warn)
    {
        _toasts.Add((msg, warn, _clock.Elapsed.TotalSeconds));
        if (_toasts.Count > 4) _toasts.RemoveAt(0);
        _notifLog.Add($"{DateTime.Now:HH:mm:ss}  {msg}");
        if (_notifLog.Count > 40) _notifLog.RemoveAt(0);
    }

    void OnEventStarted(ActiveEvent ev)
    {
        var d = ev.Def;
        _glitch = 0.7;
        Ctx.Audio.Play(d.Sound, d.Sound == "alarm" ? SoundCategory.Alarm : SoundCategory.Event);
        if (d.Voice != null) Ctx.Audio.Say(d.Voice);
        _notifLog.Add($"{DateTime.Now:HH:mm:ss}  EVENT: {d.Title}");
        if (_notifLog.Count > 40) _notifLog.RemoveAt(0);
        // events change simulation state too
        var sim = Ctx.Sim;
        switch (d.Id)
        {
            case "breach": case "unauth_access": sim.SpawnThreat(); sim.SpawnThreat(); sim.ThreatIndex = Math.Min(1, sim.ThreatIndex + 0.35); break;
            case "reactor_unstable": sim.Reactor.Stability = Math.Min(sim.Reactor.Stability, 0.4); sim.Reactor.Target = 95; break;
            case "power_fluct": sim.Reactor.Target = 40 + _rng.Next(50); break;
            case "nav_recalc": sim.Ship.Speed *= 0.6; break;
            case "motion": break;
        }
        if (d.Id == "unknown_obj" || d.Id == "breach") sim.TriggerMotion(_rng.Next(9));
    }

    // ----------------------------------------------------------------- chrome
    static string Clock() => DateTime.Now.ToString("HH:mm:ss");
    bool _dock;

    void DrawTicker(Gfx g, Rect r)
    {
        g.Fill(r, g.P.Primary, 0.05); g.Line(r.X, r.Bottom, r.Right, r.Bottom, g.P.Primary, 1, 0.25);
        var s = Ctx.Settings;
        string txt = $"{PresetNameForBar()}   //   SIM T+{Ctx.Sim.Time:0}S   //   {Fps:0} FPS   //   GFX {s.Graphics.ToString().ToUpper()}   //   HOLO {(s.Hologram ? "ON" : "OFF")}   //   ANIM {(Ctx.AnimFrozen ? "FROZEN" : s.AnimationSpeed.ToString("0.00") + "X")}   //   DATA {(Ctx.DataPaused ? "PAUSED" : "LIVE")}   //   EVENTS {Ctx.Events.Active.Count}   //   {DateTime.UtcNow:HH:mm:ss}Z   //   ALL DATA FICTIONAL   //   ";
        double w = g.TextWidth(txt, 8 * g.S.FontScale * g.Scale);
        double off = Ctx.AnimFrozen ? 0 : -(g.Time * 38) % Math.Max(1, w);
        using (g.Clip(r)) { for (double x = off; x < r.Width; x += w) g.Text(txt, x + g.U(6), r.Y + g.U(2.5), 8, g.P.Text, Align.Left, false, 0.7); }
    }

    void DrawDock(Gfx g, Rect b, double top)
    {
        var s = Ctx.Settings;
        double w = Math.Min(b.Width - g.U(12), g.U(250)), h = g.U(g.Touch ? 300 : 268);
        var r = new Rect(b.Right - w - g.U(8), top + g.U(6), w, h);
        g.Fill(r, g.P.Background, 0.92);
        var inner = g.Panel(r, "VISUAL CONTROL");
        g.AddHit("vd:frame", r);
        g.Button("vd:close", new Rect(r.Right - g.U(30), r.Y + 1, g.U(28), g.U(16)), "X", false, true, null, 8);
        double bh = g.U(g.Touch ? 32 : 26), gap = g.U(4), bw = (inner.Width - gap) / 2, y = inner.Y;
        g.Button("vd:anim", new Rect(inner.X, y, bw, bh), Ctx.AnimFrozen ? "ANIM: FROZEN" : "ANIM: RUN", Ctx.AnimFrozen, true, null, 8);
        g.Button("vd:data", new Rect(inner.X + bw + gap, y, bw, bh), Ctx.DataPaused ? "DATA: PAUSED" : "DATA: LIVE", Ctx.DataPaused, true, null, 8);
        y += bh + gap + g.U(2);
        g.Slider("vd:speed", new Rect(inner.X, y, inner.Width, g.U(36)), (s.AnimationSpeed - 0.25) / 1.75, "ANIMATION SPEED", $"{s.AnimationSpeed:0.00}X");
        y += g.U(40);
        g.Button("vd:holo", new Rect(inner.X, y, bw, bh), "HOLOGRAM", s.Hologram, true, null, 8);
        g.Button("vd:glow", new Rect(inner.X + bw + gap, y, bw, bh), "GLOW", s.Glow, true, null, 8);
        y += bh + gap;
        g.Button("vd:scan", new Rect(inner.X, y, bw, bh), "SCANLINES", s.Scanlines, true, null, 8);
        g.Button("vd:glitch", new Rect(inner.X + bw + gap, y, bw, bh), "GLITCH", s.Glitch, true, null, 8);
        y += bh + gap + g.U(2);
        g.Text("GRAPHICS QUALITY", inner.X, y, 7.5, g.P.Text, Align.Left, true, 0.7); y += g.U(11);
        double qw = (inner.Width - gap * 2) / 3; string[] qn = { "LOW", "BALANCED", "HIGH" };
        for (int i = 0; i < 3; i++) g.Button("vd:q:" + i, new Rect(inner.X + i * (qw + gap), y, qw, bh), qn[i], (int)s.Graphics == i, true, null, 7.5);
        y += bh + gap + g.U(2);
        g.Button("vd:event", new Rect(inner.X, y, bw, bh), "RANDOM EVENT", false, true, g.P.Warning, 8);
        g.Button("vd:rot", new Rect(inner.X + bw + gap, y, bw, bh), s.AutoRotate ? "ROTATE: ON" : "ROTATE: OFF", s.AutoRotate, true, null, 8);
    }

    void HandleDock(string id)
    {
        var s = Ctx.Settings;
        switch (id)
        {
            case "vd:close": _dock = false; break;
            case "vd:anim": Ctx.AnimFrozen = !Ctx.AnimFrozen; AddToast(Ctx.AnimFrozen ? "ANIMATIONS FROZEN" : "ANIMATIONS RUNNING", false); break;
            case "vd:data": Ctx.DataPaused = !Ctx.DataPaused; AddToast(Ctx.DataPaused ? "DATA STREAM PAUSED" : "DATA STREAM LIVE", false); break;
            case "vd:holo": s.Hologram = !s.Hologram; break;
            case "vd:glow": s.Glow = !s.Glow; break;
            case "vd:scan": s.Scanlines = !s.Scanlines; break;
            case "vd:glitch": s.Glitch = !s.Glitch; break;
            case "vd:rot": s.AutoRotate = !s.AutoRotate; _rotateTimer = 0; AddToast(s.AutoRotate ? $"AUTO ROTATE EVERY {s.AutoRotateSeconds}S" : "AUTO ROTATE OFF", false); break;
            case "vd:event":
                {
                    var pool = EventCatalog.All.Where(e => e.Only == null || e.Only == PresetId).ToList();
                    Ctx.Events.Trigger(pool[_rng.Next(pool.Count)].Id); break;
                }
            default:
                if (id.StartsWith("vd:q:")) { s.Graphics = (GraphicsProfile)int.Parse(id[5..]); s.GraphicsChosenByUser = true; ApplySettings(); AddToast("GRAPHICS: " + s.Graphics.ToString().ToUpper(), false); }
                break;
        }
        Ctx.Save();
    }

    void DrawTopBar(Gfx g, Rect r)
    {
        g.Fill(r, g.P.Background, 0.85);
        g.Line(r.X, r.Bottom, r.Right, r.Bottom, g.P.Primary, 1, 0.55);
        double h = r.Height, bh = h - g.U(10), x = r.X + g.U(10);
        // logo
        double lr = h * 0.32;
        g.GlowCircle(x + lr, r.Center.Y, lr, g.P.Primary, 1.4);
        g.Arc(x + lr, r.Center.Y, lr * 0.6, g.Time * 120, 240, g.P.Primary, 1.6);
        g.Disc(x + lr, r.Center.Y, lr * 0.2, g.P.Primary);
        x += lr * 2 + g.U(8);
        bool tiny = r.Width < g.U(520);
        if (!tiny)
        {
            g.Text("BLACKSITE", x, r.Y + g.U(5), 13, g.P.Primary, Align.Left, true);
            g.Text(PresetNameForBar(), x, r.Y + g.U(21), 8, g.P.Text, Align.Left, false, 0.7);
        }
        double lx = tiny ? x : x + g.U(105);
        // right-aligned buttons
        bool wide = r.Width > g.U(980);
        var items = new List<(string id, string label, bool active)>
        {
            ("top:presets", wide ? "PRESETS" : "PRE", Active is Overlays.PresetManagerOverlay),
            ("top:events", wide ? "EVENTS" : "EVT", Active is Overlays.EventsOverlay),
            ("top:settings", wide ? "SETTINGS" : "CFG", Active is Overlays.SettingsOverlay),
            ("top:ambient", wide ? "AMBIENT" : "AMB", Ctx.Settings.AmbientMode),
            ("top:vis", "VIS", _dock),
            ("top:mute", Ctx.Settings.Muted ? "MUTED" : "SND", Ctx.Settings.Muted),
        };
        if (Ctx.Platform.IsDesktop) items.Add(("top:fs", "FS", Ctx.Platform.IsFullscreen));
        double bw = wide ? g.U(84) : g.U(g.Touch ? 54 : 46), gap = g.U(5);
        double availW = r.Right - g.U(8) - lx;
        bw = Math.Min(bw, (availW - gap * (items.Count - 1)) / items.Count);
        double bfs = bw < g.U(44) ? 8 : 11;
        double rx = r.Right - g.U(8);
        for (int i = items.Count - 1; i >= 0; i--)
        {
            rx -= bw;
            g.Button(items[i].id, new Rect(rx, r.Y + g.U(5), bw, bh), items[i].label, items[i].active, true, null, bfs);
            rx -= gap;
        }
        if (rx - lx > g.U(70)) g.Text(Clock(), rx, r.Y + h / 2 - g.U(7), 12, g.P.Text, Align.Right, false, 0.8);
    }

    string PresetNameForBar() => Ctx.AllPresets().FirstOrDefault(x => x.id == PresetId).name ?? Module.Info.Name;

    void DrawActionBar(Gfx g, Rect r)
    {
        g.Fill(r, g.P.Background, 0.85);
        g.Line(r.X, r.Y, r.Right, r.Y, g.P.Primary, 1, 0.55);
        var acts = Module.Actions;
        double gap = g.U(6), pad = g.U(8), reserve = CrisisReserve(g, r);
        DrawCrisisButtons(g, r);
        if (acts.Length == 0) return;
        double avail = r.Width - reserve - pad;
        double bw = Math.Min(g.U(150), (avail - pad * 2 - gap * (acts.Length - 1)) / acts.Length);
        double total = bw * acts.Length + gap * (acts.Length - 1);
        double x = avail / 2 - total / 2 + pad / 2;
        foreach (var a in acts)
        {
            g.Button("act:" + a, new Rect(x, r.Y + g.U(6), bw, r.Height - g.U(12)), a, Module.IsActive(a), true, null, bw < g.U(70) ? 9 : 11);
            x += bw + gap;
        }
    }

    void DrawAmbientHud(Gfx g, Rect b)
    {
        g.Text(PresetNameForBar(), b.X + g.U(14), b.Bottom - g.U(24), 10, g.P.Primary, Align.Left, true, 0.7);
        g.Text(Clock(), b.Right - g.U(14), b.Bottom - g.U(24), 10, g.P.Text, Align.Right, false, 0.6);
        g.Text("AMBIENT  // TOUCH OR PRESS ANY KEY TO EXIT", b.Center.X, b.Bottom - g.U(24), 8, g.P.Text, Align.Center, false, 0.3);
    }

    void DrawEventFx(Gfx g, Rect b)
    {
        var s = Ctx.Settings;
        double inten = s.AnimationIntensity;
        double y = (Ctx.Settings.ShowHud && !s.AmbientMode ? g.U(48) : g.U(10)) + g.U(6);
        foreach (var ev in Ctx.Events.Active)
        {
            var d = ev.Def; double f = ev.Fade;
            uint col = d.Severity switch { Severity.Critical => 0xFFFF3030, Severity.Warning => g.P.Warning, Severity.Notice => g.P.Accent, _ => g.P.Primary };
            switch (d.Effect)
            {
                case EventEffect.Flash:
                    {
                        double p = 0.5 + 0.5 * Math.Sin(ev.Elapsed * 8);
                        if (!g.Reduced) g.Frame(b.Deflate(g.U(3)), col, g.U(5), 0.25 * f * p * inten);
                        g.Fill(b, col, 0.06 * f * p * inten);
                        break;
                    }
                case EventEffect.Pulse:
                    for (int i = 0; i < 3; i++)
                    {
                        double t = (ev.Elapsed * 0.5 + i / 3.0) % 1;
                        g.Circle(b.Center.X, b.Center.Y, Math.Min(b.Width, b.Height) * 0.55 * t, col, 2, 0.5 * (1 - t) * f);
                    }
                    break;
                case EventEffect.Blackout:
                    if (!g.Reduced && (int)(ev.Elapsed * 14) % 3 == 0) g.Fill(b, 0xFF000000, 0.55 * f * inten);
                    break;
            }
            // banner
            double w = Math.Min(b.Width - g.U(20), g.U(520)), h = g.U(54);
            var br = new Rect(b.Center.X - w / 2, y, w, h);
            using (g.Dc.PushOpacity(Math.Clamp(f, 0, 1)))
            {
                g.Fill(br, g.P.Background, 0.88);
                g.Fill(br, col, 0.10);
                g.Frame(br, col, 1.5, 0.95);
                g.Brackets(br, col, 12, 2);
                g.Text(d.Title, br.X + g.U(12), br.Y + g.U(7), 13, col, Align.Left, true);
                g.Text(d.Message, br.X + g.U(12), br.Y + g.U(26), 9, g.P.Text, Align.Left, false, 0.9);
                if (d.Countdown > 0)
                    g.Text($"T-{ev.CountdownLeft:00.0}", br.Right - g.U(12), br.Y + g.U(8), 18, col, Align.Right, true);
                g.Fill(new Rect(br.X, br.Bottom - g.U(3), br.Width * (1 - ev.Progress), g.U(3)), col, 0.9);
            }
            y += h + g.U(6);
        }
    }

    void DrawToasts(Gfx g, Rect b, double topH)
    {
        double now = _clock.Elapsed.TotalSeconds;
        double y = b.Bottom - g.U(Ctx.Settings.AmbientMode ? 50 : 100);
        for (int i = _toasts.Count - 1; i >= 0; i--)
        {
            var (text, warn, t) = _toasts[i];
            double age = now - t, a = Math.Clamp(Math.Min(age / 0.2, (3.2 - age) / 0.5), 0, 1);
            double w = g.TextWidth(text, 11 * g.S.FontScale * g.Scale, true) + g.U(30), h = g.U(24);
            var r = new Rect(b.Center.X - w / 2, y - (1 - a) * g.U(8), w, h);
            uint c = warn ? g.P.Warning : g.P.Primary;
            using (g.Dc.PushOpacity(a))
            {
                g.Fill(r, g.P.Background, 0.9); g.Frame(r, c, 1, 0.9);
                g.Fill(new Rect(r.X, r.Y, g.U(3), r.Height), c);
                g.TextIn(r, text, 11, c, Align.Center, true);
            }
            y -= h + g.U(5);
        }
    }

    void DrawDiagnostics(Gfx g, Rect b, double topH)
    {
        var p = Ctx.Platform;
        double w = g.U(250), x = b.X + g.U(8), y = topH + g.U(8);
        double lh = g.U(13);
        string[] k = { "FPS", "FRAME", "MEMORY", "CPU", "GRAPHICS", "DISPLAY", "DEVICE", "OS", "AUDIO", "SIM TIME", "UPDATE" };
        string cpu = p.ProcessCpuPercent.HasValue ? $"{p.ProcessCpuPercent:0}%" : "N/A";
        string[] v =
        {
            $"{Fps:0}", $"{FrameMs:0.0} MS", $"{p.ProcessMemoryBytes / 1048576.0:0} MB / {p.TotalMemoryBytes / 1073741824.0:0.0} GB", cpu,
            Ctx.Settings.Graphics.ToString().ToUpper(), $"{b.Width:0}x{b.Height:0} @{g.Scale:0.00}",
            Trunc(p.DeviceModel, 22), Trunc(p.OsVersion, 22), Ctx.Audio.Status, $"{Ctx.Sim.Time:0}S", $"{Ctx.Settings.UpdateIntervalMs:0} MS",
        };
        var r = new Rect(x, y, w, lh * k.Length + g.U(30));
        var inner = g.Panel(r, "DIAGNOSTICS");
        for (int i = 0; i < k.Length; i++) g.KeyVal(inner.X, inner.Y + i * lh, inner.Width, k[i], v[i], i == 0 ? (Fps < 25 ? g.P.Warning : g.P.Primary) : null, 9);
    }

    static string Trunc(string s, int n) => s.Length <= n ? s : s[..(n - 1)] + "…";

    void DrawSaver(Gfx g, Rect b)
    {
        g.Fill(b, 0xFF000000, 0.4);
        double t = g.Time * 0.05;
        double x = b.Width * (0.15 + 0.7 * (0.5 + 0.5 * Math.Sin(t * 1.3))), y = b.Height * (0.2 + 0.6 * (0.5 + 0.5 * Math.Sin(t * 0.9 + 1)));
        g.Text(Clock(), x, y, 36, g.P.Primary, Align.Center, true, 0.7);
        g.Text(PresetNameForBar(), x, y + g.U(44), 10, g.P.Text, Align.Center, false, 0.5);
    }

    void DrawBoot(Gfx g, Rect b)
    {
        double t = _bootT, cx = b.Center.X, cy = b.Center.Y - g.U(30);
        double rad = Math.Min(b.Width, b.Height) * 0.2;
        double p = Math.Clamp(t / 3.6, 0, 1);
        g.Rings(cx, cy, rad * (0.4 + 0.6 * Math.Min(1, t)), 5, g.P.Primary, 1.5);
        g.GlowCircle(cx, cy, rad * 0.28, g.P.Primary, 2);
        g.Arc(cx, cy, rad * 0.18, t * 300, 270, g.P.Accent, 2.4);
        g.Disc(cx, cy, g.U(5), g.P.Accent);
        string title = "BLACKSITE";
        int n = (int)Math.Clamp((t - 0.5) * 12, 0, title.Length);
        g.Text(title[..n], cx, cy + rad + g.U(18), 34, g.P.Primary, Align.Center, true);
        if (t > 1.4) g.Text("TACTICAL INTERFACE SIMULATOR", cx, cy + rad + g.U(62), 11, g.P.Text, Align.Center, false, 0.8);
        string[] lines =
        {
            "INITIALISING RENDER CORE ........ OK", "LOADING SIMULATION ENGINE ........ OK", "SYNTHESISING AUDIO BANK .......... OK",
            "MOUNTING PRESET LIBRARY .......... OK", "ENTERTAINMENT USE ONLY — ALL DATA IS FICTIONAL",
        };
        double ly = b.Bottom - g.U(110);
        for (int i = 0; i < lines.Length; i++)
            if (t > 0.8 + i * 0.5) g.Text(lines[i], cx, ly + i * g.U(14), 9, i == lines.Length - 1 ? g.P.Warning : g.P.Primary, Align.Center, false, 0.8);
        double bw = Math.Min(b.Width * 0.5, g.U(360));
        g.Frame(new Rect(cx - bw / 2, b.Bottom - g.U(30), bw, g.U(6)), g.P.Primary, 1, 0.6);
        g.Fill(new Rect(cx - bw / 2 + 1, b.Bottom - g.U(29), (bw - 2) * p, g.U(4)), g.P.Primary, 0.9);
        double fade = Math.Clamp((_bootT - 3.7) / 0.5, 0, 1);
        if (fade > 0) g.Fill(b, g.P.Background, fade);
    }
}
