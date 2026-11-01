using System.Text;
using Avalonia;
using Blacksite.Core.Themes;
using Blacksite.UI.Rendering;

namespace Blacksite.UI.Modules;

/// <summary>PRESET 02 – Hollywood cyber-operations workstation. All output is fictional and generated locally.</summary>
public sealed class HackerTerminalModule : ModuleBase
{
    sealed class Term
    {
        public string Title = "";
        public readonly List<string> Lines = new();
        public string Input = "";
        public bool Open = true, Min, Max;
        public readonly List<string> History = new();
        public int HistPos = -1;
        public double Scroll;
    }

    readonly List<Term> _terms = new();
    int _active;
    readonly Queue<(double delay, int term, string line)> _queue = new();
    double _qTimer;
    int _theme;
    static readonly (string name, uint col)[] Themes = { ("GREEN", 0xFF28FF5A), ("AMBER", 0xFFFFB020), ("CYAN", 0xFF30E0FF), ("WHITE", 0xFFE8F0F0), ("RED", 0xFFFF4040) };
    // scan / crypto state
    double _scanProg = 1; int _scanNodes;
    double _enc = 1; bool _everDecrypt; string _encTarget = "ACCESS GRANTED";
    readonly List<(int a, int b, double t)> _packets = new();
    bool _granted; double _grantedT;
    int _termCounter = 3;

    protected override void OnInit()
    {
        _terms.Add(new Term { Title = "TERM-01 // root@blacksite" });
        _terms.Add(new Term { Title = "TERM-02 // net-monitor" });
        _terms.Add(new Term { Title = "TERM-03 // crypto-core" });
        Enq(0, "BLACKSITE OPS SHELL v4.2  (SIMULATED ENVIRONMENT)", 0);
        Enq(0, "TYPE HELP FOR COMMANDS.  ALL OUTPUT IS FICTIONAL.", 0.05);
        Enq(1, "tail -f /var/log/netflow.sim", 0.1);
        Enq(2, "crypto-core ready. awaiting key material.", 0.1);
    }

    public override string[] Actions => new[] { "SCAN", "DECRYPT", "THEME", "CLEAR", "NEW TERM" };
    public override bool WantsText => true;
    public override bool IsActive(string a) => a == "SCAN" && _scanProg < 1 || a == "DECRYPT" && _enc < 1;

    void Enq(int term, string line, double delay = 0.06) => _queue.Enqueue((delay, term, line));

    Term Cur => _terms[Math.Clamp(_active, 0, _terms.Count - 1)];

    string Ip() => $"{new[] { "192.0.2", "198.51.100", "203.0.113" }[Rng.Next(3)]}.{Rng.Next(2, 250)}";

    public override string? OnAction(string id)
    {
        switch (id)
        {
            case "SCAN": Run("SCAN NETWORK"); return "NETWORK SCAN STARTED";
            case "DECRYPT": Run("DECRYPT"); return "DECRYPTION STARTED";
            case "THEME": _theme = (_theme + 1) % Themes.Length; return $"TERMINAL THEME: {Themes[_theme].name}";
            case "CLEAR": Cur.Lines.Clear(); return "TERMINAL CLEARED";
            case "NEW TERM":
                {
                    var closed = _terms.FirstOrDefault(t => !t.Open);
                    if (closed != null) { closed.Open = true; closed.Min = false; _active = _terms.IndexOf(closed); return "TERMINAL REOPENED"; }
                    if (_terms.Count >= 6) { C.Audio.Error(); return "MAXIMUM TERMINALS OPEN"; }
                    _terms.Add(new Term { Title = $"TERM-{++_termCounter:00} // shell" }); _active = _terms.Count - 1;
                    Enq(_active, "new session opened.", 0); return "NEW TERMINAL OPENED";
                }
        }
        return null;
    }

