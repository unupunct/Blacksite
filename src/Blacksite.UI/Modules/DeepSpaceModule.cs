using Avalonia;
using Blacksite.Core.Simulation;
using Blacksite.UI.Rendering;

namespace Blacksite.UI.Modules;

/// <summary>PRESET 09 – starship bridge. Coordinates, objects and missions are invented.</summary>
public sealed class DeepSpaceModule : ModuleBase
{
    sealed record Obj(string Name, string Kind, double X, double Y, double Z, uint Col, string Info);
    static readonly Obj[] Objs =
    {
        new("KEPLER STATION", "STATION", 160, -20, 10, 0xFF70FFC0, "Orbital waystation. Fuel and repairs."),
        new("NYX-4", "GAS GIANT", 95, -75, 3, 0xFFB080FF, "Banded giant, 14 moons. Magnetosphere strong."),
        new("HELION PRIME", "STAR", 140, -90, -5, 0xFFFFD060, "Class G main-sequence. Habitable zone mapped."),
        new("THE VEIL", "NEBULA", 80, -20, 6, 0xFFFF70D0, "Ionised nebula. Sensor interference expected."),
        new("DRIFT-9", "DERELICT", 175, -60, -2, 0xFFFFA050, "Unpowered hull, origin unknown."),
        new("ORPHEUS GATE", "ANOMALY", 120, 20, 20, 0xFF50E0FF, "Periodic signal source of unknown origin."),
        new("TARN", "ICE MOON", 105, -55, 0, 0xFFA0D0FF, "Subsurface ocean candidate."),
    };

    int _dest, _sel;
    double _warp, _hyper = -1, _flash, _zoom = 1.0, _scan = 1;
    bool _throttleBoost;
    int _lifeMode = 1;
    readonly bool[] _done = new bool[4];
    static readonly string[] Missions = { "REACH KEPLER STATION", "SCAN THE VEIL", "INVESTIGATE DRIFT-9", "CONTACT ORPHEUS GATE" };
    readonly List<string> _comms = new();
    readonly (double x, double y, double z)[] _stars = new (double, double, double)[220];
    ShipState S => C.Sim.Ship;

    protected override void OnInit()
    {
        for (int i = 0; i < _stars.Length; i++) _stars[i] = (Rng.NextDouble() * 2 - 1, Rng.NextDouble() * 2 - 1, Rng.NextDouble());
        _dest = 0; S.Destination = Objs[0].Name; Comm("BRIDGE: SYSTEMS NOMINAL. COURSE SET FOR " + Objs[0].Name);
    }

    void Comm(string s) { _comms.Add($"{DateTime.Now:HH:mm}  {s}"); if (_comms.Count > 9) _comms.RemoveAt(0); }

    public override string[] Actions => new[] { "DEST", "WARP", "HYPER", "COMMS", "SCAN" };
    public override bool IsActive(string a) => a == "WARP" && S.Warp || a == "HYPER" && _hyper >= 0 || a == "SCAN" && _scan < 1;
    public override string PrimaryAction => "WARP";

    double Dist(Obj o) => Math.Sqrt(Math.Pow(o.X - S.X, 2) + Math.Pow(o.Y - S.Y, 2) + Math.Pow(o.Z - S.Z, 2));

