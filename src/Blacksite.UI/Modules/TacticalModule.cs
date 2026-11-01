using Avalonia;
using Blacksite.UI.Rendering;

namespace Blacksite.UI.Modules;

/// <summary>PRESET 10 – operations room. Geography, units and intel are entirely fictional.</summary>
public sealed class TacticalModule : ModuleBase
{
    sealed class Unit
    {
        public string Call = "", Type = "";
        public bool Friendly;
        public double X, Y, TX, TY, Speed;
        public bool Revealed; public double LastLog = -99;
        public double Heading;
    }
    sealed class Objective { public string Label = ""; public double X, Y; public bool Done; }

    readonly List<Unit> _units = new();
    readonly List<Objective> _objs = new();
    readonly List<(double x, double y, double r, int kind)> _terrain = new();
    readonly List<string> _timeline = new(), _intel = new();
    int _sel = 0, _layer;
    bool _radar = true, _placing;
    double _sweep;
    static readonly string[] Layers = { "TERRAIN", "THREAT", "COMMS", "GRID ONLY" };
    readonly double[] _chan = new double[6];
    static readonly string[] ChanNames = { "COMMAND NET", "AIR SUPPORT", "LOGISTICS", "RECON", "FIRE SUPPORT", "EMERGENCY" };
    bool[] _mon = { true, true, false, true, false, true };
    string _sector = "REDHAWK SECTOR (FICTIONAL)";

    protected override void OnInit()
    {
        var r = new Random(77);
        for (int i = 0; i < 26; i++) _terrain.Add((r.NextDouble(), r.NextDouble(), 0.03 + r.NextDouble() * 0.09, r.Next(3)));
        string[] fc = { "ALPHA-1", "ALPHA-2", "BRAVO-1", "BRAVO-2", "CHARLIE-6", "EAGLE-1", "GHOST-3", "TITAN-2" };
        string[] ft = { "INF", "INF", "ARMOR", "ARMOR", "ARMOR", "AIR", "RECON", "ARTY" };
        for (int i = 0; i < 8; i++) _units.Add(new Unit { Call = fc[i], Type = ft[i], Friendly = true, X = 0.12 + r.NextDouble() * 0.25, Y = 0.2 + r.NextDouble() * 0.6, Speed = ft[i] == "AIR" ? 0.05 : ft[i] == "ARMOR" ? 0.022 : 0.012 });
        for (int i = 0; i < 6; i++) _units.Add(new Unit { Call = $"HOSTILE-{i + 1}", Type = i % 3 == 0 ? "ARMOR" : "INF", Friendly = false, X = 0.62 + r.NextDouble() * 0.33, Y = 0.15 + r.NextDouble() * 0.7, Speed = 0.01 });
        foreach (var u in _units) { u.TX = u.X; u.TY = u.Y; }
        _objs.Add(new Objective { Label = "SECURE RIDGE HILL", X = 0.5, Y = 0.35 });
        _objs.Add(new Objective { Label = "ESTABLISH CHECKPOINT", X = 0.4, Y = 0.72 });
        Tl("OPERATION BLACK LANTERN COMMENCED"); Intel("INITIAL INTEL: HOSTILE ARMOUR MASSING EAST OF GRID H5 (FICTIONAL)");
    }

    void Tl(string s) { _timeline.Add($"{DateTime.Now:HH:mm}  {s}"); if (_timeline.Count > 12) _timeline.RemoveAt(0); }
    void Intel(string s) { _intel.Add(s); if (_intel.Count > 6) _intel.RemoveAt(0); }

    public override string[] Actions => new[] { "RADAR", "LAYER", "OBJECTIVE", "INTEL", "EVENT" };
    public override bool IsActive(string a) => a == "RADAR" && _radar || a == "OBJECTIVE" && _placing;
    public override string PrimaryAction => "EVENT";