    // ---------------------------------------------------------------- command interpreter
    void Run(string raw)
    {
        var t = Cur; int ti = _active;
        string cmd = raw.Trim().ToUpperInvariant();
        t.Lines.Add("> " + raw.Trim());
        if (cmd.Length == 0) return;
        t.History.Add(raw.Trim()); t.HistPos = -1;
        switch (cmd)
        {
            case "HELP":
                foreach (var l in new[] { "COMMANDS:", "  HELP               this list", "  SCAN NETWORK       sweep simulated subnet", "  TRACE ROUTE        trace a fictional path",
                    "  DECRYPT            run decryption routine", "  ACCESS GRANTED     request clearance", "  SYSTEM STATUS      system report",
                    "  ANALYZE PACKETS    packet statistics", "  INITIALIZE PROTOCOL  arm protocol stack", "  THEME / CLEAR / HISTORY / EXIT" }) Enq(ti, l, 0.03);
                break;
            case "SCAN NETWORK":
                Enq(ti, "initiating sweep of 192.0.2.0/24 (simulated)...", 0.1);
                for (int i = 0; i < 7; i++) Enq(ti, $"  host {Ip(),-16} {(Rng.Next(4) == 0 ? "FILTERED" : "ACTIVE  ")}  ports {Rng.Next(20, 500)},{Rng.Next(500, 9000)}", 0.25);
                Enq(ti, "sweep complete. 7 hosts catalogued.", 0.3);
                _scanProg = 0; _scanNodes = 0; break;
            case "TRACE ROUTE":
                Enq(ti, "tracing route to 203.0.113.77 (fictional) max 8 hops", 0.1);
                for (int i = 1; i <= 8; i++) Enq(ti, $"  {i,2}  {Ip(),-16} {Rng.Next(2, 90)}.{Rng.Next(10)}ms", 0.2);
                Enq(ti, "trace complete.", 0.2); break;
            case "DECRYPT":
                Enq(ti, "loading cipher suite...", 0.1); Enq(ti, "brute-forcing keyspace (simulated)...", 0.4);
                _enc = 0; _everDecrypt = true; _encTarget = "ACCESS GRANTED"; _granted = false; break;
            case "ACCESS GRANTED":
                Enq(ti, "verifying clearance token...", 0.2); Enq(ti, "ACCESS GRANTED — LEVEL 5 (SIMULATED)", 0.6);
                _granted = true; _grantedT = 0; C.Audio.Confirm(); C.Events.Trigger("mission_orders"); break;
            case "SYSTEM STATUS":
                Enq(ti, $"CPU {C.Sim.CpuLoad * 100:0}%   MEM {C.Sim.MemLoad * 100:0}%   NET IN {C.Sim.BandwidthIn:0} KB/S", 0.05);
                Enq(ti, $"THREAT INDEX {C.Sim.ThreatIndex * 100:0}   ALERTS {C.Sim.Alerts}   NODES {C.Sim.Nodes.Count}", 0.1);
                Enq(ti, "ALL VALUES SIMULATED.", 0.1); break;
            case "ANALYZE PACKETS":
                Enq(ti, "capturing 4096 frames (synthetic)...", 0.1);
                Enq(ti, $"  TCP {Rng.Next(55, 70)}%  UDP {Rng.Next(15, 25)}%  ICMP {Rng.Next(1, 6)}%  OTHER {Rng.Next(2, 9)}%", 0.5);
                Enq(ti, $"  anomalies: {Rng.Next(0, 5)}   malformed: {Rng.Next(0, 3)}", 0.2); break;
            case "INITIALIZE PROTOCOL":
                foreach (var s in new[] { "arming handshake layer", "negotiating session keys", "binding virtual interfaces", "protocol stack ONLINE" }) Enq(ti, "  [ OK ] " + s, 0.3);
                C.Events.Trigger("sat_acquired"); break;
            case "CLEAR": t.Lines.Clear(); break;
            case "THEME": _theme = (_theme + 1) % Themes.Length; Enq(ti, "theme: " + Themes[_theme].name, 0.02); break;
            case "HISTORY": for (int i = 0; i < t.History.Count; i++) Enq(ti, $"  {i + 1,3}  {t.History[i]}", 0.02); break;
            case "EXIT": t.Open = false; break;
            default: Enq(ti, $"{cmd}: command not recognised. type HELP.", 0.05); C.Audio.Error(); break;
        }
    }

