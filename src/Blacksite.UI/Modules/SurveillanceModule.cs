using Avalonia;
using Blacksite.Core.Simulation;
using Blacksite.Core.Themes;
using Blacksite.UI.Rendering;

namespace Blacksite.UI.Modules;

/// <summary>PRESET 03 – intelligence monitoring room. Feeds are procedurally generated scenes; no real camera is ever accessed.</summary>
public sealed class SurveillanceModule : ModuleBase
{
    static readonly int[] Layouts = { 4, 6, 9 };
    int _layout = 2, _page, _sel;
    bool _expanded;
    readonly int[] _mode = new int[9];       // 0 normal 1 night 2 thermal
    readonly List<string> _log = new();

    public override string[] Actions => new[] { "LAYOUT", "CH+", "NIGHT", "THERMAL", "REC", "MOTION", "EXPAND" };
    public override bool IsActive(string a) => a switch
    {
        "NIGHT" => _mode[_sel] == 1, "THERMAL" => _mode[_sel] == 2, "REC" => C.Sim.Cameras[_sel].Recording, "EXPAND" => _expanded, _ => false,
    };
    public override string PrimaryAction => "MOTION";

    void Log(string s) { _log.Add($"{DateTime.Now:HH:mm:ss}  {s}"); if (_log.Count > 9) _log.RemoveAt(0); }

    public override string? OnAction(string id)
    {
        var cam = C.Sim.Cameras[_sel];
        switch (id)
        {
            case "LAYOUT": _layout = (_layout + 1) % 3; _page = 0; return $"LAYOUT {Layouts[_layout]} CAMERAS";
            case "CH+": { int n = Layouts[_layout]; _page = (_page + 1) % ((9 + n - 1) / n); _sel = Math.Min(8, _page * n); return $"CHANNEL GROUP {_page + 1}"; }
            case "NIGHT": _mode[_sel] = _mode[_sel] == 1 ? 0 : 1; return $"{cam.Name} NIGHT VISION {(_mode[_sel] == 1 ? "ON" : "OFF")}";
            case "THERMAL": _mode[_sel] = _mode[_sel] == 2 ? 0 : 2; return $"{cam.Name} THERMAL {(_mode[_sel] == 2 ? "ON" : "OFF")}";
            case "REC": cam.Recording = !cam.Recording; Log($"{cam.Name} RECORDING {(cam.Recording ? "STARTED" : "STOPPED")}"); return $"{cam.Name} RECORDING {(cam.Recording ? "STARTED" : "STOPPED")}";
            case "MOTION": C.Sim.TriggerMotion(_sel); Log($"MOTION ALERT {cam.Name}"); C.Audio.Play("ping"); C.Events.Trigger("unknown_obj"); return $"MOTION ALERT {cam.Name}";
            case "EXPAND": _expanded = !_expanded; return _expanded ? $"{cam.Name} FULLSCREEN" : "GRID VIEW";
        }
        return null;
    }

    public override void Update(double dt)
    {
        foreach (var c in C.Sim.Cameras) if (c.Motion > 0.9 && Rng.NextDouble() < dt * 2 && _log.Count == 0 || c.Motion > 0.95 && Rng.NextDouble() < 0.02) Log($"MOTION {c.Name}");
    }

