using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Blacksite.Core.Audio;
using Blacksite.UI.Rendering;

namespace Blacksite.UI.Modules;

/// <summary>
/// Artwork-driven preset: the supplied reference picture is the background (shown as a three-console wall, or focused on one console)
/// and live, interactive elements are placed on top of it at the picture's own coordinates (typing terminals, waveforms,
/// progress bars, clock, glowing COMPUTE buttons). All data is fictional.
/// </summary>
public abstract class SkinModule : ModuleBase
{
    protected sealed class Wave { public Rect R; public uint Col; public double Amp = 1; public int Seed; }
    public enum FxKind { Radar, Orbit, Node, Scan, Eq }
    /// <summary>Animated overlay primitive placed on the artwork: working radar sweep, orbiting markers, pulsing nodes, scan beams, equaliser bars.</summary>
    protected sealed class Fx
    {
        public FxKind K; public Rect R; public Point C; public double A, B = 1, Tilt, Speed = 1; public uint Col = 0xFF40FF90; public int N = 4, Seed;
        public static Fx Radar(double x, double y, double r, uint col, double ry = 1, int blips = 6, double speed = 1) => new() { K = FxKind.Radar, C = new Point(x, y), A = r, B = ry, Col = col, N = blips, Speed = speed, Seed = (int)(x + y) };
        public static Fx Orbit(double x, double y, double rx, double ry, uint col, double tilt = 0, int dots = 2, double speed = 1) => new() { K = FxKind.Orbit, C = new Point(x, y), A = rx, B = ry, Tilt = tilt, Col = col, N = dots, Speed = speed, Seed = (int)(rx * 7 + ry) };
        public static Fx Node(double x, double y, uint col, double size = 6, int seed = 0) => new() { K = FxKind.Node, C = new Point(x, y), A = size, Col = col, Seed = seed + (int)(x * 3 + y) };
        public static Fx Scan(Rect r, uint col, bool vertical = true, double speed = 1) => new() { K = FxKind.Scan, R = r, Col = col, Tilt = vertical ? 1 : 0, Speed = speed };
        public static Fx Eq(Rect r, uint col, int seed = 0) => new() { K = FxKind.Eq, R = r, Col = col, Seed = seed };
    }
    protected sealed class Band
    {
        public string Name = ""; public Rect Src;
        public uint Fg, Accent, Dim; public string Prompt = "root@blacksite:~#";
        public Rect Term; public double FontPx = 11, LinePx = 13.5; public string[] Script = Array.Empty<string>();
        public bool PicText; public Rect Compute; public string GrantedText = "ACCESS GRANTED"; public Rect GrantedAt;
        public Rect[] Subs = Array.Empty<Rect>(); public string[] SubNames = Array.Empty<string>();
        public string ClockFmt = "HH:mm:ss"; public double ClockPx; public List<Wave> Waves = new(); public List<Fx> Fx = new(); public Rect? Clock;
        public bool Timestamped; public Rect? Bar;           // optional progress bar inside the terminal
    }

    protected abstract string Resource { get; }
    protected abstract Band[] Bands { get; }
    protected abstract string[] Live(Band b, int idx, double p, double t, bool granted);   // terminal lines for the band

    Bitmap? _bmp; int[]? _px; int _w, _h;
    protected double T, _anim; int _focus;                  // 0 = all consoles, 1..n = a single console
    readonly double[] _comp = new double[8]; readonly double[] _granted = new double[8]; readonly double[] _prog = new double[8];
    Rect _dest; double _scale; Rect _srcShown;

    public override string[] Actions => new[] { "COMPUTE", "VIEW", "ALL", "RESET" };
    public override string PrimaryAction => "COMPUTE";
    public override bool NoTicker => false;

    protected override void OnInit() { for (int i = 0; i < _prog.Length; i++) { _prog[i] = 0.2 + 0.1 * i; _comp[i] = -1; _granted[i] = -1; } Load(); }

    void Load()
    {
        try
        {
            _bmp = new Bitmap(AssetLoader.Open(new Uri("avares://Blacksite.UI/Assets/skins/" + Resource)));
            _w = _bmp.PixelSize.Width; _h = _bmp.PixelSize.Height; _px = new int[_w * _h];
            var h = GCHandle.Alloc(_px, GCHandleType.Pinned);
            try { _bmp.CopyPixels(new PixelRect(0, 0, _w, _h), h.AddrOfPinnedObject(), _px.Length * 4, _w * 4); } finally { h.Free(); }
        }
        catch { _bmp = null; _px = null; }
    }

