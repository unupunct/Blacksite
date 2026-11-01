using Avalonia;
using Blacksite.Core.Simulation;
using Blacksite.UI.Rendering;

namespace Blacksite.UI.Modules;

/// <summary>PRESET 08 – global SOC. Everything is synthetic; no real network is touched.</summary>
public sealed class CyberDefenseModule : ModuleBase
{
    static readonly (double lat, double lon)[] Geo =
    {
        (71, -156), (60, 10), (30, 31), (14, 33), (34, -118), (-34, -58), (6, 3), (51, -0.1), (55, 37), (28, 77), (35, 139), (13, 100), (-33, 151), (-18, 178), (-12, -77), (15, -16),
    };
    int _mode;                 // 0 map, 1 globe, 2 graph
    int _sel = 4;
    int _protocol;
    static readonly string[] Protocols = { "OBSERVE", "HARDEN", "ISOLATE", "COUNTER" };
    double _analyze = 1; string _analysis = "";
    bool _report; double _reportT;
    readonly List<string> _timeline = new();
    double _rot = 0, _lastAlert;
    int _lastAlerts;

    public override string[] Actions => new[] { "FIREWALL", "ANALYZE", "REPORT", "MODE", "PROTOCOL" };
    public override bool IsActive(string a) => a switch { "FIREWALL" => C.Sim.FirewallActive, "ANALYZE" => _analyze < 1, "REPORT" => _report, _ => false };
    public override string PrimaryAction => "FIREWALL";

    void Log(string s) { _timeline.Add($"{DateTime.Now:HH:mm:ss}  {s}"); if (_timeline.Count > 14) _timeline.RemoveAt(0); }

    public override string? OnAction(string id)
    {
        var s = C.Sim;
        switch (id)
        {
            case "FIREWALL": s.FirewallActive = !s.FirewallActive; foreach (var n in s.Nodes) n.Firewalled = s.FirewallActive; Log("FIREWALL " + (s.FirewallActive ? "ACTIVE" : "DISABLED")); C.Audio.Play("mech"); return "SIMULATED FIREWALL " + (s.FirewallActive ? "ACTIVE" : "OFF");
            case "ANALYZE": _analyze = 0; _analysis = ""; Log("THREAT ANALYSIS STARTED"); return "THREAT ANALYSIS RUNNING";
            case "REPORT": _report = !_report; _reportT = 0; if (_report) Log("REPORT GENERATED (FICTIONAL)"); return _report ? "FICTIONAL SECURITY REPORT" : "REPORT CLOSED";
            case "MODE": _mode = (_mode + 1) % 3; return "VIEW: " + new[] { "FLAT MAP", "GLOBE", "TOPOLOGY" }[_mode];
            case "PROTOCOL":
                _protocol = (_protocol + 1) % Protocols.Length;
                switch (_protocol)
                {
                    case 0: foreach (var n in s.Nodes) n.Firewalled = s.FirewallActive; break;
                    case 1: s.Nodes[_sel].Firewalled = true; break;
                    case 2: foreach (var n in s.Nodes.Where(n => n.UnderAttack)) n.Firewalled = true; break;
                    case 3: s.Threats.Clear(); s.ThreatIndex = Math.Max(0.05, s.ThreatIndex - 0.2); break;
                }
                Log("PROTOCOL " + Protocols[_protocol]); C.Audio.Play("confirm"); return "DEFENSE PROTOCOL: " + Protocols[_protocol];
        }
        return null;
    }

    public override void Update(double dt)
    {
        _rot += dt * 6;
        if (C.Sim.Alerts != _lastAlerts) { var t = C.Sim.Threats.LastOrDefault(); if (t != null) Log($"{t.Kind} -> {C.Sim.Nodes[t.ToNode].Name}{(t.Blocked ? " [BLOCKED]" : "")}"); _lastAlerts = C.Sim.Alerts; }
        if (_analyze < 1)
        {
            _analyze = Math.Min(1, _analyze + dt / 5);
            if (_analyze >= 1)
            {
                var top = C.Sim.Threats.GroupBy(t => t.Kind).OrderByDescending(g => g.Count()).FirstOrDefault();
                _analysis = $"ANALYSIS COMPLETE: {(top?.Key ?? "NO ACTIVE VECTORS")}  //  RISK {C.Sim.ThreatIndex * 100:0}/100";
                Log(_analysis); C.Toast("THREAT ANALYSIS COMPLETE"); C.Audio.Confirm();
            }
        }
        if (_report) _reportT += dt;
    }

