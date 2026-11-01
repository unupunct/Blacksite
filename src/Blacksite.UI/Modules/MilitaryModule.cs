using Avalonia;
using Avalonia.Media;
using Blacksite.Core.Audio;
using Blacksite.Core.Themes;
using Blacksite.UI.Rendering;

namespace Blacksite.UI.Modules;

/// <summary>
/// PRESET 10 – TACTICAL / MILITARY COMMAND (v2). Three layouts (LAYOUT button): PHANTOM COMMAND, SHADOW OPERATIONS and TITAN COMMAND.
/// All locations, units, feeds and intelligence are fictional and procedurally generated.
/// </summary>
public sealed class MilitaryModule : DeckModule
{
    sealed class Sat { public string Id = "", Orbit = ""; public double Alt, Vel, Phase, Inc; }
    sealed class Alert { public string Text = ""; public int Sev; public bool Ack; public string Time = ""; }
    sealed class Unit { public string Id = "", Kind = ""; public double X, Y, TX, TY, Speed; public bool Hostile; }

    static readonly string[] LayName = { "PHANTOM COMMAND", "SHADOW OPERATIONS", "TITAN COMMAND" };
    static readonly string[] LaySub = { "STRATEGIC OPERATIONS CENTER", "INTELLIGENCE & RECONNAISSANCE", "STRATEGIC DEFENSE & AEROSPACE COMMAND" };
    static readonly string[] Threat = { "LOW", "GUARDED", "ELEVATED", "SEVERE" };
    int Lay { get => Math.Clamp(C.Settings.MilitaryLayout, 0, 2); set => C.Settings.MilitaryLayout = value; }

    readonly List<Sat> _sats = new(); readonly List<Alert> _alerts = new(); readonly List<(string t, string m)> _timeline = new(); readonly List<Unit> _units = new();
    readonly List<string> _term = new(), _intel = new(), _syslog = new();
    int _selSat, _selUnit, _view, _nav, _baseMap; bool[] _layer = { true, false, false, false, true, true };
    int _threat = 2; double _rot = 30, _tilt = 22, _zoom = 1, _alertT = 20, _termT, _aiT, _t2;
    double _power = 0.96, _cooling = 0.89, _comms = 0.93, _storage = 0.76, _temp = 22.4, _press = 1013, _hum = 45, _rad = 0.12;
    double _def = 0.98, _rec = 0.76, _str = 0.45, _sup = 0.88, _mission = 0.46;
    bool _radarOn = true; int _uav = 2; double _sweep, _access = -1; bool[] _rec4 = { true, true, true, true }; int _camSel;
    string _ai = "Analyzing current situation..."; Rect _globeR, _mapR;
    static readonly (double lat, double lon)[] Hubs = { (51.5, -0.1), (40.7, -74), (35.7, 139.7), (-33.9, 151.2), (1.3, 103.8), (19.4, -99.1), (55.7, 37.6), (-23.5, -46.6), (30, 31), (28.6, 77.2) };
    static uint Hh(uint x) { x ^= x >> 16; x *= 0x7feb352d; x ^= x >> 15; x *= 0x846ca68b; x ^= x >> 16; return x; }

    public override string[] ModuleActions => new[] { "LAYOUT", "RECON", "UAV", "RADAR", "SUPPORT", "ALERT", "VIEW" };
    protected override bool IsActionActive(string a) => a == "RADAR" && _radarOn;
    public override bool NoTicker => true;

    protected override void OnInit()
    {
        for (int i = 0; i < 8; i++)
        {
            string orb = i < 4 ? "LEO" : i < 6 ? "MEO" : "GEO"; double alt = orb == "LEO" ? 400 + i * 70 : orb == "MEO" ? 20000 + i * 1200 : 35786;
            _sats.Add(new Sat { Id = $"SAT-{i + 1:000}", Orbit = orb, Alt = alt, Vel = Math.Sqrt(398600.4 / (6371 + alt)), Phase = i * 0.8, Inc = 20 + i * 9 });
        }
        for (int i = 0; i < 4; i++) _alerts.Add(new Alert { Text = new[] { "Unknown object detected", "Satellite signal anomaly", "Airspace violation", "Low signal strength" }[i], Sev = i < 2 ? 2 : 1, Time = $"{14 + i}:{10 + i * 7:00}" });
        foreach (var (t, m) in new[] { ("12:14", "Reconnaissance complete"), ("13:32", "Satellite acquisition"), ("14:05", "Target tracking"), ("14:37", "Analysis in progress") }) _timeline.Add((t, m));
        foreach (var s in new[] { "NEW: Possible supply route detected", "SAT: Unusual activity (zone 4)", "SIG: Encrypted transmission (12.4 GHz)", "INTEL: Target movement (3 units)", "ALERT: Network anomaly (low priority)", "UPDATE: Weather pattern change" }) _intel.Add(s);
        foreach (var s in new[] { "14:32  Drone feed updated", "14:28  Satellite pass completed", "14:21  New intel received", "14:16  Analysis in progress" }) _syslog.Add(s);
        (string id, string k, double x, double y, bool h)[] us = { ("RECON-1", "UAV", 0.30, 0.55, false), ("BRAVO-2", "INF", 0.42, 0.62, false), ("ARMOR-3", "ARM", 0.55, 0.50, false), ("ALPHA", "TGT", 0.62, 0.45, true), ("HOSTILE-2", "ARM", 0.72, 0.38, true), ("HOSTILE-3", "INF", 0.66, 0.62, true), ("LOGI-1", "TRK", 0.38, 0.30, true) };
        foreach (var u in us) _units.Add(new Unit { Id = u.id, Kind = u.k, X = u.x, Y = u.y, TX = u.x, TY = u.y, Speed = u.h ? 0.004 : 0.012, Hostile = u.h });
        _selUnit = 0; _term.Add("scan network 10.0.0.0/16"); _term.Add("Scanning...");
    }

    // ---------------------------------------------------------------- panel definitions per layout
    protected override void BuildPanels()
    {
        switch (Lay)
        {
            case 0:
                DefAt("sat", "SATELLITE TRACKING", Zone.Left, 0, 0, .19, .5, PnlSats); DefAt("air", "AIRSPACE MONITORING", Zone.Left, 0, .5, .19, .5, PnlAir);
                DefAt("globe", "ORBITAL OVERVIEW", Zone.Main, .195, 0, .29, 1, (g, r) => Globe(g, r, false));
                DefAt("alerts", "GLOBAL THREAT LEVEL / ACTIVE ALERTS", Zone.Right, .49, 0, .145, .6, PnlAlerts); DefAt("timeline", "MISSION TIMELINE", Zone.Right, .49, .6, .145, .4, PnlTimeline);
                DefAt("net", "NETWORK STATUS", Zone.Right, .64, 0, .185, .37, PnlNet); DefAt("sys", "SYSTEMS OVERVIEW", Zone.Right, .64, .37, .185, .63, PnlSystems);
                DefAt("env", "ENVIRONMENTAL MONITORING", Zone.Right, .83, 0, .17, .3, PnlEnv); DefAt("comms", "COMMUNICATIONS", Zone.Right, .83, .3, .17, .7, PnlComms);
                break;
            case 1:
                for (int i = 0; i < 4; i++) { int k = i; DefAt("cam" + i, $"CAM 0{i + 1} - " + new[] { "HQ ENTRANCE", "AIRPORT", "CITY STREETS", "FACILITY" }[i], Zone.Left, 0, i * .25, .125, .25, (g, r) => Cam(g, r, k)); }
                DefAt("intel", "INTELLIGENCE FEED", Zone.Left, .13, 0, .16, .34, PnlIntel); DefAt("sig", "SIGNAL ANALYSIS", Zone.Left, .13, .34, .16, .33, PnlSignal); DefAt("drone", "DRONE FEED", Zone.Left, .13, .67, .16, .33, PnlDrone);
                DefAt("map", "TACTICAL MAP", Zone.Main, .295, 0, .28, 1, PnlMap);
                DefAt("img", "SATELLITE IMAGERY", Zone.Right, .58, 0, .13, .34, PnlImagery); DefAt("wx", "WEATHER RADAR", Zone.Right, .58, .34, .13, .33, PnlWeather); DefAt("terr", "TERRAIN ANALYSIS", Zone.Right, .58, .67, .13, .33, PnlTerrain);
                DefAt("ops", "OPERATIONAL STATUS", Zone.Right, .715, 0, .135, .3, PnlOps); DefAt("threat", "THREAT INTELLIGENCE", Zone.Right, .715, .3, .135, .33, PnlThreat); DefAt("log", "SYSTEM LOG", Zone.Right, .715, .63, .135, .37, PnlLog);
                DefAt("term", "TERMINAL", Zone.Right, .855, 0, .145, 1, PnlTerminal);
                break;
            default:
                DefAt("fleet", "FLEET OVERVIEW", Zone.Left, 0, 0, .14, .56, PnlFleet); DefAt("mission", "MISSION STATUS", Zone.Left, 0, .56, .14, .44, PnlMissionBars);
                DefAt("aero", "AEROSPACE TRACKING", Zone.Left, .145, 0, .185, .42, PnlAero); DefAt("comms", "COMMUNICATIONS", Zone.Left, .145, .42, .185, .31, PnlCommsMini); DefAt("env", "ENVIRONMENTAL", Zone.Left, .145, .73, .185, .27, PnlEnvRow);
                DefAt("globe", "GLOBAL DEFENSE OVERVIEW", Zone.Main, .335, 0, .235, 1, (g, r) => Globe(g, r, true));
                DefAt("gnet", "GLOBAL NETWORK", Zone.Right, .575, 0, .135, .5, PnlGlobalNet); DefAt("base", "BASE OPERATIONS", Zone.Right, .575, .5, .135, .5, PnlBase);
                DefAt("tac", "TACTICAL OVERVIEW", Zone.Right, .715, 0, .145, .33, PnlTactical); DefAt("intelA", "INTEL ANALYSIS", Zone.Right, .715, .33, .145, .33, PnlJet); DefAt("tl", "MISSION TIMELINE", Zone.Right, .715, .66, .145, .34, PnlTimelineChart);
                DefAt("quick", "QUICK ACTIONS", Zone.Right, .865, 0, .135, .5, PnlQuick); DefAt("ai", "AI ASSISTANT", Zone.Right, .865, .5, .135, .5, PnlAi);
                break;
        }
    }

