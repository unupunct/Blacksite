using Avalonia;
using Blacksite.Core.Audio;
using Blacksite.Core.Simulation.Domain;
using Blacksite.UI.Rendering;

namespace Blacksite.UI.Modules;

/// <summary>PRESET 11 – ORBITAL DEFENSE COMMAND. Fictional aerospace surveillance picture.</summary>
public sealed class OrbitalModule : DeckModule
{
    readonly OrbitalSim _s = new();
    int _view;                       // 0 earth-centred globe, 1 orbital 3D
    double _rotLon = 20, _tilt = 24, _zoom = 1, _sweep;
    bool _lTracks = true, _lCov = true, _lLinks = true, _lDebris = true, _lLabels = true;
    int _filter, _tblFirst, _station;
    Rect _mapR, _tblR;
    double _alertT = 20, _pulseT;
    readonly Dictionary<int, double> _acq = new();
    static readonly string[] Filters = { "ALL", "ACTIVE", "RESEARCH", "DEBRIS", "UNKNOWN", "GROUND" };

    public override string[] ModuleActions => new[] { "ACQUIRE", "TRACK", "PRIORITY", "HANDOVER", "LOSS", "RESTORE", "SCENARIO", "ALERT" };

    protected override void OnInit() { _s.Alert += (m, w) => { }; }

    protected override void BuildPanels()
    {
        Def("map", "A  MAIN ORBITAL MAP", Zone.Main, 1, DrawMap);
        Def("tele", "B  TRACKING TELEMETRY", Zone.Left, 1.25, DrawTelemetry);
        Def("radar", "C  RADAR TRACKING", Zone.Left, 1.35, DrawRadar);
        Def("elem", "ORBITAL ELEMENTS", Zone.Left, 0.9, DrawElements);
        Def("traffic", "D  ORBITAL TRAFFIC", Zone.Right, 1.5, DrawTraffic);
        Def("comms", "E  COMMUNICATION NETWORK", Zone.Right, 1.3, DrawComms);
        Def("graphs", "LINK PERFORMANCE", Zone.Right, 0.9, DrawGraphs);
        Def("mission", "F  MISSION STATUS", Zone.Bottom, 1.1, DrawMission);
        Def("sensors", "SENSOR STATUS", Zone.Bottom, 1, DrawSensors);
        Def("alerts", "ALERTS & TIMELINE", Zone.Bottom, 1.3, DrawAlerts);
    }

    OrbObject Sel => _s.Objects[_s.Selected];

    // ---------------------------------------------------------------- actions
    protected override string? DoAction(string id)
    {
        var sel = _s.Selected; string? m = null;
        switch (id)
        {
            case "ACQUIRE": m = _s.Acquire(sel); _acq[sel] = 0; C.Audio.Play("handshake", SoundCategory.Event); break;
            case "TRACK": m = _s.Track(sel); C.Audio.Play("radarpulse", SoundCategory.Event); break;
            case "PRIORITY": m = _s.CyclePriority(sel); C.Audio.Play("deepbeep"); break;
            case "HANDOVER": m = _s.Handover(sel); C.Audio.Play(m.StartsWith("HANDOVER REFUSED") ? "error" : "handshake", SoundCategory.Event); break;
            case "LOSS": m = _s.Loss(sel); C.Audio.Play("triple", SoundCategory.Alarm); break;
            case "RESTORE": m = _s.Restore(sel); C.Audio.Play("confirm"); break;
            case "SCENARIO": _s.Reset(_s.Scenario + 1); _tblFirst = 0; _station = Math.Max(0, Sel.Station); m = $"SCENARIO {_s.Scenario + 1} LOADED"; C.Audio.Play("chime", SoundCategory.Event); break;
            case "ALERT":
                {
                    var deb = _s.Objects.Where(o => o.Type == OrbType.Debris).OrderBy(o => Math.Abs(o.Alt - Sel.Alt)).First();
                    _s.Conjunction = $"{Sel.Name} / {deb.Id}  MISS DIST {5 + Rng.Next(30)} KM"; _s.Alerts++; C.Events.Trigger("orbital_alert"); m = "CONJUNCTION ALERT RAISED"; break;
                }
        }
        return m;
    }

    protected override bool IsActionActive(string a) => a == "TRACK" && Sel.Tracked || a == "LOSS" && Sel.SignalLost;

    protected override void Tick(double dt, double anim)
    {
        _s.Step(dt);
        _sweep = (_sweep + anim * 90) % 360;
        _pulseT += anim; if (_pulseT > 4) { _pulseT = 0; C.Audio.Play("radarpulse", SoundCategory.Ambient, 0.22f); }
        _alertT -= dt * Cfg.AlertFrequency;
        if (_alertT <= 0 && Cfg.AlertFrequency > 0.05)
        {
            _alertT = 30 + Rng.NextDouble() * 40;
            if (Rng.Next(3) == 0) DoAction("ALERT"); else { var u = _s.Objects.Where(o => o.Type == OrbType.Unknown).FirstOrDefault(); if (u != null) { C.Toast($"NEW RETURN: {u.Id} UNCLASSIFIED", true); C.Audio.Play("deepbeep", SoundCategory.Event); } }
        }
        foreach (var k in _acq.Keys.ToArray()) { _acq[k] += anim; if (_acq[k] > 2.5) _acq.Remove(k); }
        if (_s.Alerts > 0 && _s.Conjunction != "" && (int)(_s.Time * 0.5) % 7 == 0 && false) { }
    }

