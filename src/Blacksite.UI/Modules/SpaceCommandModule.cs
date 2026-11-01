using Avalonia;
using Blacksite.Core.Themes;
using Blacksite.UI.Rendering;

namespace Blacksite.UI.Modules;

/// <summary>PRESET 01 – deep-space mission control.</summary>
public sealed class SpaceCommandModule : ModuleBase
{
    sealed record Body(string Name, uint Col, uint Band, double Radius, double Orbit, double Period, bool Ring, int Moons, string Atmo, int Temp);
    static readonly Body[] Bodies =
    {
        new("ASTRA", 0xFFC0603A, 0xFF6A2A1A, 0.55, 0.30, 24, false, 2, "CO2 / THIN", -63),
        new("KYROS", 0xFFD8B070, 0xFF8A6030, 1.0, 0.52, 70, true, 11, "H2 / HE", -140),
        new("VELA", 0xFF5090E0, 0xFF2A60B0, 0.75, 0.74, 130, false, 5, "N2 / CH4", -200),
        new("ORYN", 0xFF58C088, 0xFF2A7A50, 0.65, 0.92, 210, false, 3, "N2 / O2", 12),
    };

    readonly (double x, double y, double z)[] _stars = new (double, double, double)[260];
    int _sel = 1;
    double _yaw = 0.6, _pitch = 1.05, _zoom = 1, _spin;
    int _selSat = -1;
    bool _tracking;
    // launch state
    enum Launch { Idle, Countdown, Ascent, Orbit }
    Launch _launch = Launch.Idle;
    double _launchT;
    readonly List<string> _log = new();
    double _comm;

    readonly double[] _satPhase = { 0.3, 1.9, 3.4, 5.0 };
    readonly double[] _satIncl = { 0.5, 1.1, -0.7, 0.0 };
    readonly double[] _satRad = { 1.55, 1.8, 2.15, 2.5 };
    readonly string[] _satName = { "RELAY-A", "SURVEYOR", "BEACON-3", "ARGO-LINK" };

    protected override void OnInit()
    {
        for (int i = 0; i < _stars.Length; i++) _stars[i] = (Rng.NextDouble(), Rng.NextDouble(), 0.2 + Rng.NextDouble() * 0.8);
        Log("MISSION CONTROL ONLINE");
    }

    void Log(string s) { _log.Add($"T+{C.Sim.Time:0000}  {s}"); if (_log.Count > 8) _log.RemoveAt(0); }

    public override string[] Actions => new[] { "TRACK", "LAUNCH", "SIGNAL", "ZOOM+", "ZOOM-" };
    public override bool IsActive(string a) => a == "TRACK" && _tracking || a == "LAUNCH" && _launch != Launch.Idle;

    public override string? OnAction(string id)
    {
        switch (id)
        {
            case "TRACK":
                _tracking = !_tracking;
                if (_tracking && _selSat < 0) _selSat = 0;
                Log(_tracking ? $"TRACKING {_satName[Math.Max(0, _selSat)]}" : "TRACKING OFF");
                return _tracking ? "TRACKING MODE ENGAGED" : "TRACKING DISENGAGED";
            case "LAUNCH":
                if (_launch != Launch.Idle) { C.Audio.Error(); return "LAUNCH SEQUENCE ALREADY ACTIVE"; }
                _launch = Launch.Countdown; _launchT = 0; Log("LAUNCH SEQUENCE ARGO-1 STARTED");
                C.Audio.Say("Launch sequence initiated.");
                return "LAUNCH SEQUENCE INITIATED";
            case "SIGNAL":
                C.Events.Trigger("incoming_tx"); Log("INCOMING TRANSMISSION"); return "OPENING DEEP SPACE CHANNEL";
            case "ZOOM+": _zoom = Clamp(_zoom * 1.25, 0.5, 3); return $"ZOOM {_zoom:0.0}X";
            case "ZOOM-": _zoom = Clamp(_zoom / 1.25, 0.5, 3); return $"ZOOM {_zoom:0.0}X";
        }
        return null;
    }