    Palette Pal(Palette p) => Lay switch
    {
        0 => p with { Background = 0xFF020A0A, Panel = 0xFF04141A, Primary = 0xFF34E8A0, Secondary = 0xFF1AA880, Accent = 0xFFB4FFE4, Text = 0xFFB6E8D8, Warning = 0xFFFFB02E },
        1 => p with { Background = 0xFF030A08, Panel = 0xFF05150F, Primary = 0xFF4CF0A8, Secondary = 0xFF20B070, Accent = 0xFFD2FFC8, Text = 0xFFBFE8D0, Warning = 0xFFFFB02E },
        _ => p with { Background = 0xFF03070F, Panel = 0xFF071526, Primary = 0xFF3AA2FF, Secondary = 0xFF1668C8, Accent = 0xFF9ADCFF, Text = 0xFFC8E0F8, Warning = 0xFFFF9A2E },
    };

    // ---------------------------------------------------------------- actions
    void Tl(string m) { _timeline.Insert(0, (DateTime.Now.ToString("HH:mm"), m)); if (_timeline.Count > 12) _timeline.RemoveAt(12); }
    void Log(string m) { _syslog.Insert(0, $"{DateTime.Now:HH:mm}  {m}"); if (_syslog.Count > 12) _syslog.RemoveAt(12); }

    protected override string? DoAction(string id)
    {
        string m = "";
        switch (id)
        {
            case "LAYOUT": Lay = (Lay + 1) % 3; C.Save(); Rebuild(); m = "LAYOUT: " + LayName[Lay]; C.Audio.Play("chime", SoundCategory.Ui, 0.6f); break;
            case "RECON": { var u = new Unit { Id = $"RECON-{_units.Count + 1}", Kind = "UAV", X = 0.15, Y = 0.7, TX = 0.6, TY = 0.45, Speed = 0.03 }; _units.Add(u); _rec = Math.Min(1, _rec + 0.06); Tl("Recon flight launched"); Log("Recon mission launched"); m = "RECON MISSION LAUNCHED"; C.Audio.Play("radio", SoundCategory.Event); break; }
            case "UAV": _uav++; _units.Add(new Unit { Id = $"UAV-{_uav}", Kind = "UAV", X = 0.2, Y = 0.5, TX = 0.5, TY = 0.55, Speed = 0.025 }); Tl("UAV deployed"); Log("UAV deployed"); m = "UAV DEPLOYED"; C.Audio.Play("whoosh", SoundCategory.Event, 0.5f); break;
            case "RADAR": _radarOn = !_radarOn; Tl(_radarOn ? "Radar activated" : "Radar standby"); m = _radarOn ? "RADAR ACTIVE" : "RADAR STANDBY"; C.Audio.Play("radarpulse", SoundCategory.Event); break;
            case "SUPPORT": _sup = Math.Min(1, _sup + 0.04); _units.Add(new Unit { Id = $"FRIENDLY-{_units.Count + 1}", Kind = "ARM", X = 0.2, Y = 0.4, TX = 0.45, TY = 0.5, Speed = 0.012 }); Tl("Support requested — units en route"); m = "SUPPORT REQUESTED"; C.Audio.Play("radio", SoundCategory.Event); break;
            case "ALERT": NewAlert(); m = "ALERT RAISED"; break;
            case "VIEW": _view = (_view + 1) % 5; m = "VIEW: " + new[] { "EARTH", "SATELLITE", "TOPOGRAPHY", "THERMAL", "NIGHT VISION" }[_view]; C.Audio.Play("tick"); break;
        }
        return m;
    }

    void NewAlert()
    {
        string[] t = { "Unknown object detected", "Satellite signal anomaly", "Airspace violation", "Encrypted burst detected", "Perimeter sensor tripped", "GPS interference reported" };
        _alerts.Insert(0, new Alert { Text = t[Rng.Next(t.Length)], Sev = Rng.Next(1, 4), Time = DateTime.Now.ToString("HH:mm") }); if (_alerts.Count > 8) _alerts.RemoveAt(8);
        _threat = Math.Clamp(1 + _alerts.Count(a => !a.Ack && a.Sev >= 2) / 2, 0, 3); Tl("Alert: " + _alerts[0].Text); C.Audio.Play("triple", SoundCategory.Alarm, 0.5f);
    }

    protected override void Tick(double dt, double anim)
    {
        _t2 += anim; _sweep = (_sweep + anim * (_radarOn ? 70 : 0)) % 360;
        foreach (var s in _sats) s.Phase += anim * 0.4 * (s.Orbit == "LEO" ? 1.6 : s.Orbit == "MEO" ? 0.6 : 0.2);
        double j(double s, double o) => 0.5 + 0.5 * Math.Sin(_t2 * s + o);
        _power = 0.94 + 0.04 * j(0.2, 0); _cooling = 0.86 + 0.06 * j(0.17, 1); _comms = 0.9 + 0.06 * j(0.23, 2); _storage = 0.75 + 0.02 * j(0.05, 3);
        _temp = 22.4 + 0.6 * Math.Sin(_t2 * 0.1); _press = 1013 + 2 * Math.Sin(_t2 * 0.07); _hum = 45 + 2 * Math.Sin(_t2 * 0.09); _rad = 0.12 + 0.01 * Math.Sin(_t2 * 0.3);
        _mission = Math.Min(1, _mission + dt * 0.0007); _rec = Math.Clamp(_rec + (Rng.NextDouble() - 0.5) * 0.001, 0.5, 1); _str = Math.Clamp(_str + (Rng.NextDouble() - 0.4) * 0.001, 0.2, 0.9);
        foreach (var u in _units)
        {
            double dx = u.TX - u.X, dy = u.TY - u.Y, d = Math.Sqrt(dx * dx + dy * dy);
            if (d > 0.004) { double k = Math.Min(1, u.Speed * dt / d); u.X += dx * k; u.Y += dy * k; }
            else if (u.Hostile || u.Kind == "UAV") { u.TX = Math.Clamp(u.X + (Rng.NextDouble() - 0.5) * 0.2, 0.08, 0.92); u.TY = Math.Clamp(u.Y + (Rng.NextDouble() - 0.5) * 0.2, 0.1, 0.9); }
        }
        _alertT -= dt * Cfg.AlertFrequency; if (_alertT <= 0 && Cfg.AlertFrequency > 0.05) { _alertT = 35 + Rng.NextDouble() * 40; NewAlert(); }
        _termT -= dt; if (_termT <= 0)
        {
            _termT = 1.3; string[] script = { "Scanning...", "Host 10.0.12.34 - open", "Host 10.0.12.67 - open", "Host 10.0.15.89 - filtered", "Host 10.0.18.21 - open", "Scan complete: 3/254 hosts.", "> analyze image", "Processing satellite image...", "Target detected: 2 objects", "Confidence: 87%", "> decrypt", "Attempting decryption...", "Key: 7f3d9c2e...", $"Decryption in progress: {Rng.Next(30, 99)}%" };
            _term.Add(script[_term.Count % script.Length]); if (_term.Count > 40) _term.RemoveAt(0);
        }
        _aiT -= dt; if (_aiT <= 0) { _aiT = 7; _ai = new[] { "Analyzing current situation...", "No immediate threats detected. All systems nominal.", $"Tracking {_units.Count(u => u.Hostile)} hostile contacts. Recommend continued surveillance.", "Satellite pass in 4 minutes — imagery update queued." }[(int)(_t2 / 7) % 4]; }
        if (_access >= 0) { _access += dt; if (_access > 4) _access = -1; }
    }

    // ---------------------------------------------------------------- frame: header + nav + deck
    public override void Draw(Gfx g, Rect area)
    {
        var saved = g.P; g.P = Pal(saved); double sc0 = g.Scale;
        g.Scale *= 1.0 + 0.28 * Math.Clamp((area.Width - 1000) / 900.0, 0, 1);   // larger instrument text on big displays
        g.Fill(area, g.P.Background, 1);
        double hh = g.U(g.Touch ? 50 : 46);
        DrawHeader(g, new Rect(area.X, area.Y, area.Width, hh));
        var body = new Rect(area.X, area.Y + hh, area.Width, area.Height - hh);
        bool wide = area.Width >= g.U(900) && area.Width / area.Height >= 1.2;
        if (Lay == 0 && wide) { double nw = g.U(104); DrawNav(g, new Rect(body.X + g.U(4), body.Y + g.U(4), nw, body.Height - g.U(8))); body = new Rect(body.X + nw + g.U(6), body.Y, body.Width - nw - g.U(6), body.Height); }
        if (Lay == 2) g.Frame(area.Deflate(g.U(1)), 0xFFFF9A2E, g.U(2.5), 0.55);
        base.Draw(g, body);
        g.Scale = sc0; g.P = saved;
    }