    public override string? OnAction(string id)
    {
        switch (id)
        {
            case "DEST":
                _dest = (_dest + 1) % Objs.Length; _sel = _dest; S.Destination = Objs[_dest].Name; S.Warp = false;
                C.Events.Trigger("nav_recalc"); Comm("NAV: NEW DESTINATION " + S.Destination); return "DESTINATION: " + S.Destination;
            case "WARP":
                S.Warp = !S.Warp; C.Audio.Play("whoosh"); Comm(S.Warp ? "WARP DRIVE ENGAGED" : "WARP DRIVE DISENGAGED"); return S.Warp ? "WARP DRIVE ENGAGED" : "DROPPING OUT OF WARP";
            case "HYPER":
                if (_hyper >= 0) return null; _hyper = 0; S.Warp = true; C.Audio.Play("whoosh", Blacksite.Core.Audio.SoundCategory.Alarm); Comm("HYPERSPACE SEQUENCE INITIATED"); C.Audio.Say("Hyperspace sequence initiated."); return "HYPERSPACE SEQUENCE";
            case "COMMS":
                {
                    var o = Objs[_sel]; Comm($"COMMS: HAILING {o.Name}..."); C.Events.Trigger(o.Kind == "ANOMALY" ? "deep_signal" : "incoming_tx");
                    if (o.Name == "ORPHEUS GATE" && Dist(o) < 60) { Done(3); Comm("ORPHEUS GATE: REPEATING PATTERN ACKNOWLEDGED"); }
                    else Comm(o.Kind is "STATION" ? "KEPLER: DOCKING CLEARANCE GRANTED" : "NO RESPONSE — ENTITY NOT CRYPTOGRAPHIC");
                    return "COMMUNICATIONS OPEN: " + o.Name;
                }
            case "SCAN": _scan = 0; C.Audio.Play("ping"); return "LONG-RANGE SCAN RUNNING";
        }
        return null;
    }

    void Done(int k) { if (_done[k]) return; _done[k] = true; C.Toast("OBJECTIVE COMPLETE: " + Missions[k]); C.Audio.Confirm(); }

    public override void Update(double dt)
    {
        double target = S.Warp ? 1 : 0; _warp += (target - _warp) * Math.Min(1, dt * (S.Warp ? 0.9 : 2));
        if (_hyper >= 0)
        {
            _hyper += dt / 6; _flash = _hyper > 0.8 ? (_hyper - 0.8) / 0.2 : 0;
            if (_hyper >= 1)
            {
                _hyper = -1; _flash = 1;
                var o = Objs[_dest]; S.X = o.X - 3; S.Y = o.Y - 2; S.Z = o.Z; S.Warp = false; Comm("HYPERSPACE EXIT — " + o.Name + " IN RANGE");
            }
        }
        _flash = Math.Max(0, _flash - dt * 1.2);
        // travel toward destination under warp/impulse
        var d = Objs[_dest]; double dist = Dist(d);
        double spd = (S.Warp ? 9.6 : 0.12) * (_throttleBoost ? 1.4 : 1) * dt;
        if (dist > 0.5)
        {
            double k = Math.Min(1, spd / dist); S.X += (d.X - S.X) * k; S.Y += (d.Y - S.Y) * k; S.Z += (d.Z - S.Z) * k;
        }
        else if (S.Warp) { S.Warp = false; Comm("ARRIVED AT " + d.Name); C.Toast("ARRIVED: " + d.Name); if (_dest == 0) Done(0); }
        if (_scan < 1) { _scan = Math.Min(1, _scan + dt / 4); if (_scan >= 1) { var o = Objs[_sel]; Comm("SCAN COMPLETE: " + o.Name + " — " + o.Info); if (o.Name == "THE VEIL" && Dist(o) < 40) Done(1); if (o.Name == "DRIFT-9" && Dist(o) < 40) Done(2); } }
        // life support drift
        S.Oxygen = Math.Clamp(S.Oxygen + (_lifeMode - 1) * 0.0004 * dt * 10 + (S.Oxygen < 0.98 ? 0.0002 : 0), 0.6, 1);
    }

    public override void Draw(Gfx g, Rect area)
    {
        g.Nebula(area, g.P.Primary, g.P.Secondary, 7, 0.6);
        DrawStarfield(g, area);
        var lay = Layout.Adaptive(g, area, 6, 0.46);
        DrawNav(g, lay.Main);
        for (int i = 0; i < lay.Panels.Count; i++) DrawPanel(g, i, lay.Panels[i]);
        if (_hyper >= 0) DrawHyper(g, area);
        if (_flash > 0) g.Fill(area, 0xFFFFFFFF, Math.Min(0.9, _flash));
    }