    public override void Draw(Gfx g, Rect area)
    {
        var sim = C.Sim;
        double gap = g.U(6);
        area = area.Deflate(gap);
        bool narrow = area.Width < g.U(800) || area.Width / area.Height < 1.2;
        Rect grid, side;
        if (_expanded) { grid = area; side = default; }
        else if (narrow) { var rr = Layout.Rows(area, gap, 3.2, 1); grid = rr[0]; side = rr[1]; }
        else { var cc = Layout.Cols(area, gap, 3.6, 1); grid = cc[0]; side = cc[1]; }

        if (_expanded) DrawFeed(g, grid, sim.Cameras[_sel], _sel, true);
        else
        {
            int n = Layouts[_layout];
            int cols = n == 4 ? 2 : n == 6 ? 3 : 3, rows = n == 4 ? 2 : n == 6 ? 2 : 3;
            if (narrow && n == 6) { cols = 2; rows = 3; }
            double cw = (grid.Width - gap * (cols - 1)) / cols, ch = (grid.Height - gap * (rows - 1)) / rows;
            for (int i = 0; i < n; i++)
            {
                int ci = (_page * n + i) % 9;
                var r = new Rect(grid.X + (i % cols) * (cw + gap), grid.Y + (i / cols) * (ch + gap), cw, ch);
                DrawFeed(g, r, sim.Cameras[ci], ci, false);
            }
        }
        if (_expanded || side.Width < 10) return;
        var rrs = Layout.Rows(side, gap, 1.25, 1);
        var c1 = g.Panel(rrs[0], "CAMERA STATUS");
        double lh = Math.Min(g.U(g.Touch ? 26 : 20), c1.Height / 9);
        for (int i = 0; i < 9; i++)
        {
            var cam = sim.Cameras[i]; var rc = new Rect(c1.X, c1.Y + i * lh, c1.Width, lh);
            if (i == _sel) g.Fill(rc, g.P.Primary, 0.14);
            g.StatusDot(rc.X + g.U(6), rc.Center.Y, cam.Online, 3);
            g.Text($"{cam.Name}", rc.X + g.U(16), rc.Y + (lh - g.U(10)) / 2, 9, g.P.Text, Align.Left, i == _sel);
            g.Text(!cam.Online ? "OFFLINE" : cam.Motion > 0.3 ? "MOTION" : cam.Recording ? "REC" : "STBY", rc.Right - g.U(4), rc.Y + (lh - g.U(10)) / 2, 8.5, !cam.Online ? g.P.Warning : cam.Motion > 0.3 ? g.P.Warning : g.P.Primary, Align.Right, true);
            g.AddHit("cam:" + i, rc);
        }
        var c2 = g.Panel(rrs[1], "EVENT LOG");
        double l2 = g.U(12.5);
        int max = (int)(c2.Height / l2);
        for (int i = Math.Max(0, _log.Count - max); i < _log.Count; i++) g.Text(_log[i], c2.X, c2.Y + (i - Math.Max(0, _log.Count - max)) * l2, 8.5, _log[i].Contains("MOTION") ? g.P.Warning : g.P.Text);
        if (_log.Count == 0) g.Text("NO EVENTS", c2.X, c2.Y, 9, g.P.Text, Align.Left, false, 0.5);
    }

    // ---------------------------------------------------------------- feed rendering
    uint Lum(int mode, double l, double a = 1)
    {
        l = Math.Clamp(l, 0, 1);
        byte B(double v) => (byte)Math.Clamp(v * 255, 0, 255);
        uint c;
        if (mode == 1) c = Palette.Rgb(B(l * 0.2), B(0.12 + l * 0.88), B(l * 0.28));
        else if (mode == 2) c = l < 0.25 ? Palette.Lerp(0xFF05001A, 0xFF5A0A9A, l / 0.25) : l < 0.5 ? Palette.Lerp(0xFF5A0A9A, 0xFFE03020, (l - 0.25) / 0.25) : l < 0.8 ? Palette.Lerp(0xFFE03020, 0xFFFFD020, (l - 0.5) / 0.3) : Palette.Lerp(0xFFFFD020, 0xFFFFFFFF, (l - 0.8) / 0.2);
        else c = Palette.Rgb(B(l * 0.62), B(l * 0.76), B(l * 0.82));
        return Palette.WithAlpha(c, a);
    }