    public override void Key(string key, string? text)
    {
        var t = Cur;
        if (text != null) { foreach (var ch in text) if (!char.IsControl(ch) && t.Input.Length < 60) t.Input += ch; C.Audio.Play("click", Blacksite.Core.Audio.SoundCategory.Ui, 0.35f); return; }
        switch (key)
        {
            case "Enter": var c = t.Input; t.Input = ""; Run(c); C.Audio.Play("beep"); break;
            case "Back": if (t.Input.Length > 0) t.Input = t.Input[..^1]; break;
            case "Up": if (t.History.Count > 0) { t.HistPos = t.HistPos < 0 ? t.History.Count - 1 : Math.Max(0, t.HistPos - 1); t.Input = t.History[t.HistPos]; } break;
            case "Down": if (t.HistPos >= 0) { t.HistPos++; t.Input = t.HistPos >= t.History.Count ? "" : t.History[t.HistPos]; if (t.HistPos >= t.History.Count) t.HistPos = -1; } break;
            case "Tab": _active = NextOpen(_active); break;
            case "Space": if (t.Input.Length < 60) t.Input += " "; break;
            default: if (key.Length == 1 && t.Input.Length < 60) t.Input += key; break;
        }
    }

    int NextOpen(int from) { for (int i = 1; i <= _terms.Count; i++) { int j = (from + i) % _terms.Count; if (_terms[j].Open && !_terms[j].Min) return j; } return from; }

    public override void Update(double dt)
    {
        _qTimer -= dt;
        while (_queue.Count > 0 && _qTimer <= 0)
        {
            var (d, ti, line) = _queue.Dequeue();
            if (ti < _terms.Count) { _terms[ti].Lines.Add(line); if (_terms[ti].Lines.Count > 200) _terms[ti].Lines.RemoveAt(0); }
            _qTimer = d;
        }
        // ambient log chatter
        if (Rng.NextDouble() < dt * 1.8 && _terms.Count > 1 && _terms[1].Open && _queue.Count < 3)
        {
            string[] k = { "ACK", "SYN", "RST", "PSH", "FIN" };
            Enq(1, $"{DateTime.Now:HH:mm:ss.fff} {Ip(),-15} -> {Ip(),-15} {k[Rng.Next(k.Length)]} len={Rng.Next(40, 1500)}", 0);
        }
        if (_scanProg < 1) { _scanProg = Math.Min(1, _scanProg + dt / 3.2); _scanNodes = (int)(_scanProg * C.Sim.Nodes.Count); }
        if (_enc < 1)
        {
            _enc = Math.Min(1, _enc + dt / 6);
            if (_enc >= 1) { Enq(_active, "KEY RECOVERED: " + string.Concat(Enumerable.Range(0, 16).Select(_ => "0123456789ABCDEF"[Rng.Next(16)])), 0.05); Enq(_active, "ACCESS GRANTED", 0.2); _granted = true; _grantedT = 0; C.Audio.Confirm(); }
        }
        if (_granted) { _grantedT += dt; if (_grantedT > 3) _granted = false; }
        // packets on links
        if (Rng.NextDouble() < dt * 14 && _packets.Count < 40) { int a = Rng.Next(C.Sim.Nodes.Count); _packets.Add((a, (a + (Rng.Next(2) == 0 ? 1 : 8)) % C.Sim.Nodes.Count, 0)); }
        for (int i = _packets.Count - 1; i >= 0; i--) { var p = _packets[i]; p.t += dt * 1.2; if (p.t >= 1) _packets.RemoveAt(i); else _packets[i] = p; }
    }

    public override void Draw(Gfx g, Rect area)
    {
        var saved = g.P;
        g.P = saved with { Primary = Themes[_theme].col, Accent = Palette.Lerp(Themes[_theme].col, 0xFFFFFFFF, 0.55), Secondary = Palette.Lerp(Themes[_theme].col, 0xFF000000, 0.35) };
        var lay = Layout.Adaptive(g, area, 6, 0.54);
        DrawTerminals(g, lay.Main);
        for (int i = 0; i < lay.Panels.Count; i++) DrawPanel(g, i, lay.Panels[i]);
        if (_granted)
        {
            double a = Math.Clamp(1 - _grantedT / 3, 0, 1);
            g.Text("ACCESS GRANTED", area.Center.X, area.Center.Y - g.U(20), 40, g.P.Accent, Align.Center, true, a);
            g.Frame(new Rect(area.Center.X - g.U(190), area.Center.Y - g.U(34), g.U(380), g.U(66)), g.P.Primary, 2, a);
        }
        g.P = saved;
    }

