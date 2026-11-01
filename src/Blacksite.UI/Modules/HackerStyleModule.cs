using System.Text;
using Avalonia;
using Avalonia.Media;
using Blacksite.Core.Audio;
using Blacksite.Core.Themes;
using Blacksite.UI.Rendering;

namespace Blacksite.UI.Modules;

/// <summary>
/// PRESET 02 – HACKER TERMINAL (interface v3). Header bar, 4 + 4 numbered panels, a realistic TERMINAL window with a hex block and
/// a COMPUTE button. Three scenes (STYLE button): green ORBITAL SATELLITE, blue GLOBAL DATABASE, red SAT-7.
/// Everything is fictional and generated locally; nothing real is scanned or attacked.
/// </summary>
public sealed class HackerStyleModule : ModuleBase
{
    enum Kind { Out, Cmd, Ok, Info, Warn, Err, Dim, Prompt, Progress, Hex, Stamp }
    sealed class Line { public string Text = ""; public Kind K; public int Shown; public bool Done; }

    static readonly string[] SceneNames = { "ORBITAL SATELLITE", "GLOBAL DATABASE", "SAT-7 HACK" };
    static readonly string[] Hosts = { "203.0.113.47", "198.51.100.23", "192.0.2.77" };
    static readonly string[] Targets = { "ORBITAL SATELLITE", "GLOBAL DATABASE", "SATELLITE SAT-7" };
    int Scene { get => Math.Clamp(C.Settings.HackerStyle, 0, 2); set => C.Settings.HackerStyle = value; }
    string Host => Hosts[Scene];

    readonly List<Line> _lines = new();
    readonly Queue<(string text, Kind k, double cps, double after)> _q = new();
    Line? _cur; double _curCps, _curAfter, _wait, _t;
    string _input = ""; readonly List<string> _history = new(); int _hpos = -1;
    int _nav; int _stage; double _idleT;
    // operation state (0..1)
    double _crack = 0.3, _db = 0.2, _key = 0.2, _fw = 0.3, _up = 0.5, _trace = 0.3, _files = 0.3;
    double _compute = -1, _grantedT = -1; bool _granted;
    readonly List<(double lon, double lat)> _dots = new();
    readonly List<(double x, double y, double z)> _sph = new(); readonly List<(int a, int b)> _sphEdges = new();
    static readonly string[] Words = { "sunrise77", "Orpheus!24", "trustno1", "P@ssw0rd!", "kepler-x2", "Winter2026", "nightfall", "H3lios#1", "vanguard7", "satlink!1", "zenith_88" };

    public override string[] Actions => new[] { "COMPUTE", "SCAN", "STYLE", "RESET", "CLEAR" };
    public override bool WantsText => true;
    public override string PrimaryAction => "COMPUTE";
    public override bool NoTicker => true;
    public override bool IsActive(string a) => a == "COMPUTE" && _compute >= 0;

    protected override void OnInit()
    {
        // dot-matrix world map: sample the bundled continent outlines on a lat/lon lattice
        for (int la = -58; la <= 80; la += 3)
            for (int lo = -170; lo <= 178; lo += 3)
                if (OnLand(lo, la)) _dots.Add((lo, la));
        // fibonacci sphere with neighbour links (decryption visualiser)
        int n = 110; for (int i = 0; i < n; i++) { double y = 1 - 2.0 * (i + 0.5) / n, rr = Math.Sqrt(1 - y * y), a = i * 2.399963; _sph.Add((Math.Cos(a) * rr, y, Math.Sin(a) * rr)); }
        for (int i = 0; i < n; i++) for (int j = i + 1; j < n; j++) { var p = _sph[i]; var q = _sph[j]; double d = (p.x - q.x) * (p.x - q.x) + (p.y - q.y) * (p.y - q.y) + (p.z - q.z) * (p.z - q.z); if (d < 0.2) _sphEdges.Add((i, j)); }
        ResetOp();
    }