    void DrawFeed(Gfx g, Rect r, Camera cam, int idx, bool big)
    {
        bool sel = idx == _sel;
        int mode = _mode[idx];
        g.Fill(r, 0xFF000000);
        using (g.Clip(r))
        {
            if (!cam.Online) DrawNoSignal(g, r, cam);
            else
            {
                DrawScene(g, r, cam, mode);
                if (cam.Interference > 0.05) DrawInterference(g, r, cam.Interference);
            }
        }
        // lens vignette + sensor bloom
        var vg = new Avalonia.Media.RadialGradientBrush { GradientStops = { new Avalonia.Media.GradientStop(Avalonia.Media.Color.FromArgb(0, 0, 0, 0), 0.55), new Avalonia.Media.GradientStop(Avalonia.Media.Color.FromArgb(170, 0, 0, 0), 1) } };
        g.Dc.DrawRectangle(vg, null, r);
        // overlays
        uint hud = mode == 1 ? 0xFF50FF90 : mode == 2 ? 0xFFFFE070 : g.P.Accent;
        double pad = g.U(6), fs = big ? 12 : 9;
        g.Text($"{cam.Name}  {SceneName(cam)}", r.X + pad, r.Y + pad, fs, hud, Align.Left, true);
        g.Text(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), r.X + pad, r.Bottom - pad - g.U(fs + 2), fs - 1, hud, Align.Left);
        if (cam.Online && cam.Recording) { g.Disc(r.Right - pad - g.U(32), r.Y + pad + g.U(5), g.U(4), 0xFFFF3030, g.Pulse(1) > 0.4 ? 1 : 0.15); g.Text("REC", r.Right - pad, r.Y + pad, fs, 0xFFFF5050, Align.Right, true); }
        if (mode != 0) g.Text(mode == 1 ? "NV" : "THERMAL", r.Right - pad, r.Bottom - pad - g.U(fs + 2), fs, hud, Align.Right, true);
        // signal bars
        for (int b = 0; b < 4; b++) g.Fill(new Rect(r.Right - pad - g.U(40) + b * g.U(5), r.Y + pad + g.U(16) + g.U(6 - b * 1.5), g.U(3), g.U(2 + b * 1.5)), hud, cam.Online && b < (int)(4 * (1 - cam.Interference * 0.7)) ? 0.9 : 0.2);
        g.Brackets(r.Deflate(g.U(2)), hud, 8, 1, 0.5);
        if (cam.Online && cam.Motion > 0.3)
        {
            double f = g.Pulse(3);
            g.Frame(r.Deflate(g.U(1)), 0xFFFF3030, 2.2, 0.4 + 0.6 * f);
            g.Text("MOTION DETECTED", r.Center.X, r.Y + pad, fs, 0xFFFF5050, Align.Center, true, 0.5 + 0.5 * f);
        }
        g.Frame(r, sel ? g.P.Accent : g.P.Primary, sel ? 2 : 1, sel ? 1 : 0.35);
        if (!big) g.AddHit("cam:" + idx, r);
    }

    static string SceneName(Camera c) => (c.Scene % 4) switch { 0 => "CORRIDOR B", 1 => "LOT 4", 2 => "SERVER HALL", _ => "PERIMETER" };

    void DrawNoSignal(Gfx g, Rect r, Camera c)
    {
        for (int i = 0; i < 28; i++) g.Fill(new Rect(r.X, r.Y + Rng.NextDouble() * r.Height, r.Width, g.U(1 + Rng.NextDouble() * 3)), 0xFFFFFFFF, 0.05 + Rng.NextDouble() * 0.12);
        g.Text("NO SIGNAL", r.Center.X, r.Center.Y - g.U(8), 14, g.P.Warning, Align.Center, true, 0.6 + 0.4 * g.Pulse(1));
    }

    void DrawInterference(Gfx g, Rect r, double amt)
    {
        int n = (int)(amt * 14);
        for (int i = 0; i < n; i++)
        {
            double y = r.Y + Rng.NextDouble() * r.Height;
            g.Fill(new Rect(r.X, y, r.Width, g.U(1 + Rng.NextDouble() * 4)), 0xFFFFFFFF, 0.08 + Rng.NextDouble() * 0.2 * amt);
        }
    }

    void DrawScene(Gfx g, Rect r, Camera cam, int mode)
    {
        double t = g.Time, cx = r.Center.X, hz = r.Y + r.Height * 0.42;
        g.Fill(r, Lum(mode, 0.10));
        switch (cam.Scene % 4)
        {
            case 0: // corridor with perspective
                {
                    double vx = cx + Math.Sin(t * 0.2 + cam.Id) * r.Width * 0.04;
                    g.Fill(new Rect(r.X, r.Y, r.Width, hz - r.Y), Lum(mode, 0.18));
                    g.Fill(new Rect(r.X, hz, r.Width, r.Bottom - hz), Lum(mode, 0.12));
                    for (int i = 0; i < 9; i++)
                    {
                        double f = Math.Pow((i + 1) / 9.0, 1.7); double w = r.Width * (0.06 + 0.9 * f * 0.5);
                        double top = hz - (hz - r.Y) * f, bot = hz + (r.Bottom - hz) * f;
                        g.Line(vx - w, top, vx - w, bot, Lum(mode, 0.35), 1, 0.7); g.Line(vx + w, top, vx + w, bot, Lum(mode, 0.35), 1, 0.7);
                        g.Line(vx - w, top, vx + w, top, Lum(mode, 0.25), 1, 0.7);
                        if (i % 3 == 0) g.Fill(new Rect(vx - w * 0.15, top + 2, w * 0.3, Math.Max(2, f * 10)), Lum(mode, 0.95), 0.8 - f * 0.3);
                    }
                    Figure(g, r, mode, 0.5 + 0.35 * Math.Sin(t * 0.35 + cam.Id), hz + (r.Bottom - hz) * 0.5, 1.0 + 0.3 * Math.Sin(t * 0.35 + cam.Id), cam);
                    break;
                }
            case 1: // parking lot
                {
                    g.Fill(new Rect(r.X, hz, r.Width, r.Bottom - hz), Lum(mode, 0.15));
                    for (int i = 0; i < 8; i++) g.Line(r.X + r.Width * i / 7, hz, r.X + (r.Width * i / 7 - cx) * 2.4 + cx, r.Bottom, Lum(mode, 0.4), 1, 0.7);
                    for (int i = 0; i < 4; i++)
                    {
                        double x = r.X + r.Width * (0.12 + 0.22 * i), y = hz + (r.Bottom - hz) * (0.25 + 0.1 * i);
                        double w = r.Width * 0.14, h = r.Height * 0.09;
                        g.Fill(new Rect(x, y, w, h), Lum(mode, 0.32 + 0.1 * i), 1); g.Fill(new Rect(x + w * 0.15, y - h * 0.5, w * 0.7, h * 0.55), Lum(mode, 0.22), 1);
                        g.Fill(new Rect(x + w * 0.08, y + h * 0.5, w * 0.18, h * 0.3), mode == 2 ? Lum(mode, 0.85) : Lum(mode, 0.05), 1);
                    }
                    double mv = ((t * 0.07 + cam.Id * 0.13) % 1.4) - 0.2;
                    g.Fill(new Rect(r.X + r.Width * mv, hz + (r.Bottom - hz) * 0.65, r.Width * 0.16, r.Height * 0.1), Lum(mode, mode == 2 ? 0.9 : 0.55), 1);
                    Figure(g, r, mode, 0.3 + 0.4 * ((t * 0.12 + cam.Id) % 1), hz + (r.Bottom - hz) * 0.7, 0.9, cam);
                    break;
                }
            case 2: // server hall
                {
                    for (int i = 0; i < 6; i++)
                    {
                        double x = r.X + r.Width * (0.05 + 0.16 * i), w = r.Width * 0.12, y = r.Y + r.Height * 0.12, h = r.Height * 0.72;
                        g.Fill(new Rect(x, y, w, h), Lum(mode, mode == 2 ? 0.28 + 0.05 * (i % 3) : 0.2), 1); g.Frame(new Rect(x, y, w, h), Lum(mode, 0.45), 1);
                        for (int k = 0; k < 14; k++)
                        {
                            bool on = Math.Sin(t * (1 + (k + i) % 4) + k * 3.1 + i) > 0.2;
                            g.Fill(new Rect(x + w * 0.12, y + h * 0.05 + k * h * 0.065, w * 0.12, g.U(2.5)), on ? (mode == 0 ? 0xFF30FF70 : Lum(mode, 0.95)) : Lum(mode, 0.12), 1);
                            g.Fill(new Rect(x + w * 0.35, y + h * 0.05 + k * h * 0.065, w * 0.5, g.U(2)), Lum(mode, 0.3), 1);
                        }
                    }
                    break;
                }
            default: // perimeter fence & searchlight
                {
                    g.Fill(new Rect(r.X, hz, r.Width, r.Bottom - hz), Lum(mode, 0.08));
                    for (double x = r.X; x < r.Right; x += g.U(14)) g.Line(x, hz - r.Height * 0.12, x, hz + r.Height * 0.2, Lum(mode, 0.4), 1, 0.7);
                    for (int k = 0; k < 4; k++) g.Line(r.X, hz - r.Height * 0.12 + k * r.Height * 0.08, r.Right, hz - r.Height * 0.12 + k * r.Height * 0.08, Lum(mode, 0.4), 1, 0.6);
                    double sx = r.X + r.Width * (0.5 + 0.45 * Math.Sin(t * 0.5 + cam.Id));
                    var beam = new[] { new Point(r.Right - g.U(10), r.Y), new Point(sx - r.Width * 0.12, r.Bottom), new Point(sx + r.Width * 0.12, r.Bottom) };
                    g.PolyFill(beam, Lum(mode, 0.9), 0.14);
                    Figure(g, r, mode, 0.15 + 0.7 * ((t * 0.09 + cam.Id * 0.3) % 1), hz + (r.Bottom - hz) * 0.55, 0.85, cam);
                    break;
                }
        }
        // sensor noise
        int noise = g.Quality == Core.Configuration.GraphicsProfile.Low ? 0 : mode == 0 ? 10 : 24;
        for (int i = 0; i < noise; i++) g.Fill(new Rect(r.X + Rng.NextDouble() * r.Width, r.Y + Rng.NextDouble() * r.Height, g.U(1.5), g.U(1.5)), Lum(mode, Rng.NextDouble()), 0.35);
    }

    void Figure(Gfx g, Rect r, int mode, double xf, double footY, double scale, Camera cam)
    {
        double h = r.Height * 0.2 * scale, w = h * 0.28, x = r.X + r.Width * Math.Clamp(xf, 0.05, 0.95);
        double bob = Math.Sin(g.Time * 6 + cam.Id) * h * 0.02;
        double lum = mode == 2 ? 0.98 : 0.7;
        g.Fill(new Rect(x - w / 2, footY - h * 0.75 + bob, w, h * 0.45), Lum(mode, lum), 1);          // torso
        g.Disc(x, footY - h * 0.88 + bob, h * 0.09, Lum(mode, lum));                                    // head
        double sw = Math.Sin(g.Time * 6 + cam.Id) * w * 0.4;
        g.Line(x - w * 0.2, footY - h * 0.3, x - w * 0.2 + sw, footY, Lum(mode, lum * 0.9), Math.Max(2, w * 0.28));
        g.Line(x + w * 0.2, footY - h * 0.3, x + w * 0.2 - sw, footY, Lum(mode, lum * 0.9), Math.Max(2, w * 0.28));
        if (cam.Motion > 0.25)
        {   // motion-tracking box
            var box = new Rect(x - w * 1.0, footY - h * 1.02, w * 2, h * 1.08);
            g.Frame(box, 0xFFFF4040, 1.4, 0.9);
            g.Text($"ID {cam.Id * 137 % 900 + 100}", box.X, box.Y - g.U(10), 8, 0xFFFF6060, Align.Left, true);
        }
    }

    public override void Tap(Point p, string? id)
    {
        if (id != null && id.StartsWith("cam:")) { int i = int.Parse(id[4..]); if (_sel != i) { _sel = i; C.Audio.Play("beep"); } }
    }

    public override void DoubleTap(Point p, string? id)
    {
        if (id != null && id.StartsWith("cam:")) { _sel = int.Parse(id[4..]); _expanded = !_expanded; C.Toast(_expanded ? $"{C.Sim.Cameras[_sel].Name} FULLSCREEN" : "GRID VIEW"); }
    }
}