    void DrawTerminals(Gfx g, Rect r)
    {
        var open = _terms.Select((t, i) => (t, i)).Where(x => x.t.Open && !x.t.Min).ToList();
        var chips = _terms.Select((t, i) => (t, i)).Where(x => x.t.Open && x.t.Min).ToList();
        double chipH = chips.Count > 0 ? g.U(26) : 0;
        var area = new Rect(r.X, r.Y, r.Width, r.Height - chipH);
        var max = open.FirstOrDefault(x => x.t.Max);
        List<(Term t, int i, Rect r)> placed = new();
        double gap = g.U(6);
        if (max.t != null) placed.Add((max.t, max.i, area));
        else if (open.Count == 1) placed.Add((open[0].t, open[0].i, area));
        else if (open.Count == 2) { var rr = area.Width > area.Height * 1.4 ? Layout.Cols(area, gap, 1, 1) : Layout.Rows(area, gap, 1, 1); for (int k = 0; k < 2; k++) placed.Add((open[k].t, open[k].i, rr[k])); }
        else if (open.Count >= 3)
        {
            int cols = area.Width > g.U(520) ? 2 : 1; int n = open.Count; int rows = (n + cols - 1) / cols;
            double cw = (area.Width - gap * (cols - 1)) / cols, ch = (area.Height - gap * (rows - 1)) / rows;
            for (int k = 0; k < n; k++) placed.Add((open[k].t, open[k].i, new Rect(area.X + (k % cols) * (cw + gap), area.Y + (k / cols) * (ch + gap), cw, ch)));
        }
        foreach (var (t, i, rc) in placed) DrawTerm(g, t, i, rc);
        double cx = r.X;
        foreach (var (t, i) in chips)
        {
            double w = g.U(150);
            g.Button("term:min:" + i, new Rect(cx, r.Bottom - chipH + g.U(2), w, chipH - g.U(4)), t.Title.Split(' ')[0] + " ▲".Replace("▲", "^"), false, true, null, 9);
            cx += w + g.U(4);
        }
    }

    void DrawTerm(Gfx g, Term t, int idx, Rect r)
    {
        bool act = idx == _active;
        g.Fill(r, g.P.Background, 0.85);
        g.Frame(r, g.P.Primary, act ? 1.6 : 1, act ? 0.95 : 0.4);
        if (act) g.Brackets(r, g.P.Accent, 9, 1.8);
        double th = g.U(g.Touch ? 26 : 20);
        var bar = new Rect(r.X, r.Y, r.Width, th);
        g.Fill(bar, g.P.Primary, act ? 0.22 : 0.08);
        g.Text(t.Title, r.X + g.U(8), r.Y + g.U(4), 9.5, g.P.Primary, Align.Left, true);
        g.AddHit("term:sel:" + idx, new Rect(r.X, r.Y + th, r.Width, r.Height - th));
        double bw = th - g.U(4);
        g.Button("term:close:" + idx, new Rect(r.Right - bw - g.U(3), r.Y + g.U(2), bw, bw), "x", false, true, g.P.Warning, 9);
        g.Button("term:max:" + idx, new Rect(r.Right - bw * 2 - g.U(6), r.Y + g.U(2), bw, bw), "o", t.Max, true, null, 9);
        g.Button("term:min:" + idx, new Rect(r.Right - bw * 3 - g.U(9), r.Y + g.U(2), bw, bw), "_", false, true, null, 9);

        double fs = 9.5, lh = g.U(fs + 4);
        var body = new Rect(r.X + g.U(6), r.Y + th + g.U(4), r.Width - g.U(12), r.Height - th - g.U(8));
        int maxLines = Math.Max(1, (int)(body.Height / lh) - 1);
        using (g.Clip(body))
        {
            int start = Math.Max(0, t.Lines.Count - maxLines - (int)t.Scroll);
            int end = Math.Min(t.Lines.Count, start + maxLines);
            for (int i = start; i < end; i++)
            {
                var line = t.Lines[i]; bool cmd = line.StartsWith("> ");
                g.Text(line, body.X, body.Y + (i - start) * lh, fs, cmd ? g.P.Accent : g.P.Primary, Align.Left, cmd, cmd ? 1 : 0.85);
            }
            double y = body.Y + (end - start) * lh;
            string prompt = (act ? "> " : "  ") + t.Input + (act && g.Pulse(1.6) > 0.5 ? "█".Replace("█", "|") : " ");
            g.Text(prompt, body.X, Math.Min(y, body.Bottom - lh), fs, g.P.Accent, Align.Left, true);
        }
    }