    public override void Update(double dt)
    {
        _spin += dt * 0.35;
        for (int i = 0; i < 4; i++) _satPhase[i] += dt * (0.5 + 0.18 * i) / (1 + 0.2 * _satRad[i]);
        if (_tracking && _selSat >= 0) _yaw += (-_satPhase[_selSat] + 1.0 - _yaw) * Math.Min(1, dt * 0.8) * 0.2;
        _comm = 0.7 + 0.3 * Math.Sin(C.Sim.Time * 0.5);
        if (_launch == Launch.Countdown)
        {
            _launchT += dt;
            if (_launchT >= 10) { _launch = Launch.Ascent; _launchT = 0; C.Audio.Play("whoosh", Blacksite.Core.Audio.SoundCategory.Alarm); Log("LIFTOFF"); C.Toast("LIFTOFF"); }
        }
        else if (_launch == Launch.Ascent)
        {
            _launchT += dt;
            if (_launchT >= 8) { _launch = Launch.Orbit; Log("ARGO-1 ORBIT ACHIEVED"); C.Toast("ORBIT ACHIEVED"); C.Audio.Confirm(); C.Audio.Say("Orbit achieved."); }
        }
    }

    (double x, double y, double z) Orbit(double rad, double incl, double ang)
    {
        double x = Math.Cos(ang) * rad, z = Math.Sin(ang) * rad;
        double y = z * Math.Sin(incl); z *= Math.Cos(incl);
        // yaw
        double x2 = x * Math.Cos(_yaw) - z * Math.Sin(_yaw), z2 = x * Math.Sin(_yaw) + z * Math.Cos(_yaw);
        // pitch (view tilt)
        double y2 = y * Math.Cos(_pitch) - z2 * Math.Sin(_pitch), zz = y * Math.Sin(_pitch) + z2 * Math.Cos(_pitch);
        return (x2, y2, zz);
    }

    public override void Draw(Gfx g, Rect area)
    {
        g.Nebula(area, g.P.Secondary, 0xFF7028B0, 3, 0.85);
        DrawStars(g, area);
        var lay = Layout.Adaptive(g, area, 6, 0.46);
        DrawMain(g, lay.Main);
        for (int i = 0; i < lay.Panels.Count; i++) DrawPanel(g, i, lay.Panels[i]);
    }

    void DrawStars(Gfx g, Rect a)
    {
        int n = g.Quality == Core.Configuration.GraphicsProfile.Low ? 60 : g.Quality == Core.Configuration.GraphicsProfile.Balanced ? 160 : _stars.Length;
        double drift = g.Reduced ? 0 : g.Time * 3;
        for (int i = 0; i < n; i++)
        {
            var s = _stars[i];
            double x = a.X + ((s.x * a.Width + drift * s.z * 4) % a.Width), y = a.Y + s.y * a.Height;
            double tw = 0.5 + 0.5 * Math.Sin(g.Time * (1 + s.z * 3) + i);
            g.Disc(x, y, g.U(0.5 + s.z * 1.1), 0xFFFFFFFF, 0.15 + 0.5 * s.z * tw);
        }
    }