    public override string? OnAction(string id)
    {
        switch (id)
        {
            case "RADAR": _radar = !_radar; return _radar ? "RADAR ACTIVE" : "RADAR OFF";
            case "LAYER": _layer = (_layer + 1) % Layers.Length; return "LAYER: " + Layers[_layer];
            case "OBJECTIVE": _placing = !_placing; return _placing ? "TAP MAP TO PLACE OBJECTIVE" : "OBJECTIVE MODE OFF";
            case "INTEL":
                {
                    string[] a = { "ENEMY LOGISTICS CONVOY", "ARTILLERY BATTERY", "COMMAND POST", "RECON PATROL", "AIR DEFENSE SITE" };
                    string[] b = { "MOVING NORTH", "STATIONARY", "UNDER OBSERVATION", "ESTIMATED 12 PAX", "LOW CONFIDENCE" };
                    string rep = $"INTEL: {a[Rng.Next(a.Length)]} — {b[Rng.Next(b.Length)]} @ {Grid(Rng.NextDouble(), Rng.NextDouble())}";
                    Intel(rep); Tl("INTEL REPORT RECEIVED"); C.Audio.Play("radio"); return "FICTIONAL INTEL REPORT RECEIVED";
                }
            case "EVENT":
                {
                    string[] ev = { "incoming_tx", "mission_orders", "unknown_obj", "emergency_comms" };
                    var e = ev[Rng.Next(ev.Length)]; C.Events.Trigger(e); Tl("EVENT: " + Core.Events.EventCatalog.Get(e)?.Title); return "MISSION EVENT TRIGGERED";
                }
        }
        return null;
    }

    static string Grid(double x, double y) => $"{(char)('A' + Math.Clamp((int)(x * 10), 0, 9))}{Math.Clamp((int)(y * 10), 0, 9) + 1}";

    public override void Update(double dt)
    {
        _sweep += dt * 70;
        foreach (var u in _units)
        {
            double dx = u.TX - u.X, dy = u.TY - u.Y, d = Math.Sqrt(dx * dx + dy * dy);
            if (d > 0.004) { double k = Math.Min(1, u.Speed * dt / d); u.X += dx * k; u.Y += dy * k; u.Heading = Math.Atan2(dy, dx); }
            else if (!u.Friendly && Rng.NextDouble() < dt * 0.15) { u.TX = Math.Clamp(u.X + (Rng.NextDouble() - 0.5) * 0.2, 0.5, 0.97); u.TY = Math.Clamp(u.Y + (Rng.NextDouble() - 0.5) * 0.2, 0.08, 0.92); }
        }
        // detection
        foreach (var h in _units.Where(u => !u.Friendly))
        {
            bool seen = _units.Any(f => f.Friendly && Hypot(f.X - h.X, f.Y - h.Y) < (f.Type == "RECON" ? 0.2 : f.Type == "AIR" ? 0.25 : 0.1));
            if (_radar) { double ang = Math.Atan2(h.Y - 0.5, h.X - 0.05) * 57.3; seen |= Math.Abs(((_sweep % 360) - ang + 540) % 360 - 180) < 25 && _radar; }
            if (seen && !h.Revealed && C.Sim.Time - h.LastLog > 25) { Tl($"CONTACT {h.Call} @ {Grid(h.X, h.Y)}"); h.LastLog = C.Sim.Time; }
            h.Revealed = seen || h.Revealed && Rng.NextDouble() > dt * 0.3;
        }
        foreach (var o in _objs.Where(o => !o.Done))
            if (_units.Any(u => u.Friendly && Hypot(u.X - o.X, u.Y - o.Y) < 0.04)) { o.Done = true; Tl("OBJECTIVE COMPLETE: " + o.Label); C.Toast("OBJECTIVE COMPLETE: " + o.Label); C.Audio.Confirm(); }
        for (int i = 0; i < _chan.Length; i++) _chan[i] = Math.Clamp(_chan[i] + (Rng.NextDouble() - 0.5) * dt * 3, 0, 1) * (_mon[i] ? 1 : 0.2);
    }

    public override void Draw(Gfx g, Rect area)
    {
        var lay = Layout.Adaptive(g, area, 5, 0.52);
        DrawMap(g, lay.Main);
        for (int i = 0; i < lay.Panels.Count; i++) DrawPanel(g, i, lay.Panels[i]);
    }

    Point P(Rect m, double x, double y) => new(m.X + x * m.Width, m.Y + y * m.Height);