    // ---------------------------------------------------------------- projections
    Point Orb3(double cx, double cy, double rad, OrbObject o, out double depth)
    {
        double u = o.Phase, i = o.Incl, raan = o.Raan - OrbitalSim.EarthRate * _s.Time * OrbitalSim.TimeScale * 0;
        double r = rad * (1 + o.Alt / 2200.0);
        double x = (Math.Cos(raan) * Math.Cos(u) - Math.Sin(raan) * Math.Sin(u) * Math.Cos(i)) * r, y = (Math.Sin(raan) * Math.Cos(u) + Math.Cos(raan) * Math.Sin(u) * Math.Cos(i)) * r, z = Math.Sin(u) * Math.Sin(i) * r;
        double yaw = _rotLon * Math.PI / 180, pit = _tilt * Math.PI / 180;
        double x1 = x * Math.Cos(yaw) - y * Math.Sin(yaw), y1 = x * Math.Sin(yaw) + y * Math.Cos(yaw);
        double yy = z * Math.Cos(pit) - y1 * Math.Sin(pit), dd = z * Math.Sin(pit) + y1 * Math.Cos(pit);
        depth = dd; return new Point(cx + x1, cy - yy);
    }

    Point Ortho(double cx, double cy, double rad, double lat, double lon, out bool vis)
    {
        var (p, z) = World.Ortho(cx, cy, rad, lon, lat, _rotLon, _tilt); vis = z > 0.02; return p;
    }

    // ---------------------------------------------------------------- A: main map
    void DrawMap(Gfx g, Rect r)
    {
        _mapR = r; g.AddHit("or:map", r);
        double top = g.U(22);
        // layer buttons
        string[] ids = { "or:view", "or:l:tracks", "or:l:cov", "or:l:links", "or:l:debris", "or:l:labels" };
        string[] lab = { _view == 0 ? "VIEW: EARTH" : "VIEW: ORBIT", "TRACKS", "COVERAGE", "LINKS", "DEBRIS", "LABELS" };
        bool[] on = { _view == 1, _lTracks, _lCov, _lLinks, _lDebris, _lLabels };
        double bw = Math.Min(g.U(86), (r.Width - g.U(5) * 5) / 6);
        for (int i = 0; i < 6; i++) g.Button(ids[i], new Rect(r.X + i * (bw + g.U(5)), r.Y, bw, top - g.U(3)), lab[i], on[i], true, null, bw < g.U(60) ? 6.5 : 8);
        var m = new Rect(r.X, r.Y + top, r.Width, r.Height - top);
        double cx = m.Center.X, cy = m.Center.Y, rad = Math.Min(m.Width, m.Height) * 0.44 * _zoom * (_view == 1 ? 0.55 : 1);
        using var clip = g.Clip(m);
        if (_view == 0) DrawGlobeView(g, m, cx, cy, rad); else DrawOrbitView(g, m, cx, cy, rad);
        // overlays: coordinates and selected object
        var sel = Sel;
        g.Text($"LAT {sel.Lat:+00.00;-00.00}  LON {sel.Lon:+000.00;-000.00}  ALT {sel.Alt:0} KM", m.X + g.U(4), m.Bottom - g.U(13), 8.5, g.P.Primary, Align.Left, true, 0.9);
        g.Text($"ROT {_rotLon % 360:0}°  TILT {_tilt:0}°  ZOOM {_zoom:0.0}X  SIM {OrbitalSim.TimeScale:0}X", m.Right - g.U(4), m.Bottom - g.U(13), 8, g.P.Text, Align.Right, false, 0.6);
        g.Text("SIMULATED — NOT REAL TRACKING DATA", m.X + g.U(4), m.Y + g.U(2), 7.5, g.P.Warning, Align.Left, false, 0.7);
        if (!string.IsNullOrEmpty(_s.Conjunction))
        {
            var br = new Rect(m.Center.X - g.U(160), m.Y + g.U(14), g.U(320), g.U(18)); g.Fill(br, 0xFF000000, 0.6); g.Frame(br, g.P.Warning, 1.2, 0.5 + 0.5 * g.Pulse(2));
            g.TextIn(br, "CONJUNCTION: " + _s.Conjunction, 8, g.P.Warning, Align.Center, true);
        }
    }