    void DrawPanel(Gfx g, int i, Rect r)
    {
        switch (i)
        {
            case 0: DrawTopology(g, r); break;
            case 1:
                {
                    var c = g.Panel(r, "HEX STREAM // 0x7F");
                    using (g.Clip(c)) g.HexStream(c, Rng, g.P.Primary, 14, 8.5);
                    break;
                }
            case 2:
                {
                    var c = g.Panel(r, "SYSTEM ACTIVITY");
                    var rows = Layout.Rows(c, g.U(4), 1, 1, 1);
                    g.Graph(rows[0], C.Sim.Cpu, g.P.Primary, 0, 1, true, "CPU");
                    g.Graph(rows[1], C.Sim.NetIn, g.P.Accent, null, null, true, "NET IN", " KB/S");
                    g.Graph(rows[2], C.Sim.Packets, g.P.Secondary, null, null, true, "PKT/S");
                    break;
                }
            case 3:
                {
                    var c = g.Panel(r, "IP MONITOR");
                    double lh = g.U(13); int n = Math.Min(8, (int)(c.Height / lh));
                    for (int k = 0; k < n; k++)
                    {
                        var node = C.Sim.Nodes[k];
                        g.StatusDot(c.X + g.U(4), c.Y + k * lh + g.U(6), !node.UnderAttack, 2.5);
                        g.Text(node.Ip, c.X + g.U(12), c.Y + k * lh, 8.5, g.P.Text);
                        g.Text(node.Name, c.Right, c.Y + k * lh, 8.5, node.UnderAttack ? g.P.Warning : g.P.Primary, Align.Right, true);
                    }
                    break;
                }
            case 4:
                {
                    var c = g.Panel(r, "ENCRYPTION");
                    double encV = _everDecrypt ? _enc : 0; g.Bar(new Rect(c.X, c.Y, c.Width, g.U(26)), encV, g.P.Primary, "KEY RECOVERY", $"{encV * 100:0}%");
                    int cols = Math.Max(6, (int)(c.Width / g.U(11))); var sb = new StringBuilder();
                    int shown = _everDecrypt ? (int)(_encTarget.Length * _enc) : 0;
                    for (int k = 0; k < _encTarget.Length; k++) sb.Append(k < shown ? _encTarget[k] : "0123456789ABCDEF#%&"[Rng.Next(19)]);
                    g.Text(sb.ToString(), c.Center.X, c.Y + g.U(34), 15, _everDecrypt && _enc >= 1 ? g.P.Accent : g.P.Primary, Align.Center, true, _everDecrypt ? 1 : 0.4);
                    g.Text(!_everDecrypt ? "STATUS: IDLE — RUN DECRYPT" : _enc >= 1 ? "STATUS: DECRYPTED" : "STATUS: WORKING", c.Center.X, c.Y + g.U(58), 9, g.P.Text, Align.Center, false, 0.8);
                    break;
                }
            case 5:
                {
                    var c = g.Panel(r, "QUICK COMMANDS");
                    string[] cmds = { "HELP", "SCAN NETWORK", "TRACE ROUTE", "DECRYPT", "SYSTEM STATUS", "ANALYZE PACKETS", "INITIALIZE PROTOCOL", "ACCESS GRANTED" };
                    int cols = c.Width > g.U(300) ? 2 : 1; int rowsN = (cmds.Length + cols - 1) / cols;
                    double gap = g.U(3), bh = Math.Min(g.U(g.Touch ? 32 : 24), (c.Height - gap * (rowsN - 1)) / rowsN);
                    double bw = (c.Width - gap * (cols - 1)) / cols;
                    for (int k = 0; k < cmds.Length; k++) g.Button("cmd:" + cmds[k], new Rect(c.X + (k % cols) * (bw + gap), c.Y + (k / cols) * (bh + gap), bw, bh), cmds[k], false, true, null, 8);
                    break;
                }
        }
    }