    void DrawMap(Gfx g, Rect r)
    {
        var c = g.Panel(r, $"TACTICAL MAP // {_sector} // {Layers[_layer]}"); MapRect = c;
        using var clip = g.Clip(c);
        g.Fill(c, g.P.Background, 0.7);
        g.AddHit("map", c);
        if (_layer != 3)
            foreach (var t in _terrain)
            {
                var p = P(c, t.x, t.y); double rx = t.r * c.Width, ry = t.r * c.Height;
                uint col = t.kind == 0 ? g.P.Primary : t.kind == 1 ? g.P.Secondary : g.P.Text;
                g.Dc.DrawEllipse(g.Br(col, _layer == 0 ? 0.08 : 0.04), g.Pn(col, 1, _layer == 0 ? 0.3 : 0.12), p, rx, ry);
                g.Dc.DrawEllipse(null, g.Pn(col, 1, 0.15), p, rx * 0.6, ry * 0.6);
            }
        // grid and coordinates
        for (int i = 0; i <= 10; i++)
        {
            double x = c.X + c.Width * i / 10, y = c.Y + c.Height * i / 10;
            g.Line(x, c.Y, x, c.Bottom, g.P.Primary, 1, i % 5 == 0 ? 0.28 : 0.12); g.Line(c.X, y, c.Right, y, g.P.Primary, 1, i % 5 == 0 ? 0.28 : 0.12);
            if (i < 10) { g.Text(((char)('A' + i)).ToString(), x + c.Width / 20, c.Y + g.U(1), 8, g.P.Primary, Align.Center, true, 0.6); g.Text((i + 1).ToString(), c.X + g.U(2), y + c.Height / 20 - g.U(5), 8, g.P.Primary, Align.Left, true, 0.6); }
        }
        // radar sweep from the command post
        var cp = P(c, 0.05, 0.5);
        if (_radar)
        {
            double rr = c.Width * 0.9;
            for (int k = 0; k < 18; k++) { double a = (_sweep - k * 2.5) * Math.PI / 180; g.Line(cp.X, cp.Y, cp.X + Math.Cos(a) * rr, cp.Y + Math.Sin(a) * rr, g.P.Secondary, 1.5, 0.2 * (1 - k / 18.0)); }
            g.GlowLine(cp.X, cp.Y, cp.X + Math.Cos(_sweep * Math.PI / 180) * rr, cp.Y + Math.Sin(_sweep * Math.PI / 180) * rr, g.P.Secondary, 1.4, 0.8);
        }
        g.Fill(new Rect(cp.X - g.U(5), cp.Y - g.U(5), g.U(10), g.U(10)), g.P.Secondary); g.Text("CP", cp.X + g.U(8), cp.Y - g.U(5), 8, g.P.Secondary, Align.Left, true);
        // comms layer links
        if (_layer == 2) foreach (var u in _units.Where(u => u.Friendly)) { var p = P(c, u.X, u.Y); g.Line(cp.X, cp.Y, p.X, p.Y, g.P.Secondary, 1, 0.3); }
        // objectives
        foreach (var o in _objs)
        {
            var p = P(c, o.X, o.Y); uint col = o.Done ? g.P.Primary : g.P.Secondary;
            g.Circle(p.X, p.Y, g.U(11 + 2 * g.Pulse(1)), col, 1.2, 0.8, g.U(3)); g.Circle(p.X, p.Y, g.U(4), col, 1.2);
            g.Text(o.Label + (o.Done ? " ✓".Replace("✓", "OK") : ""), p.X + g.U(14), p.Y - g.U(5), 8, col, Align.Left, true);
        }
        // units
        for (int i = 0; i < _units.Count; i++)
        {
            var u = _units[i];
            if (!u.Friendly && !u.Revealed && _layer != 1) continue;
            var p = P(c, u.X, u.Y); bool sel = i == _sel; uint col = u.Friendly ? g.P.Primary : g.P.Warning;
            double s = g.U(u.Type == "ARMOR" ? 6 : 5);
            if (u.Friendly) { g.Fill(new Rect(p.X - s, p.Y - s * 0.7, s * 2, s * 1.4), col, 0.35); g.Frame(new Rect(p.X - s, p.Y - s * 0.7, s * 2, s * 1.4), col, 1.5); }
            else g.PolyFill(new[] { new Point(p.X, p.Y - s), new Point(p.X + s, p.Y), new Point(p.X, p.Y + s), new Point(p.X - s, p.Y) }, col, u.Revealed ? 0.35 : 0.1, col, 1.5, u.Revealed ? 1 : 0.4);
            g.Text(u.Type == "ARMOR" ? "X" : u.Type == "AIR" ? "A" : u.Type == "RECON" ? "R" : u.Type == "ARTY" ? "H" : "I", p.X, p.Y - g.U(5), 7, col, Align.Center, true);
            if (sel) g.GlowCircle(p.X, p.Y, g.U(12), g.P.Accent, 1.4);
            if (sel || g.Quality == Core.Configuration.GraphicsProfile.High) g.Text(u.Call, p.X + g.U(9), p.Y + g.U(3), 7.5, sel ? g.P.Accent : g.P.Text, Align.Left, sel, 0.8);
            if (sel && (Math.Abs(u.TX - u.X) > 0.005 || Math.Abs(u.TY - u.Y) > 0.005)) { var t = P(c, u.TX, u.TY); g.Line(p.X, p.Y, t.X, t.Y, g.P.Accent, 1, 0.6); g.Circle(t.X, t.Y, g.U(4), g.P.Accent, 1.2); }
            g.AddHit("unit:" + i, new Rect(p.X - g.U(13), p.Y - g.U(13), g.U(26), g.U(26)));
        }
        if (_placing) g.Text("OBJECTIVE MODE: TAP MAP TO PLACE", c.Center.X, c.Y + g.U(18), 10, g.P.Secondary, Align.Center, true, 0.5 + 0.5 * g.Pulse(2));
        g.Text("ALL LOCATIONS AND FORCES FICTIONAL", c.Right - g.U(4), c.Bottom - g.U(12), 7.5, g.P.Text, Align.Right, false, 0.5);
    }