    void DrawStarfield(Gfx g, Rect a)
    {
        int n = g.Quality == Core.Configuration.GraphicsProfile.Low ? 60 : g.Quality == Core.Configuration.GraphicsProfile.Balanced ? 140 : _stars.Length;
        double cx = a.Center.X, cy = a.Center.Y, max = Math.Max(a.Width, a.Height);
        for (int i = 0; i < n; i++)
        {
            var s = _stars[i];
            double z = (s.z + (g.Reduced ? 0 : g.Time * (0.02 + 0.45 * _warp))) % 1.0;
            double f = z * z; double x = cx + s.x * max * 0.6 * (0.1 + f * 1.6), y = cy + s.y * max * 0.6 * (0.1 + f * 1.6);
            double len = _warp * f * g.U(40);
            if (len > 1) g.Line(x, y, x - (x - cx) / max * len * 3, y - (y - cy) / max * len * 3, i % 4 == 0 ? g.P.Secondary : g.P.Primary, 1 + f, 0.2 + f * 0.7);
            else g.Disc(x, y, g.U(0.4 + f * 1.4), 0xFFFFFFFF, 0.15 + f * 0.7);
        }
    }

    void DrawHyper(Gfx g, Rect a)
    {
        double cx = a.Center.X, cy = a.Center.Y, t = _hyper;
        for (int i = 0; i < 14; i++)
        {
            double z = (i / 14.0 + g.Time * 0.8) % 1.0, rr = Math.Pow(z, 2.2) * Math.Max(a.Width, a.Height) * 0.8;
            g.Circle(cx, cy, rr, i % 2 == 0 ? g.P.Primary : g.P.Secondary, 1.5 + z * 4, 0.7 * Math.Sin(Math.PI * Math.Min(1, t * 3)) * (1 - z * 0.4));
        }
        g.Text("HYPERSPACE", cx, a.Y + a.Height * 0.12, 22, g.P.Accent, Align.Center, true, 0.5 + 0.5 * g.Pulse(3));
        g.Text($"JUMP {t * 100:0}%", cx, a.Y + a.Height * 0.12 + g.U(30), 10, g.P.Text, Align.Center);
    }

    void DrawNav(Gfx g, Rect r)
    {
        var c = g.Panel(r, "NAVIGATION // STAR CHART");
        double cx = c.Center.X, cy = c.Center.Y, sc = Math.Min(c.Width, c.Height) / 2 / 80.0 * _zoom;
        using var clip = g.Clip(c);
        g.AddHit("nav", c);
        g.HoloBase(c, cx, c.Bottom - g.U(8), Math.Min(c.Width, c.Height) * 0.8);
        for (int i = 1; i <= 4; i++) g.Circle(cx, cy, 20 * i * sc, g.P.Primary, 1, 0.12, g.U(3));
        g.Line(c.X, cy, c.Right, cy, g.P.Primary, 1, 0.1); g.Line(cx, c.Y, cx, c.Bottom, g.P.Primary, 1, 0.1);
        for (int i = 0; i < Objs.Length; i++)
        {
            var o = Objs[i]; double x = cx + (o.X - S.X) * sc, y = cy + (o.Y - S.Y) * sc;
            bool sel = i == _sel, dest = i == _dest;
            double rad = o.Kind switch { "STAR" => 8, "GAS GIANT" => 7, "NEBULA" => 12, "STATION" => 4, _ => 4.5 };
            if (o.Kind == "NEBULA") g.Disc(x, y, g.U(rad * 2), o.Col, 0.12);
            g.Disc(x, y, g.U(rad), o.Col, 0.9);
            if (o.Kind == "GAS GIANT") g.Circle(x, y, g.U(rad * 1.7), o.Col, 1, 0.5);
            if (dest) { g.Line(cx, cy, x, y, g.P.Accent, 1.2, 0.5); g.GlowCircle(x, y, g.U(rad + 6), g.P.Accent, 1.4, 1, g.U(3)); }
            if (sel) g.Frame(new Rect(x - g.U(rad + 9), y - g.U(rad + 9), g.U(rad * 2 + 18), g.U(rad * 2 + 18)), g.P.Text, 1, 0.6);
            g.Text(o.Name, x + g.U(rad + 5), y - g.U(5), 8, sel || dest ? g.P.Accent : g.P.Text, Align.Left, sel, sel || dest ? 1 : 0.6);
            if (sel) g.Text($"{Dist(o):0.0} LY  {o.Kind}", x + g.U(rad + 5), y + g.U(5), 7.5, o.Col, Align.Left);
            g.AddHit("obj:" + i, new Rect(x - g.U(16), y - g.U(16), g.U(32), g.U(32)));
        }
        // ship
        double ang = Math.Atan2(Objs[_dest].Y - S.Y, Objs[_dest].X - S.X);
        g.Rotated(cx, cy, ang * 57.3, () => { g.PolyFill(new[] { new Point(cx + g.U(10), cy), new Point(cx - g.U(7), cy - g.U(6)), new Point(cx - g.U(4), cy), new Point(cx - g.U(7), cy + g.U(6)) }, g.P.Accent, 0.9, g.P.Accent, 1); });
        g.Text("YOUR SHIP", cx + g.U(10), cy + g.U(10), 8, g.P.Accent, Align.Left, true);
        double bx = c.X + g.U(4), by = c.Y + g.U(2);
        g.Text($"POS {S.X:0.0} / {S.Y:0.0} / {S.Z:0.0} LY (FICTIONAL)", bx, by, 8.5, g.P.Text, Align.Left, false, 0.8);
        g.Text($"DEST {S.Destination}  {Dist(Objs[_dest]):0.0} LY  ETA {(S.Warp ? Dist(Objs[_dest]) / 9.6 : Dist(Objs[_dest]) / 0.12):0}S", bx, by + g.U(12), 8.5, g.P.Primary, Align.Left, true);
        g.Text(S.Warp ? $"WARP {S.Speed:0.0}C" : $"IMPULSE {S.Speed:0.00}C", c.Right - g.U(4), by, 10, S.Warp ? g.P.Accent : g.P.Text, Align.Right, true);
        if (_scan < 1) { g.Circle(cx, cy, _scan * Math.Max(c.Width, c.Height) * 0.6, g.P.Accent, 2, 1 - _scan); }
        g.HoloShimmer(c);
    }

