using Avalonia;
using Blacksite.Core.Themes;
using Blacksite.UI.Rendering;

namespace Blacksite.UI.Modules;

/// <summary>PRESET 04 – futuristic AI assistant core.</summary>
public sealed class JarvisModule : ModuleBase
{
    static readonly string[] Personas = { "CLASSIC", "ANALYTIC", "TACTICAL", "CALM" };
    int _persona;
    bool _active = true;
    double _energy = 0.4, _speakT, _listenT;
    bool _listening;
    readonly List<(bool ai, string text)> _chat = new();
    // diagnostics
    double _diag = 1; readonly string[] _subs = { "NEURAL MESH", "SENSOR ARRAY", "COMMS UPLINK", "POWER BUS", "THERMAL CTRL", "DATA VAULT", "VOICE ENGINE", "SECURITY" };
    double _analyze = 1; double[] _bars = new double[16];
    string _status = "STANDBY";

    protected override void OnInit() { for (int i = 0; i < _bars.Length; i++) _bars[i] = 0.2 + 0.5 * Math.Abs(Math.Sin(i * 1.7)); Reply(Greeting()); }

    string Greeting() => DateTime.Now.Hour switch { < 12 => "Good morning. All systems operational.", < 18 => "Good afternoon. All systems operational.", _ => "Good evening. All systems operational." };

    public override string[] Actions => new[] { "ACTIVATE", "DIAGNOSE", "ANALYZE", "PERSONA", "SPEAK", "VOICE" };
    public override bool IsActive(string a) => a == "ACTIVATE" && _active || a == "DIAGNOSE" && _diag < 1 || a == "ANALYZE" && _analyze < 1 || a == "VOICE" && _listening;

    void Reply(string text)
    {
        _chat.Add((true, text)); if (_chat.Count > 12) _chat.RemoveAt(0);
        _speakT = Math.Max(2.2, text.Length * 0.065);
        C.Audio.Say(text, Personas[_persona]);
    }

    string Style(string s) => _persona switch
    {
        1 => s.Replace("All systems operational", "All systems operational. Efficiency at 97 percent"),
        2 => s.Replace("Good evening. ", "Commander. ").Replace("Good morning. ", "Commander. ").Replace("Good afternoon. ", "Commander. "),
        3 => s.Replace("Warning. ", "Gentle alert. "),
        _ => s,
    };

    public override string? OnAction(string id)
    {
        switch (id)
        {
            case "ACTIVATE":
                _active = !_active; _status = _active ? "ONLINE" : "STANDBY";
                if (_active) Reply(Style(Greeting())); else { _chat.Add((true, "Entering standby.")); }
                return _active ? "AI CORE ACTIVATED" : "AI CORE IN STANDBY";
            case "DIAGNOSE":
                if (!_active) return Wake(); _diag = 0; _status = "DIAGNOSTICS"; _chat.Add((false, "Run diagnostics.")); return "DIAGNOSTICS STARTED";
            case "ANALYZE":
                if (!_active) return Wake(); _analyze = 0; _status = "ANALYZING"; _chat.Add((false, "Run analysis.")); return "ANALYSIS SEQUENCE STARTED";
            case "PERSONA": _persona = (_persona + 1) % Personas.Length; Reply(Style("Personality profile " + Personas[_persona].ToLower() + " loaded. Awaiting your command.")); return "PERSONA: " + Personas[_persona];
            case "SPEAK":
                if (!_active) return Wake();
                string[] m = { "Satellite connection established.", "Environmental conditions nominal.", "Warning. Unauthorized access detected.", "Awaiting your command.", "Analysis complete." };
                Reply(Style(m[Rng.Next(m.Length)])); return "SPEAKING";
            case "VOICE":
                if (!_active) return Wake(); _listening = !_listening; _listenT = 0; _status = _listening ? "LISTENING" : "ONLINE";
                return _listening ? "VOICE INTERACTION ACTIVE (SIMULATED)" : "VOICE INTERACTION OFF";
        }
        return null;
    }