    void DrawPanel(Gfx g, int i, Rect r)
    {
        switch (i)
        {
            case 0:
                {
                    var u = _units[Math.Clamp(_sel, 0, _units.Count - 1)];
                    var c = g.Panel(r, "COMMAND // " + u.Call);
                    double lh = g.U(14);
                    string[] k = { "TYPE", "GRID", "STATUS", "SPEED" }; string[] v = { u.Type, Grid(u.X, u.Y), Math.Abs(u.TX - u.X) > 0.005 ? "MOVING" : "HOLDING", $"{u.Speed * 300:0} KM/H" };
                    for (int n = 0; n < 4; n++) g.KeyVal(c.X, c.Y + n * lh, c.Width, k[n], v[n], null, 9);
                    if (u.Friendly)
                    {
                        double by = c.Y + 4 * lh + g.U(4), bh = Math.Min(g.U(g.Touch ? 30 : 22), c.Bottom - by), bw = (c.Width - g.U(6)) / 3;
                        if (bh > g.U(14))
                        {
                            g.Button("hold", new Rect(c.X, by, bw, bh), "HOLD", false, true, null, 8);
                            g.Button("rtb", new Rect(c.X + bw + g.U(3), by, bw, bh), "RTB", false, true, null, 8);
                            g.Button("adv", new Rect(c.X + 2 * (bw + g.U(3)), by, bw, bh), "ADVANCE", false, true, null, 8);
                        }
                    }
                    break;
                }
            case 1:
                {
                    var c = g.Panel(r, "RADAR");
                    double rad = Math.Min(c.Width, c.Height) / 2 - g.U(2);
                    var blips = new List<(double, double, uint, string?)>();
                    foreach (var u in _units) { if (!u.Friendly && !u.Revealed) continue; blips.Add((Math.Atan2(u.Y - 0.5, u.X - 0.05) * 57.3, Math.Min(1, Hypot(u.X - 0.05, u.Y - 0.5) / 1.0), u.Friendly ? g.P.Primary : g.P.Warning, null)); }
                    g.Radar(c.Center.X, c.Center.Y, rad, _radar ? _sweep % 360 : 0, blips, g.P.Secondary);
                    break;
                }
            case 2:
                {
                    var c = g.Panel(r, "OPERATIONAL TIMELINE");
                    double lh = g.U(12.5); int max = Math.Max(1, (int)(c.Height / lh));
                    for (int k = Math.Max(0, _timeline.Count - max), n = 0; k < _timeline.Count; k++, n++) g.Text(_timeline[k], c.X, c.Y + n * lh, 8, _timeline[k].Contains("CONTACT") ? g.P.Warning : g.P.Text);
                    break;
                }
            case 3:
                {
                    var c = g.Panel(r, "MISSION STATUS");
                    int done = _objs.Count(o => o.Done);
                    g.Bar(new Rect(c.X, c.Y, c.Width, g.U(24)), _objs.Count == 0 ? 0 : (double)done / _objs.Count, g.P.Primary, "OBJECTIVES", $"{done}/{_objs.Count}");
                    double lh = g.U(13); int max = Math.Max(1, (int)((c.Height - g.U(28)) / lh));
                    for (int k = 0; k < Math.Min(max, _objs.Count); k++) g.Text($"[{(_objs[k].Done ? "X" : " ")}] {_objs[k].Label}", c.X, c.Y + g.U(28) + k * lh, 8.5, _objs[k].Done ? g.P.Primary : g.P.Secondary, Align.Left, !_objs[k].Done);
                    var hint = _intel.LastOrDefault(); if (hint != null && c.Height > g.U(110)) g.Text(hint.Length > 52 ? hint[..52] + "…" : hint, c.X, c.Bottom - g.U(12), 7.5, g.P.Text, Align.Left, false, 0.7);
                    break;
                }
            case 4:
                {
                    var c = g.Panel(r, "COMMUNICATION CHANNELS");
                    double lh = Math.Min(g.U(g.Touch ? 26 : 19), c.Height / 6);
                    for (int k = 0; k < 6; k++)
                    {
                        var rc = new Rect(c.X, c.Y + k * lh, c.Width, lh);
                        g.StatusDot(rc.X + g.U(5), rc.Center.Y, _mon[k], 3);
                        g.Text(ChanNames[k], rc.X + g.U(14), rc.Y + (lh - g.U(9)) / 2, 8.5, g.P.Text, Align.Left, _mon[k]);
                        g.Bar(new Rect(rc.Right - g.U(60), rc.Y + lh * 0.3, g.U(60), lh * 0.4), _chan[k], g.P.Secondary);
                        g.AddHit("chan:" + k, rc, HitKind.Button);
                    }
                    break;
                }
        }
    }