    void DrawGlobeView(Gfx g, Rect m, double cx, double cy, double rad)
    {
        g.Globe(cx, cy, rad, _rotLon, _tilt, 0xFF061A30, g.P.Secondary, true);
        var sel = Sel;
        // inclination lines at +/- incl of the selected object
        foreach (int sgn in new[] { -1, 1 })
        {
            var pts = new List<Point>();
            for (int lon = -180; lon <= 180; lon += 4) { var p = Ortho(cx, cy, rad, sgn * sel.Incl * 57.2958, lon, out bool v); if (v) pts.Add(p); else { if (pts.Count > 1) g.Poly(pts, g.P.Warning, 1, 0.35); pts.Clear(); } }
            if (pts.Count > 1) g.Poly(pts, g.P.Warning, 1, 0.35);
        }
        // selected object's orbit (full-orbit ground track at current epoch)
        if (_lTracks)
        {
            var pts = new List<Point>(); double T = sel.Period / OrbitalSim.TimeScale;
            for (int k = 0; k <= 72; k++) { var (la, lo) = _s.Predict(sel, k * T / 72 - T * 0.15); var p = Ortho(cx, cy, rad, la, lo, out bool v); if (v) pts.Add(p); else { if (pts.Count > 1) g.Poly(pts, g.P.Accent, 1.4, 0.9); pts.Clear(); } }
            if (pts.Count > 1) g.Poly(pts, g.P.Accent, 1.4, 0.9);
            foreach (var o in _s.Objects.Where(o => o != sel && (o.Tracked || o.Type == OrbType.Active) && o.Type != OrbType.Debris).Take(8))
            {
                var tp = new List<Point>(); double T2 = o.Period / OrbitalSim.TimeScale;
                for (int k = 0; k <= 40; k++) { var (la, lo) = _s.Predict(o, k * T2 / 40 - T2 * 0.1); var p = Ortho(cx, cy, rad, la, lo, out bool v); if (v) tp.Add(p); else { if (tp.Count > 1) g.Poly(tp, g.P.Primary, 1, 0.22); tp.Clear(); } }
                if (tp.Count > 1) g.Poly(tp, g.P.Primary, 1, 0.22);
            }
            foreach (var o in _s.Objects.Where(o => o.Trail.Count > 2 && (o.Tracked || o == sel)))
            {
                var tp = new List<Point>(); foreach (var (la, lo) in o.Trail) { var p = Ortho(cx, cy, rad, la, lo, out bool v); if (v) tp.Add(p); else { if (tp.Count > 1) g.Poly(tp, g.P.Warning, 1.6, 0.8); tp.Clear(); } }
                if (tp.Count > 1) g.Poly(tp, g.P.Warning, 1.6, 0.8);
            }
        }
        // ground stations + links
        var stPts = new Point?[_s.Stations.Count];
        for (int i = 0; i < _s.Stations.Count; i++)
        {
            var st = _s.Stations[i]; var p = Ortho(cx, cy, rad, st.Lat, st.Lon, out bool v); if (!v) continue; stPts[i] = p;
            uint col = st.Online ? 0xFF30FF70 : 0xFFFF3030;
            g.Fill(new Rect(p.X - g.U(3), p.Y - g.U(3), g.U(6), g.U(6)), col, 0.9); g.Frame(new Rect(p.X - g.U(5), p.Y - g.U(5), g.U(10), g.U(10)), col, 1, 0.6);
            if (_lLabels) g.Text(st.Id, p.X + g.U(7), p.Y - g.U(5), 7, col, Align.Left, false, 0.8);
        }
        foreach (var o in _s.Objects)
        {
            if (o.Type == OrbType.Debris && !_lDebris) continue;
            var p = Ortho(cx, cy, rad, o.Lat, o.Lon, out bool v); if (!v) continue;
            bool isSel = o == sel;
            uint col = o.Type switch { OrbType.Active => g.P.Primary, OrbType.Research => 0xFF70FFC0, OrbType.Debris => 0xFF8A98A8, OrbType.Unknown => g.P.Warning, _ => g.P.Accent };
            if (o.SignalLost) col = 0xFFFF3030;
            if (_lLinks && o.Station >= 0 && !o.SignalLost && stPts[o.Station] is { } sp && o.Type != OrbType.Debris && o.Type != OrbType.Unknown)
                g.Line(sp.X, sp.Y, p.X, p.Y, o.Snr > 20 ? 0xFF30FF70 : g.P.Warning, isSel || o.Tracked ? 1.4 : 0.8, isSel ? 0.9 : 0.35);
            if (_lCov && (isSel || o.Tracked) && o.Type != OrbType.Debris)
            {
                double ang = Math.Acos(OrbitalSim.Re / (OrbitalSim.Re + o.Alt)) * 180 / Math.PI * 0.8; var cp = new List<Point>();
                for (int b = 0; b <= 360; b += 12)
                {
                    double br = b * Math.PI / 180, d = ang * Math.PI / 180, la1 = o.Lat * Math.PI / 180, lo1 = o.Lon * Math.PI / 180;
                    double la2 = Math.Asin(Math.Sin(la1) * Math.Cos(d) + Math.Cos(la1) * Math.Sin(d) * Math.Cos(br));
                    double lo2 = lo1 + Math.Atan2(Math.Sin(br) * Math.Sin(d) * Math.Cos(la1), Math.Cos(d) - Math.Sin(la1) * Math.Sin(la2));
                    var q = Ortho(cx, cy, rad, la2 * 57.2958, lo2 * 57.2958, out bool qv); if (qv) cp.Add(q);
                }
                if (cp.Count > 2) { g.Poly(cp, col, 1, 0.5); g.PolyFill(cp, col, 0.05); }
            }
            double sz = o.Type == OrbType.Debris ? 1.4 : isSel ? 4 : 2.8;
            g.Disc(p.X, p.Y, g.U(sz), col, o.Type == OrbType.Debris ? 0.6 : 1);
            if (isSel) { g.GlowCircle(p.X, p.Y, g.U(10), g.P.Accent, 1.3); g.Text($"{o.Name}  {o.Id}", p.X + g.U(13), p.Y - g.U(12), 8.5, g.P.Accent, Align.Left, true); g.Text($"{o.Velocity:0.00} KM/S", p.X + g.U(13), p.Y - g.U(2), 8, g.P.Text, Align.Left); }
            else if (_lLabels && (o.Tracked || o.Type == OrbType.Unknown || o.Type == OrbType.Station)) g.Text(o.Name, p.X + g.U(6), p.Y - g.U(4), 7, col, Align.Left, false, 0.8);
            if (_acq.TryGetValue(_s.Objects.IndexOf(o), out var at)) g.Circle(p.X, p.Y, g.U(40 * (1 - at / 2.5)), g.P.Accent, 1.5, 1 - at / 2.5);
            g.AddHit("obj:" + _s.Objects.IndexOf(o), new Rect(p.X - g.U(8), p.Y - g.U(8), g.U(16), g.U(16)));
        }
    }