    void DrawTopology(Gfx g, Rect r)
    {
        var c = g.Panel(r, "NETWORK TOPOLOGY");
        var nodes = C.Sim.Nodes;
        Point P(int i) => new(c.X + nodes[i].X * c.Width, c.Y + (0.1 + (nodes[i].Y - 0.1) * 0.95) * c.Height * 0.95);
        for (int i = 0; i < nodes.Count; i++)
        {
            int j1 = (i + 1) % nodes.Count, j2 = (i + 8) % nodes.Count;
            var a = P(i); var b1 = P(j1); var b2 = P(j2);
            g.Line(a.X, a.Y, b1.X, b1.Y, g.P.Primary, 1, 0.15); g.Line(a.X, a.Y, b2.X, b2.Y, g.P.Primary, 1, 0.15);
        }
        foreach (var p in _packets) { var a = P(p.a); var b = P(p.b); g.Disc(a.X + (b.X - a.X) * p.t, a.Y + (b.Y - a.Y) * p.t, g.U(1.8), g.P.Accent); }
        for (int i = 0; i < nodes.Count; i++)
        {
            var p = P(i); bool scanned = _scanProg >= 1 || i < _scanNodes;
            g.Disc(p.X, p.Y, g.U(3), scanned ? g.P.Primary : g.P.Secondary, scanned ? 1 : 0.4);
            if (nodes[i].UnderAttack) g.Circle(p.X, p.Y, g.U(6 + 2 * g.Pulse(2)), g.P.Warning, 1.2);
        }
        if (_scanProg < 1) g.Text($"SCANNING {_scanProg * 100:0}%", c.Right, c.Y, 8.5, g.P.Accent, Align.Right, true);
    }

    public override void Tap(Point p, string? id)
    {
        if (id == null) return;
        if (id.StartsWith("term:sel:")) { _active = int.Parse(id[9..]); }
    }

    public override bool Drag(Point p, Vector d, string? id)
    {
        if (id != null && id.StartsWith("term:sel:")) { var t = _terms[int.Parse(id[9..])]; t.Scroll = Math.Clamp(t.Scroll + d.Y / 12, 0, Math.Max(0, t.Lines.Count - 4)); return true; }
        return false;
    }

    public override void Wheel(Point p, double delta) { var t = Cur; t.Scroll = Math.Clamp(t.Scroll + delta * 3, 0, Math.Max(0, t.Lines.Count - 4)); }

    /// <summary>Button handler for terminal-window and quick-command buttons (routed by the shell as module buttons).</summary>
    public override string? OnModuleButton(string id)
    {
        if (id.StartsWith("cmd:")) { Run(id[4..]); return id[4..] + " EXECUTED"; }
        var p = id.Split(':');
        if (p[0] != "term") return null;
        int i = int.Parse(p[2]); var t = _terms[i];
        switch (p[1])
        {
            case "close": t.Open = false; if (_active == i) _active = NextOpen(i); return "TERMINAL CLOSED";
            case "max": t.Max = !t.Max; foreach (var o in _terms) if (o != t) o.Max = false; _active = i; return t.Max ? "MAXIMISED" : "RESTORED";
            case "min": t.Min = !t.Min; if (t.Min && _active == i) _active = NextOpen(i); else _active = i; return t.Min ? "MINIMISED" : "RESTORED";
        }
        return null;
    }
}