    public override string? OnModuleButton(string id)
    {
        var u = _units[Math.Clamp(_sel, 0, _units.Count - 1)];
        switch (id)
        {
            case "hold": u.TX = u.X; u.TY = u.Y; Tl(u.Call + " HOLD"); return u.Call + " HOLDING";
            case "rtb": u.TX = 0.06; u.TY = 0.5; Tl(u.Call + " RETURN TO BASE"); return u.Call + " RETURNING TO BASE";
            case "adv": u.TX = Math.Min(0.9, u.X + 0.15); u.TY = u.Y; Tl(u.Call + " ADVANCING"); return u.Call + " ADVANCING";
        }
        if (id.StartsWith("chan:")) { int k = int.Parse(id[5..]); _mon[k] = !_mon[k]; Tl($"{ChanNames[k]} {(_mon[k] ? "MONITORED" : "MUTED")}"); return $"{ChanNames[k]} {(_mon[k] ? "MONITORED" : "MUTED")}"; }
        return null;
    }

    public override void Tap(Point p, string? id)
    {
        if (id == null) return;
        if (id.StartsWith("unit:")) { _sel = int.Parse(id[5..]); var u = _units[_sel]; C.Toast($"{u.Call} @ {Grid(u.X, u.Y)}"); C.Audio.Play("beep"); return; }
        if (id == "map")
        {
            var m = MapRect; if (m.Width < 1) return;
            double x = Math.Clamp((p.X - m.X) / m.Width, 0.02, 0.98), y = Math.Clamp((p.Y - m.Y) / m.Height, 0.02, 0.98);
            if (_placing) { _objs.Add(new Objective { Label = $"OBJECTIVE {_objs.Count + 1} @ {Grid(x, y)}", X = x, Y = y }); Tl("OBJECTIVE CREATED @ " + Grid(x, y)); _placing = false; C.Toast("OBJECTIVE CREATED @ " + Grid(x, y)); C.Audio.Confirm(); return; }
            var u = _units[Math.Clamp(_sel, 0, _units.Count - 1)];
            if (u.Friendly) { u.TX = x; u.TY = y; Tl($"{u.Call} MOVING TO {Grid(x, y)}"); C.Toast($"{u.Call} MOVING TO {Grid(x, y)}"); C.Audio.Play("radio"); }
        }
    }

    Rect MapRect;

    public override bool Drag(Point p, Vector d, string? id) => false;
}