    void DrawHeader(Gfx g, Rect r)
    {
        g.Fill(r, g.P.Primary, 0.06); g.Line(r.X, r.Bottom, r.Right, r.Bottom, g.P.Primary, 1, 0.55);
        double cx = r.X + g.U(10), cy = r.Center.Y;
        var hx = new Point[6]; for (int i = 0; i < 6; i++) hx[i] = new Point(cx + g.U(16) + Math.Cos(i * Math.PI / 3 + 0.5) * g.U(15), cy + Math.Sin(i * Math.PI / 3 + 0.5) * g.U(15));
        g.Poly(hx, g.P.Primary, 1.8, 1, true); g.Circle(cx + g.U(16), cy, g.U(6), g.P.Primary, 1.4); g.Disc(cx + g.U(16), cy, g.U(2), g.P.Accent);
        g.Text("BLACKSITE", cx + g.U(38), cy - g.U(14), 17, 0xFFFFFFFF, Align.Left, true); g.Text("TACTICAL INTERFACE SIMULATOR", cx + g.U(38), cy + g.U(5), 6.5, g.P.Text, Align.Left, false, 0.75);
        double mx = r.Center.X - r.Width * 0.12; g.Text(LayName[Lay], mx, cy - g.U(15), 17, Lay == 2 ? 0xFFFF9A2E : g.P.Accent, Align.Left, true); g.Text(LaySub[Lay], mx, cy + g.U(5), 7.5, Lay == 2 ? 0xFFFF9A2E : g.P.Primary, Align.Left, false, 0.9);
        string c = Lay switch { 0 => "CLASSIFIED   //   EYES ONLY", 1 => "OPERATION: NIGHTFALL   //   REGION: GLOBAL   //   STATUS: ACTIVE", _ => $"GLOBAL DEFENSE NETWORK   //   FLEET: ACTIVE   //   READINESS: {Math.Min(99, (int)((_def + _rec + _sup) / 3 * 100 + 12))}%" };
        g.Text(c, r.Center.X + r.Width * 0.12, cy - g.U(5), 8.5, Lay == 0 ? g.P.Warning : g.P.Text, Align.Left, true, 0.95);
        g.Text($"SYS ONLINE  ●".Replace("●", " "), r.Right - g.U(150), cy - g.U(5), 8, g.P.Primary, Align.Left, true); g.Led(r.Right - g.U(168), cy, 1);
        g.Text(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), r.Right - g.U(8), cy - g.U(5), 8.5, g.P.Text, Align.Right, false, 0.9);
    }

    void DrawNav(Gfx g, Rect r)
    {
        string[] n = { "OVERVIEW", "SATELLITES", "AIRCRAFT", "INTELLIGENCE", "COMMUNICATIONS", "ENVIRONMENT", "SYSTEMS", "LOGS" }; double ih = Math.Min(g.U(g.Touch ? 36 : 31), (r.Height - g.U(4)) / n.Length);
        g.Fill(r, g.P.Panel, 0.6); g.Frame(r, g.P.Primary, 1, 0.5);
        for (int i = 0; i < n.Length; i++)
        {
            var rc = new Rect(r.X + g.U(4), r.Y + g.U(4) + i * ih, r.Width - g.U(8), ih - g.U(3)); bool sel = i == _nav;
            if (sel) { g.Fill(rc, g.P.Primary, 0.2); g.Frame(rc, g.P.Primary, 1.4, 0.9); }
            g.Circle(rc.X + g.U(11), rc.Center.Y, g.U(5), sel ? g.P.Accent : g.P.Primary, 1.2, sel ? 1 : 0.6); g.Disc(rc.X + g.U(11), rc.Center.Y, g.U(1.6), sel ? g.P.Accent : g.P.Primary, 0.8);
            g.Text(n[i], rc.X + g.U(22), rc.Y + (rc.Height - g.U(9)) / 2, 7.5, sel ? 0xFFFFFFFF : g.P.Text, Align.Left, sel, sel ? 1 : 0.8);
            g.AddHit("ml:nav:" + i, rc, HitKind.Button);
        }
    }

    // ---------------------------------------------------------------- shared visuals
    void Ring(Gfx g, double cx, double cy, double rad, double v, string label, uint? col = null)
    {
        uint c = col ?? g.P.Primary; g.Circle(cx, cy, rad, c, 3, 0.15); g.Arc(cx, cy, rad, -90, 360 * Math.Clamp(v, 0, 1), c, 3.2, 1);
        g.Text($"{v * 100:0}%", cx, cy - rad * 0.32, Math.Clamp(rad * 0.55 / g.Scale, 8, 22), g.P.Accent, Align.Center, true); g.Text(label, cx, cy + rad + g.U(4), 7, g.P.Text, Align.Center, true, 0.85);
    }

    void Jet(Gfx g, double cx, double cy, double s, uint c, double rot = -20)
    {
        var p = new (double x, double y)[] { (1, 0), (0.3, 0.1), (0.05, 0.55), (-0.15, 0.58), (-0.1, 0.12), (-0.55, 0.1), (-0.7, 0.3), (-0.8, 0.28), (-0.78, 0), (-0.8, -0.28), (-0.7, -0.3), (-0.55, -0.1), (-0.1, -0.12), (-0.15, -0.58), (0.05, -0.55), (0.3, -0.1) };
        double a = rot * Math.PI / 180; var pts = p.Select(q => new Point(cx + (q.x * Math.Cos(a) - q.y * Math.Sin(a)) * s, cy + (q.x * Math.Sin(a) + q.y * Math.Cos(a)) * s)).ToList();
        g.PolyFill(pts, c, 0.18, c, 1.2, 0.9);
    }

    void Wave(Gfx g, Rect r, uint c, double amp = 1, int seed = 0)
    {
        var pts = new List<Point>(); int n = Math.Max(24, (int)(r.Width / g.U(2)));
        for (int i = 0; i < n; i++) { double x = i / (double)(n - 1), env = 0.35 + 0.65 * Math.Abs(Math.Sin(x * 9 + _t2 * 0.8 + seed)); double v = ((Hh((uint)(i * 31 + (int)(_t2 * 22) + seed * 7)) % 1000) / 500.0 - 1) * env * amp; pts.Add(new Point(r.X + r.Width * x, r.Center.Y - v * r.Height * 0.45)); }
        if (g.Glow) g.Poly(pts, c, 3.2, 0.14); g.Poly(pts, c, 1.1, 0.95);
    }

    void DotMap(Gfx g, Rect r, bool links, uint? col = null)
    {
        uint c = col ?? g.P.Primary;
        g.FlatMap(r, 10, 12, 1.0, c, false, false); g.Fill(r, g.P.Background, 0.35);
        var pts = Hubs.Select(h => World.Project(r, h.lon, h.lat, 10, 12, 1.0)).ToArray();
        if (links) for (int i = 0; i < pts.Length - 1; i++) { var a = pts[i]; var b = pts[(i * 3 + 2) % pts.Length]; var m = new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2 - g.U(10)); var arc = new List<Point>(); for (int k = 0; k <= 10; k++) { double t = k / 10.0; arc.Add(new Point((1 - t) * (1 - t) * a.X + 2 * (1 - t) * t * m.X + t * t * b.X, (1 - t) * (1 - t) * a.Y + 2 * (1 - t) * t * m.Y + t * t * b.Y)); } g.Poly(arc, g.P.Accent, 1, 0.55); }
        for (int i = 0; i < pts.Length; i++) { g.Disc(pts[i].X, pts[i].Y, g.U(2.4), c, 0.5 + 0.5 * g.Pulse(0.7, i)); if (g.Glow) g.Disc(pts[i].X, pts[i].Y, g.U(5), c, 0.12); }
    }

    // ---------------------------------------------------------------- Phantom panels
    void PnlSats(Gfx g, Rect r)
    {
        double ow = r.Width * 0.3; var t = new Rect(r.X, r.Y, r.Width - ow - g.U(3), r.Height);
        var rows = _sats.Select(s => new[] { s.Id, s.Orbit, $"{s.Alt:N0} km", $"{s.Vel:0.00} km/s" }).ToList();
        g.Table(t, new[] { "ID", "ORBIT", "ALT", "VEL" }, new[] { 1.1, 0.7, 1.1, 1.1 }, rows, 0, _selSat, "ml:sat:", (ri, ci) => ci == 1 ? (_sats[ri].Orbit == "GEO" ? g.P.Warning : null) : null, 7.5);
        double cx = r.Right - ow / 2, cy = r.Center.Y, rad = Math.Min(ow * 0.4, r.Height * 0.3); g.Disc(cx, cy, g.U(5), g.P.Secondary, 0.7);
        for (int k = 0; k < 3; k++) { g.Rotated(cx, cy, 25 + k * 50, () => g.Dc.DrawEllipse(null, g.Pn(g.P.Primary, 1, 0.5), new Point(cx, cy), rad, rad * 0.35)); }
        var s = _sats[_selSat]; g.Disc(cx + Math.Cos(s.Phase) * rad, cy + Math.Sin(s.Phase) * rad * 0.35, g.U(2.6), g.P.Accent);
    }

    void PnlAir(Gfx g, Rect r)
    {
        (string n, int c, int alt, int spd)[] a = { ("F-22", 12, 28000, 523), ("F-35", 18, 32000, 487), ("AWACS", 2, 41000, 402), ("TANKER", 4, 34000, 455) };
        double lh = Math.Min(g.U(26), r.Height / 5); g.Text("TYPE    CNT    ALT         SPD", r.X + g.U(2), r.Y, 7, g.P.Primary, Align.Left, true, 0.9);
        for (int i = 0; i < 4; i++) { double y = r.Y + (i + 1) * lh; g.Text(a[i].n, r.X + g.U(2), y, 8, g.P.Text, Align.Left, true); g.Text($"{a[i].c}", r.X + r.Width * 0.27, y, 8, g.P.Text, Align.Left); g.Text($"{a[i].alt:N0} ft", r.X + r.Width * 0.4, y, 8, g.P.Text, Align.Left); g.Text($"{a[i].spd + (int)(Math.Sin(_t2 + i) * 6)} kts", r.X + r.Width * 0.62, y, 8, g.P.Text, Align.Left); }
        Jet(g, r.Right - r.Width * 0.14, r.Y + r.Height * 0.3, r.Width * 0.12, g.P.Primary, -25); Jet(g, r.Right - r.Width * 0.12, r.Y + r.Height * 0.75, r.Width * 0.09, g.P.Secondary, 160);
    }

    void PnlAlerts(Gfx g, Rect r)
    {
        g.Text("GLOBAL THREAT LEVEL", r.X, r.Y, 8, g.P.Primary, Align.Left, true); var bar = new Rect(r.X, r.Y + g.U(13), r.Width, g.U(8)); g.Fill(bar, g.P.Primary, 0.1); g.Frame(bar, g.P.Primary, 1, 0.5);
        uint tc = _threat >= 3 ? 0xFFFF3B2F : _threat == 2 ? g.P.Warning : g.P.Primary; g.Fill(new Rect(bar.X + 1, bar.Y + 1, (bar.Width - 2) * (_threat + 1) / 4.0, bar.Height - 2), tc, 0.85);
        g.Text(Threat[_threat], r.Right, r.Y + g.U(23), 7.5, tc, Align.Right, true);
        g.Text("ACTIVE ALERTS", r.X, r.Y + g.U(26), 8, g.P.Primary, Align.Left, true);
        double lh = Math.Min(g.U(g.Touch ? 26 : 19), (r.Height - g.U(40)) / Math.Max(1, Math.Min(5, _alerts.Count)));
        for (int i = 0; i < Math.Min(5, _alerts.Count); i++)
        {
            var a = _alerts[i]; double y = r.Y + g.U(40) + i * lh; uint c = a.Ack ? 0xFF507060 : a.Sev >= 3 ? 0xFFFF3B2F : a.Sev == 2 ? 0xFFFF6A3A : g.P.Warning;
            g.Disc(r.X + g.U(5), y + g.U(5), g.U(3.4), c, a.Ack ? 0.4 : 0.6 + 0.4 * g.Pulse(1.5, i)); g.Text(a.Text, r.X + g.U(14), y, 8, a.Ack ? g.P.Text : g.P.Accent, Align.Left, !a.Ack, a.Ack ? 0.45 : 1);
            g.AddHit("ml:al:" + i, new Rect(r.X, y - 1, r.Width, lh), HitKind.Button);
        }
    }

    void PnlTimeline(Gfx g, Rect r)
    {
        double lh = Math.Min(g.U(15), r.Height / Math.Max(1, _timeline.Count));
        for (int i = 0; i < Math.Min(_timeline.Count, (int)(r.Height / lh)); i++) { double y = r.Y + i * lh; g.Text(_timeline[i].t, r.X, y, 8, i == 0 ? g.P.Warning : g.P.Primary, Align.Left, i == 0); g.Text(_timeline[i].m, r.X + g.U(40), y, 8, i == 0 ? g.P.Accent : g.P.Text, Align.Left, i == 0, i == 0 ? 1 : 0.8); }
    }

    void PnlNet(Gfx g, Rect r)
    {
        var gr = new Rect(r.X, r.Y, r.Width * 0.5, r.Height); var pts = new Point[10]; for (int i = 0; i < 10; i++) pts[i] = new Point(gr.X + gr.Width * (0.1 + 0.8 * ((Hh((uint)i * 5) % 100) / 100.0)), gr.Y + gr.Height * (0.1 + 0.8 * ((Hh((uint)i * 9 + 3) % 100) / 100.0)));
        for (int i = 0; i < 10; i++) for (int j = i + 1; j < 10; j++) if (Math.Abs(pts[i].X - pts[j].X) + Math.Abs(pts[i].Y - pts[j].Y) < gr.Width * 0.75) g.Line(pts[i].X, pts[i].Y, pts[j].X, pts[j].Y, g.P.Primary, 1, 0.3);
        for (int i = 0; i < 10; i++) g.Disc(pts[i].X, pts[i].Y, g.U(i % 3 == 0 ? 3.4 : 2.4), i % 3 == 0 ? g.P.Accent : g.P.Primary, 0.6 + 0.4 * g.Pulse(0.8, i));
        g.KvGrid(new Rect(r.X + r.Width * 0.52, r.Y + g.U(6), r.Width * 0.48, r.Height), new (string, string, uint?)[] { ("SATELLITES", "24/36", null), ("GROUND STATIONS", "6/8", null), ("LINKS", $"{12 + (int)(_comms * 4 - 3)}/16", null), ("BANDWIDTH", $"{_comms * 80:0}%", null) }, 1, 7.5);
    }

    void PnlSystems(Gfx g, Rect r)
    {
        double rad = Math.Min(r.Width * 0.2, r.Height * 0.2); var v = new[] { _power, _cooling, _comms, _storage }; string[] l = { "POWER", "COOLING", "COMMS", "STORAGE" };
        for (int i = 0; i < 4; i++) { double cx = r.X + r.Width * (0.27 + 0.46 * (i % 2)), cy = r.Y + r.Height * (0.25 + 0.5 * (i / 2)); Ring(g, cx, cy, rad, v[i], l[i]); }
    }

    void MiniGauge(Gfx g, double cx, double cy, double rad, string lab, string val, double frac)
    {
        g.Circle(cx, cy, rad, g.P.Primary, 1.4, 0.8); g.Arc(cx, cy, rad - g.U(2), 130, 280 * Math.Clamp(frac, 0, 1), g.P.Accent, 2, 0.9);
        g.Text(lab, cx + rad + g.U(5), cy - g.U(9), 6.5, g.P.Text, Align.Left, false, 0.7); g.Text(val, cx + rad + g.U(5), cy + g.U(1), 9, g.P.Accent, Align.Left, true);
    }

    void PnlEnv(Gfx g, Rect r)
    {
        double rad = Math.Min(r.Height * 0.2, r.Width * 0.1);
        MiniGauge(g, r.X + rad + g.U(2), r.Y + r.Height * 0.28, rad, "TEMP", $"{_temp:0.0}°C", _temp / 40); MiniGauge(g, r.X + r.Width * 0.5 + rad, r.Y + r.Height * 0.28, rad, "PRESSURE", $"{_press:0}", (_press - 950) / 100);
        MiniGauge(g, r.X + rad + g.U(2), r.Y + r.Height * 0.74, rad, "HUMIDITY", $"{_hum:0}%", _hum / 100); MiniGauge(g, r.X + r.Width * 0.5 + rad, r.Y + r.Height * 0.74, rad, "RADIATION", $"{_rad:0.00}", _rad * 4);
    }

    void PnlComms(Gfx g, Rect r)
    {
        var w = new Rect(r.X, r.Y, r.Width * 0.62, r.Height * 0.33); Wave(g, w, g.P.Primary, 0.9);
        g.KvGrid(new Rect(r.X + r.Width * 0.64, r.Y, r.Width * 0.36, r.Height * 0.33), new (string, string, uint?)[] { ("CHANNEL 1", "SATCOM", null), ("FREQUENCY", "8.45 GHz", null), ("SIGNAL", $"{_comms * 94:0}%", null), ("LATENCY", $"{12 + (int)(Math.Sin(_t2) * 2)} ms", null) }, 1, 6.5);
        g.Text("ENCRYPTED // SATCOM", r.X, r.Y + r.Height * 0.34, 7, g.P.Accent, Align.Left, true, 0.9);
        DotMap(g, new Rect(r.X, r.Y + r.Height * 0.4, r.Width, r.Height * 0.6), true);
    }

    // ---------------------------------------------------------------- globe (phantom + titan)
    void Globe(Gfx g, Rect r, bool city)
    {
        _globeR = r; g.AddHit("ml:globe", r);
        double tabs = city ? 0 : g.U(22); var a = new Rect(r.X, r.Y, r.Width, r.Height - tabs);
        double cx = a.Center.X, cy = a.Center.Y - (city ? g.U(20) : 0), rad = Math.Min(a.Width * 0.45, a.Height * (city ? 0.34 : 0.42)) * _zoom;
        g.HoloBase(a, cx, a.Bottom - g.U(city ? 22 : 8), rad * 1.7);
        if (g.Glow) { g.Circle(cx, cy, rad * 1.06, g.P.Primary, 3, 0.25); g.Circle(cx, cy, rad * 1.14, g.P.Primary, 1, 0.12); }
        g.Globe(cx, cy, rad, _rot + _t2 * 3, _tilt, 0xFF061A30, g.P.Secondary, _view == 2 || _view == 1);
        var clip = new EllipseGeometry(new Rect(cx - rad, cy - rad, rad * 2, rad * 2));
        if (_view == 3) using (g.Dc.PushGeometryClip(clip)) g.Fill(new Rect(cx - rad, cy - rad, rad * 2, rad * 2), 0xFFFF6020, 0.28);
        if (_view == 4) using (g.Dc.PushGeometryClip(clip)) g.Fill(new Rect(cx - rad, cy - rad, rad * 2, rad * 2), 0xFF30FF50, 0.22);
        // network hubs and links
        var pr = Hubs.Select(h => World.Ortho(cx, cy, rad, h.lon, h.lat, _rot + _t2 * 3, _tilt)).ToArray();
        for (int i = 0; i < pr.Length; i++) { var (pa, za) = pr[i]; var (pb, zb) = pr[(i * 3 + 2) % pr.Length]; if (za > 0.05 && zb > 0.05) { var m = new Point((pa.X + pb.X) / 2 + (cx - (pa.X + pb.X) / 2) * -0.0, (pa.Y + pb.Y) / 2 - rad * 0.12); var arc = new List<Point>(); for (int k = 0; k <= 12; k++) { double t = k / 12.0; arc.Add(new Point((1 - t) * (1 - t) * pa.X + 2 * (1 - t) * t * m.X + t * t * pb.X, (1 - t) * (1 - t) * pa.Y + 2 * (1 - t) * t * m.Y + t * t * pb.Y)); } g.Poly(arc, g.P.Accent, 1, 0.5); double f = (_t2 * 0.3 + i * 0.13) % 1; var q = arc[(int)(f * 12)]; g.Disc(q.X, q.Y, g.U(1.6), 0xFFFFFFFF, 0.9); } }
        for (int i = 0; i < pr.Length; i++) { var (p, z) = pr[i]; if (z <= 0.05) continue; g.Disc(p.X, p.Y, g.U(3), i % 4 == 0 ? 0xFFFF4A3A : g.P.Accent, 0.9); if (g.Glow) g.Disc(p.X, p.Y, g.U(7), i % 4 == 0 ? 0xFFFF4A3A : g.P.Primary, 0.15); }
        // satellite orbits
        if (_view != 0) for (int i = 0; i < _sats.Count; i++)
            {
                var s = _sats[i]; double orad = rad * (1.12 + s.Alt / 60000.0 * 0.5), inc = s.Inc * Math.PI / 180;
                g.Rotated(cx, cy, s.Inc, () => g.Dc.DrawEllipse(null, g.Pn(g.P.Primary, 1, i == _selSat ? 0.7 : 0.25), new Point(cx, cy), orad, orad * 0.34));
                double ex = Math.Cos(s.Phase) * orad, ey = Math.Sin(s.Phase) * orad * 0.34, rx = ex * Math.Cos(inc) - ey * Math.Sin(inc), ry = ex * Math.Sin(inc) + ey * Math.Cos(inc);
                g.Disc(cx + rx, cy + ry, g.U(i == _selSat ? 3.6 : 2.4), i == _selSat ? g.P.Warning : g.P.Accent); if (i == _selSat) { g.GlowCircle(cx + rx, cy + ry, g.U(8), g.P.Warning, 1.2); g.Text(s.Id, cx + rx + g.U(10), cy + ry - g.U(10), 8, g.P.Warning, Align.Left, true); g.Text($"ALT {s.Alt:N0} KM", cx + rx + g.U(10), cy + ry, 7, g.P.Text, Align.Left); }
                g.AddHit("ml:sat:" + i, new Rect(cx + rx - g.U(9), cy + ry - g.U(9), g.U(18), g.U(18)));
            }
        if (city) DrawCity(g, a, cx, a.Bottom - g.U(26), rad);
        g.HoloShimmer(new Rect(cx - rad * 1.05, cy - rad * 1.05, rad * 2.1, rad * 2.1), null, true);
        var s2 = _sats[_selSat]; g.Fill(new Rect(r.Right - g.U(112), r.Y + g.U(4), g.U(108), g.U(34)), g.P.Background, 0.7); g.Frame(new Rect(r.Right - g.U(112), r.Y + g.U(4), g.U(108), g.U(34)), g.P.Primary, 1, 0.5);
        g.Text($"LAT {45.7631 + Math.Sin(s2.Phase) * 20:0.0000}° N", r.Right - g.U(108), r.Y + g.U(7), 7, g.P.Text, Align.Left); g.Text($"LON {12.9865 + Math.Cos(s2.Phase) * 60:0.0000}° E", r.Right - g.U(108), r.Y + g.U(17), 7, g.P.Text, Align.Left); g.Text($"ALT {s2.Alt:0} km", r.Right - g.U(108), r.Y + g.U(27), 7, g.P.Text, Align.Left);
        if (!city) { string[] tn = { "EARTH VIEW", "SATELLITE VIEW", "TOPOGRAPHY", "THERMAL", "NIGHT VISION" }; double tw = a.Width / 5; for (int i = 0; i < 5; i++) g.Button("ml:view:" + i, new Rect(r.X + i * tw, r.Bottom - tabs + g.U(2), tw - g.U(2), tabs - g.U(3)), tn[i], _view == i, true, null, tw < g.U(70) ? 5.8 : 7); }
    }

    void DrawCity(Gfx g, Rect a, double cx, double baseY, double rad)
    {
        for (int i = 0; i < 46; i++)
        {
            uint h = Hh((uint)i * 91); double gx = ((h % 100) / 100.0 - 0.5) * rad * 1.9, gz = (((h >> 8) % 100) / 100.0) * rad * 0.34; double bw = g.U(5 + (h >> 16) % 8), bh = g.U(6 + (h >> 20) % 26) * (0.7 + 0.3 * Math.Sin(_t2 * 0.5 + i));
            double x = cx + gx, y = baseY - gz * 0.6; uint c = (h & 1) == 0 ? g.P.Primary : g.P.Accent;
            g.PolyFill(new[] { new Point(x, y), new Point(x + bw, y - bw * 0.4), new Point(x + bw, y - bw * 0.4 - bh), new Point(x, y - bh) }, c, 0.22, c, 1, 0.8);
            g.PolyFill(new[] { new Point(x, y - bh), new Point(x + bw, y - bw * 0.4 - bh), new Point(x, y - bh - bw * 0.4), new Point(x - bw, y - bw * 0.4 - bh) }, c, 0.32, c, 1, 0.9);
        }
    }

    // ---------------------------------------------------------------- Shadow panels
    void Cam(Gfx g, Rect r, int idx)
    {
        uint hh = (uint)(idx * 7919 + 5); g.Fill(r, 0xFF05080A, 1);
        double hz = r.Y + r.Height * 0.55; bool rec = _rec4[idx];
        switch (idx)
        {
            case 0: g.Fill(new Rect(r.X, hz, r.Width, r.Bottom - hz), 0xFF141A1A, 1); g.Fill(new Rect(r.X + r.Width * 0.12, r.Y + r.Height * 0.15, r.Width * 0.76, hz - r.Y - r.Height * 0.15), 0xFF1C2222, 1);
                for (int i = 0; i < 12; i++) for (int j = 0; j < 3; j++) { bool lit = Hh(hh + (uint)(i * 17 + j * 5)) % 3 != 0; g.Fill(new Rect(r.X + r.Width * (0.16 + i * 0.058), r.Y + r.Height * (0.22 + j * 0.12), r.Width * 0.04, r.Height * 0.08), lit ? 0xFFE8D090 : 0xFF202626, lit ? 0.7 : 1); }
                g.PolyFill(new[] { new Point(r.X + r.Width * 0.45, hz), new Point(r.X + r.Width * 0.55, hz), new Point(r.X + r.Width * 0.7, r.Bottom), new Point(r.X + r.Width * 0.3, r.Bottom) }, 0xFFE8D090, 0.10); break;
            case 1: g.Fill(new Rect(r.X, hz, r.Width, r.Bottom - hz), 0xFF121618, 1); for (int i = -4; i <= 4; i++) g.Line(r.Center.X + i * r.Width * 0.02, hz, r.Center.X + i * r.Width * 0.22, r.Bottom, 0xFFE8E8E8, 1, 0.3);
                for (int i = 0; i < 16; i++) g.Disc(r.X + r.Width * i / 15.0, hz + g.U(1), g.U(1.4), i % 2 == 0 ? 0xFF4A9AFF : 0xFFFFE070, 0.9);
                double px = r.X + r.Width * ((_t2 * 0.03) % 1.3 - 0.15); Jet(g, px, hz + r.Height * 0.2, r.Width * 0.14, 0xFFC8D4E0, 5); break;
            case 2: for (int i = 0; i < 9; i++) { double bw = r.Width * (0.08 + (Hh(hh + (uint)i) % 5) * 0.02), bx = r.X + i * r.Width / 9.0, bh = r.Height * (0.25 + (Hh(hh + (uint)i * 3) % 6) * 0.08); g.Fill(new Rect(bx, hz - bh, bw, bh), 0xFF1A2024, 1); for (int k = 0; k < 14; k++) if (Hh(hh + (uint)(i * 31 + k)) % 3 == 0) g.Fill(new Rect(bx + bw * (0.15 + (k % 3) * 0.28), hz - bh + (k / 3) * bh / 5.2 + 2, bw * 0.16, bh * 0.07), 0xFFE0D090, 0.8); }
                g.Fill(new Rect(r.X, hz, r.Width, r.Bottom - hz), 0xFF0E1214, 1); for (int i = 0; i < 6; i++) { double t = (_t2 * 0.25 + i * 0.17) % 1; g.Line(r.Center.X + (i % 2 == 0 ? -1 : 1) * t * r.Width * 0.3, hz + t * (r.Bottom - hz), r.Center.X + (i % 2 == 0 ? -1 : 1) * t * r.Width * 0.3 + g.U(7) * t, hz + t * (r.Bottom - hz), i % 2 == 0 ? 0xFFFFE8A0 : 0xFFFF4040, 2, 0.9); } break;
            default: g.Fill(new Rect(r.X, hz, r.Width, r.Bottom - hz), 0xFF121614, 1); g.PolyFill(new[] { new Point(r.X + r.Width * 0.15, hz), new Point(r.X + r.Width * 0.3, r.Y + r.Height * 0.25), new Point(r.X + r.Width * 0.7, r.Y + r.Height * 0.25), new Point(r.X + r.Width * 0.85, hz) }, 0xFF222A2A, 1);
                for (int i = 0; i < 3; i++) g.PolyFill(new[] { new Point(r.X + r.Width * (0.25 + i * 0.25), r.Y + r.Height * 0.2), new Point(r.X + r.Width * (0.2 + i * 0.25), hz), new Point(r.X + r.Width * (0.3 + i * 0.25), hz) }, 0xFFE8E0C0, 0.12); break;
        }
        for (int i = 0; i < 40; i++) g.Fill(new Rect(r.X + Rng.NextDouble() * r.Width, r.Y + Rng.NextDouble() * r.Height, g.U(1), g.U(1)), 0xFFFFFFFF, 0.2);
        var vg = new RadialGradientBrush { GradientStops = { new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.6), new GradientStop(Color.FromArgb(150, 0, 0, 0), 1) } }; g.Dc.DrawRectangle(vg, null, r);
        g.Text(DateTime.Now.ToString("HH:mm:ss"), r.X + g.U(4), r.Bottom - g.U(11), 7, g.P.Accent, Align.Left, false, 0.9);
        if (rec) { g.Disc(r.Right - g.U(30), r.Y + g.U(6), g.U(3), 0xFFFF3030, g.Pulse(1) > 0.4 ? 1 : 0.2); g.Text("REC", r.Right - g.U(4), r.Y + g.U(1), 7, 0xFFFF5050, Align.Right, true); }
        if (_camSel == idx) g.Frame(r, g.P.Accent, 2, 0.9);
        g.AddHit("ml:cam:" + idx, r);
    }

    void PnlIntel(Gfx g, Rect r)
    {
        double lh = Math.Min(g.U(14), r.Height / Math.Max(1, _intel.Count));
        for (int i = 0; i < _intel.Count && (i + 1) * lh <= r.Height + 2; i++) { var s = _intel[i]; uint c = s.StartsWith("ALERT") ? g.P.Warning : s.StartsWith("SIG") ? g.P.Accent : g.P.Text; g.Disc(r.X + g.U(3), r.Y + i * lh + g.U(5), g.U(2.2), c, 0.9); g.Text(s, r.X + g.U(10), r.Y + i * lh, 7, c, Align.Left, false, 0.92); }
    }

    void PnlSignal(Gfx g, Rect r)
    {
        var b = new Rect(r.X, r.Y + g.U(10), r.Width, r.Height - g.U(24)); int n = Math.Max(20, (int)(b.Width / g.U(3)));
        for (int i = 0; i < n; i++)
        {
            double x = i / (double)n, v = 0.12 + 0.2 * ((Hh((uint)(i * 13 + (int)(_t2 * 14))) % 100) / 100.0) + 0.7 * Math.Exp(-Math.Pow((x - 0.28) / 0.03, 2)) * (0.7 + 0.3 * Math.Sin(_t2 * 3)) + 0.55 * Math.Exp(-Math.Pow((x - 0.62) / 0.025, 2)); bool hot = x > 0.57 && x < 0.67;
            g.Fill(new Rect(b.X + i * b.Width / n, b.Bottom - v * b.Height, b.Width / n - 1, v * b.Height), hot ? 0xFFFF3B2F : (x > 0.2 && x < 0.35 ? 0xFF30E060 : g.P.Primary), 0.8);
        }
        g.Text("THREAT", r.Right, r.Y, 7, 0xFFFF5050, Align.Right, true); for (int i = 0; i < 4; i++) g.Text(new[] { "2.4", "5.8", "12.4", "24.0" }[i] + " GHz", b.X + b.Width * i / 4, r.Bottom - g.U(11), 6, g.P.Text, Align.Left, false, 0.7);
    }

    void PnlDrone(Gfx g, Rect r) { g.Image(Sprites.Terrain(4, 1), new Rect(r.X, r.Y, r.Width * 0.6, r.Height), 0.95); Reticle(g, new Rect(r.X, r.Y, r.Width * 0.6, r.Height)); g.KvGrid(new Rect(r.X + r.Width * 0.62, r.Y, r.Width * 0.38, r.Height), new (string, string, uint?)[] { ("LAT", "34.7281° N", null), ("LON", "118.1234° E", null), ("ALT", "1,243 m", null), ("SPD", $"{105 + (int)(Math.Sin(_t2) * 4)} km/h", null) }, 1, 6.5); }

    void Reticle(Gfx g, Rect r) { double cx = r.Center.X, cy = r.Center.Y, s = Math.Min(r.Width, r.Height) * 0.18; g.Circle(cx, cy, s, g.P.Accent, 1.2, 0.8); g.Line(cx - s * 1.6, cy, cx - s * 0.5, cy, g.P.Accent, 1, 0.8); g.Line(cx + s * 0.5, cy, cx + s * 1.6, cy, g.P.Accent, 1, 0.8); g.Line(cx, cy - s * 1.6, cx, cy - s * 0.5, g.P.Accent, 1, 0.8); g.Line(cx, cy + s * 0.5, cx, cy + s * 1.6, g.P.Accent, 1, 0.8); g.Brackets(r.Deflate(g.U(3)), g.P.Accent, 6, 1, 0.6); }

    void PnlMap(Gfx g, Rect r)
    {
        _mapR = r; g.AddHit("ml:map", r);
        var m = new Rect(r.X, r.Y, r.Width, r.Height - g.U(46)); var mapClip = g.Clip(m);
        g.Image(Sprites.Terrain(7, _baseMap == 1 ? 1 : _baseMap == 3 ? 2 : 0), m, 1);
        if (_baseMap == 2) g.Fill(m, 0xFFFF6A20, 0.28);
        for (int i = 1; i < 10; i++) { g.Line(m.X + m.Width * i / 10, m.Y, m.X + m.Width * i / 10, m.Bottom, g.P.Primary, 1, 0.1); g.Line(m.X, m.Y + m.Height * i / 10, m.Right, m.Y + m.Height * i / 10, g.P.Primary, 1, 0.1); }
        if (_layer[5]) foreach (var u in _units.Where(u => !u.Hostile && u.Kind != "TGT")) { var a = new Point(m.X + u.X * m.Width, m.Y + u.Y * m.Height); var b = new Point(m.X + u.TX * m.Width, m.Y + u.TY * m.Height); g.Dc.DrawLine(g.Pn(g.P.Accent, 1.2, 0.6, g.U(3)), a, b); }
        for (int i = 0; i < _units.Count; i++)
        {
            var u = _units[i]; var p = new Point(m.X + u.X * m.Width, m.Y + u.Y * m.Height); uint c = u.Hostile ? 0xFFFF3B2F : g.P.Primary; bool sel = i == _selUnit;
            if (u.Hostile && _layer[4]) { g.Circle(p.X, p.Y, g.U(16 + 3 * g.Pulse(1, i)), 0xFFFF3B2F, 1, 0.5, g.U(3)); g.Disc(p.X, p.Y, g.U(16), 0xFFFF3B2F, 0.07); }
            g.Fill(new Rect(p.X - g.U(5), p.Y - g.U(4), g.U(10), g.U(8)), c, 0.4); g.Frame(new Rect(p.X - g.U(5), p.Y - g.U(4), g.U(10), g.U(8)), c, 1.4);
            if (u.Kind == "TGT") { g.GlowCircle(p.X, p.Y, g.U(13), 0xFFFF3B2F, 1.6); g.Text("TARGET ALPHA", p.X + g.U(14), p.Y - g.U(16), 8.5, 0xFFFF5A4A, Align.Left, true); g.Text("34.7281° N  118.1234° E", p.X + g.U(14), p.Y - g.U(6), 7, g.P.Text, Align.Left); g.Text("SPD 105 kts", p.X + g.U(14), p.Y + g.U(3), 7, g.P.Text, Align.Left); }
            if (sel) g.GlowCircle(p.X, p.Y, g.U(11), g.P.Accent, 1.3);
            if (sel || i < 3) g.Text(u.Id, p.X + g.U(8), p.Y + g.U(4), 7, sel ? g.P.Accent : g.P.Text, Align.Left, sel, 0.85);
            g.AddHit("ml:unit:" + i, new Rect(p.X - g.U(10), p.Y - g.U(10), g.U(20), g.U(20)));
        }
        if (_radarOn) { var rp = new Point(m.X + m.Width * 0.5, m.Y + m.Height * 0.5); for (int k = 0; k < 14; k++) { double a = (_sweep - k * 2.2) * Math.PI / 180; g.Line(rp.X, rp.Y, rp.X + Math.Cos(a) * m.Width * 0.5, rp.Y + Math.Sin(a) * m.Width * 0.5, g.P.Primary, 1.4, 0.2 * (1 - k / 14.0)); } }
        // layers legend
        var lg = new Rect(m.X + g.U(5), m.Y + g.U(5), g.U(76), g.U(76)); g.Fill(lg, g.P.Background, 0.7); g.Frame(lg, g.P.Primary, 1, 0.5); g.Text("MAP LAYERS", lg.X + g.U(4), lg.Y + g.U(2), 6.5, g.P.Primary, Align.Left, true);
        string[] ln = { "Terrain", "Satellite", "Thermal", "Topography", "Threats", "Routes" };
        for (int i = 0; i < 6; i++) { bool on = i < 4 ? _baseMap == i : _layer[i]; double y = lg.Y + g.U(12) + i * g.U(10.5); g.Frame(new Rect(lg.X + g.U(4), y + 1, g.U(7), g.U(7)), g.P.Primary, 1, 0.8); if (on) g.Fill(new Rect(lg.X + g.U(5.5), y + 2.5, g.U(4), g.U(4)), g.P.Accent, 1); g.Text(ln[i], lg.X + g.U(15), y, 7, on ? g.P.Accent : g.P.Text, Align.Left, on, on ? 1 : 0.7); g.AddHit("ml:lay:" + i, new Rect(lg.X, y - 1, lg.Width, g.U(10.5)), HitKind.Button); }
        g.Text($"ZOOM {4.2 * _zoom:0.0}x", m.Right - g.U(5), m.Bottom - g.U(12), 8, g.P.Accent, Align.Right, true);
        mapClip.Dispose();
        // thumbnails
        double th = g.U(40), tw = (r.Width - g.U(12)) / 3;
        for (int i = 0; i < 3; i++) { var tr = new Rect(r.X + i * (tw + g.U(4)), r.Bottom - th - g.U(2), tw, th); g.Image(Sprites.Terrain(10 + i, 1), tr, 0.95); g.Frame(tr, g.P.Primary, 1, 0.6); g.Brackets(tr.Deflate(g.U(2)), g.P.Accent, 5, 1, 0.7); }
    }

    void PnlImagery(Gfx g, Rect r) { g.Image(Sprites.Terrain(21, 1), r, 1); Reticle(g, r); g.Text("RES 0.5 m", r.Right - g.U(4), r.Bottom - g.U(11), 7, g.P.Accent, Align.Right, true); g.Text("LIVE", r.X + g.U(4), r.Y + g.U(2), 7, 0xFFFF5050, Align.Left, true); }

    void PnlWeather(Gfx g, Rect r)
    {
        g.Fill(r, 0xFF05100E, 1); g.FlatMap(r, 10, 20, 1.5, g.P.Secondary, false, false); g.Fill(r, g.P.Background, 0.45);
        double dx = (_t2 * 1.5) % r.Width; g.Image(Sprites.Weather(5), new Rect(r.X - dx * 0.0, r.Y, r.Width, r.Height), 0.95);
        string[] l = { "10", "20", "30", "40" }; uint[] c = { 0xFF28BE46, 0xFFFAE628, 0xFFFF9614, 0xFFF01E1E }; for (int i = 0; i < 4; i++) g.Fill(new Rect(r.X + g.U(3) + i * g.U(10), r.Bottom - g.U(8), g.U(9), g.U(4)), c[i], 0.95);
    }

    void PnlTerrain(Gfx g, Rect r)
    {
        int rows = 14; for (int j = 0; j < rows; j++)
        {
            var pts = new List<Point>(); double yy = r.Y + r.Height * (0.3 + 0.55 * j / rows); double persp = 0.55 + 0.45 * j / rows;
            for (int i = 0; i <= 40; i++) { double x = i / 40.0; double h = Noise.Fbm((float)(x * 3.2 + _t2 * 0.02), j * 0.19f, 3, 4); pts.Add(new Point(r.X + r.Width * (0.5 + (x - 0.5) * persp), yy - h * r.Height * 0.4 * persp)); }
            g.Poly(pts, j % 2 == 0 ? g.P.Primary : g.P.Accent, 1, 0.35 + 0.5 * j / rows);
        }
        g.Text("ELEVATION 2,413 m", r.Right - g.U(3), r.Bottom - g.U(11), 7, g.P.Accent, Align.Right, true);
    }

    void PnlOps(Gfx g, Rect r)
    {
        (string n, double v, string t)[] o = { ("RECON DRONES", _uav / 4.0 * 0.75, "3/4"), ("SATELLITES", 5 / 8.0, "5/8"), ("ANALYSTS", 0.75, "12/16"), ("COMMS", _comms, "8/10"), ("INTELLIGENCE", 0.67, "4/6") }; double lh = Math.Min(g.U(30), r.Height / o.Length);
        for (int i = 0; i < o.Length; i++) { g.Disc(r.X + g.U(3), r.Y + i * lh + lh * 0.45, g.U(2.2), g.P.Primary, 0.9); g.Text(o[i].n, r.X + g.U(9), r.Y + i * lh + lh * 0.12, 6.8, g.P.Text, Align.Left, false, 0.85); g.Text(o[i].t, r.Right, r.Y + i * lh + lh * 0.12, 7, g.P.Accent, Align.Right, true); g.Bar(new Rect(r.X + r.Width * 0.45, r.Y + i * lh + lh * 0.45, r.Width * 0.38, g.U(5)), o[i].v, g.P.Primary, null, null); }
    }

    void PnlThreat(Gfx g, Rect r)
    {
        (string t, int n)[] th = { ("High value target", 1), ("Logistics convoy", 3), ("Comm relay", 3), ("Unknown signal", 2) }; double lh = Math.Min(g.U(30), r.Height / th.Length);
        for (int i = 0; i < th.Length; i++) { g.Disc(r.X + g.U(4), r.Y + i * lh + lh * 0.45, g.U(3.4), 0xFFFF3B2F, 0.6 + 0.4 * g.Pulse(1.3, i)); g.Text(th[i].t, r.X + g.U(12), r.Y + i * lh + lh * 0.15, 7.5, g.P.Text, Align.Left); g.Text(th[i].n.ToString(), r.Right - g.U(4), r.Y + i * lh + lh * 0.15, 8, 0xFFFF6A5A, Align.Right, true); }
    }

    void PnlLog(Gfx g, Rect r) { double lh = g.U(13); for (int i = 0; i < _syslog.Count && (i + 1) * lh <= r.Height + 2; i++) g.Text(_syslog[i], r.X, r.Y + i * lh, 7.2, i == 0 ? g.P.Accent : g.P.Text, Align.Left, i == 0, i == 0 ? 1 : 0.8); }

    void PnlTerminal(Gfx g, Rect r)
    {
        double lh = g.U(11.5); int rows = Math.Max(1, (int)((r.Height - g.U(34)) / lh)); int start = Math.Max(0, _term.Count - rows);
        for (int i = start; i < _term.Count; i++) { var l = _term[i]; uint c = l.StartsWith(">") || l.StartsWith("scan") ? g.P.Accent : l.Contains("open") || l.Contains("Target") ? g.P.Primary : g.P.Text; g.Text(l, r.X, r.Y + (i - start) * lh, 7.2, c, Align.Left, l.StartsWith(">"), 0.92); }
        if ((int)(_t2 * 2) % 2 == 0) g.Fill(new Rect(r.X, r.Y + (_term.Count - start) * lh, g.U(5), g.U(8)), g.P.Primary, 0.9);
        var br = new Rect(r.X, r.Bottom - g.U(28), r.Width, g.U(26)); double c2 = g.U(7);
        var poly = new[] { new Point(br.X + c2, br.Y), new Point(br.Right - c2, br.Y), new Point(br.Right, br.Center.Y), new Point(br.Right - c2, br.Bottom), new Point(br.X + c2, br.Bottom), new Point(br.X, br.Center.Y) };
        g.PolyFill(poly, g.P.Primary, _access >= 0 ? 0.35 : 0.12); g.Poly(poly, g.P.Primary, 1.6, 1, true); g.Circle(br.X + g.U(14), br.Center.Y, g.U(5), g.P.Primary, 1.4);
        g.Text("ACCESS GRANTED", br.Center.X + g.U(8), br.Center.Y - g.U(5), 8, g.P.Accent, Align.Center, true); g.AddHit("ml:access", br, HitKind.Button);
    }

    // ---------------------------------------------------------------- Titan panels
    void PnlFleet(Gfx g, Rect r)
    {
        (string n, int a, int t)[] f = { ("FIGHTERS", 24, 32), ("BOMBERS", 6, 8), ("UAVs", 18 + _uav - 2, 24), ("TRANSPORTS", 4, 6), ("TANKERS", 3, 4), ("AWACS", 2, 3) }; double lh = Math.Min(g.U(34), r.Height / f.Length);
        for (int i = 0; i < f.Length; i++) { Jet(g, r.X + g.U(11), r.Y + i * lh + lh * 0.5, g.U(8), g.P.Primary, 0); g.Text(f[i].n, r.X + g.U(26), r.Y + i * lh + lh * 0.25, 7.5, g.P.Text, Align.Left); g.Text($"{f[i].a}/{f[i].t}", r.Right - g.U(2), r.Y + i * lh + lh * 0.25, 8, g.P.Accent, Align.Right, true); }
    }

    void PnlMissionBars(Gfx g, Rect r)
    {
        (string n, double v)[] m = { ("DEFENSE", _def), ("RECON", _rec), ("STRIKE", _str), ("SUPPORT", _sup) }; double lh = Math.Min(g.U(40), r.Height / m.Length);
        for (int i = 0; i < m.Length; i++) { g.Text(m[i].n, r.X, r.Y + i * lh, 7.5, g.P.Text, Align.Left, false, 0.85); g.Text($"{m[i].v * 100:0}%", r.Right, r.Y + i * lh, 8, g.P.Accent, Align.Right, true); g.Bar(new Rect(r.X, r.Y + i * lh + g.U(12), r.Width, g.U(6)), m[i].v, g.P.Primary, null, null); }
    }

    void PnlAero(Gfx g, Rect r)
    {
        var rd = new Rect(r.X, r.Y, r.Width * 0.58, r.Height); double rad = Math.Min(rd.Width, rd.Height) / 2 - g.U(3), cx = rd.Center.X, cy = rd.Center.Y;
        g.Disc(cx, cy, rad, g.P.Background, 0.5); for (int i = 1; i <= 4; i++) g.Circle(cx, cy, rad * i / 4, g.P.Primary, 1, 0.25); g.Line(cx - rad, cy, cx + rad, cy, g.P.Primary, 1, 0.2); g.Line(cx, cy - rad, cx, cy + rad, g.P.Primary, 1, 0.2);
        for (int k = 0; k < 16; k++) { double a = (_sweep - k * 2.5) * Math.PI / 180; g.Line(cx, cy, cx + Math.Cos(a) * rad, cy + Math.Sin(a) * rad, g.P.Primary, 1.4, 0.28 * (1 - k / 16.0)); }
        for (int i = 0; i < 20; i++) { uint h = Hh((uint)i * 37); double a = (h % 360) * Math.PI / 180 + _t2 * 0.01 * (i % 3), d = 0.2 + ((h >> 9) % 70) / 100.0; bool hos = i % 9 == 0, unk = i % 7 == 0; g.Disc(cx + Math.Cos(a) * rad * d, cy + Math.Sin(a) * rad * d, g.U(hos ? 2.6 : 2), hos ? 0xFFFF3B2F : unk ? g.P.Warning : g.P.Primary, 0.9); }
        g.KvGrid(new Rect(r.X + r.Width * 0.6, r.Y, r.Width * 0.4, r.Height), new (string, string, uint?)[] { ("TRACKED OBJECTS", "36", null), ("FRIENDLY", "28", null), ("UNKNOWN", "4", g.P.Warning), ("HOSTILE", "4", 0xFFFF5A4A) }, 1, 7);
    }

    void PnlCommsMini(Gfx g, Rect r) { Wave(g, new Rect(r.X, r.Y, r.Width * 0.62, r.Height), g.P.Primary, 0.9, 2); g.KvGrid(new Rect(r.X + r.Width * 0.64, r.Y, r.Width * 0.36, r.Height), new (string, string, uint?)[] { ("CHANNEL", "SATCOM-1", null), ("FREQUENCY", "14.2 GHz", null), ("SIGNAL", $"{_comms * 94:0}%", null), ("LATENCY", "8 ms", null) }, 1, 6.5); g.Text("ENCRYPTED", r.X, r.Y, 6.5, g.P.Accent, Align.Left, true, 0.8); }

    void PnlEnvRow(Gfx g, Rect r)
    {
        double w = r.Width / 4, rad = Math.Min(w * 0.22, r.Height * 0.28); string[] l = { "TEMP", "PRESSURE", "HUMIDITY", "RADIATION" }; string[] v = { $"{_temp - 4:0.0}°C", $"{_press / 10:0.0} kPa", $"{_hum - 3:0}%", $"{_rad - 0.04:0.00} mSv/h" }; double[] f = { _temp / 40, 0.5, _hum / 100, _rad * 4 };
        for (int i = 0; i < 4; i++) { double cx = r.X + i * w + w * 0.5; g.Circle(cx, r.Y + r.Height * 0.3, rad, g.P.Primary, 1.4, 0.8); g.Arc(cx, r.Y + r.Height * 0.3, rad - g.U(2), 130, 280 * Math.Clamp(f[i], 0, 1), g.P.Accent, 2, 0.9); g.Text(l[i], cx, r.Y + r.Height * 0.3 + rad + g.U(2), 6, g.P.Text, Align.Center, false, 0.7); g.Text(v[i], cx, r.Y + r.Height * 0.3 + rad + g.U(11), 7, g.P.Accent, Align.Center, true); }
    }

    void PnlGlobalNet(Gfx g, Rect r) { DotMap(g, new Rect(r.X, r.Y, r.Width, r.Height - g.U(12)), true); string[] l = { "SATELLITES", "GROUND STATIONS", "COMM LINKS", "THREAT ZONES" }; for (int i = 0; i < 4; i++) g.Text(l[i], r.X + r.Width * i / 4, r.Bottom - g.U(10), 5.5, i == 3 ? 0xFFFF5A4A : g.P.Text, Align.Left, false, 0.75); }

    void PnlBase(Gfx g, Rect r)
    {
        var kv = new (string, string, uint?)[] { ("THREAT LEVEL", Threat[Math.Min(_threat, 1)], 0xFF30FF70), ("FACILITY STATUS", "NORMAL", 0xFF30FF70), ("POWER", $"{_power * 100 + 4:0}%", null), ("COOLING", $"{_cooling * 100 + 7:0}%", null), ("COMMS", $"{_comms * 100 + 6:0}%", null), ("SECURITY", "98%", null) };
        g.KvGrid(r, kv, 1, 8);
    }

    void PnlTactical(Gfx g, Rect r)
    {
        var m = new Rect(r.X, r.Y, r.Width * 0.62, r.Height); g.Image(Sprites.Terrain(31, 1), m, 1); Reticle(g, m);
        for (int i = 0; i < 4; i++) { var u = _units[(i + 3) % _units.Count]; g.Frame(new Rect(m.X + u.X * m.Width - g.U(4), m.Y + u.Y * m.Height - g.U(4), g.U(8), g.U(8)), u.Hostile ? 0xFFFF3B2F : g.P.Primary, 1.4); }
        g.KvGrid(new Rect(r.X + r.Width * 0.64, r.Y, r.Width * 0.36, r.Height), new (string, string, uint?)[] { ("LAT", "34.7281° N", null), ("LON", "118.1234° W", null), ("ALT", "2,415 m", null), ("TARGETS", $"{_units.Count(u => u.Hostile)}", g.P.Warning), ("FRIENDLY", $"{_units.Count(u => !u.Hostile)}", null), ("HOSTILE", "1", 0xFFFF5A4A) }, 1, 6.5);
    }

    void PnlJet(Gfx g, Rect r)
    {
        double cx = r.X + r.Width * 0.27, cy = r.Center.Y, s = r.Width * 0.22; Jet(g, cx, cy, s, g.P.Primary, -15); g.Jet2(cx, cy, s);
        g.KvGrid(new Rect(r.X + r.Width * 0.55, r.Y, r.Width * 0.45, r.Height), new (string, string, uint?)[] { ("TYPE", "Fighter", null), ("CLASS", "5th Gen", null), ("RANGE", "1,700 km", null), ("SPEED", "2,400 km/h", null), ("THREAT", "HIGH", 0xFFFF5A4A) }, 1, 6.8);
    }

    void PnlTimelineChart(Gfx g, Rect r)
    {
        double lh = g.U(12.5); for (int i = 0; i < Math.Min(4, _timeline.Count); i++) { g.Text(_timeline[i].t, r.X, r.Y + i * lh, 7, g.P.Primary, Align.Left, i == 0); g.Text(_timeline[i].m, r.X + g.U(34), r.Y + i * lh, 7, g.P.Text, Align.Left, false, 0.9); }
        var gr = new Rect(r.X + r.Width * 0.55, r.Y + r.Height * 0.45, r.Width * 0.45, r.Height * 0.55); var pts = new List<Point>(); for (int i = 0; i <= 20; i++) pts.Add(new Point(gr.X + gr.Width * i / 20.0, gr.Bottom - gr.Height * (0.15 + 0.7 * Math.Pow(i / 20.0, 1.6) * (0.85 + 0.15 * Math.Sin(i + _t2)))));
        g.Poly(pts, g.P.Accent, 1.6, 0.95);
    }

    void PnlQuick(Gfx g, Rect r)
    {
        string[] n = { "LAUNCH RECON", "DEPLOY UAV", "ACTIVATE RADAR", "REQUEST SUPPORT" }; string[] a = { "RECON", "UAV", "RADAR", "SUPPORT" }; double bh = Math.Min(g.U(g.Touch ? 40 : 32), r.Height / 4 - g.U(3));
        for (int i = 0; i < 4; i++) { var b = new Rect(r.X, r.Y + i * (bh + g.U(4)), r.Width, bh); g.Button("ml:qa:" + a[i], b, "    " + n[i], i == 2 && _radarOn, true, null, 8); g.Circle(b.X + g.U(14), b.Center.Y, g.U(6), g.P.Primary, 1.3); g.Disc(b.X + g.U(14), b.Center.Y, g.U(1.8), g.P.Accent); }
    }

    void PnlAi(Gfx g, Rect r)
    {
        double bx = r.X + r.Width * 0.2, by = r.Y + r.Height * 0.3, rad = Math.Min(r.Width * 0.16, r.Height * 0.2);
        for (int k = 0; k < 3; k++) g.Arc(bx, by, rad * (0.55 + 0.25 * k), _t2 * (40 + k * 30) * (k % 2 == 0 ? 1 : -1), 220, g.P.Accent, 1.4, 0.8); g.Disc(bx, by, rad * 0.22, g.P.Accent, 0.9); if (g.Glow) g.Disc(bx, by, rad * 0.5, g.P.Primary, 0.18);
        Shell.Overlays.HeatWarningOverlay.Wrap(g, _ai, r.X + r.Width * 0.4, r.Y + g.U(2), r.Width * 0.58, 7.3, g.P.Text);
        var pr = new Rect(r.X, r.Bottom - g.U(22), r.Width, g.U(20)); g.Fill(pr, g.P.Primary, 0.08); g.Frame(pr, g.P.Primary, 1, 0.5); g.Text("Awaiting your command...", pr.X + g.U(6), pr.Y + g.U(5), 7.5, g.P.Text, Align.Left, false, 0.6);
    }

    // ---------------------------------------------------------------- input
    protected override string? PanelButton(string id)
    {
        if (id.StartsWith("ml:view:")) { _view = int.Parse(id[8..]); C.Audio.Play("tick"); return null; }
        if (id.StartsWith("ml:qa:")) return DoAction(id[6..]);
        if (id == "ml:access") { _access = 0; Tl("Access granted"); Log("Terminal access granted"); C.Audio.Play("access", SoundCategory.Event); C.Toast("ACCESS GRANTED"); return null; }
        if (id.StartsWith("ml:lay:")) { int i = int.Parse(id[7..]); if (i < 4) _baseMap = i; else _layer[i] = !_layer[i]; C.Audio.Play("tick"); return null; }
        if (id.StartsWith("ml:nav:"))
        {
            _nav = int.Parse(id[7..]); C.Audio.Play("deepbeep", SoundCategory.Ui, 0.7f);
            switch (_nav) { case 1: _view = 1; return "SATELLITE VIEW"; case 2: Tl("Airspace picture refreshed"); return "AIRSPACE"; case 3: Tl("Intel summary updated"); return "INTELLIGENCE"; case 4: Tl("Comms check complete"); return "COMMUNICATIONS"; case 5: Tl("Environment scan complete"); return "ENVIRONMENT"; case 6: Tl("Systems check complete"); return "SYSTEMS"; case 7: return "LOGS"; default: _view = 0; return "OVERVIEW"; }
        }
        return null;
    }

    protected override void PanelTap(Point p, string? id)
    {
        if (id == null) return;
        if (id.StartsWith("ml:sat:")) { _selSat = int.Parse(id[7..]); _view = Math.Max(_view, 1); C.Toast($"{_sats[_selSat].Id}  {_sats[_selSat].Orbit}  {_sats[_selSat].Alt:N0} km"); C.Audio.Play("ping", SoundCategory.Ui, 0.7f); }
        else if (id.StartsWith("ml:al:")) { int i = int.Parse(id[6..]); if (i < _alerts.Count) { _alerts[i].Ack = true; _threat = Math.Clamp(1 + _alerts.Count(a => !a.Ack && a.Sev >= 2) / 2, 0, 3); Tl("Alert acknowledged"); C.Audio.Play("confirm"); } }
        else if (id.StartsWith("ml:cam:")) { _camSel = int.Parse(id[7..]); _rec4[_camSel] = !_rec4[_camSel]; C.Audio.Play("tick"); C.Toast($"CAM 0{_camSel + 1} {(_rec4[_camSel] ? "RECORDING" : "STANDBY")}"); }
        else if (id.StartsWith("ml:unit:")) { _selUnit = int.Parse(id[8..]); C.Toast($"{_units[_selUnit].Id}  {(_units[_selUnit].Hostile ? "HOSTILE" : "FRIENDLY")}"); C.Audio.Play("ping", SoundCategory.Ui, 0.6f); }
        else if (id == "ml:map" && _mapR.Width > 1)
        {
            var u = _units[Math.Clamp(_selUnit, 0, _units.Count - 1)]; if (u.Hostile) return;
            u.TX = Math.Clamp((p.X - _mapR.X) / _mapR.Width, 0.03, 0.97); u.TY = Math.Clamp((p.Y - _mapR.Y) / (_mapR.Height - 46 * 1.0), 0.03, 0.97); Tl($"{u.Id} moving to waypoint"); C.Audio.Play("radio", SoundCategory.Ui, 0.6f); C.Toast($"{u.Id} MOVING");
        }
    }

    protected override bool PanelDrag(Point p, Vector d, string? id)
    {
        if (id == "ml:globe" || id != null && id.StartsWith("ml:sat:") && _globeR.Contains(p)) { _rot -= d.X * 0.4; _tilt = Math.Clamp(_tilt + d.Y * 0.3, -60, 60); return true; }
        return false;
    }

    protected override bool PanelWheel(Point p, double delta) { if (_globeR.Contains(p)) { _zoom = Math.Clamp(_zoom * (1 + delta * 0.1), 0.6, 1.8); return true; } return false; }
}

static class JetExt
{
    public static void Jet2(this Gfx g, double cx, double cy, double s) { g.Line(cx - s * 0.8, cy, cx + s, cy, g.P.Accent, 1, 0.35); g.Line(cx, cy - s * 0.6, cx, cy + s * 0.6, g.P.Accent, 1, 0.2); }
}