    void DrawMain(Gfx g, Rect r)
    {
        var inner = g.Panel(r, $"ORBITAL VIEW // {Bodies[_sel].Name}");
        using var _ = g.Clip(inner);
        double cx = inner.Center.X, cy = inner.Center.Y;
        double rad = Math.Min(inner.Width, inner.Height) * 0.17 * _zoom * (0.7 + 0.5 * Bodies[_sel].Radius);
        var b = Bodies[_sel];
        g.AddHit("main:view", inner);
        g.HoloBase(inner, cx, inner.Bottom - g.U(10), Math.Min(inner.Width, inner.Height) * 0.7);
        // orbits (back pass)
        for (int s = 0; s < 4; s++) DrawOrbit(g, cx, cy, rad, s, false);
        g.Planet(cx, cy, rad, b.Col, b.Band, _spin, b.Ring, 0.28, _sel switch { 0 => PlanetKind.Rocky, 1 => PlanetKind.Gas, 2 => PlanetKind.Ice, _ => PlanetKind.Earth }, _sel + 1);
        for (int s = 0; s < 4; s++) DrawOrbit(g, cx, cy, rad, s, true);
        // satellites
        for (int s = 0; s < 4; s++)
        {
            var p = Orbit(_satRad[s] * rad, _satIncl[s], _satPhase[s]);
            double sx = cx + p.x, sy = cy + p.y;
            bool hidden = p.z < 0 && Math.Sqrt(p.x * p.x + p.y * p.y) < rad;
            if (hidden) continue;
            bool sel = s == _selSat;
            g.Disc(sx, sy, g.U(sel ? 4 : 3), sel ? g.P.Accent : g.P.Primary);
            g.AddHit("sat:" + s, new Rect(sx - g.U(14), sy - g.U(14), g.U(28), g.U(28)));
            if (sel)
            {
                g.GlowCircle(sx, sy, g.U(11), g.P.Accent, 1.2, 1, g.U(3));
                g.Text(_satName[s], sx + g.U(14), sy - g.U(14), 9, g.P.Accent, Align.Left, true);
                g.Text($"ALT {(_satRad[s] - 1) * 4200:0} KM", sx + g.U(14), sy - g.U(3), 8, g.P.Text, Align.Left);
                if (_tracking) { g.Line(sx - g.U(18), sy, sx - g.U(8), sy, g.P.Accent, 1); g.Line(sx + g.U(8), sy, sx + g.U(18), sy, g.P.Accent, 1); }
            }
        }
        // launch vehicle
        if (_launch == Launch.Ascent || _launch == Launch.Orbit)
        {
            double t = _launch == Launch.Orbit ? 1 : _launchT / 8;
            double lr = rad * (1 + 0.55 * t), la = -Math.PI / 2 + t * 2.2 + (_launch == Launch.Orbit ? (g.Time * 0.6) : 0);
            double lx = cx + Math.Cos(la) * lr, ly = cy + Math.Sin(la) * lr * 0.95;
            g.GlowCircle(lx, ly, g.U(4), g.P.Warning, 1.4);
            g.Text("ARGO-1", lx + g.U(8), ly - g.U(4), 9, g.P.Warning, Align.Left, true);
            if (_launch == Launch.Ascent)
                for (int i = 1; i < 8; i++) g.Disc(lx - Math.Cos(la + 1.5) * i * g.U(3), ly - Math.Sin(la + 1.5) * i * g.U(3), g.U(2.4 - i * 0.25), g.P.Warning, 0.6 - i * 0.07);
        }
        if (_launch == Launch.Countdown)
        {
            double left = 10 - _launchT;
            g.Text($"T-{left:00.0}", cx, inner.Y + inner.Height * 0.1, 34, left < 3 ? g.P.Warning : g.P.Accent, Align.Center, true);
            g.Text("LAUNCH SEQUENCE ARGO-1", cx, inner.Y + inner.Height * 0.1 + g.U(40), 10, g.P.Text, Align.Center);
        }
        g.HoloShimmer(inner);
        g.Text("DRAG: ROTATE   WHEEL/PINCH: ZOOM   TAP SAT: SELECT", inner.X + g.U(4), inner.Bottom - g.U(12), 8, g.P.Text, Align.Left, false, 0.45);
    }

    void DrawOrbit(Gfx g, double cx, double cy, double rad, int s, bool frontOnly)
    {
        int seg = g.Quality == Core.Configuration.GraphicsProfile.Low ? 36 : 72;
        var pts = new List<Point>(seg + 1);
        for (int i = 0; i <= seg; i++)
        {
            var p = Orbit(_satRad[s] * rad, _satIncl[s], i * Math.PI * 2 / seg);
            bool hide = p.z < 0 && Math.Sqrt(p.x * p.x + p.y * p.y) < rad;
            if (frontOnly && p.z <= 0) { if (pts.Count > 1) g.Poly(pts, g.P.Primary, 1, 0.5); pts.Clear(); continue; }
            if (!frontOnly && hide) { if (pts.Count > 1) g.Poly(pts, g.P.Primary, 1, 0.25); pts.Clear(); continue; }
            pts.Add(new Point(cx + p.x, cy + p.y));
        }
        if (pts.Count > 1) g.Poly(pts, g.P.Primary, 1, frontOnly ? 0.5 : 0.25);
    }