    string Wake() { _active = true; Reply("Core activated. Awaiting your command."); return "AI CORE ACTIVATED"; }

    public override void Update(double dt)
    {
        _energy += ((_active ? (_speakT > 0 || _listening ? 1.0 : 0.55) : 0.15) - _energy) * Math.Min(1, dt * 2.5);
        if (_speakT > 0) { _speakT -= dt; if (_speakT <= 0 && _diag >= 1 && _analyze >= 1 && !_listening) _status = _active ? "ONLINE" : "STANDBY"; }
        if (_diag < 1)
        {
            double before = _diag; _diag = Math.Min(1, _diag + dt / 7);
            if (_diag >= 1) { Reply(Style("Diagnostics complete. All systems nominal.")); }
        }
        if (_analyze < 1)
        {
            _analyze = Math.Min(1, _analyze + dt / 6);
            for (int i = 0; i < _bars.Length; i++) _bars[i] += (Rng.NextDouble() - _bars[i]) * 0.15;
            if (_analyze >= 1) Reply(Style("Analysis complete."));
        }
        if (_listening)
        {
            _listenT += dt;
            if (_listenT > 4.5)
            {
                _listenT = 0; string[] heard = { "Status report.", "Run diagnostics.", "Show environmental data.", "Open satellite link." };
                string h = heard[Rng.Next(heard.Length)]; _chat.Add((false, h));
                Reply(h.StartsWith("Run") ? "Starting diagnostics." : h.StartsWith("Status") ? Style("All systems operational.") : h.StartsWith("Show") ? "Environmental conditions nominal." : "Satellite connection established.");
                if (h.StartsWith("Run")) { _diag = 0; }
            }
        }
    }

    public override void Draw(Gfx g, Rect area)
    {
        var lay = Layout.Adaptive(g, area, 6, 0.44);
        DrawCore(g, lay.Main);
        for (int i = 0; i < lay.Panels.Count; i++) DrawPanel(g, i, lay.Panels[i]);
    }