    uint Sample(double x, double y)
    {
        if (_px == null) return 0xFF000000;
        int ix = Math.Clamp((int)x, 0, _w - 1), iy = Math.Clamp((int)y, 0, _h - 1); long r = 0, g = 0, b = 0; int n = 0;
        for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) { int p = _px[Math.Clamp(iy + dy, 0, _h - 1) * _w + Math.Clamp(ix + dx, 0, _w - 1)]; r += (p >> 16) & 255; g += (p >> 8) & 255; b += p & 255; n++; }
        return 0xFF000000 | ((uint)(r / n) << 16) | ((uint)(g / n) << 8) | (uint)(b / n);
    }

    public override string? OnAction(string id)
    {
        switch (id)
        {
            case "COMPUTE": return Compute(_focus == 0 ? 0 : _focus - 1);
            case "VIEW": _focus = (_focus + 1) % (Bands.Length + 1); return _focus == 0 ? "ALL CONSOLES" : "CONSOLE: " + Bands[_focus - 1].Name;
            case "ALL": _focus = 0; return "ALL CONSOLES";
            case "RESET": for (int i = 0; i < _comp.Length; i++) { _comp[i] = -1; _granted[i] = -1; } return "RESET";
        }
        return null;
    }

    string? Compute(int i)
    {
        if (_comp[i] >= 0 && _comp[i] < 1) { C.Audio.Error(); return "COMPUTE ALREADY RUNNING"; }
        _comp[i] = 0; _granted[i] = -1; C.Audio.Play("scan", SoundCategory.Event); C.Audio.Play("surge", SoundCategory.Event, 0.5f); return $"{Bands[i].Name}: COMPUTE STARTED";
    }

    public override string? OnModuleButton(string id)
    {
        if (id.StartsWith("sk:compute:")) return Compute(int.Parse(id[11..]));
        if (id.StartsWith("sk:sub:")) { var p = id.Split(':'); C.Audio.Play("handshake", SoundCategory.Ui, 0.6f); _prog[int.Parse(p[2])] = Math.Min(1, _prog[int.Parse(p[2])] + 0.12); return Bands[int.Parse(p[2])].SubNames[int.Parse(p[3])] + " EXECUTED"; }
        return null;
    }

    public override void Tap(Point p, string? id) { if (id != null && id.StartsWith("sk:band:") && _focus == 0) { _focus = int.Parse(id[8..]) + 1; C.Toast("CONSOLE: " + Bands[_focus - 1].Name); C.Audio.Play("tick"); } }
    public override void DoubleTap(Point p, string? id) { if (_focus != 0) _focus = 0; }

    public override void Update(double dt)
    {
        T += dt;
        for (int i = 0; i < Bands.Length; i++)
        {
            if (_comp[i] >= 0 && _comp[i] < 1)
            {
                _comp[i] += dt / 7.0; _prog[i] = Math.Max(_prog[i], Math.Clamp(_comp[i], 0, 1));
                if (_comp[i] >= 1) { _comp[i] = 1; _granted[i] = 0; C.Audio.Play("access", SoundCategory.Event); C.Audio.Play("chime", SoundCategory.Event); C.Toast(Bands[i].Name + ": " + Bands[i].GrantedText); }
            }
            else if (_comp[i] < 0 || _comp[i] >= 1) { if (_granted[i] >= 0) { _granted[i] += dt; if (_granted[i] > 9) { _granted[i] = -1; _comp[i] = -1; _prog[i] = 0.25; } } else _prog[i] = 0.3 + 0.55 * (0.5 + 0.5 * Math.Sin(T * 0.11 + i * 1.7)); }
        }
    }

    Point M(double x, double y) => new(_dest.X + (x - _srcShown.X) * _scale, _dest.Y + (y - _srcShown.Y) * _scale);
    Rect MR(Rect r) { var a = M(r.X, r.Y); return new Rect(a.X, a.Y, r.Width * _scale, r.Height * _scale); }

    public override void Draw(Gfx g, Rect area)
    {
        g.Fill(area, 0xFF000000, 1);
        if (_bmp == null) { g.Text("ARTWORK NOT FOUND", area.Center.X, area.Center.Y, 14, g.P.Warning, Align.Center, true); return; }
        _srcShown = _focus == 0 ? new Rect(0, 0, _w, _h) : Bands[_focus - 1].Src;
        _scale = Math.Min(area.Width / _srcShown.Width, area.Height / _srcShown.Height);
        _dest = new Rect(area.X + (area.Width - _srcShown.Width * _scale) / 2, area.Y + (area.Height - _srcShown.Height * _scale) / 2, _srcShown.Width * _scale, _srcShown.Height * _scale);
        using var clip = g.Clip(_dest);
        using (g.Dc.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality })) g.Dc.DrawImage(_bmp, _srcShown, _dest);
        for (int i = 0; i < Bands.Length; i++) { var b = Bands[i]; var vis = _srcShown.Intersect(b.Src); if (vis.Width < 5 || vis.Height < 5) continue; DrawBand(g, b, i); if (_focus == 0) g.AddHit("sk:band:" + i, MR(b.Src)); }
        // global finish: subtle scan shimmer
        g.HoloShimmer(_dest, null);
    }

    void DrawBand(Gfx g, Band b, int i)
    {
        double p = _prog[i]; bool gr = _granted[i] >= 0;
        foreach (var w in b.Waves) DrawWave(g, w);
        foreach (var f in b.Fx) DrawFx(g, f);
        if (b.Clock.HasValue) { var r = MR(b.Clock.Value); g.Fill(r, Sample(b.Clock.Value.X - 3, b.Clock.Value.Y + 2), 1); g.Text(DateTime.Now.ToString(b.ClockFmt), r.X + 1, r.Y, (b.ClockPx > 0 ? b.ClockPx : b.FontPx) * _scale, b.Fg, Align.Left, false, 0.95); }
        // terminal: cover the baked text and draw the live console
        if (b.Term.Width > 1) DrawTerm(g, b, i, p, gr);
        DrawButtons(g, b, i, gr);
    }

    void DrawTerm(Gfx g, Band b, int i, double p, bool gr)
    {
        var tr = MR(b.Term); g.Fill(tr, Sample(b.Term.X + 6, b.Term.Y + b.Term.Height * 0.5), 1);
        var lines = Live(b, i, p, T, gr); double fs = b.FontPx * _scale, lh = b.LinePx * _scale;
        int revealed = (int)(T * 4.0) % 400; int total = lines.Length; double cycle = T % (total / 4.0 + 14); int shown = cycle < total / 4.0 ? (int)(cycle * 4) : total; if (_comp[i] >= 0 || gr) shown = total;
        using (g.Clip(tr.Inflate(1)))
            for (int k = 0; k < Math.Min(shown, total); k++)
            {
                string l = lines[k]; double y = tr.Y + k * lh; uint c = l.StartsWith("[+]") || l.Contains("GRANTED") || l.Contains("granted") ? b.Accent : l.StartsWith("root@") ? b.Accent : l.Length > 0 && (l[0] == ' ' || char.IsDigit(l[0]) && l.Contains(' ') && !l.Contains(':') && l.Length > 20) ? b.Dim : b.Fg;
                if (l.StartsWith("[#BAR]")) { var bar = new Rect(tr.X + g.U(2) + fs * 6.5, y + lh * 0.2, tr.Width * 0.62, lh * 0.6); g.Fill(bar, b.Fg, 0.12); g.Frame(bar, b.Fg, 1, 0.7); g.Fill(new Rect(bar.X + 1, bar.Y + 1, (bar.Width - 2) * p, bar.Height - 2), b.Accent, 0.85); g.Text("Ingest:", tr.X, y, fs, b.Dim, Align.Left); continue; }
                g.Text(l, tr.X, y, fs, c, Align.Left, false, 0.97);
            }
        if ((int)(T * 2) % 2 == 0 && shown >= total) g.Fill(new Rect(tr.X + (b.Prompt.Length + 1) * fs * 0.6, tr.Y + (total - 1) * lh + lh * 0.1, fs * 0.55, lh * 0.8), b.Fg, 0.9);
    }

    void DrawButtons(Gfx g, Band b, int i, bool gr)
    {
        double fs = b.FontPx * _scale;
        // COMPUTE button: hover / press / running glow
        if (b.Compute.Width > 1) DrawCompute(g, b, i, gr, fs);
        for (int k = 0; k < b.Subs.Length; k++) { var sr = MR(b.Subs[k]); g.AddHit($"sk:sub:{i}:{k}", sr, HitKind.Button); if (g.IsHover($"sk:sub:{i}:{k}")) g.Fill(sr, b.Accent, 0.18); }
        if (gr && b.GrantedAt.Width > 1) { double f = Math.Clamp(1 - (_granted[i] - 5) / 4, 0, 1); if (_granted[i] < 1) f = _granted[i]; var ga = MR(b.GrantedAt); using (g.Dc.PushOpacity(Math.Clamp(f, 0, 1))) { if (!b.PicText) g.Fill(ga.Inflate(g.U(3)), 0xFF000000, 0.55); g.Frame(ga.Inflate(g.U(3)), b.Accent, 2, 0.95); if (!b.PicText) g.TextIn(ga, b.GrantedText, ga.Height * 0.55, b.Accent, Align.Center, true); } }
    }

    void DrawCompute(Gfx g, Band b, int i, bool gr, double fs)
    {
        var cb = MR(b.Compute); string cid = "sk:compute:" + i; bool hover = g.IsHover(cid), down = g.IsPressed(cid), run = _comp[i] >= 0 && _comp[i] < 1;
        double a = (hover ? 0.18 : 0) + (down ? 0.25 : 0) + (run ? 0.12 + 0.12 * g.Pulse(2.5) : 0) + (gr ? 0.16 : 0);
        if (a > 0) { g.Fill(cb, b.Accent, a); g.Frame(cb, b.Accent, 2, 0.5 + 0.5 * Math.Min(1, a * 3)); }
        if (run) g.Text($"{_comp[i] * 100:0}%", cb.Right - g.U(6), cb.Center.Y - fs * 0.6, fs * 1.2, b.Accent, Align.Right, true);
        g.AddHit(cid, cb, HitKind.Button);
    }

    void DrawWave(Gfx g, Wave w)
    {
        var r = MR(w.R); g.Fill(r, Sample(w.R.X + 2, w.R.Y + w.R.Height * 0.5), 1);
        var pts = new List<Point>(); int n = Math.Max(24, (int)(r.Width / 2));
        for (int i = 0; i < n; i++) { double x = i / (double)(n - 1), env = 0.3 + 0.7 * Math.Abs(Math.Sin(x * 7 + T * 0.9 + w.Seed)); double v = ((Math.Sin(x * 91 + T * 9 + w.Seed) * 0.5 + Math.Sin(x * 37 - T * 5) * 0.35 + ((((i * 2654435761u + (uint)(T * 24) * 40503u + (uint)w.Seed * 977u) >> 7) % 1000) / 500.0 - 1) * 0.5)) * env * w.Amp; pts.Add(new Point(r.X + r.Width * x, r.Center.Y - v * r.Height * 0.42)); }
        g.Poly(pts, w.Col, 3.6, 0.14); g.Poly(pts, w.Col, 1.2, 0.98);
    }

    static double Hh(double i) { double v = Math.Sin(i * 12.9898 + 78.233) * 43758.5453; return v - Math.Floor(v); }

    void DrawFx(Gfx g, Fx f)
    {
        switch (f.K)
        {
            case FxKind.Radar:
                {
                    var c = M(f.C.X, f.C.Y); double rx = f.A * _scale, ry = rx * f.B; double sw = (T * 1.9 * f.Speed) % (Math.PI * 2);
                    using (g.Dc.PushGeometryClip(new EllipseGeometry(new Rect(c.X - rx, c.Y - ry, rx * 2, ry * 2))))
                    {
                        for (int k = 0; k < 34; k++) { double a = sw - k * 0.045; g.Line(c.X, c.Y, c.X + Math.Cos(a) * rx, c.Y + Math.Sin(a) * ry, f.Col, 2.2, 0.42 * Math.Pow(1 - k / 34.0, 1.6)); }
                        g.Line(c.X, c.Y, c.X + Math.Cos(sw) * rx, c.Y + Math.Sin(sw) * ry, 0xFFFFFFFF, 1.4, 0.85);
                        for (int i = 0; i < f.N; i++)
                        {
                            double a = Hh(f.Seed + i * 3.1) * Math.PI * 2, d = 0.25 + 0.7 * Hh(f.Seed + i * 5.7 + 1), since = ((sw - a) % (Math.PI * 2) + Math.PI * 2) % (Math.PI * 2), life = Math.Pow(1 - since / (Math.PI * 2), 2.2);
                            double bx = c.X + Math.Cos(a) * rx * d, by = c.Y + Math.Sin(a) * ry * d, br = g.U(1.6 + 2.2 * life);
                            g.Disc(bx, by, br * 2.6, f.Col, 0.18 * life); g.Disc(bx, by, br, 0xFFFFFFFF, 0.25 + 0.7 * life);
                        }
                    }
                    double rp = (T * 0.55 * f.Speed) % 1; g.Dc.DrawEllipse(null, g.Pn(f.Col, 1.2, 0.5 * (1 - rp)), c, rx * rp, ry * rp);
                    g.Dc.DrawEllipse(null, g.Pn(f.Col, 1.2, 0.35 + 0.2 * Math.Sin(T * 3)), c, rx, ry);
                    break;
                }
            case FxKind.Orbit:
                {
                    var c = M(f.C.X, f.C.Y); double rx = f.A * _scale, ry = f.B * _scale, ct = Math.Cos(f.Tilt), st = Math.Sin(f.Tilt);
                    for (int i = 0; i < f.N; i++)
                    {
                        double a0 = T * 0.5 * f.Speed * (0.7 + 0.5 * Hh(f.Seed + i)) + i * Math.PI * 2 / f.N;
                        for (int k = 0; k < 12; k++)
                        {
                            double a = a0 - k * 0.05, ex = Math.Cos(a) * rx, ey = Math.Sin(a) * ry; double px = c.X + ex * ct - ey * st, py = c.Y + ex * st + ey * ct;
                            g.Disc(px, py, g.U(k == 0 ? 2.4 : 1.8 - k * 0.12), f.Col, k == 0 ? 0.95 : 0.4 * (1 - k / 12.0));
                        }
                        double hx = Math.Cos(a0) * rx, hy = Math.Sin(a0) * ry; g.Disc(c.X + hx * ct - hy * st, c.Y + hx * st + hy * ct, g.U(6), f.Col, 0.18);
                    }
                    break;
                }
            case FxKind.Node:
                {
                    var c = M(f.C.X, f.C.Y); double ph = (T * 0.7 + Hh(f.Seed)) % 1, r = f.A * _scale;
                    g.Dc.DrawEllipse(null, g.Pn(f.Col, 1.4, 0.8 * (1 - ph)), c, r * (0.5 + 1.8 * ph), r * (0.5 + 1.8 * ph));
                    g.Disc(c.X, c.Y, r * 0.45, f.Col, 0.55 + 0.35 * Math.Sin(T * 5 + f.Seed));
                    break;
                }
            case FxKind.Scan:
                {
                    var r = MR(f.R); double ph = (T * 0.32 * f.Speed + Hh(f.Seed)) % 1.15; using var cl = g.Clip(r);
                    if (f.Tilt > 0) { double y = r.Y + ph * r.Height; g.Fill(new Rect(r.X, y - g.U(14), r.Width, g.U(14)), f.Col, 0.10); g.Fill(new Rect(r.X, y - 1, r.Width, 2), f.Col, 0.65); }
                    else { double x = r.X + ph * r.Width; g.Fill(new Rect(x - g.U(14), r.Y, g.U(14), r.Height), f.Col, 0.10); g.Fill(new Rect(x - 1, r.Y, 2, r.Height), f.Col, 0.65); }
                    break;
                }
            case FxKind.Eq:
                {
                    var src = f.R; var r = MR(src); g.Fill(r, Sample(src.X + 1, src.Y + src.Height - 2), 1);
                    int n = Math.Max(8, (int)(r.Width / g.U(3.2))); double bw = r.Width / n;
                    for (int i = 0; i < n; i++)
                    {
                        double x = i / (double)n, v = 0.2 + 0.8 * Math.Abs(Math.Sin(x * 9 + T * 1.7 + f.Seed) * 0.55 + Math.Sin(x * 31 - T * 4.3) * 0.3 + (Hh(i * 1.3 + Math.Floor(T * 14) + f.Seed) - 0.5) * 0.45);
                        double h = r.Height * Math.Min(1, v); uint c = v > 0.85 ? 0xFFFFFFFF : f.Col;
                        g.Fill(new Rect(r.X + i * bw, r.Bottom - h, Math.Max(1, bw - 1), h), c, 0.85);
                    }
                    break;
                }
        }
    }
}