    void DrawPanel(Gfx g, int i, Rect r)
    {
        switch (i)
        {
            case 0: DrawSystem(g, r); break;
            case 1:
                {
                    var c = g.Panel(r, "SPACECRAFT TELEMETRY");
                    var rows = Layout.Rows(c, g.U(4), 1, 1, 1.3);
                    g.Graph(rows[0], C.Sim.Signal, g.P.Primary, 0, 1, true, "LINK", "");
                    g.Graph(rows[1], C.Sim.Power, g.P.Secondary, null, null, true, "BUS PWR", "%");
                    g.KeyVal(rows[2].X, rows[2].Y + g.U(2), rows[2].Width, "VELOCITY", $"{7.2 + C.Sim.Noise(0.05):0.00} KM/S");
                    g.KeyVal(rows[2].X, rows[2].Y + g.U(16), rows[2].Width, "ATTITUDE", $"{Math.Sin(C.Sim.Time * 0.2) * 3:+0.0;-0.0} DEG");
                    g.KeyVal(rows[2].X, rows[2].Y + g.U(30), rows[2].Width, "FUEL", $"{C.Sim.Ship.Fuel * 100:0.0} %");
                    break;
                }
            case 2:
                {
                    var c = g.Panel(r, "ORBITAL RADAR");
                    double rad = Math.Min(c.Width, c.Height) / 2 - g.U(2);
                    var blips = new List<(double, double, uint, string?)>();
                    for (int s = 0; s < 4; s++) blips.Add((_satPhase[s] * 57.3 % 360, 0.3 + 0.15 * s, s == _selSat ? g.P.Accent : g.P.Primary, _satName[s]));
                    g.Radar(c.Center.X, c.Center.Y, rad, g.Time * 90 % 360, blips);
                    break;
                }
            case 3: DrawTimeline(g, r); break;
            case 4:
                {
                    var c = g.Panel(r, "COMMUNICATIONS");
                    string[] ch = { "DSN-ALPHA", "RELAY NET", "CREW LINK", "TELEMETRY" };
                    for (int k = 0; k < ch.Length; k++)
                    {
                        double y = c.Y + k * g.U(19);
                        g.StatusDot(c.X + g.U(5), y + g.U(7), k != 2 || _comm > 0.6);
                        g.Text(ch[k], c.X + g.U(16), y, 10, g.P.Text);
                        g.Bar(new Rect(c.Right - g.U(70), y + g.U(2), g.U(70), g.U(8)), 0.5 + 0.5 * Math.Sin(C.Sim.Time * 0.4 + k * 1.3) * _comm, g.P.Primary);
                    }
                    g.Text($"UPLINK {C.Sim.BandwidthOut:0} KBPS", c.X, c.Bottom - g.U(12), 9, g.P.Primary, Align.Left, true);
                    break;
                }
            case 5: DrawInfo(g, r); break;
        }
    }

    void DrawSystem(Gfx g, Rect r)
    {
        var c = g.Panel(r, "SYSTEM OVERVIEW");
        double cx = c.X + g.U(20), cy = c.Center.Y;
        double maxR = c.Width - g.U(30);
        g.Disc(cx, cy, g.U(10), 0xFFFFC040, 0.9); g.Circle(cx, cy, g.U(14), 0xFFFFC040, 1, 0.3);
        for (int i = 0; i < Bodies.Length; i++)
        {
            var b = Bodies[i];
            double x = cx + b.Orbit * maxR;
            double ang = C.Sim.Time * 6.28 / b.Period;
            double py = cy + Math.Sin(ang) * Math.Min(c.Height * 0.4, g.U(34));
            g.Line(x, c.Y + g.U(2), x, c.Bottom - g.U(2), g.P.Primary, 1, 0.07);
            double pr = g.U(4 + b.Radius * 5);
            g.Disc(x, py, pr, b.Col);
            if (b.Ring) g.Circle(x, py, pr * 1.7, b.Col, 1, 0.4);
            if (i == _sel) g.GlowCircle(x, py, pr + g.U(5), g.P.Accent, 1.2, 1, g.U(3));
            g.Text(b.Name, x, c.Bottom - g.U(11), 8, i == _sel ? g.P.Accent : g.P.Text, Align.Center, i == _sel);
            g.AddHit("planet:" + i, new Rect(x - g.U(18), c.Y, g.U(36), c.Height));
        }
    }