    void DrawCore(Gfx g, Rect r)
    {
        double cx = r.Center.X, cy = r.Center.Y, rad = Math.Min(r.Width, r.Height) * 0.46;
        double e = _energy;
        g.HoloBase(r, cx, r.Bottom - g.U(26), rad * 1.6);
        g.Rings(cx, cy, rad, 6, g.P.Primary, 0.6 + e);
        // outer ticks ring
        double rot = g.Time * 6;
        for (int i = 0; i < 120; i++)
        {
            double a = (rot + i * 3) * Math.PI / 180, long_ = i % 10 == 0 ? 0.06 : 0.025;
            g.Line(cx + Math.Cos(a) * rad * 1.02, cy + Math.Sin(a) * rad * 1.02, cx + Math.Cos(a) * rad * (1.02 + long_), cy + Math.Sin(a) * rad * (1.02 + long_), g.P.Primary, 1, i % 10 == 0 ? 0.8 : 0.3);
        }
        string[] labels = { "COGNITION", "SENSORS", "NETWORK", "MEMORY" };
        for (int i = 0; i < 4; i++)
        {
            double a = (-rot * 0.5 + i * 90) * Math.PI / 180;
            g.Text(labels[i], cx + Math.Cos(a) * rad * 1.17, cy + Math.Sin(a) * rad * 1.17 - g.U(4), 8, g.P.Primary, Align.Center, false, 0.65);
        }
        // core sphere
        double cr = rad * (0.26 + 0.03 * Math.Sin(g.Time * 2.4) * e + 0.04 * e);
        var br = new Avalonia.Media.RadialGradientBrush
        {
            GradientStops = { new Avalonia.Media.GradientStop(g.Col(0xFFFFFFFF, 0.95), 0), new Avalonia.Media.GradientStop(g.Col(g.P.Primary, 0.75 * (0.4 + e * 0.6)), 0.35), new Avalonia.Media.GradientStop(g.Col(g.P.Primary, 0), 1) }
        };
        g.Dc.DrawEllipse(br, null, new Point(cx, cy), cr * 1.8, cr * 1.8);
        g.Plasma(cx, cy, cr * 1.15, g.P.Primary, 5, 0.55 + 0.4 * e);
        g.GlowCircle(cx, cy, cr, g.P.Accent, 2, 0.9);
        g.Arc(cx, cy, cr * 0.7, g.Time * 220, 90 + 120 * e, g.P.Accent, 2.4);
        g.Arc(cx, cy, cr * 0.5, -g.Time * 300, 70, g.P.Primary, 2);
        // voice waveform ring
        double amp = (_speakT > 0 ? 1 : _listening ? 0.7 : 0.12) * (0.4 + e);
        var pts = new List<Point>();
        for (int i = 0; i <= 120; i++)
        {
            double a = i * Math.PI * 2 / 120, w = Math.Sin(a * 9 + g.Time * 11) * Math.Sin(a * 3 - g.Time * 4) * amp;
            double rr = rad * 0.58 + w * rad * 0.07;
            pts.Add(new Point(cx + Math.Cos(a) * rr, cy + Math.Sin(a) * rr));
        }
        g.Poly(pts, g.P.Accent, 1.4, 0.85, true);
        // floating callouts
        string[] t = { $"NEURAL LOAD {C.Sim.CpuLoad * 100:0}%", $"LATENCY {8 + C.Sim.Noise(2):0} MS", $"CONFIDENCE {96 + C.Sim.Noise(1):0.0}%", $"THREAT {C.Sim.ThreatIndex * 100:0}" };
        for (int i = 0; i < 4; i++)
        {
            double a = (-35 + i * 90 + Math.Sin(g.Time * 0.4 + i) * 6) * Math.PI / 180;
            double x1 = cx + Math.Cos(a) * rad * 0.78, y1 = cy + Math.Sin(a) * rad * 0.78;
            double x2 = cx + Math.Cos(a) * rad * 1.0 + (Math.Cos(a) > 0 ? g.U(8) : -g.U(8)), y2 = cy + Math.Sin(a) * rad * 1.0;
            g.Line(x1, y1, x2, y2, g.P.Primary, 1, 0.4);
            g.Text(t[i], x2 + (Math.Cos(a) > 0 ? g.U(4) : -g.U(4)), y2 - g.U(5), 8.5, g.P.Accent, Math.Cos(a) > 0 ? Align.Left : Align.Right, true);
        }
        g.HoloShimmer(new Rect(cx - rad * 1.2, cy - rad * 1.2, rad * 2.4, rad * 2.4), null, true);
        g.Text(Personas[_persona] + " // " + _status, cx, r.Bottom - g.U(22), 11, g.P.Primary, Align.Center, true);
        g.AddHit("core", new Rect(cx - cr * 1.4, cy - cr * 1.4, cr * 2.8, cr * 2.8));
    }