    static bool OnLand(double lon, double lat)
    {
        foreach (var poly in World.Continents)
        {
            bool inside = false; int n = poly.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double xi = poly[i * 2], yi = poly[i * 2 + 1], xj = poly[j * 2], yj = poly[j * 2 + 1];
                if ((yi > lat) != (yj > lat) && lon < (xj - xi) * (lat - yi) / (yj - yi) + xi) inside = !inside;
            }
            if (inside) return true;
        }
        return false;
    }

    // ------------------------------------------------------------------ palette per scene
    Palette Pal(Palette p) => Scene switch
    {
        0 => p with { Background = 0xFF010803, Panel = 0xFF03130A, Primary = 0xFF33FF66, Secondary = 0xFF139A3A, Accent = 0xFFD8FFE0, Text = 0xFF9CFFB4, Warning = 0xFFFFC040 },
        1 => p with { Background = 0xFF020812, Panel = 0xFF05122A, Primary = 0xFF2AA8FF, Secondary = 0xFF1666C8, Accent = 0xFF8AE0FF, Text = 0xFFCFE8FF, Warning = 0xFFFFB020 },
        _ => p with { Background = 0xFF0C0204, Panel = 0xFF1A0508, Primary = 0xFFFF3232, Secondary = 0xFFA81C1C, Accent = 0xFFFFA090, Text = 0xFFFFD2CA, Warning = 0xFFFFB020 },
    };

    // ------------------------------------------------------------------ scripted session
    void Enq(string t, Kind k, double cps = 90, double after = 0.05) => _q.Enqueue((t, k, cps, after));

    void ResetOp()
    {
        _lines.Clear(); _q.Clear(); _cur = null; _compute = -1; _grantedT = -1; _granted = false; _stage = 0; _idleT = 0; _wait = 0;
        _crack = 0.3; _db = 0.2; _key = 0.2; _fw = 0.3; _up = 0.5; _trace = 0.3; _files = 0.3;
        Script(0);
    }

    void Script(int block)
    {
        switch (Scene)
        {
            case 0:
                if (block == 0)
                {
                    Enq($"\u0001./decrypt.sh --target={Host}", Kind.Prompt, 34, 0.4);
                    foreach (var l in new[] { "[*] Initializing attack vector...", "[*] Target identified: ORBITAL SATELLITE", "[*] Establishing connection...", "[*] Connection established.", "[*] Loading encryption modules...", "[*] Starting brute force attack..." }) Enq(l, Kind.Info, 110, 0.25);
                }
                else if (block == 1) { Enq("[*] Progress: 47%  |  Speed: 12.4M keys/sec", Kind.Info, 110, 0.4); Enq("[*] Remaining time: 00:02:31", Kind.Info, 110, 0.3); Enq("[*] Press COMPUTE to run the full decrypt", Kind.Info, 110, 0.2); }
                break;
            case 1:
                if (block == 0)
                {
                    foreach (var l in new[] { "[*] Scanning target systems...", "[*] Found 1 open port (22/tcp)", $"[*] Connecting to {Host}...", "[*] Authentication required...", "[*] Attempting credential brute force...", "[*] User: admin  |  Password: *********", "[*] Access level: 1", "[*] Enumerating database structure...", "[*] Found 42 tables, 1287 records" }) Enq(l, Kind.Info, 110, 0.28);
                }
                else if (block == 1) { Enq("[*] Decrypting data blocks...", Kind.Info, 110, 0.4); Enq("[*] Use DECRYPT / EXTRACT / ACCESS or COMPUTE", Kind.Info, 110, 0.2); }
                break;
            default:
                if (block == 0)
                {
                    foreach (var l in new[] { "Establishing connection to SAT-7...", "Connection established.", "Starting authentication bypass...", "Bypassing encryption (AES-256)...", "Extracting data blocks...", "Decryption progress: 42%", "Decryption progress: 67%", "Decryption progress: 91%" }) Enq($"[{DateTime.Now.AddSeconds(Enq0++):HH:mm:ss}] {l}", Kind.Stamp, 100, 0.3);
                }
                else if (block == 1) Enq("Awaiting COMPUTE for final decrypt...", Kind.Warn, 100, 0.2);
                break;
        }
    }
    int Enq0;

    public override string? OnAction(string id)
    {
        switch (id)
        {
            case "COMPUTE": return StartCompute();
            case "SCAN": Run("SCAN NETWORK"); C.Audio.Play("scan", SoundCategory.Ui); return "NETWORK SWEEP";
            case "STYLE": Scene = (Scene + 1) % 3; C.Save(); ResetOp(); C.Audio.Play("chime", SoundCategory.Ui, 0.6f); return "SCENE: " + SceneNames[Scene];
            case "RESET": ResetOp(); C.Audio.Play("deepbeep"); return "SESSION RESET";
            case "CLEAR": _lines.Clear(); _q.Clear(); _cur = null; return "TERMINAL CLEARED";
        }
        return null;
    }

    string? StartCompute()
    {
        if (_compute >= 0) { C.Audio.Error(); return "COMPUTE ALREADY RUNNING"; }
        _compute = 0; _granted = false; _grantedT = -1; _q.Clear(); _cur = null;
        _lines.Add(new Line { Text = "\u0001compute --decrypt --all --target " + Host, K = Kind.Prompt, Shown = 999, Done = true });
        Enq("[*] spawning 512 workers across 4 GPU nodes", Kind.Info, 160, 0.2);
        _lines.Add(new Line { Text = "COMPUTE", K = Kind.Progress, Done = true, Shown = 7 });
        C.Audio.Play("scan", SoundCategory.Event); C.Audio.Play("surge", SoundCategory.Event, 0.5f);
        return "COMPUTE STARTED";
    }

    void Run(string raw)
    {
        string cmd = raw.Trim().ToUpperInvariant(); if (cmd.Length == 0) return;
        _history.Add(raw.Trim()); _hpos = -1;
        _lines.Add(new Line { Text = "\u0001" + raw.Trim(), K = Kind.Prompt, Shown = 999, Done = true });
        switch (cmd)
        {
            case "HELP": foreach (var l in new[] { "commands:", "  compute            run the full decrypt (access granted)", "  scan network       sweep the target", "  trace route        trace the path", "  decrypt            single-key brute force", "  system status      session telemetry", "  analyze packets    packet statistics", "  initialize protocol  arm the stack", "  style | reset | clear" }) Enq(l, Kind.Out, 400, 0.02); break;
            case "COMPUTE": case "ACCESS GRANTED": StartCompute(); break;
            case "SCAN NETWORK": Script(0); break;
            case "TRACE ROUTE": Enq($"traceroute to {Host}, 8 hops max", Kind.Out, 300, 0.1); for (int i = 1; i <= 7; i++) Enq($" {i}  198.51.100.{Rng.Next(2, 250),-3}  {Rng.Next(3, 80)}.{Rng.Next(10)} ms", Kind.Out, 300, 0.12); Enq($" 8  {Host}  {Rng.Next(60, 140)}.2 ms  [TARGET]", Kind.Ok, 300, 0.2); _trace = 1; break;
            case "DECRYPT": case "EXTRACT": case "ACCESS":
                Enq($"[*] {cmd.ToLower()}: working on {Targets[Scene].ToLower()}...", Kind.Info, 200, 0.3);
                if (cmd == "DECRYPT") _crack = Math.Min(1, _crack + 0.15); if (cmd == "EXTRACT") _files = Math.Min(1, _files + 0.2); if (cmd == "ACCESS") { if (_crack > 0.85 && _files > 0.85) StartCompute(); else Enq("[!] access denied — decrypt and extract first (or press COMPUTE)", Kind.Warn, 200, 0.2); }
                break;
            case "SYSTEM STATUS": Enq($"session {Host}  workers 512  gpu 4/4  temp 71C", Kind.Out, 300, 0.1); Enq($"crack {_crack * 100:0}%  db {_db * 100:0}%  key {_key * 100:0}%  uplink {_up * 100:0}%", Kind.Out, 300, 0.1); break;
            case "ANALYZE PACKETS": Enq("capturing 8192 frames ...", Kind.Info, 200, 0.4); Enq($"  tcp {Rng.Next(55, 70)}%  udp {Rng.Next(15, 25)}%  icmp {Rng.Next(1, 6)}%  anomalies {Rng.Next(2, 9)}", Kind.Out, 300, 0.2); break;
            case "INITIALIZE PROTOCOL": foreach (var s in new[] { "arming handshake layer", "negotiating session keys", "binding virtual interfaces" }) Enq("  [ OK ] " + s, Kind.Ok, 300, 0.25); break;
            case "STYLE": OnAction("STYLE"); break;
            case "RESET": ResetOp(); break;
            case "CLEAR": _lines.Clear(); break;
            default: Enq($"bash: {raw.Trim().Split(' ')[0]}: command not found", Kind.Err, 300, 0.1); C.Audio.Error(); break;
        }
    }

    public override void Key(string key, string? text)
    {
        if (text != null) { foreach (var ch in text) if (!char.IsControl(ch) && _input.Length < 70) _input += ch; C.Audio.Play("click", SoundCategory.Ambient, 0.35f); return; }
        switch (key)
        {
            case "Enter": var c = _input; _input = ""; Run(c); C.Audio.Play("beep", SoundCategory.Ui, 0.6f); break;
            case "Back": if (_input.Length > 0) _input = _input[..^1]; break;
            case "Up": if (_history.Count > 0) { _hpos = _hpos < 0 ? _history.Count - 1 : Math.Max(0, _hpos - 1); _input = _history[_hpos]; } break;
            case "Down": if (_hpos >= 0) { _hpos++; _input = _hpos >= _history.Count ? "" : _history[_hpos]; if (_hpos >= _history.Count) _hpos = -1; } break;
            case "Space": if (_input.Length < 70) _input += " "; break;
            default: if (key.Length == 1 && _input.Length < 70) _input += key; break;
        }
    }

    // ------------------------------------------------------------------ simulation
    public override void Update(double dt)
    {
        _t += dt;
        if (_cur != null)
        {
            _cur.Shown = Math.Min(_cur.Text.Length, _cur.Shown + Math.Max(1, (int)(dt * _curCps)));
            if (_cur.Shown >= _cur.Text.Length) { _cur.Done = true; _cur = null; _wait = _curAfter; }
            else if ((int)(_t * 40) % 3 == 0) C.Audio.Play("click", SoundCategory.Ambient, 0.10f);
        }
        else if (_wait > 0) _wait -= dt;
        else if (_q.Count > 0)
        {
            var (text, k, cps, after) = _q.Dequeue(); var l = new Line { Text = text, K = k, Shown = cps <= 0 ? text.Length : 0 };
            _lines.Add(l); if (_lines.Count > 300) _lines.RemoveAt(0);
            if (cps <= 0) { l.Done = true; _wait = after; } else { _cur = l; _curCps = cps; _curAfter = after; }
        }
        else if (_compute < 0 && !_granted)
        {
            _idleT += dt;
            if (_idleT > 3 && _stage == 0) { Script(1); _stage = 2; _idleT = 0; }
            else if (_idleT > 16 && _stage >= 2) { _idleT = 0; Enq($"[*] heartbeat ok  rtt {20 + Rng.Next(60)}ms  workers 512", Kind.Dim, 150, 0.1); }
        }
        // ambient breathing so every panel is always alive
        if (_compute < 0 && _grantedT < 0)
        {
            double b(double s, double o) => 0.5 + 0.5 * Math.Sin(_t * s + o);
            _crack = 0.25 + 0.5 * b(0.11, 0); _db = 0.2 + 0.55 * b(0.09, 1); _key = 0.15 + 0.5 * b(0.08, 2); _fw = 0.3 + 0.4 * b(0.14, 3); _up = 0.45 + 0.4 * b(0.2, 4); _trace = 0.3 + 0.5 * b(0.1, 5); _files = 0.25 + 0.6 * b(0.07, 6);
        }
        if (_compute >= 0)
        {
            _compute += dt / 9.0; double c = Math.Clamp(_compute, 0, 1), e = c * c * (3 - 2 * c);
            _crack = Math.Max(_crack, e); _db = Math.Max(_db, Math.Min(1, e * 1.05)); _key = Math.Max(_key, Math.Min(1, e * 1.1)); _fw = Math.Max(_fw, Math.Min(1, e * 1.2)); _up = Math.Max(_up, Math.Min(1, e * 1.15)); _trace = Math.Max(_trace, Math.Min(1, e * 1.25)); _files = Math.Max(_files, Math.Min(1, e * 1.1));
            if (_compute >= 1)
            {
                _compute = -1; _granted = true; _grantedT = 0; _crack = _db = _key = _fw = _up = _trace = _files = 1;
                Enq("[+] all hashes resolved — session key 256-bit RECOVERED", Kind.Ok, 200, 0.25); Enq("[+] uplink authenticated — data stream OPEN", Kind.Ok, 200, 0.25); Enq("ACCESS GRANTED", Kind.Ok, 20, 0.2);
                C.Audio.Play("access", SoundCategory.Event); C.Audio.Play("chime", SoundCategory.Event); C.Events.Trigger("mission_orders");
            }
        }
        if (_grantedT >= 0) { _grantedT += dt; if (_grantedT > 10) { _grantedT = -1; _granted = false; _stage = 1; _idleT = 0; } }
    }

    // ------------------------------------------------------------------ helpers
    static uint Hh(uint x) { x ^= x >> 16; x *= 0x7feb352d; x ^= x >> 15; x *= 0x846ca68b; x ^= x >> 16; return x; }
    static string HexBytes(uint seed, int n) { var sb = new StringBuilder(); uint h = Hh(seed); for (int i = 0; i < n; i++) { sb.Append((h & 0xFF).ToString("X2")).Append(' '); h = Hh(h + 0x9e3779b9u); } return sb.ToString().TrimEnd(); }
    double Pct(double v) => Math.Round(v * 100);

    // ------------------------------------------------------------------ layout
    public override void Draw(Gfx g, Rect area)
    {
        var saved = g.P; g.P = Pal(saved);
        g.Fill(area, g.P.Background, 1);
        if (Scene == 2) g.Grid(area, 40, g.P.Primary, 0.03); else if (Scene == 1) g.Grid(area, 40, g.P.Primary, 0.035);
        double gap = g.U(7), hh = g.U(24), fh = Scene == 2 ? g.U(16) : 0;
        DrawHeader(g, new Rect(area.X, area.Y, area.Width, hh));
        var a = new Rect(area.X + gap, area.Y + hh + gap * 0.6, area.Width - gap * 2, area.Height - hh - gap * 1.6 - fh);
        bool narrow = area.Width < g.U(900) || area.Width / area.Height < 1.25;
        Rect term; var L = new Rect[4]; var R = new Rect[4];
        if (!narrow)
        {
            double navW = Scene == 0 ? g.U(64) : 0; var body = new Rect(a.X + navW, a.Y, a.Width - navW, a.Height);
            if (navW > 0) DrawNav(g, new Rect(a.X, a.Y, navW - gap, a.Height));
            double sw = body.Width * 0.235;
            L = Layout.Column(new Rect(body.X, body.Y, sw, body.Height), 4, gap).ToArray(); R = Layout.Column(new Rect(body.Right - sw, body.Y, sw, body.Height), 4, gap).ToArray();
            term = new Rect(body.X + sw + gap, body.Y, body.Width - sw * 2 - gap * 2, body.Height);
        }
        else
        {
            term = new Rect(a.X, a.Y, a.Width, a.Height * 0.52); double ty = term.Bottom + gap, cw = (a.Width - gap) / 2, ch = (a.Bottom - ty - gap * 3) / 4;
            for (int i = 0; i < 4; i++) { L[i] = new Rect(a.X, ty + i * (ch + gap), cw, ch); R[i] = new Rect(a.X + cw + gap, ty + i * (ch + gap), cw, ch); }
        }
        string[][] titles = { new[] { "SATELLITE SCAN", "NETWORK TRAFFIC", "ENCRYPTION ANALYSIS", "SYSTEM LOGS", "DECRYPTION VISUALIZER", "FILE EXTRACTION", "SIGNAL ANALYSIS", "GEO LOCATION" },
            new[] { "THREAT MAP", "DATA FLOW", "SYSTEM INTEGRITY", "USER ACTIVITY", "DATABASE STRUCTURE", "DECRYPTION MATRIX", "SECURITY PROTOCOLS", "SATELLITE LINK" },
            new[] { "VISUAL RECON", "SIGNAL ANALYSIS", "NETWORK NODES", "THREAT DETECTION", "DATA TRANSFER", "FILE STRUCTURE", "ENCRYPTION KEYS", "SYSTEM STATUS" } };
        for (int i = 0; i < 4; i++) { Panel(g, L[i], $"{i + 1:00} - {titles[Scene][i]}", (gg, r) => Body(gg, r, i)); Panel(g, R[i], $"{i + 5:00} - {titles[Scene][i + 4]}", (gg, r) => Body(gg, r, i + 4)); }
        DrawTerminal(g, term);
        if (fh > 0) { g.Text("◉ BLACKSITE  v3.2.7".Replace("◉", "x"), area.X + g.U(10), area.Bottom - fh + g.U(2), 7.5, g.P.Primary, Align.Left, true, 0.7); g.Text("SECURE  //  ANONYMOUS  //  NO LOGS", area.Right - g.U(10), area.Bottom - fh + g.U(2), 7.5, g.P.Text, Align.Right, false, 0.6); }
        if (_granted) DrawGranted(g, area);
        if (Scene == 0 && g.Quality != Core.Configuration.GraphicsProfile.Low) for (double y = area.Y; y < area.Bottom; y += g.U(3)) g.Fill(new Rect(area.X, y, area.Width, 1), 0xFF000000, 0.12);
        g.P = saved;
    }

    void DrawHeader(Gfx g, Rect r)
    {
        g.Fill(r, g.P.Primary, 0.05); g.Line(r.X, r.Bottom, r.Right, r.Bottom, g.P.Primary, 1, 0.5);
        g.Text("BLACKSITE", r.X + g.U(10), r.Y + g.U(3), 15, g.P.Primary, Align.Left, true);
        g.Text(Scene == 2 ? "HACKER INTERFACE" : Scene == 1 ? "// HACKING INTERFACE" : "HACKING INTERFACE", r.X + g.U(106), r.Y + g.U(9), 7.5, g.P.Text, Align.Left, false, 0.65);
        string status = _granted ? "ACCESS GRANTED" : _compute >= 0 ? "DECRYPTING" : "ENCRYPTED";
        g.Text($"TARGET: {Targets[Scene]}   //   IP: {Host}   //   STATUS: {status}", r.Center.X, r.Y + g.U(7), 8.5, _granted ? g.P.Accent : g.P.Text, Align.Center, true, 0.95);
        g.Text(Scene == 1 ? $"{DateTime.Now:HH:mm:ss}    SYSTEM ONLINE" : "SYSTEM v3.2.7", r.Right - g.U(22), r.Y + g.U(8), 7.5, g.P.Text, Align.Right, false, 0.75);
        g.Led(r.Right - g.U(10), r.Y + g.U(12), 1);
    }

    void DrawNav(Gfx g, Rect r)
    {
        string[] items = { "MAIN", "NETWORK", "SATELLITE", "DATABASE", "FILES", "SETTINGS" }; double ih = g.U(g.Touch ? 28 : 22);
        for (int i = 0; i < items.Length; i++)
        {
            var rc = new Rect(r.X, r.Y + g.U(6) + i * ih, r.Width, ih); bool sel = i == _nav;
            if (sel) { g.Fill(rc, g.P.Primary, 0.14); g.Fill(new Rect(rc.X, rc.Y, g.U(2), rc.Height), g.P.Primary, 1); }
            g.Text(items[i], rc.X + g.U(8), rc.Y + (ih - g.U(10)) / 2, 8.5, sel ? g.P.Accent : g.P.Text, Align.Left, sel, sel ? 1 : 0.65);
            g.AddHit("hk:nav:" + i, rc, HitKind.Button);
        }
    }

    void Panel(Gfx g, Rect r, string title, Action<Gfx, Rect> body)
    {
        g.Fill(r, g.P.Panel, 0.78); g.Frame(r, g.P.Primary, 3, 0.10); g.Frame(r, g.P.Primary, 1, 0.75); g.Brackets(r, g.P.Primary, 7, 1.6);
        g.Text(title, r.X + g.U(7), r.Y + g.U(4), 8, g.P.Primary, Align.Left, true);
        g.Text("x", r.Right - g.U(9), r.Y + g.U(3), 8, g.P.Text, Align.Right, false, 0.5);
        var inner = new Rect(r.X + g.U(6), r.Y + g.U(18), r.Width - g.U(12), r.Height - g.U(22));
        using (g.Clip(inner)) body(g, inner);
    }

    // ------------------------------------------------------------------ panel bodies
    void Stats(Gfx g, Rect r, params (string k, string v)[] rows)
    {
        double lh = g.U(11.5);
        for (int i = 0; i < rows.Length; i++) { if (r.Y + (i + 1) * lh > r.Bottom + 2) break; g.Text(rows[i].k, r.X, r.Y + i * lh, 7.5, g.P.Text, Align.Left, false, 0.75); g.Text(rows[i].v, r.X + g.TextWidth(rows[i].k, 7.5 * g.Scale * g.S.FontScale) + g.U(5), r.Y + i * lh, 7.5, g.P.Accent, Align.Left, true); }
    }

    void Body(Gfx g, Rect r, int idx)
    {
        var left = new Rect(r.X, r.Y, r.Width * 0.5, r.Height); var right = new Rect(r.X + r.Width * 0.5 + g.U(4), r.Y, r.Width * 0.5 - g.U(4), r.Height);
        double ts = g.TextWidth("X", 7.5 * g.Scale);
        switch (Scene)
        {
            case 0:
                switch (idx)
                {
                    case 0: Globe(g, new Rect(r.X, r.Y, r.Width, r.Height - g.U(24)), true); g.Text("Scanning frequency bands...", r.X, r.Bottom - g.U(23), 7.5, g.P.Accent, Align.Left); g.Text($"Found {1 + (int)(_up * 3)} active satellites", r.X, r.Bottom - g.U(12), 7.5, g.P.Text, Align.Left, false, 0.8); break;
                    case 1: WorldDots(g, new Rect(r.X, r.Y, r.Width, r.Height - g.U(26)), false, 6); g.Text($"Inbound:   {12.4 * (0.7 + _up * 0.4):0.0} MB/s", r.X, r.Bottom - g.U(25), 7.5, g.P.Text, Align.Left); g.Text($"Outbound:  {8.7 * (0.7 + _fw * 0.4):0.0} MB/s", r.X, r.Bottom - g.U(13), 7.5, g.P.Text, Align.Left); break;
                    case 2: Wave(g, new Rect(r.X, r.Y, r.Width, r.Height - g.U(26)), 1.0, 0); g.Text($"Analyzing encryption... {Pct(_crack)}%", r.X, r.Bottom - g.U(25), 7.5, g.P.Text, Align.Left); g.Text($"Brute force: {12.4 * (0.8 + _crack * 0.3):0.0}M attempts/sec", r.X, r.Bottom - g.U(13), 7.5, g.P.Text, Align.Left); break;
                    case 3: LogLines(g, r); break;
                    case 4: Sphere(g, left); Stats(g, new Rect(right.X, right.Y + g.U(2), right.Width, right.Height - g.U(30)), ("Keyspace:", "2^64"), ("Entropy:", $"{8.7 * (0.9 + _key * 0.1):0.0}"), ("Status:", _granted ? "Decrypted" : "Decrypting..."), ("Progress:", $"{Pct(_crack)}%")); Eq(g, new Rect(right.X, r.Bottom - g.U(26), right.Width, g.U(18))); g.Bar(new Rect(r.X, r.Bottom - g.U(5), r.Width, g.U(4)), _crack, g.P.Primary, null, null); break;
                    case 5: Files(g, r, new[] { ("/sat_data/telemetry.bin", _files), ("/sat_data/images.bin", _files * 0.7), ("/sat_data/logs.bin", _files * 0.45), ("/sat_data/config.bin", _files * 0.25), ("/ext_data/archive.bin", _files * 0.12) }); break;
                    case 6: Wave(g, new Rect(r.X, r.Y, r.Width, r.Height - g.U(36)), 0.8, 3, true); Stats(g, new Rect(r.X, r.Bottom - g.U(35), r.Width, g.U(35)), ("Frequency:", "2.4 GHz"), ("Signal:", $"{-68 - (1 - _up) * 6:0} dBm"), ("SNR:", $"{-192 + _up * 12:0} dBm")); break;
                    default: GeoMarker(g, new Rect(r.X, r.Y, r.Width, r.Height - g.U(36))); Stats(g, new Rect(r.X, r.Bottom - g.U(35), r.Width, g.U(35)), ("Lat:", "34.0522° N"), ("Lon:", "118.2437° W"), ("Alt:", "35786 km")); break;
                }
                break;
            case 1:
                switch (idx)
                {
                    case 0: WorldDots(g, left, true, 8); Stats(g, new Rect(right.X, right.Y + g.U(2), right.Width, right.Height), ("Global Threats", ""), ($"• {12 + (int)(_fw * 4)} Active", ""), ("• 3 Critical", ""), ("• 7 Monitoring", "")); break;
                    case 1: Flow(g, left); Stats(g, new Rect(right.X, right.Y + g.U(4), right.Width, right.Height), ("IN:", $"{48.7 * (0.7 + _up * 0.5):0.0} MB/s"), ("OUT:", $"{32.4 * (0.7 + _fw * 0.5):0.0} MB/s"), ("TOTAL:", $"{81.1 * (0.7 + _up * 0.4):0.0} MB/s")); break;
                    case 2: Ring(g, new Rect(left.X, left.Y, left.Width, left.Height), 0.72 + 0.1 * _fw); Stats(g, new Rect(right.X, right.Y + g.U(2), right.Width, right.Height), ("Firewall:", "ACTIVE"), ("IDS:", "ACTIVE"), ("Antivirus:", "ACTIVE"), ("System:", "STABLE")); break;
                    case 3: Users(g, r); break;
                    case 4: DbIcon(g, left); Stats(g, new Rect(right.X, right.Y + g.U(2), right.Width, right.Height), ("users", $"{42}"), ("logs", $"{(int)(1287 * (0.5 + _db * 0.5))}"), ("config", "12"), ("media", "843"), ("archive", "256")); break;
                    case 5: HexMatrix(g, r); break;
                    case 6: Shield(g, left); Stats(g, new Rect(right.X, right.Y + g.U(2), right.Width, right.Height - g.U(14)), ("Encryption:", "AES-256"), ("Key Exchange:", "RSA-4096"), ("Hash:", "SHA-3"), ("Status:", _granted ? "BYPASSED" : "BYPASSING...")); g.Bar(new Rect(right.X, r.Bottom - g.U(9), right.Width, g.U(6)), _fw, g.P.Primary, null, null); break;
                    default: Dish(g, left); Stats(g, new Rect(right.X, right.Y + g.U(2), right.Width, right.Height), ("Signal:", $"{-72 + (_up - 0.5) * 6:0} dBm"), ("Latency:", $"{124 - (int)(_up * 20)} ms"), ("Encryption:", "ON"), ("Status:", _granted ? "UNLOCKED" : "LOCKED")); break;
                }
                break;
            default:
                switch (idx)
                {
                    case 0: Globe(g, left, false); Stats(g, new Rect(right.X, right.Y + g.U(4), right.Width, right.Height), ("Target:", "SAT-7"), ("Type:", "Comms Sat"), ("Status:", "Online"), ("Encryption:", "AES-256")); break;
                    case 1: Wave(g, new Rect(r.X, r.Y, r.Width, r.Height - g.U(48)), 1.1, 5); Stats(g, new Rect(r.X, r.Bottom - g.U(47), r.Width, g.U(47)), ("Freq:", "14.260 GHz"), ("Bandwidth:", "12.5 MHz"), ("Signal:", $"{-67 + (_up - 0.5) * 4:0} dBm"), ("Noise:", "-101 dBm")); break;
                    case 2: Nodes(g, left); Stats(g, new Rect(right.X, right.Y + g.U(2), right.Width, right.Height), ("Nodes:", "247"), ("Active:", $"{89 + (int)(_trace * 20)}"), ("Idle:", "142"), ("Blocked:", $"{16 + (int)(_fw * 6)}")); break;
                    case 3: AlertRing(g, left); Stats(g, new Rect(right.X, right.Y + g.U(2), right.Width, right.Height), ("Threat Level:", _granted ? "LOW" : "HIGH"), ("Anomalies:", $"{12 - (int)(_fw * 8)}"), ("", ""), (_granted ? "Cleared" : "Scanning...", "")); break;
                    case 4: BarsViz(g, new Rect(r.X, r.Y, r.Width, r.Height - g.U(36))); Stats(g, new Rect(r.X, r.Bottom - g.U(35), r.Width, g.U(35)), ("Transfer Rate:", $"{48.3 * (0.6 + _files * 0.5):0.0} MB/s"), ("Total Size:", "12.7 GB"), ("Elapsed:", $"00:{(int)(_t / 60 % 60):00}:{(int)(_t % 60):00}")); break;
                    case 5: Folders(g, r); break;
                    case 6: KeyIcon(g, left); Stats(g, new Rect(right.X, right.Y + g.U(2), right.Width, right.Height), ("Key 1:", "VALID"), ("Key 2:", _key > 0.4 ? "VALID" : "PENDING"), ("Key 3:", _granted ? "VALID" : "INVALID"), ("Key 4:", "VALID")); break;
                    default: WorldDots(g, new Rect(r.X, r.Y, r.Width, r.Height - g.U(36)), true, 5); Stats(g, new Rect(r.X, r.Bottom - g.U(35), r.Width, g.U(35)), ("Global Access:", "ENABLED"), ("Stealth Mode:", "ACTIVE"), ("Detection Risk:", _granted ? "NONE" : "LOW")); break;
                }
                break;
        }
    }

    // ---- visual widgets ---------------------------------------------------------------------------------------------
    void Globe(Gfx g, Rect r, bool orbits)
    {
        double cx = r.Center.X, cy = r.Center.Y, rad = Math.Min(r.Width, r.Height) * 0.42, rot = _t * 14, tilt = 22;
        g.Circle(cx, cy, rad, g.P.Primary, 1.4, 0.9);
        var pts = new List<Point>();
        for (int lon = -180; lon < 180; lon += 30) { pts.Clear(); for (int lat = -90; lat <= 90; lat += 6) { var (p, z) = World.Ortho(cx, cy, rad, lon, lat, rot, tilt); if (z > 0) pts.Add(p); else { if (pts.Count > 1) g.Poly(pts, g.P.Primary, 1, 0.5); pts.Clear(); } } if (pts.Count > 1) g.Poly(pts, g.P.Primary, 1, 0.5); }
        for (int lat = -60; lat <= 60; lat += 30) { pts.Clear(); for (int lon = -180; lon <= 180; lon += 6) { var (p, z) = World.Ortho(cx, cy, rad, lon, lat, rot, tilt); if (z > 0) pts.Add(p); else { if (pts.Count > 1) g.Poly(pts, g.P.Primary, 1, 0.5); pts.Clear(); } } if (pts.Count > 1) g.Poly(pts, g.P.Primary, 1, 0.5); }
        foreach (var d in _dots) { if (((int)d.lon + (int)d.lat) % 6 != 0) continue; var (p, z) = World.Ortho(cx, cy, rad, d.lon, d.lat, rot, tilt); if (z > 0.05) g.Disc(p.X, p.Y, g.U(0.9), g.P.Accent, 0.5 * z); }
        if (orbits) for (int k = 0; k < 2; k++)
            {
                g.Rotated(cx, cy, 25 + k * 55, () => g.Dc.DrawEllipse(null, g.Pn(g.P.Accent, 1, 0.55, g.U(3)), new Point(cx, cy), rad * 1.28, rad * 0.4));
                double a = _t * (0.8 + k * 0.4) + k * 2; double ang = (25 + k * 55) * Math.PI / 180, ex = Math.Cos(a) * rad * 1.28, ey = Math.Sin(a) * rad * 0.4;
                g.Disc(cx + ex * Math.Cos(ang) - ey * Math.Sin(ang), cy + ex * Math.Sin(ang) + ey * Math.Cos(ang), g.U(2.6), g.P.Accent);
            }
    }

    void WorldDots(Gfx g, Rect r, bool redThreat, int stride)
    {
        foreach (var d in _dots) { if (((int)d.lon / 3 + (int)d.lat / 3) % 1 != 0) continue; var p = World.Project(r, d.lon, d.lat, 10, 12, 1.0); if (!r.Contains(p)) continue; g.Disc(p.X, p.Y, g.U(Scene == 2 ? 1.1 : 1.0), g.P.Primary, 0.5); }
        var hubs = new[] { (51.5, -0.1), (40.7, -74.0), (35.7, 139.7), (-33.9, 151.2), (1.3, 103.8), (19.4, -99.1), (55.7, 37.6) };
        int arcs = 3 + (int)(_trace * (hubs.Length - 3));
        for (int i = 0; i < arcs - 1 && i < hubs.Length - 1; i++)
        {
            var a = World.Project(r, hubs[i].Item2, hubs[i].Item1, 10, 12, 1.0); var b = World.Project(r, hubs[i + 1].Item2, hubs[i + 1].Item1, 10, 12, 1.0); var m = new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2 - g.U(14));
            var arc = new List<Point>(); for (int k = 0; k <= 12; k++) { double t = k / 12.0; arc.Add(new Point((1 - t) * (1 - t) * a.X + 2 * (1 - t) * t * m.X + t * t * b.X, (1 - t) * (1 - t) * a.Y + 2 * (1 - t) * t * m.Y + t * t * b.Y)); }
            g.Poly(arc, g.P.Accent, 1.2, 0.8); double f = (_t * 0.7 + i * 0.3) % 1; int ci = (int)(f * 12); g.Disc(arc[ci].X, arc[ci].Y, g.U(1.8), 0xFFFFFFFF, 0.9);
        }
        for (int i = 0; i < hubs.Length; i++) { var p = World.Project(r, hubs[i].Item2, hubs[i].Item1, 10, 12, 1.0); bool hot = redThreat && i % 2 == 0; g.Disc(p.X, p.Y, g.U(2.6), hot ? 0xFFFF3030 : g.P.Accent, 0.4 + 0.6 * g.Pulse(1.2, i)); if (hot) g.Circle(p.X, p.Y, g.U(4 + 5 * ((_t + i) % 1)), 0xFFFF3030, 1, 1 - (_t + i) % 1); }
    }

    void GeoMarker(Gfx g, Rect r)
    {
        g.FlatMap(r, 0, 20, 1.6, g.P.Secondary, false, false);
        var p = World.Project(r, -118.2, 34.0, 0, 20, 1.6); if (!r.Contains(p)) p = r.Center;
        g.Disc(p.X, p.Y, g.U(3), 0xFFFF3030); for (int k = 0; k < 2; k++) g.Circle(p.X, p.Y, g.U(4 + 12 * ((_t * 0.8 + k * 0.5) % 1)), 0xFFFF3030, 1.2, 1 - (_t * 0.8 + k * 0.5) % 1);
        g.Line(r.X, p.Y, r.Right, p.Y, g.P.Accent, 1, 0.25); g.Line(p.X, r.Y, p.X, r.Bottom, g.P.Accent, 1, 0.25);
    }

    void Wave(Gfx g, Rect r, double amp, int seed, bool peaks = false)
    {
        var pts = new List<Point>(); int n = Math.Max(20, (int)(r.Width / g.U(2)));
        for (int i = 0; i < n; i++)
        {
            double x = i / (double)(n - 1), ph = _t * 3 + seed;
            double v = 0.35 * Math.Sin(x * 31 + ph) * Math.Sin(x * 7 - ph * 0.4) + 0.25 * Math.Sin(x * 57 + ph * 1.7) + ((Hh((uint)(i * 31 + (int)(_t * 24) + seed)) % 1000) / 1000.0 - 0.5) * 0.5;
            if (peaks) v += 0.9 * Math.Exp(-Math.Pow((x - 0.35) / 0.025, 2)) + 0.55 * Math.Exp(-Math.Pow((x - 0.7) / 0.02, 2));
            pts.Add(new Point(r.X + r.Width * x, r.Center.Y - v * amp * r.Height * 0.46));
        }
        if (g.Glow) g.Poly(pts, g.P.Primary, 3.4, 0.14); g.Poly(pts, g.P.Primary, 1.2, 0.95);
        g.Line(r.X, r.Bottom, r.Right, r.Bottom, g.P.Primary, 1, 0.3);
    }

    void Eq(Gfx g, Rect r) { int n = Math.Max(6, (int)(r.Width / g.U(4))); for (int i = 0; i < n; i++) { double h = (0.2 + 0.8 * ((Hh((uint)(i * 17 + (int)(_t * 12))) % 100) / 100.0)) * r.Height; g.Fill(new Rect(r.X + i * r.Width / n, r.Bottom - h, r.Width / n - 1, h), g.P.Primary, 0.75); } }

    void LogLines(Gfx g, Rect r)
    {
        string[] logs = { "> Connecting to target...", "> Authentication required...", "> Bypassing firewall...", "> Access level: UNKNOWN", "> Attempting decryption...", "> Injecting payload...", "> Escalating privileges...", "> Session key negotiated..." };
        double lh = g.U(11.5); int rows = Math.Max(1, (int)(r.Height / lh)); int off = (int)(_t * 0.7);
        for (int i = 0; i < rows; i++) { int idx = (off + i) % logs.Length; bool last = i == rows - 1; g.Text(logs[idx], r.X, r.Y + i * lh, 7.5, last ? g.P.Accent : g.P.Text, Align.Left, last, last ? 1 : 0.75); }
    }

    void Sphere(Gfx g, Rect r)
    {
        double cx = r.Center.X, cy = r.Center.Y, rad = Math.Min(r.Width, r.Height) * 0.46, a = _t * 0.7, b = 0.5 + Math.Sin(_t * 0.3) * 0.2;
        var P = new Point[_sph.Count]; var Z = new double[_sph.Count];
        for (int i = 0; i < _sph.Count; i++) { var (x, y, z) = _sph[i]; double x1 = x * Math.Cos(a) - z * Math.Sin(a), z1 = x * Math.Sin(a) + z * Math.Cos(a), y1 = y * Math.Cos(b) - z1 * Math.Sin(b), z2 = y * Math.Sin(b) + z1 * Math.Cos(b); P[i] = new Point(cx + x1 * rad, cy - y1 * rad); Z[i] = z2; }
        int shown = (int)(_sphEdges.Count * (0.35 + 0.65 * _crack));
        for (int k = 0; k < shown; k++) { var (i, j) = _sphEdges[k]; g.Line(P[i].X, P[i].Y, P[j].X, P[j].Y, g.P.Primary, 1, 0.12 + 0.3 * (Z[i] + Z[j] + 2) / 4); }
        for (int i = 0; i < P.Length; i++) g.Disc(P[i].X, P[i].Y, g.U(1.1 + 0.6 * (Z[i] + 1) / 2), g.P.Accent, 0.3 + 0.6 * (Z[i] + 1) / 2);
    }

    void Files(Gfx g, Rect r, (string n, double p)[] f)
    {
        double lh = r.Height / f.Length;
        for (int i = 0; i < f.Length; i++)
        {
            double y = r.Y + i * lh; g.Text(f[i].n, r.X, y, 7.5, g.P.Text, Align.Left); g.Text($"{Pct(f[i].p)}%", r.Right, y, 7.5, g.P.Primary, Align.Right, true);
            g.Bar(new Rect(r.X, y + g.U(11), r.Width, g.U(5)), f[i].p, g.P.Primary, null, null);
        }
    }

    void Flow(Gfx g, Rect r)
    {
        for (int k = 0; k < 9; k++)
        {
            var pts = new List<Point>(); for (int i = 0; i <= 28; i++) { double x = i / 28.0; double y = 0.5 + 0.36 * Math.Sin(x * 6 + _t * 1.4 + k * 0.5) * Math.Sin(x * 3.14 + k * 0.2) * (1 - Math.Abs(x - 0.5) * 0.4); pts.Add(new Point(r.X + r.Width * x, r.Y + r.Height * y)); }
            g.Poly(pts, k % 3 == 0 ? g.P.Accent : g.P.Primary, 1, 0.55);
            double f = (_t * 0.5 + k * 0.11) % 1; int ci = (int)(f * 28); g.Disc(pts[ci].X, pts[ci].Y, g.U(1.5), 0xFFFFFFFF, 0.8);
        }
    }

    void Ring(Gfx g, Rect r, double pct)
    {
        double cx = r.Center.X, cy = r.Center.Y, rad = Math.Min(r.Width, r.Height) * 0.42;
        g.Circle(cx, cy, rad, g.P.Primary, 3, 0.15); g.Arc(cx, cy, rad, -90, 360 * pct, g.P.Primary, 4, 1); g.Circle(cx, cy, rad * 0.78, g.P.Primary, 1, 0.25, g.U(3));
        g.Text($"{pct * 100:0}%", cx, cy - g.U(8), 17, g.P.Accent, Align.Center, true);
    }

    void Users(Gfx g, Rect r)
    {
        string[] n = { "admin", "root", "guest", "service", "backup" }; bool[] on = { true, _granted, false, true, _compute >= 0 }; double lh = r.Height / n.Length;
        for (int i = 0; i < n.Length; i++)
        {
            double y = r.Y + i * lh + lh / 2; g.Disc(r.X + g.U(8), y - g.U(2), g.U(3), g.P.Primary, 0.9); g.Fill(new Rect(r.X + g.U(4), y + g.U(1), g.U(8), g.U(5)), g.P.Primary, 0.6);
            g.Text(n[i], r.X + g.U(22), y - g.U(5), 8, g.P.Text, Align.Left); g.Text(on[i] ? "ONLINE" : "OFFLINE", r.Right - g.U(4), y - g.U(5), 8, on[i] ? g.P.Accent : g.P.Text, Align.Right, on[i], on[i] ? 1 : 0.5);
        }
    }

    void DbIcon(Gfx g, Rect r)
    {
        double cx = r.Center.X, w = Math.Min(r.Width * 0.5, r.Height * 0.7), top = r.Y + r.Height * 0.18, h = r.Height * 0.62;
        for (int k = 0; k < 3; k++)
        {
            double y = top + k * h / 3; g.Dc.DrawEllipse(g.Br(g.P.Primary, 0.18), g.Pn(g.P.Primary, 1.4, 0.95), new Point(cx, y + h / 6), w, h * 0.09);
            g.Line(cx - w, y + h / 6, cx - w, y + h / 3 + h / 6, g.P.Primary, 1.4, 0.9); g.Line(cx + w, y + h / 6, cx + w, y + h / 3 + h / 6, g.P.Primary, 1.4, 0.9);
        }
        g.Dc.DrawEllipse(null, g.Pn(g.P.Primary, 1.4, 0.95), new Point(cx, top + h + h / 6 - h / 3 + h / 3), w, h * 0.09);
        g.Bar(new Rect(r.X, r.Bottom - g.U(5), r.Width, g.U(4)), _db, g.P.Accent, null, null);
    }

    void HexMatrix(Gfx g, Rect r)
    {
        int cols = 18, rows = 8; double cw = r.Width / cols, ch = r.Height / rows; int hi = (int)(_t * 9) % (cols * rows);
        for (int j = 0; j < rows; j++) for (int i = 0; i < cols; i++)
            {
                int idx = j * cols + i; bool h = idx == hi || idx == (hi * 7) % (cols * rows); uint v = Hh((uint)(idx * 31 + (int)(_t * (idx % 5 == 0 ? 6 : 1.2))));
                if (h) g.Fill(new Rect(r.X + i * cw, r.Y + j * ch, cw, ch), g.P.Accent, 0.25);
                g.Text(((v & 0xF)).ToString("X") + ((v >> 4) & 0xF).ToString("X"), r.X + i * cw + cw / 2, r.Y + j * ch + ch * 0.2, 6.5, h ? g.P.Accent : g.P.Primary, Align.Center, h, h ? 1 : 0.55);
            }
        g.Text($"{Pct(_key)}%", r.Right - g.U(2), r.Bottom - g.U(10), 7.5, g.P.Accent, Align.Right, true);
    }

    void Shield(Gfx g, Rect r)
    {
        double cx = r.Center.X, cy = r.Center.Y, s = Math.Min(r.Width, r.Height) * 0.42;
        var sh = new[] { new Point(cx, cy - s), new Point(cx + s * 0.85, cy - s * 0.6), new Point(cx + s * 0.75, cy + s * 0.3), new Point(cx, cy + s), new Point(cx - s * 0.75, cy + s * 0.3), new Point(cx - s * 0.85, cy - s * 0.6) };
        g.PolyFill(sh, g.P.Primary, 0.14, g.P.Primary, 1.8, 0.95); g.Poly(new[] { new Point(cx - s * 0.3, cy), new Point(cx - s * 0.05, cy + s * 0.25), new Point(cx + s * 0.4, cy - s * 0.25) }, g.P.Accent, 2.4, 0.95);
    }

    void Dish(Gfx g, Rect r) => g.DishIcon(r.Center.X, r.Center.Y, Math.Min(r.Width, r.Height) * 0.9, 120 + _t * 3, 52 + 8 * Math.Sin(_t * 0.4), g.P.Primary, true, true, _up);

    void Nodes(Gfx g, Rect r)
    {
        int n = 14; var p = new Point[n]; for (int i = 0; i < n; i++) p[i] = new Point(r.X + r.Width * (0.1 + 0.8 * ((Hh((uint)i * 7) % 100) / 100.0)), r.Y + r.Height * (0.12 + 0.76 * ((Hh((uint)i * 13 + 5) % 100) / 100.0)));
        for (int i = 0; i < n; i++) for (int j = i + 1; j < n; j++) { double d = Math.Sqrt(Math.Pow(p[i].X - p[j].X, 2) + Math.Pow(p[i].Y - p[j].Y, 2)); if (d < r.Width * 0.45) g.Line(p[i].X, p[i].Y, p[j].X, p[j].Y, g.P.Primary, 1, 0.3); }
        for (int i = 0; i < n; i++) { bool hot = i % 5 == 0; g.Disc(p[i].X, p[i].Y, g.U(hot ? 3.4 : 2.4), hot ? g.P.Accent : g.P.Primary, 0.5 + 0.5 * g.Pulse(0.8, i)); }
    }

    void AlertRing(Gfx g, Rect r)
    {
        double cx = r.Center.X, cy = r.Center.Y, rad = Math.Min(r.Width, r.Height) * 0.42; uint c = _granted ? g.P.Accent : 0xFFFF3030;
        g.Circle(cx, cy, rad, c, 1.6, 0.9); g.Circle(cx, cy, rad * 0.75, c, 1, 0.4, g.U(3)); g.Arc(cx, cy, rad * 0.92, _t * 140, 90, c, 2.5, 0.9);
        g.PolyFill(new[] { new Point(cx, cy - rad * 0.5), new Point(cx + rad * 0.5, cy + rad * 0.38), new Point(cx - rad * 0.5, cy + rad * 0.38) }, c, 0.25 + 0.25 * g.Pulse(1.5), c, 2, 1);
        g.Text("!", cx, cy - rad * 0.2, rad * 0.6 / g.Scale, c, Align.Center, true);
    }

    void BarsViz(Gfx g, Rect r) { int n = Math.Max(10, (int)(r.Width / g.U(5))); for (int i = 0; i < n; i++) { double h = (0.1 + 0.9 * Math.Pow((Hh((uint)(i * 19 + (int)(_t * 9))) % 100) / 100.0, 1.5)) * r.Height * (0.4 + 0.6 * _files); g.Fill(new Rect(r.X + i * r.Width / n, r.Bottom - h, r.Width / n - 1.5, h), g.P.Primary, 0.8); } }

    void Folders(Gfx g, Rect r)
    {
        (string n, string s)[] f = { ("/sat_data", "2.4 GB"), ("/images", "1.2 GB"), ("/telemetry", "3.8 GB"), ("/logs", "512 MB"), ("/config", "256 MB") }; double lh = r.Height / f.Length;
        for (int i = 0; i < f.Length; i++)
        {
            double y = r.Y + i * lh; g.Fill(new Rect(r.X, y + g.U(2), g.U(14), g.U(9)), g.P.Primary, 0.85); g.Fill(new Rect(r.X, y, g.U(6), g.U(3)), g.P.Primary, 0.85);
            g.Text(f[i].n, r.X + g.U(22), y, 8, g.P.Text, Align.Left); g.Text(f[i].s, r.Right, y, 8, g.P.Text, Align.Right, false, 0.8);
        }
    }

    void KeyIcon(Gfx g, Rect r)
    {
        double cx = r.Center.X, cy = r.Center.Y, s = Math.Min(r.Width, r.Height) * 0.4;
        g.Circle(cx, cy - s * 0.55, s * 0.32, g.P.Primary, 2.2, 0.95); g.Line(cx, cy - s * 0.23, cx, cy + s * 0.75, g.P.Primary, 2.4, 0.95); g.Line(cx, cy + s * 0.45, cx + s * 0.3, cy + s * 0.45, g.P.Primary, 2.2, 0.9); g.Line(cx, cy + s * 0.7, cx + s * 0.22, cy + s * 0.7, g.P.Primary, 2.2, 0.9);
    }

    // ------------------------------------------------------------------ terminal window
    void DrawTerminal(Gfx g, Rect r)
    {
        g.Fill(r, 0xFF000000, 0.78); g.Frame(r.Inflate(g.U(2)), g.P.Primary, 3.5, 0.12); g.Frame(r, g.P.Primary, 1.4, 0.95); g.Brackets(r, g.P.Primary, 12, 2.2);
        string title = Scene == 0 ? "TERMINAL - ACCESS LOG" : Scene == 1 ? "TERMINAL - REMOTE ACCESS" : ">_ TERMINAL - SATELLITE HACK";
        g.Text(title, r.X + g.U(10), r.Y + g.U(5), 8.5, g.P.Primary, Align.Left, true); g.Text("x", r.Right - g.U(10), r.Y + g.U(4), 9, g.P.Text, Align.Right, false, 0.6);
        g.Line(r.X, r.Y + g.U(19), r.Right, r.Y + g.U(19), g.P.Primary, 1, 0.4);
        double fs = 10.5, lh = g.U(fs + 4.5);
        double btnH = g.U(g.Touch ? 44 : 38), foot = Scene == 1 ? g.U(14) : 0;
        var scr = new Rect(r.X + g.U(8), r.Y + g.U(23), r.Width - g.U(16), r.Height - g.U(23) - btnH - g.U(14) - foot);
        int maxLines = Math.Max(2, (int)(scr.Height / lh));
        var vis = new List<Line>(); int hexRows = 2; for (int i = _lines.Count - 1; i >= 0 && vis.Count < maxLines - 1 - hexRows; i--) vis.Insert(0, _lines[i]);
        double y = scr.Y; using (g.Clip(scr))
        {
            foreach (var l in vis) { DrawLine(g, l, scr.X, y, fs); y += lh; }
            if (Scene == 1 && _compute < 0 && !_granted) { double p = _db; string bar = "[" + new string('#', (int)(p * 40)) + new string('-', 40 - (int)(p * 40)) + "]"; g.Text($"[*] Progress: {Pct(p)}%  {bar}", scr.X, y, fs - 1, g.P.Accent, Align.Left, true, 0.9); y += lh; }
            y += lh * 0.4;
            for (int i = 0; i < hexRows; i++) { g.Text(HexBytes((uint)(_t * 3) + (uint)i * 77, Math.Max(6, (int)(scr.Width / g.U(26)))), scr.X, y, fs - 1.5, g.P.Primary, Align.Left, false, 0.75); y += lh * 0.95; }
            y += lh * 0.3; double px = scr.X; string host = Scene == 2 ? "root@sat-7" : "root@blacksite";
            g.Text(host, px, y, fs, g.P.Primary, Align.Left, true); px += g.TextWidth(host, fs * g.Scale * g.S.FontScale, true);
            g.Text(Scene == 2 ? ":~# " : ":~# ", px, y, fs, g.P.Text, Align.Left, true); px += g.TextWidth(":~# ", fs * g.Scale * g.S.FontScale, true);
            g.Text(_input, px, y, fs, g.P.Accent, Align.Left); px += g.TextWidth(_input, fs * g.Scale * g.S.FontScale);
            if ((int)(_t * 2) % 2 == 0) g.Fill(new Rect(px + 1, y, g.U(6.4), lh - g.U(5)), g.P.Primary, 0.95);
        }
        // COMPUTE button (hex bordered) + status
        double bw = Math.Min(g.U(210), r.Width * 0.4), by = r.Bottom - btnH - g.U(8) - foot; var br = new Rect(r.X + r.Width * 0.5 - (Scene == 0 ? bw * 0.95 : bw / 2), by, bw, btnH);
        string label = _compute >= 0 ? $"COMPUTING {_compute * 100:0}%" : "COMPUTE";
        HexButton(g, br, label, _compute >= 0 || _granted);
        if (Scene == 0) g.Text(_granted ? "ACCESS GRANTED" : _compute >= 0 ? "DECRYPTING..." : "AWAITING COMPUTE", br.Right + g.U(16), br.Center.Y - g.U(6), 11, _granted ? g.P.Accent : g.P.Text, Align.Left, true, _granted ? 1 : 0.7);
        if (Scene == 1) { string[] sub = { "> DECRYPT", "> EXTRACT", "> ACCESS" }; double sw = br.Width / 3; for (int i = 0; i < 3; i++) g.Button("hk:sub:" + i, new Rect(br.X + i * sw, br.Bottom + g.U(2), sw - g.U(3), foot), sub[i], false, true, null, 7.5); }
        if (Scene == 2) { g.Text("<<", br.X - g.U(26), br.Center.Y - g.U(7), 14, g.P.Primary, Align.Left, true, 0.6); g.Text(">>", br.Right + g.U(8), br.Center.Y - g.U(7), 14, g.P.Primary, Align.Left, true, 0.6); }
    }

    void HexButton(Gfx g, Rect r, string label, bool active)
    {
        double c = g.U(10);
        var poly = new[] { new Point(r.X + c, r.Y), new Point(r.Right - c, r.Y), new Point(r.Right, r.Center.Y), new Point(r.Right - c, r.Bottom), new Point(r.X + c, r.Bottom), new Point(r.X, r.Center.Y) };
        bool hover = g.IsHover("hk:compute"), down = g.IsPressed("hk:compute");
        g.PolyFill(poly, g.P.Primary, active ? 0.30 : hover ? 0.22 : 0.10); g.Poly(poly, g.P.Primary, 4, 0.14 + (active ? 0.15 : 0), true); g.Poly(poly, g.P.Primary, 1.8, 1, true);
        double ix = r.X + c + g.U(12), iy = r.Center.Y;
        if (Scene == 0) { g.Circle(ix, iy, g.U(7), g.P.Primary, 1.6); g.Arc(ix, iy, g.U(11), _t * 90, 240, g.P.Primary, 1.2, 0.8); g.Disc(ix, iy, g.U(2.5), g.P.Primary); }
        else if (Scene == 1) { g.Circle(ix, iy, g.U(8), g.P.Primary, 1.6); g.Circle(ix, iy, g.U(4), g.P.Primary, 1.4); g.Disc(ix, iy, g.U(1.6), g.P.Accent); }
        else { var hx = new Point[6]; for (int i = 0; i < 6; i++) hx[i] = new Point(ix + Math.Cos(i * Math.PI / 3) * g.U(8), iy + Math.Sin(i * Math.PI / 3) * g.U(8)); g.Poly(hx, g.P.Primary, 1.6, 1, true); g.Disc(ix, iy, g.U(2.2), g.P.Primary); }
        g.Text(label, r.Center.X + g.U(8), r.Center.Y - g.U(8), 14, g.P.Accent, Align.Center, true);
        g.AddHit("hk:compute", r, HitKind.Button);
        if (down) g.Poly(poly, g.P.Accent, 2.4, 0.9, true);
    }

    void DrawLine(Gfx g, Line l, double x, double y, double fs)
    {
        string t = l.Shown < l.Text.Length ? l.Text[..l.Shown] : l.Text;
        switch (l.K)
        {
            case Kind.Prompt:
                {
                    string cmd = t.StartsWith('\u0001') ? t[1..] : t; string host = Scene == 2 ? "root@sat-7" : "root@blacksite"; double px = x;
                    g.Text(host, px, y, fs, g.P.Primary, Align.Left, true); px += g.TextWidth(host, fs * g.Scale * g.S.FontScale, true);
                    g.Text(":~# ", px, y, fs, g.P.Text, Align.Left, true); px += g.TextWidth(":~# ", fs * g.Scale * g.S.FontScale, true);
                    g.Text(cmd, px, y, fs, g.P.Accent, Align.Left, true); break;
                }
            case Kind.Progress:
                {
                    double p = _compute >= 0 ? Math.Clamp(_compute, 0, 1) : 1; int n = 34, f = (int)(p * n);
                    g.Text($"[{new string('#', f)}{new string('-', n - f)}] {p * 100:0}%  {(_compute >= 0 ? "decrypting…" : "complete")}", x, y, fs, p >= 1 ? g.P.Accent : g.P.Warning, Align.Left, true); break;
                }
            default:
                {
                    uint c = l.K switch { Kind.Ok => g.P.Accent, Kind.Info => g.P.Primary, Kind.Warn => g.P.Warning, Kind.Err => 0xFFFF4040, Kind.Dim => g.P.Text, Kind.Stamp => g.P.Text, _ => g.P.Text };
                    bool strong = l.K == Kind.Ok && l.Text.StartsWith("ACCESS");
                    g.Text(t, x, y, strong ? fs + 3 : fs, c, Align.Left, l.K is Kind.Ok or Kind.Warn or Kind.Err, l.K == Kind.Dim ? 0.55 : l.K == Kind.Out ? 0.85 : 1);
                    break;
                }
        }
    }

    void DrawGranted(Gfx g, Rect a)
    {
        double f = _grantedT < 1 ? _grantedT : Math.Clamp(1 - (_grantedT - 4) / 6, 0, 1);
        double w = Math.Min(a.Width * 0.5, g.U(480)), h = g.U(80); var r = new Rect(a.Center.X - w / 2, a.Center.Y - h / 2, w, h);
        using (g.Dc.PushOpacity(Math.Clamp(f, 0, 1)))
        {
            g.Fill(r, 0xFF000000, 0.82); g.Frame(r, g.P.Accent, 2.5, 0.95); g.Brackets(r, g.P.Accent, 16, 3);
            g.Text("ACCESS GRANTED", r.Center.X, r.Y + g.U(10), 36, g.P.Accent, Align.Center, true);
            g.Text($"TARGET {Host}  //  {Targets[Scene]}  //  DATA STREAM OPEN", r.Center.X, r.Bottom - g.U(20), 8.5, g.P.Text, Align.Center, false, 0.85);
        }
    }

    public override string? OnModuleButton(string id)
    {
        if (id == "hk:compute") return OnAction("COMPUTE");
        if (id.StartsWith("hk:sub:")) { Run(new[] { "DECRYPT", "EXTRACT", "ACCESS" }[int.Parse(id[7..])]); C.Audio.Play("handshake", SoundCategory.Ui, 0.6f); return null; }
        if (id.StartsWith("hk:nav:"))
        {
            _nav = int.Parse(id[7..]); C.Audio.Play("deepbeep", SoundCategory.Ui, 0.7f);
            switch (_nav)
            {
                case 0: return "MAIN";
                case 1: Run("SCAN NETWORK"); return "NETWORK";
                case 2: Run("TRACE ROUTE"); return "SATELLITE";
                case 3: Run("ANALYZE PACKETS"); return "DATABASE";
                case 4: Run("DECRYPT"); return "FILES";
                default: return OnAction("STYLE");
            }
        }
        return null;
    }
}