    void DrawTimeline(Gfx g, Rect r)
    {
        var c = g.Panel(r, "MISSION TIMELINE");
        string[] names = { "SYSTEMS CHECK", "ORBIT INSERTION", "RELAY DEPLOY", "SURVEY PASS", "DEEP LINK", "RETURN BURN" };
        double t = C.Sim.Time;
        double lh = Math.Min(g.U(17), c.Height / names.Length);
        g.Line(c.X + g.U(5), c.Y + g.U(4), c.X + g.U(5), c.Y + lh * names.Length, g.P.Primary, 1, 0.4);
        for (int i = 0; i < names.Length; i++)
        {
            double y = c.Y + i * lh;
            bool done = t > (i + 1) * 40, cur = !done && t > i * 40;
            g.Disc(c.X + g.U(5), y + lh / 2, g.U(3.5), done ? g.P.Primary : cur ? g.P.Warning : g.P.Text, done || cur ? 1 : 0.3);
            g.Text($"T+{(i + 1) * 40:000}  {names[i]}", c.X + g.U(16), y + (lh - g.U(10)) / 2, 9, cur ? g.P.Warning : g.P.Text, Align.Left, cur, done ? 0.55 : 1);
        }
        if (_launch != Launch.Idle) g.Text("ARGO-1: " + _launch.ToString().ToUpper(), c.Right, c.Bottom - g.U(11), 9, g.P.Warning, Align.Right, true);
    }

    void DrawInfo(Gfx g, Rect r)
    {
        var b = Bodies[_sel];
        var c = g.Panel(r, "PLANETARY DATA // SIMULATED");
        double lh = g.U(13);
        string[] k = { "DESIGNATION", "RADIUS", "ORBIT PERIOD", "MOONS", "ATMOSPHERE", "SURFACE TEMP" };
        string[] v = { b.Name, $"{b.Radius * 7400:0} KM", $"{b.Period:0} D", b.Moons.ToString(), b.Atmo, $"{b.Temp} C" };
        int rows = Math.Min(6, (int)(c.Height / lh));
        for (int i = 0; i < rows; i++) g.KeyVal(c.X, c.Y + i * lh, c.Width, k[i], v[i], null, 9);
        if (_selSat >= 0 && c.Height > lh * 7)
            g.Text($"{_satName[_selSat]}  INCL {_satIncl[_selSat] * 57.3:0}°", c.X, c.Bottom - lh, 9, g.P.Accent, Align.Left, true);
    }

    public override void Tap(Point p, string? id)
    {
        if (id == null) return;
        if (id.StartsWith("planet:")) { _sel = int.Parse(id[7..]); _selSat = -1; Log($"FOCUS {Bodies[_sel].Name}"); C.Toast($"TARGET {Bodies[_sel].Name}"); C.Audio.Play("ping"); }
        else if (id.StartsWith("sat:")) { _selSat = int.Parse(id[4..]); Log($"SELECT {_satName[_selSat]}"); C.Toast($"{_satName[_selSat]} SELECTED"); C.Audio.Play("confirm"); }
    }

    public override bool Drag(Point p, Vector d, string? id)
    {
        if (id != "main:view") return false;
        _yaw += d.X * 0.008; _pitch = Clamp(_pitch - d.Y * 0.006, 0.3, 1.5);
        return true;
    }

    public override void Wheel(Point p, double delta) => _zoom = Clamp(_zoom * (1 + delta * 0.12), 0.5, 3);
}