    void DrawOrbitView(Gfx g, Rect m, double cx, double cy, double rad)
    {
        g.Globe(cx, cy, rad, _rotLon + _s.Time * 0.5, _tilt, 0xFF061A30, g.P.Secondary, false);
        var sel = Sel;
        for (int ring = 1; ring <= 3; ring++) g.Circle(cx, cy, rad * (1 + ring * 0.4), g.P.Primary, 1, 0.07, g.U(3));
        void Orbit(OrbObject o, uint col, double w, double a)
        {
            var back = new List<Point>(); var front = new List<Point>();
            double save = o.Phase;
            for (int k = 0; k <= 90; k++)
            {
                o.Phase = k * Math.PI * 2 / 90; var p = Orb3(cx, cy, rad, o, out double d);
                if (d >= 0 || Math.Sqrt((p.X - cx) * (p.X - cx) + (p.Y - cy) * (p.Y - cy)) > rad) front.Add(p); else { if (front.Count > 1) g.Poly(front, col, w, a); front.Clear(); }
            }
            if (front.Count > 1) g.Poly(front, col, w, a);
            o.Phase = save;
        }
        foreach (var o in _s.Objects.Where(o => o != sel && (o.Tracked || o.Type == OrbType.Active || o.Type == OrbType.Station)).Take(14)) Orbit(o, g.P.Primary, 1, 0.2);
        Orbit(sel, g.P.Accent, 1.6, 0.95);
        foreach (var o in _s.Objects)
        {
            if (o.Type == OrbType.Debris && !_lDebris) continue;
            var p = Orb3(cx, cy, rad, o, out double d);
            if (d < 0 && Math.Sqrt((p.X - cx) * (p.X - cx) + (p.Y - cy) * (p.Y - cy)) < rad) continue;
            uint col = o.Type == OrbType.Debris ? 0xFF8A98A8 : o.Type == OrbType.Unknown ? g.P.Warning : o.Type == OrbType.Research ? 0xFF70FFC0 : g.P.Primary;
            bool isSel = o == sel; g.Disc(p.X, p.Y, g.U(o.Type == OrbType.Debris ? 1.4 : isSel ? 4 : 2.6), isSel ? g.P.Accent : col, o.Type == OrbType.Debris ? 0.6 : 1);
            if (isSel) { g.GlowCircle(p.X, p.Y, g.U(10), g.P.Accent, 1.3); g.Text(o.Name, p.X + g.U(12), p.Y - g.U(6), 8.5, g.P.Accent, Align.Left, true); }
            g.AddHit("obj:" + _s.Objects.IndexOf(o), new Rect(p.X - g.U(8), p.Y - g.U(8), g.U(16), g.U(16)));
        }
        g.Text("ORBITAL VIEW — ALTITUDES EXAGGERATED", m.Center.X, m.Y + g.U(16), 8, g.P.Text, Align.Center, false, 0.5);
    }