    void DrawPanel(Gfx g, int i, Rect r)
    {
        switch (i)
        {
            case 0:
                {
                    var c = g.Panel(r, "SYSTEM ACTIVITY");
                    var rows = Layout.Rows(c, g.U(4), 1, 1, 1);
                    g.Graph(rows[0], C.Sim.Cpu, g.P.Primary, 0, 1, true, "PROCESSING");
                    g.Graph(rows[1], C.Sim.Mem, g.P.Accent, 0, 1, true, "MEMORY");
                    g.Graph(rows[2], C.Sim.Signal, g.P.Secondary, 0, 1, true, "UPLINK");
                    break;
                }
            case 1:
                {
                    var c = g.Panel(r, "DIAGNOSTICS");
                    double lh = Math.Min(g.U(15), c.Height / _subs.Length);
                    for (int k = 0; k < _subs.Length; k++)
                    {
                        double frac = Math.Clamp(_diag * _subs.Length - k, 0, 1);
                        bool done = _diag >= 1 || frac >= 1;
                        g.Text(_subs[k], c.X, c.Y + k * lh, 8.5, g.P.Text, Align.Left, false, 0.85);
                        g.Text(done ? "NOMINAL" : frac > 0 ? "SCANNING" : "PENDING", c.Right, c.Y + k * lh, 8.5, done ? g.P.Primary : g.P.Warning, Align.Right, true);
                    }
                    break;
                }
            case 2:
                {
                    var c = g.Panel(r, "COMMUNICATION LOG");
                    double lh = g.U(13); int max = Math.Max(1, (int)(c.Height / lh) - 0);
                    int start = Math.Max(0, _chat.Count - max); double y = c.Y;
                    for (int k = start; k < _chat.Count; k++)
                    {
                        var (ai, text) = _chat[k];
                        string line = (ai ? "AI> " : "YOU> ") + text;
                        if (g.TextWidth(line, 8.5 * g.Scale) > c.Width) line = line[..Math.Max(8, (int)(line.Length * c.Width / g.TextWidth(line, 8.5 * g.Scale)) - 2)] + "…";
                        g.Text(line, c.X, y, 8.5, ai ? g.P.Primary : g.P.Text, Align.Left, ai); y += lh;
                    }
                    break;
                }
            case 3:
                {
                    var c = g.Panel(r, "ENVIRONMENT (SIMULATED)");
                    double lh = g.U(14);
                    string[] k = { "TEMPERATURE", "HUMIDITY", "PRESSURE", "RADIATION", "ALERTS" };
                    string[] v = { $"{C.Sim.EnvTemp:0.0} C", $"{C.Sim.EnvHumidity:0}%", $"{C.Sim.EnvPressure:0} HPA", $"{C.Sim.EnvRadiation:0.00} uSv/h", C.Sim.Alerts.ToString() };
                    for (int n = 0; n < Math.Min(5, (int)(c.Height / lh)); n++) g.KeyVal(c.X, c.Y + n * lh, c.Width, k[n], v[n], null, 9);
                    break;
                }
            case 4:
                {
                    var c = g.Panel(r, _analyze < 1 ? $"DATA ANALYSIS {_analyze * 100:0}%" : "DATA ANALYSIS");
                    g.Bars(new Rect(c.X, c.Y, c.Width, c.Height - g.U(2)), _bars.Select(Math.Abs).ToArray(), g.P.Primary);
                    break;
                }
            case 5:
                {
                    var c = g.Panel(r, _listening ? "VOICE INPUT // LISTENING" : "AUDIO WAVEFORM");
                    var pts = new List<Point>(); double amp = (_speakT > 0 ? 1 : _listening ? 0.8 : 0.1);
                    int n = 140;
                    for (int k = 0; k < n; k++)
                    {
                        double x = c.X + c.Width * k / (n - 1), env = Math.Sin(Math.PI * k / (n - 1));
                        double y = c.Center.Y + Math.Sin(k * 0.55 + g.Time * 14) * Math.Sin(k * 0.13 - g.Time * 3) * amp * c.Height * 0.42 * env;
                        pts.Add(new Point(x, y));
                    }
                    g.Line(c.X, c.Center.Y, c.Right, c.Center.Y, g.P.Primary, 1, 0.2);
                    g.Poly(pts, g.P.Accent, 1.6, 0.95);
                    break;
                }
        }
    }

    public override void Tap(Point p, string? id)
    {
        if (id == "core") { C.Toast(_active ? "AI CORE: AWAITING COMMAND" : "AI CORE ACTIVATED"); if (!_active) Wake(); else Reply(Style("Awaiting your command.")); }
    }
}