    Point Pos(Rect c, int i, double cx, double cy, double rad, out bool vis)
    {
        var (la, lo) = Geo[i];
        vis = true;
        if (_mode == 0) return World.Project(c, lo, la, 0, 10, 1.0);
        if (_mode == 1) { var (p, z) = World.Ortho(cx, cy, rad, lo, la, _rot - 20, 20); vis = z > 0; return p; }
        double a = i * Math.PI * 2 / Geo.Length - Math.PI / 2; return new Point(cx + Math.Cos(a) * rad, cy + Math.Sin(a) * rad);
    }

    public override void Draw(Gfx g, Rect area)
    {
        var lay = Layout.Adaptive(g, area, 5, 0.56);
        DrawMain(g, lay.Main);
        for (int i = 0; i < lay.Panels.Count; i++) DrawPanel(g, i, lay.Panels[i]);
    }

    void DrawMain(Gfx g, Rect r)
    {
        var c = g.Panel(r, new[] { "GLOBAL NETWORK MAP", "GLOBE VIEW", "TOPOLOGY GRAPH" }[_mode] + " // SIMULATED");
        double cx = c.Center.X, cy = c.Center.Y, rad = Math.Min(c.Width, c.Height) * (_mode == 2 ? 0.4 : 0.46);
        using var clip = g.Clip(c);
        if (_mode == 1) g.HoloBase(c, cx, c.Bottom - g.U(16), rad * 1.5);
        if (_mode == 0) g.FlatMap(c, 0, 10, 1.0, g.P.Secondary, true, false);
        else if (_mode == 1) g.Globe(cx, cy, rad, _rot - 20, 20, g.P.Panel, g.P.Secondary, true);
        else { g.Circle(cx, cy, rad, g.P.Primary, 1, 0.2); g.Circle(cx, cy, rad * 0.5, g.P.Primary, 1, 0.1); }
        var nodes = C.Sim.Nodes;
        // links
        for (int i = 0; i < nodes.Count; i++)
        {
            int j = (i + 1) % nodes.Count; var a = Pos(c, i, cx, cy, rad, out bool va); var b = Pos(c, j, cx, cy, rad, out bool vb);
            if (va && vb) g.Line(a.X, a.Y, b.X, b.Y, g.P.Primary, 1, 0.12);
        }
        // attacks
        foreach (var t in C.Sim.Threats)
        {
            var a = Pos(c, t.FromNode, cx, cy, rad, out bool va); var b = Pos(c, t.ToNode, cx, cy, rad, out bool vb);
            if (!va || !vb) continue;
            uint col = t.Blocked ? g.P.Primary : g.P.Warning;
            var mid = new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2 - Hypot(b.X - a.X, b.Y - a.Y) * 0.25);
            Point Q(double k) => new((1 - k) * (1 - k) * a.X + 2 * (1 - k) * k * mid.X + k * k * b.X, (1 - k) * (1 - k) * a.Y + 2 * (1 - k) * k * mid.Y + k * k * b.Y);
            var pts = new List<Point>(); for (int k = 0; k <= 16; k++) pts.Add(Q(Math.Min(t.Progress, k / 16.0 * t.Progress + 0)));
            var trail = new List<Point>(); for (int k = 0; k <= 12; k++) trail.Add(Q(t.Progress * k / 12));
            g.Poly(trail, col, 1.3, 0.55);
            var h = Q(t.Progress); g.Disc(h.X, h.Y, g.U(3), col);
            if (t.Progress > 0.92) g.Circle(b.X, b.Y, g.U(14 * (t.Progress - 0.9) * 10), col, 1.2, 0.6);
        }
        // nodes
        for (int i = 0; i < nodes.Count; i++)
        {
            var p = Pos(c, i, cx, cy, rad, out bool vis); if (!vis) continue;
            var n = nodes[i]; bool sel = i == _sel;
            uint col = n.UnderAttack ? g.P.Warning : n.Firewalled ? g.P.Accent : g.P.Primary;
            g.Disc(p.X, p.Y, g.U(sel ? 5 : 3.5), col, 0.5 + 0.5 * n.Health);
            if (n.Firewalled) g.Circle(p.X, p.Y, g.U(8), g.P.Accent, 1, 0.5, g.U(2));
            if (sel) { g.GlowCircle(p.X, p.Y, g.U(11), g.P.Accent, 1.4); }
            if (sel || _mode == 2 || g.Quality == Core.Configuration.GraphicsProfile.High) g.Text(n.Name, p.X + g.U(8), p.Y - g.U(5), 8, sel ? g.P.Accent : g.P.Text, Align.Left, sel, sel ? 1 : 0.6);
            g.AddHit("node:" + i, new Rect(p.X - g.U(14), p.Y - g.U(14), g.U(28), g.U(28)));
        }
        g.HoloShimmer(c, g.P.Secondary);
        double iy = c.Bottom - g.U(14);
        g.Text(_analysis.Length > 0 ? _analysis : $"FIREWALL {(C.Sim.FirewallActive ? "ACTIVE" : "OFF")}  |  PROTOCOL {Protocols[_protocol]}  |  VECTORS {C.Sim.Threats.Count}", c.X + g.U(4), iy, 8.5, g.P.Text, Align.Left, false, 0.8);
        if (_analyze < 1) g.Bar(new Rect(c.X + c.Width * 0.2, c.Y + g.U(6), c.Width * 0.6, g.U(18)), _analyze, g.P.Accent, "THREAT ANALYSIS", $"{_analyze * 100:0}%");
        if (_report) DrawReport(g, c);
    }

    void DrawReport(Gfx g, Rect c)
    {
        var rr = new Rect(c.X + c.Width * 0.12, c.Y + c.Height * 0.12, c.Width * 0.76, c.Height * 0.72);
        g.Fill(rr, g.P.Background, 0.95); g.Frame(rr, g.P.Accent, 1.5); g.Brackets(rr, g.P.Accent, 12, 2);
        g.AddHit("report", rr);
        var s = C.Sim;
        string[] lines =
        {
            "SECURITY REPORT  //  FICTIONAL  //  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            $"OVERALL RISK:          {s.ThreatIndex * 100:0}/100  ({(s.ThreatIndex > 0.5 ? "ELEVATED" : s.ThreatIndex > 0.25 ? "MODERATE" : "LOW")})",
            $"EVENTS CAPTURED:       {s.Alerts}", $"NODES COMPROMISED:     {s.DefenseNodeHits}", $"FIREWALL:              {(s.FirewallActive ? "ACTIVE" : "INACTIVE")}",
            $"DEFENSE PROTOCOL:      {Protocols[_protocol]}", $"MEAN NODE HEALTH:      {s.Nodes.Average(n => n.Health) * 100:0}%",
            "RECOMMENDATION:        " + (s.FirewallActive ? "MAINTAIN POSTURE" : "ENABLE FIREWALL"), "", "ALL DATA SYNTHETIC. TAP TO CLOSE.",
        };
        double lh = Math.Min(g.U(17), rr.Height / (lines.Length + 1));
        for (int i = 0; i < lines.Length; i++) g.Text(lines[i], rr.X + g.U(12), rr.Y + g.U(10) + i * lh, 9.5, i == 0 ? g.P.Accent : g.P.Text, Align.Left, i == 0);
    }

    void DrawPanel(Gfx g, int i, Rect r)
    {
        var s = C.Sim;
        switch (i)
        {
            case 0:
                {
                    var c = g.Panel(r, "THREAT DETECTION");
                    double lh = g.U(14); int n = Math.Min(s.Threats.Count, (int)(c.Height / lh));
                    for (int k = 0; k < n; k++)
                    {
                        var t = s.Threats[s.Threats.Count - 1 - k];
                        g.Disc(c.X + g.U(4), c.Y + k * lh + g.U(6), g.U(2.5), t.Blocked ? g.P.Primary : g.P.Warning);
                        g.Text(t.Kind, c.X + g.U(12), c.Y + k * lh, 8.5, g.P.Text);
                        g.Text(s.Nodes[t.ToNode].Name, c.Right, c.Y + k * lh, 8.5, t.Blocked ? g.P.Primary : g.P.Warning, Align.Right, true);
                    }
                    if (n == 0) g.Text("NO ACTIVE VECTORS", c.X, c.Y, 9, g.P.Text, Align.Left, false, 0.5);
                    break;
                }
            case 1:
                {
                    var c = g.Panel(r, "NETWORK HEALTH");
                    double gr = Math.Min(c.Height, c.Width * 0.45) / 2 - g.U(2);
                    double health = s.Nodes.Average(n => n.Health) * 100;
                    g.Gauge(c.X + gr + g.U(2), c.Center.Y, gr, 100 - health, 100, "RISK", "%", g.P.Warning, 0.4);
                    double tx = c.X + gr * 2 + g.U(14);
                    string[] k = { "BANDWIDTH", "PACKETS/S", "LATENCY", "ALERTS" }; string[] v = { $"{s.BandwidthIn:0} MB/S", $"{s.PacketRate:0}", $"{12 + s.ThreatIndex * 80:0} MS", s.Alerts.ToString() };
                    for (int n = 0; n < 4; n++) g.KeyVal(tx, c.Y + n * g.U(16), c.Right - tx, k[n], v[n], null, 8.5);
                    break;
                }
            case 2:
                {
                    var c = g.Panel(r, "SECURITY EVENT TIMELINE");
                    double lh = g.U(12.5); int max = Math.Max(1, (int)(c.Height / lh));
                    for (int k = Math.Max(0, _timeline.Count - max), n = 0; k < _timeline.Count; k++, n++) g.Text(_timeline[k], c.X, c.Y + n * lh, 8, _timeline[k].Contains("BLOCKED") ? g.P.Primary : _timeline[k].Contains("->") ? g.P.Warning : g.P.Text);
                    break;
                }
            case 3:
                {
                    var c = g.Panel(r, "ENCRYPTION CHANNEL");
                    int cols = Math.Max(8, (int)(c.Width / g.U(9))), rowsN = Math.Max(2, (int)(c.Height / g.U(10)));
                    for (int y = 0; y < rowsN; y++) for (int x = 0; x < cols; x++)
                        {
                            double v = Math.Sin(x * 0.7 + g.Time * 2 + y * 1.3) * Math.Sin(y * 0.9 - g.Time * 1.4 + x * 0.3);
                            g.Fill(new Rect(c.X + x * g.U(9), c.Y + y * g.U(10), g.U(6), g.U(7)), v > 0.3 ? g.P.Accent : g.P.Primary, 0.08 + Math.Abs(v) * 0.5);
                        }
                    break;
                }
            case 4:
                {
                    var n = s.Nodes[_sel]; var c = g.Panel(r, "NODE // " + n.Name);
                    double lh = g.U(14);
                    string[] k = { "ADDRESS", "HEALTH", "STATE", "FIREWALL" }; string[] v = { n.Ip, $"{n.Health * 100:0}%", n.UnderAttack ? "UNDER ATTACK" : "SECURE", n.Firewalled ? "ENABLED" : "DISABLED" };
                    for (int m = 0; m < Math.Min(4, (int)(c.Height / lh)); m++) g.KeyVal(c.X, c.Y + m * lh, c.Width, k[m], v[m], m == 2 && n.UnderAttack ? g.P.Warning : null, 9);
                    break;
                }
        }
    }

    public override void Tap(Point p, string? id)
    {
        if (id == null) return;
        if (id.StartsWith("node:")) { _sel = int.Parse(id[5..]); C.Toast("NODE " + C.Sim.Nodes[_sel].Name); C.Audio.Play("ping"); }
        else if (id == "report") { _report = false; }
    }

    public override bool Drag(Point p, Vector d, string? id)
    {
        if (_mode == 1) { _rot -= d.X * 0.4; return true; }
        return false;
    }
}