    // ---------------------------------------------------------------- B: telemetry
    void DrawTelemetry(Gfx g, Rect r)
    {
        var o = Sel; var (nla, nlo) = _s.Predict(o, 60); string st = o.Station >= 0 ? _s.Stations[o.Station].Name : "NONE";
        double ecc = o.Ecc, apo = (OrbitalSim.Re + o.Alt) * (1 + ecc) - OrbitalSim.Re;
        var items = new (string, string, uint?)[]
        {
            ("OBJECT ID", o.Id, null), ("NAME", o.Name, g.P.Accent), ("CLASS", o.Type.ToString().ToUpper(), null), ("PRIORITY", "P" + o.Priority, o.Priority <= 2 ? g.P.Warning : null),
            ("ALTITUDE", $"{o.Alt:0.0} KM", null), ("APOGEE", $"{apo:0.0} KM", null), ("INCLINATION", $"{o.Incl * 57.2958:0.00}°", null), ("ECCENTRICITY", $"{ecc:0.0000}", null),
            ("VELOCITY", $"{o.Velocity:0.000} KM/S", null), ("PERIOD", $"{o.Period / 60:0.00} MIN", null), ("LATITUDE", $"{o.Lat:+00.000;-00.000}°", null), ("LONGITUDE", $"{o.Lon:+000.000;-000.000}°", null),
            ("NEXT +60S", $"{nla:+00.0;-00.0}/{nlo:+000.0;-000.0}", null), ("CONFIDENCE", $"{o.Confidence * 100:0}%", o.Confidence < 0.4 ? g.P.Warning : null),
            ("SIGNAL", o.SignalLost ? "LOST" : $"{o.Snr:0.0} DB", o.SignalLost ? 0xFFFF3030 : null), ("STATUS", o.Status, o.Status == "NOMINAL" ? 0xFF30FF70 : g.P.Warning),
            ("LAST UPDATE", $"{Math.Max(0, _s.Time - o.LastUpdate):0.0} S AGO", null), ("STATION", st, null), ("ELEVATION", o.Elevation > 0 ? $"{o.Elevation:0.0}°" : "BELOW HORIZON", null), ("RANGE", o.Station >= 0 ? $"{o.Range:0} KM" : "--", null),
        };
        g.KvGrid(r, items, r.Width > g.U(330) ? 2 : 1, 8.5);
    }

    // ---------------------------------------------------------------- C: radar (sky plot from the selected station)
    void DrawRadar(Gfx g, Rect r)
    {
        var top = new Rect(r.X, r.Y, r.Width, g.U(20));
        var st = _s.Stations[_station];
        g.Button("or:st", new Rect(r.X, r.Y, g.U(120), g.U(18)), "SITE: " + st.Id, false, true, null, 7.5);
        g.Text($"RANGE {3000} KM  SWEEP {_sweep:0}°", r.Right, r.Y + g.U(3), 8, g.P.Text, Align.Right, false, 0.7);
        var a = new Rect(r.X, r.Y + g.U(22), r.Width, r.Height - g.U(22));
        double rad = Math.Min(a.Width, a.Height) / 2 - g.U(8), cx = a.Center.X, cy = a.Center.Y;
        g.Disc(cx, cy, rad, g.P.Background, 0.5);
        for (int i = 1; i <= 3; i++) { g.Circle(cx, cy, rad * i / 3, g.P.Primary, 1, 0.2); g.Text($"{90 - i * 30}°", cx + g.U(2), cy - rad * i / 3, 7, g.P.Text, Align.Left, false, 0.45); }
        for (int d = 0; d < 360; d += 30) { double an = (d - 90) * Math.PI / 180; g.Line(cx, cy, cx + Math.Cos(an) * rad, cy + Math.Sin(an) * rad, g.P.Primary, 1, d % 90 == 0 ? 0.25 : 0.1); if (d % 90 == 0) g.Text(d == 0 ? "N" : d == 90 ? "E" : d == 180 ? "S" : "W", cx + Math.Cos(an) * (rad + g.U(7)), cy + Math.Sin(an) * (rad + g.U(7)) - g.U(4), 8, g.P.Primary, Align.Center, true); }
        for (int k = 0; k < 20; k++) { double an = (_sweep - 90 - k * 2.2) * Math.PI / 180; g.Line(cx, cy, cx + Math.Cos(an) * rad, cy + Math.Sin(an) * rad, g.P.Primary, 1.5, 0.3 * (1 - k / 20.0)); }
        g.GlowLine(cx, cy, cx + Math.Cos((_sweep - 90) * Math.PI / 180) * rad, cy + Math.Sin((_sweep - 90) * Math.PI / 180) * rad, g.P.Primary, 1.5);
        int shown = 0;
        for (int i = 0; i < _s.Objects.Count; i++)
        {
            var o = _s.Objects[i]; var (el, rg) = OrbitalSim.Look(o, st); if (el < 2) continue;
            double az = OrbitalSim.Bearing(st.Lat, st.Lon, o.Lat, o.Lon), zr = (90 - el) / 90.0 * rad;
            double an = (az - 90) * Math.PI / 180, x = cx + Math.Cos(an) * zr, y = cy + Math.Sin(an) * zr;
            double diff = ((_sweep - az) % 360 + 360) % 360, fade = diff < 220 ? 1 - diff / 260.0 : 0.15;
            uint col = o.Type == OrbType.Unknown ? g.P.Warning : o.Type == OrbType.Debris ? 0xFF8A98A8 : o.Tracked ? g.P.Accent : g.P.Primary;
            bool sel = i == _s.Selected;
            if (o.Type == OrbType.Debris && !sel && !o.Tracked) { g.Disc(x, y, g.U(1.4), col, 0.2 + 0.6 * fade); continue; }
            g.Disc(x, y, g.U(sel ? 3.6 : 2.6), col, 0.3 + 0.7 * fade); shown++;
            // tracking vector from 20 s prediction
            var (pl, po) = _s.Predict(o, 25); var tmp = new Gblip(); double az2 = OrbitalSim.Bearing(st.Lat, st.Lon, pl, po); var look2 = OrbitalSim.Look(new OrbObject { Alt = o.Alt, Lat = pl, Lon = po }, st);
            double zr2 = (90 - Math.Max(0, look2.el)) / 90.0 * rad, an2 = (az2 - 90) * Math.PI / 180;
            if (o.Tracked || sel) g.Line(x, y, cx + Math.Cos(an2) * zr2, cy + Math.Sin(an2) * zr2, col, 1, 0.6);
            if (sel || o.Tracked || o.Type == OrbType.Unknown) g.Text($"{o.Id[3..]}{(o.Type == OrbType.Unknown ? "?" : "")}", x + g.U(5), y - g.U(5), 7, col, Align.Left, sel, 0.4 + 0.6 * fade);
            if (sel) { g.Frame(new Rect(x - g.U(7), y - g.U(7), g.U(14), g.U(14)), g.P.Accent, 1.2); }
            if (_acq.TryGetValue(i, out var at)) g.Circle(x, y, g.U(30 * (1 - at / 2.5)) + g.U(3), g.P.Accent, 1.6, 1 - at / 2.5);
            g.AddHit("rb:" + i, new Rect(x - g.U(9), y - g.U(9), g.U(18), g.U(18)));
        }
        g.Circle(cx, cy, rad, g.P.Primary, 1.4, 0.8);
        g.Text($"RETURNS {shown}   {(_s.Objects.Any(o => o.Type == OrbType.Unknown && OrbitalSim.Look(o, st).el > 2) ? "UNKNOWN IN VIEW" : "")}", a.X + g.U(2), a.Bottom - g.U(11), 7.5, _s.Objects.Any(o => o.Type == OrbType.Unknown && OrbitalSim.Look(o, st).el > 2) ? g.P.Warning : g.P.Text, Align.Left, true, 0.8);
    }
    sealed class Gblip { }