    void DrawPanel(Gfx g, int i, Rect r)
    {
        switch (i)
        {
            case 0:
                {
                    var c = g.Panel(r, "ENGINE STATUS");
                    double gr = Math.Min(c.Height, c.Width * 0.5) / 2 - g.U(2);
                    g.Gauge(c.X + gr + g.U(2), c.Center.Y, gr, S.Engine * 100, 100, "WARP CORE", "%");
                    double tx = c.X + gr * 2 + g.U(14);
                    g.KeyVal(tx, c.Y, c.Right - tx, "FUEL", $"{S.Fuel * 100:0.0}%", null, 9);
                    g.KeyVal(tx, c.Y + g.U(14), c.Right - tx, "SPEED", $"{S.Speed:0.00} C", null, 9);
                    g.Button("boost", new Rect(tx, c.Y + g.U(32), c.Right - tx, g.U(g.Touch ? 30 : 22)), _throttleBoost ? "BOOST ON" : "BOOST OFF", _throttleBoost, true, null, 8.5);
                    break;
                }
            case 1:
                {
                    var c = g.Panel(r, "LIFE SUPPORT");
                    double lh = Math.Min(g.U(24), (c.Height - g.U(26)) / 3);
                    g.Bar(new Rect(c.X, c.Y, c.Width, lh), S.Oxygen, g.P.Primary, "OXYGEN", $"{S.Oxygen * 100:0.0}%");
                    g.Bar(new Rect(c.X, c.Y + lh + g.U(2), c.Width, lh), 0.35 + 0.05 * Math.Sin(C.Sim.Time * 0.2), g.P.Secondary, "CO2 SCRUBBER", "OK");
                    g.Bar(new Rect(c.X, c.Y + (lh + g.U(2)) * 2, c.Width, lh), 0.7, g.P.Accent, "CABIN PRESSURE", "101 KPA");
                    g.Button("life", new Rect(c.X, c.Bottom - g.U(g.Touch ? 28 : 22), c.Width, g.U(g.Touch ? 28 : 22)), "MODE: " + new[] { "ECO", "NORMAL", "BOOST" }[_lifeMode], false, true, null, 8.5);
                    break;
                }
            case 2:
                {
                    var c = g.Panel(r, "HULL INTEGRITY");
                    double cx = c.Center.X, cy = c.Center.Y, w = Math.Min(c.Width * 0.8, c.Height * 2.2), h = w * 0.3;
                    var hull = new[] { new Point(cx + w / 2, cy), new Point(cx + w * 0.2, cy - h * 0.6), new Point(cx - w * 0.3, cy - h * 0.5), new Point(cx - w / 2, cy - h * 0.25), new Point(cx - w / 2, cy + h * 0.25), new Point(cx - w * 0.3, cy + h * 0.5), new Point(cx + w * 0.2, cy + h * 0.6) };
                    g.PolyFill(hull, g.P.Primary, 0.08 + 0.1 * S.Hull, S.Hull < 0.5 ? g.P.Warning : g.P.Primary, 1.5);
                    for (int k = 0; k < 6; k++) g.Line(cx - w * 0.4 + k * w * 0.14, cy - h * 0.4, cx - w * 0.4 + k * w * 0.14, cy + h * 0.4, g.P.Primary, 1, 0.2);
                    g.Text($"{S.Hull * 100:0.0}%", cx, cy - g.U(8), 16, S.Hull < 0.5 ? g.P.Warning : g.P.Text, Align.Center, true);
                    break;
                }
            case 3:
                {
                    var c = g.Panel(r, "MISSION OBJECTIVES");
                    double lh = Math.Min(g.U(18), c.Height / 4);
                    for (int k = 0; k < 4; k++)
                    {
                        var box = new Rect(c.X, c.Y + k * lh + g.U(2), g.U(10), g.U(10));
                        g.Frame(box, g.P.Primary, 1); if (_done[k]) g.Fill(box.Deflate(g.U(2)), g.P.Primary);
                        g.Text(Missions[k], c.X + g.U(16), c.Y + k * lh, 9, _done[k] ? g.P.Primary : g.P.Text, Align.Left, !_done[k], _done[k] ? 0.6 : 1);
                    }
                    break;
                }
            case 4:
                {
                    var c = g.Panel(r, "COMMUNICATIONS");
                    double lh = g.U(12.5); int max = Math.Max(1, (int)(c.Height / lh));
                    for (int k = Math.Max(0, _comms.Count - max), n = 0; k < _comms.Count; k++, n++) g.Text(_comms[k], c.X, c.Y + n * lh, 8, g.P.Text, Align.Left, false, 0.9);
                    break;
                }
            case 5:
                {
                    var c = g.Panel(r, "EXPLORATION RADAR");
                    double rad = Math.Min(c.Width, c.Height) / 2 - g.U(2);
                    var blips = new List<(double, double, uint, string?)>();
                    for (int k = 0; k < Objs.Length; k++) { var o = Objs[k]; double dx = o.X - S.X, dy = o.Y - S.Y; blips.Add((Math.Atan2(dy, dx) * 57.3, Math.Min(1, Math.Sqrt(dx * dx + dy * dy) / 100), k == _dest ? g.P.Accent : o.Col, k == _sel ? o.Name : null)); }
                    g.Radar(c.Center.X, c.Center.Y, rad, g.Time * 70 % 360, blips);
                    break;
                }
        }
    }

    public override string? OnModuleButton(string id)
    {
        if (id == "boost") { _throttleBoost = !_throttleBoost; return _throttleBoost ? "ENGINE BOOST ON" : "ENGINE BOOST OFF"; }
        if (id == "life") { _lifeMode = (_lifeMode + 1) % 3; return "LIFE SUPPORT: " + new[] { "ECO", "NORMAL", "BOOST" }[_lifeMode]; }
        return null;
    }

    public override void Tap(Point p, string? id)
    {
        if (id != null && id.StartsWith("obj:")) { _sel = int.Parse(id[4..]); var o = Objs[_sel]; C.Toast($"{o.Name} — {o.Info}"); C.Audio.Play("ping"); }
    }

    public override void DoubleTap(Point p, string? id)
    {
        if (id != null && id.StartsWith("obj:")) { _dest = int.Parse(id[4..]); _sel = _dest; S.Destination = Objs[_dest].Name; Comm("NAV: DESTINATION " + S.Destination); C.Toast("DESTINATION: " + S.Destination); }
    }

    public override void Wheel(Point p, double delta) => _zoom = Clamp(_zoom * (1 + delta * 0.12), 0.5, 4);
}