    // ---------------------------------------------------------------- orbital elements
    void DrawElements(Gfx g, Rect r)
    {
        var o = Sel; double cx = r.X + r.Height * 0.5, cy = r.Center.Y, rad = r.Height * 0.2;
        g.Disc(cx, cy, rad, g.P.Secondary, 0.4); g.Circle(cx, cy, rad, g.P.Primary, 1, 0.8);
        double rr = rad * (1 + o.Alt / 1500.0), a = o.Ecc * 12;
        g.Dc.DrawEllipse(null, g.Pn(g.P.Accent, 1.2, 0.8), new Point(cx + rr * a * 0.5, cy), rr, rr * Math.Sqrt(1 - a * a * 0.2));
        double u = o.Phase; g.Disc(cx + rr * a * 0.5 + Math.Cos(u) * rr, cy + Math.Sin(u) * rr * 0.9, g.U(3), g.P.Accent);
        g.Text("PERIGEE", cx + rr * a * 0.5 + rr, cy - g.U(12), 7, g.P.Text, Align.Right, false, 0.6);
        double tx = r.X + r.Height + g.U(6);
        g.KvGrid(new Rect(tx, r.Y, r.Right - tx, r.Height), new (string, string, uint?)[] { ("SEMI-MAJOR", $"{OrbitalSim.Re + o.Alt:0} KM", null), ("MEAN MOTION", $"{86400.0 / o.Period:0.00} REV/D", null), ("RAAN", $"{o.Raan * 57.2958 % 360:0.0}°", null), ("TRUE ANOM.", $"{(o.Phase * 57.2958) % 360:0.0}°", null), ("DECAY", o.Type == OrbType.Debris ? "-0.4 M/S" : "NEGLIGIBLE", null) }, 1, 8);
    }

    // ---------------------------------------------------------------- D: traffic
    List<string[]> Rows()
    {
        var rows = new List<string[]>();
        if (_filter == 5) { foreach (var s in _s.Stations) rows.Add(new[] { s.Id, "GROUND", "0", "0.00", $"{s.Lat:0}", s.Online ? "ONLINE" : "FAULT", "P1" }); return rows; }
        for (int i = 0; i < _s.Objects.Count; i++)
        {
            var o = _s.Objects[i];
            bool ok = _filter switch { 0 => true, 1 => o.Type == OrbType.Active || o.Type == OrbType.Station, 2 => o.Type == OrbType.Research, 3 => o.Type == OrbType.Debris, 4 => o.Type == OrbType.Unknown, _ => true };
            if (ok) rows.Add(new[] { i.ToString(), o.Id, o.Type.ToString().ToUpper(), $"{o.Alt:0}", $"{o.Velocity:0.00}", $"{o.Incl * 57.2958:0.0}", o.Status, "P" + o.Priority });
        }
        return rows;
    }

    void DrawTraffic(Gfx g, Rect r)
    {
        double bw = (r.Width - g.U(3) * 5) / 6;
        for (int i = 0; i < Filters.Length; i++) g.Button("or:f:" + i, new Rect(r.X + i * (bw + g.U(3)), r.Y, bw, g.U(g.Touch ? 24 : 18)), Filters[i], _filter == i, true, null, bw < g.U(54) ? 6 : 7.5);
        var t = new Rect(r.X, r.Y + g.U(g.Touch ? 28 : 22), r.Width, r.Height - g.U(g.Touch ? 28 : 22)); _tblR = t;
        var raw = Rows(); bool ground = _filter == 5;
        var rows = raw.Select(x => ground ? x : x.Skip(1).ToArray()).ToList();
        int selRow = ground ? -1 : raw.FindIndex(x => x[0] == _s.Selected.ToString());
        _tblFirst = Math.Clamp(_tblFirst, 0, Math.Max(0, rows.Count - g.TableVisibleRows(t)));
        g.Table(t, new[] { "ID", "TYPE", "ALT KM", "KM/S", "INC", "STATUS", "PRI" }, new[] { 1.25, 1, 0.9, 0.8, 0.7, 1.3, 0.55 }, rows, _tblFirst, selRow, "tr:", (ri, ci) =>
        {
            if (ci == 5) return rows[ri][5] is "NOMINAL" or "ONLINE" ? 0xFF30FF70 : rows[ri][5] is "UNCLASSIFIED" or "MANOEUVRING" ? g.P.Warning : 0xFFFF5050;
            if (ci == 1) return rows[ri][1] switch { "UNKNOWN" => g.P.Warning, "DEBRIS" => (uint?)0xFF8A98A8, _ => null };
            return null;
        });
    }

    // ---------------------------------------------------------------- E: comm network
    void DrawComms(Gfx g, Rect r)
    {
        double lw = r.Width * 0.38, rh = Math.Min(g.U(18), (r.Height - g.U(30)) / _s.Stations.Count);
        var sats = _s.Objects.Where(o => o.Station >= 0 && o.Type is OrbType.Active or OrbType.Research or OrbType.Station).OrderBy(o => o.Station).Take(Math.Max(3, (int)((r.Height - g.U(30)) / g.U(12)))).ToList();
        var sp = new Point[_s.Stations.Count];
        for (int i = 0; i < _s.Stations.Count; i++)
        {
            var s = _s.Stations[i]; var rc = new Rect(r.X, r.Y + i * rh, lw, rh - 1); sp[i] = new Point(rc.Right, rc.Center.Y);
            g.Fill(rc, s.Online ? g.P.Primary : 0xFFFF3030, 0.08); g.Frame(rc, s.Online ? g.P.Primary : 0xFFFF3030, 1, 0.5);
            g.Led(rc.X + g.U(6), rc.Center.Y, s.Online ? 1 : 3);
            g.Text(s.Id, rc.X + g.U(14), rc.Y + (rh - g.U(9)) / 2, 7.5, g.P.Text, Align.Left, true);
            g.Text($"{s.Load:0}/{s.Bandwidth:0}M {s.Latency:0}ms", rc.Right - g.U(3), rc.Y + (rh - g.U(8)) / 2, 7, s.Online ? g.P.Primary : 0xFFFF3030, Align.Right);
            g.AddHit("st:" + i, rc, HitKind.Button);
        }
        double sh = Math.Min(g.U(12), (r.Height - g.U(30)) / Math.Max(1, sats.Count));
        for (int i = 0; i < sats.Count; i++)
        {
            var o = sats[i]; var p = new Point(r.Right - g.U(60), r.Y + i * sh + sh / 2);
            uint col = o.SignalLost ? 0xFFFF3030 : o.Snr > 18 ? 0xFF30FF70 : g.P.Warning;
            g.Dc.DrawLine(g.Pn(col, o == Sel ? 1.8 : 1, 0.5, o.SignalLost ? g.U(3) : 0), sp[o.Station], p);
            g.Disc(p.X, p.Y, g.U(2.4), col); g.Text(o.Name, p.X + g.U(6), p.Y - g.U(5), 7, o == Sel ? g.P.Accent : g.P.Text, Align.Left, o == Sel);
        }
        double by = r.Bottom - g.U(26);
        g.HMeter(new Rect(r.X, by, r.Width / 2 - g.U(4), g.U(24)), _s.Bandwidth.Last, 0, _s.Stations.Sum(s => s.Bandwidth), "BANDWIDTH", $"{_s.Bandwidth.Last:0} MBPS", null, double.NaN, _s.Stations.Sum(s => s.Bandwidth) * 0.85);
        g.HMeter(new Rect(r.Center.X + g.U(4), by, r.Width / 2 - g.U(4), g.U(24)), _s.Latency.Last, 0, 120, "LATENCY", $"{_s.Latency.Last:0} MS", g.P.Secondary, double.NaN, 80);
    }

    protected override void BeforeDraw(Gfx g) { if (!_init) { _init = true; _station = Math.Max(0, Sel.Station); } }
    bool _init;

    void DrawGraphs(Gfx g, Rect r)
    {
        var rows = Layout.Rows(r, g.U(3), 1, 1);
        var c = Layout.Cols(rows[0], g.U(3), 1, 1);
        g.Graph(c[0], _s.TrackedCount, g.P.Accent, 0, null, true, "TRACKED");
        g.Graph(c[1], _s.Quality, g.P.Primary, 0, 45, true, "AVG SNR", "dB");
        var d = Layout.Cols(rows[1], g.U(3), 1, 1);
        g.Graph(d[0], _s.Bandwidth, g.P.Secondary, 0, null, true, "BANDWIDTH");
        g.Graph(d[1], _s.Latency, g.P.Warning, 0, null, true, "LATENCY", "ms");
    }

    // ---------------------------------------------------------------- F: mission / sensors / alerts
    void DrawMission(Gfx g, Rect r)
    {
        double gr = Math.Min(r.Height, r.Width * 0.3) / 2 - g.U(2);
        g.Gauge(r.X + gr + g.U(2), r.Center.Y, gr, _s.Readiness * 100, 100, "READINESS", "%", null, 2);
        double tx = r.X + gr * 2 + g.U(12), lh = Math.Min(g.U(24), r.Height / _s.Missions.Count);
        for (int i = 0; i < _s.Missions.Count; i++) { var m = _s.Missions[i]; g.Bar(new Rect(tx, r.Y + i * lh, r.Right - tx, lh - g.U(2)), m.Progress, g.P.Primary, m.Name, $"{m.Progress * 100:0}%"); }
    }

    void DrawSensors(Gfx g, Rect r)
    {
        double lh = r.Height / OrbitalSim.SensorNames.Length;
        for (int i = 0; i < OrbitalSim.SensorNames.Length; i++)
        {
            double h = _s.SensorHealth[i]; g.Led(r.X + g.U(5), r.Y + i * lh + lh / 2, h > 0.8 ? 1 : h > 0.55 ? 2 : 3);
            g.HMeter(new Rect(r.X + g.U(14), r.Y + i * lh, r.Width - g.U(14), lh), h * 100, 0, 100, OrbitalSim.SensorNames[i], $"{h * 100:0}%", null, 60);
        }
    }

    void DrawAlerts(Gfx g, Rect r)
    {
        double lh = g.U(11.5); int max = Math.Max(1, (int)(r.Height / lh));
        int start = Math.Max(0, _s.Log.Count - max);
        for (int i = start; i < _s.Log.Count; i++) { string l = _s.Log[i]; bool w = l.Contains("LOSS") || l.Contains("CONJUNCTION") || l.Contains("REFUSED"); g.Text(l, r.X, r.Y + (i - start) * lh, 7.5, w ? g.P.Warning : g.P.Text, Align.Left, w, 0.92); }
        g.Text($"ALERTS {_s.Alerts}", r.Right, r.Y, 8, _s.Alerts > 0 ? g.P.Warning : g.P.Text, Align.Right, true);
    }

    // ---------------------------------------------------------------- input
    protected override string? PanelButton(string id)
    {
        if (id == "or:view") { _view = 1 - _view; return _view == 0 ? "EARTH-CENTRED VIEW" : "ORBITAL VIEW"; }
        if (id == "or:st") { _station = (_station + 1) % _s.Stations.Count; return "RADAR SITE " + _s.Stations[_station].Name; }
        if (id.StartsWith("or:l:")) { switch (id[5..]) { case "tracks": _lTracks = !_lTracks; break; case "cov": _lCov = !_lCov; break; case "links": _lLinks = !_lLinks; break; case "debris": _lDebris = !_lDebris; break; case "labels": _lLabels = !_lLabels; break; } return null; }
        if (id.StartsWith("or:f:")) { _filter = int.Parse(id[5..]); _tblFirst = 0; return "FILTER: " + Filters[_filter]; }
        if (id.StartsWith("st:")) { var s = _s.Stations[int.Parse(id[3..])]; s.Online = !s.Online; C.Audio.Play(s.Online ? "handshake" : "error"); return $"{s.Id} {(s.Online ? "ONLINE" : "CONNECTION FAILURE")}"; }
        return null;
    }

    void SelectObj(int i) { if (i < 0 || i >= _s.Objects.Count) return; _s.Selected = i; var o = Sel; C.Toast($"{o.Name}  {o.Id}  {o.Type.ToString().ToUpper()}"); C.Audio.Play("deepbeep", SoundCategory.Ui, 0.7f); }

    protected override void PanelTap(Point p, string? id)
    {
        if (id == null) return;
        if (id.StartsWith("obj:")) SelectObj(int.Parse(id[4..]));
        else if (id.StartsWith("rb:")) SelectObj(int.Parse(id[3..]));
        else if (id.StartsWith("tr:")) { var raw = Rows(); int k = int.Parse(id[3..]); if (_filter == 5) { _station = k; C.Toast("SITE " + _s.Stations[k].Name); } else SelectObj(int.Parse(raw[k][0])); }
    }

    protected override void PanelDoubleTap(Point p, string? id) { if (id != null && id.StartsWith("obj:")) { SelectObj(int.Parse(id[4..])); DoAction("TRACK"); } }

    protected override bool PanelDrag(Point p, Vector d, string? id)
    {
        if (id != null && id.StartsWith("tr:")) { _tblFirst = Math.Max(0, _tblFirst - (int)Math.Round(d.Y / 8)); return true; }
        if (id == "or:map" || id != null && id.StartsWith("obj:")) { _rotLon -= d.X * 0.4; _tilt = Math.Clamp(_tilt + d.Y * 0.3, -75, 75); return true; }
        return false;
    }

    protected override bool PanelWheel(Point p, double delta)
    {
        if (_tblR.Contains(p)) { _tblFirst = Math.Max(0, _tblFirst - (int)Math.Round(delta * 3)); return true; }
        if (_mapR.Contains(p)) { _zoom = Math.Clamp(_zoom * (1 + delta * 0.12), 0.6, 3); return true; }
        return false;
    }
}
