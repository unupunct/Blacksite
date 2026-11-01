using Avalonia;
using Blacksite.Core.Events;
using Blacksite.Core.Themes;
using Blacksite.UI.Rendering;

namespace Blacksite.UI.Modules;

/// <summary>PRESET 05 – emergency command interface. Purely simulated; never touches real OS security.</summary>
public sealed class LockdownModule : ModuleBase
{
    bool _locked, _effects = true, _threatShown;
    int _level = 1;
    double _verify = 1, _countdown, _threatT;
    bool _counting;
    string _code = "";
    string _codeMsg = "ENTER 4-DIGIT OVERRIDE CODE";
    readonly bool[] _doors = new bool[8];
    readonly string[] _doorNames = { "MAIN GATE", "ARMORY", "DATA CORE", "REACTOR BAY", "COMMAND DECK", "HANGAR", "COMMS ROOM", "MEDICAL" };
    readonly List<string> _comms = new();
    readonly double[] _integrity = { 0.97, 0.92, 0.88, 0.95, 0.9, 0.84 };
    readonly string[] _intNames = { "HULL", "POWER GRID", "FIREWALL", "LIFE SUPPORT", "SENSORS", "BLAST DOORS" };
    string _threatTxt = "";
    const string DemoCode = "4711";

    protected override void OnInit() { for (int i = 0; i < 8; i++) _doors[i] = false; Comm("SECURITY NET ONLINE. DEMO OVERRIDE CODE: " + DemoCode); }

    void Comm(string s) { _comms.Add($"{DateTime.Now:HH:mm:ss} {s}"); if (_comms.Count > 10) _comms.RemoveAt(0); }

    public override string[] Actions => new[] { "LOCKDOWN", "VERIFY", "COUNTDOWN", "THREAT", "EFFECTS", "CODE" };
    public override bool IsActive(string a) => a switch { "LOCKDOWN" => _locked, "VERIFY" => _verify < 1, "COUNTDOWN" => _counting, "THREAT" => _threatShown, "EFFECTS" => _effects, _ => false };
    public override string PrimaryAction => "LOCKDOWN";

    public override string? OnAction(string id)
    {
        switch (id)
        {
            case "LOCKDOWN": return _locked ? Release("MANUAL RELEASE") : Lock();
            case "VERIFY": if (_verify < 1) return null; _verify = 0; Comm("IDENTITY VERIFICATION STARTED"); return "SECURITY VERIFICATION STARTED";
            case "COUNTDOWN":
                _counting = !_counting; if (_counting) { _countdown = 30; Comm("LOCKDOWN COUNTDOWN ARMED T-30"); C.Events.Trigger("lockdown", 12); } else Comm("COUNTDOWN ABORTED");
                return _counting ? "COUNTDOWN STARTED" : "COUNTDOWN ABORTED";
            case "THREAT": _threatShown = !_threatShown; if (_threatShown) { BuildThreat(); _threatT = 0; Comm("THREAT ASSESSMENT RUN"); C.Audio.Play("ping"); } return _threatShown ? "THREAT ASSESSMENT (FICTIONAL)" : "ASSESSMENT CLOSED";
            case "EFFECTS": _effects = !_effects; return _effects ? "EMERGENCY EFFECTS ON" : "EMERGENCY EFFECTS OFF";
            case "CODE": _code = ""; _codeMsg = "ENTER 4-DIGIT OVERRIDE CODE (DEMO: " + DemoCode + ")"; return "KEYPAD READY";
        }
        return null;
    }

    string Lock()
    {
        _locked = true; _level = 5; for (int i = 0; i < _doors.Length; i++) _doors[i] = true;
        C.Events.Trigger("lockdown", 12); Comm("*** FACILITY LOCKDOWN ENGAGED ***");
        for (int i = 0; i < _integrity.Length; i++) _integrity[i] = Math.Max(0.4, _integrity[i] - 0.1);
        return "LOCKDOWN ENGAGED";
    }

    string Release(string why)
    {
        _locked = false; _counting = false; _level = 1; for (int i = 0; i < _doors.Length; i++) _doors[i] = false;
        C.Events.Clear(); Comm("LOCKDOWN RELEASED: " + why); C.Audio.Confirm(); return "LOCKDOWN RELEASED";
    }

    void BuildThreat()
    {
        string[] src = { "UNKNOWN INTRUDER", "HOSTILE PROBE", "INSIDER ANOMALY", "SIGNAL SPOOFING" };
        _threatTxt = $"{src[Rng.Next(src.Length)]}  //  CONFIDENCE {Rng.Next(61, 96)}%  //  SECTOR {Rng.Next(1, 9)}-{(char)('A' + Rng.Next(6))}";
    }

    public override void Update(double dt)
    {
        if (_verify < 1)
        {
            _verify = Math.Min(1, _verify + dt / 4);
            if (_verify >= 1) { if (_locked) { Comm("VERIFICATION DENIED: LOCKDOWN ACTIVE"); C.Audio.Error(); C.Toast("ACCESS DENIED — ENTER OVERRIDE CODE", true); } else { Comm("IDENTITY VERIFIED (SIMULATED)"); C.Audio.Confirm(); C.Toast("IDENTITY VERIFIED"); } }
        }
        if (_counting)
        {
            _countdown -= dt;
            if (_countdown <= 0) { _counting = false; if (!_locked) Lock(); else Comm("COUNTDOWN ZERO"); }
        }
        if (_threatShown) _threatT += dt;
        if (_locked && Rng.NextDouble() < dt * 0.4) Comm(new[] { "SECTOR SEALED", "BLAST DOOR CONFIRMED", "EMERGENCY POWER ACTIVE", "AWAITING OVERRIDE" }[Rng.Next(4)]);
        for (int i = 0; i < _integrity.Length; i++) _integrity[i] = Math.Clamp(_integrity[i] + (_locked ? -0.001 : 0.002) + C.Sim.Noise(0.002), 0.3, 1);
    }

    public override void Draw(Gfx g, Rect area)
    {
        var saved = g.P;
        bool crit = _locked || C.Events.Active.Any(e => e.Def.Severity == Severity.Critical);
        if (!crit) g.P = saved with { Primary = Palette.Lerp(saved.Primary, 0xFFFF9030, 0.0) };
        var lay = Layout.Adaptive(g, area, 6, 0.42);
        DrawMain(g, lay.Main);
        for (int i = 0; i < lay.Panels.Count; i++) DrawPanel(g, i, lay.Panels[i]);
        if (_effects && _locked && !g.Reduced)
        {
            double p = g.Pulse(1.6);
            g.Frame(area.Deflate(g.U(2)), g.P.Primary, g.U(6), 0.15 + 0.3 * p);
            double ang = g.Time * 180;
            g.Rotated(area.Center.X, area.Center.Y, ang, () => g.Fill(new Rect(area.Center.X, area.Center.Y - g.U(40), Math.Max(area.Width, area.Height), g.U(80)), 0xFFFF3030, 0.05));
        }
        g.P = saved;
    }

    void DrawMain(Gfx g, Rect r)
    {
        var c = g.Panel(r, _locked ? "STATUS // LOCKDOWN ACTIVE" : "STATUS // NORMAL OPERATIONS");
        double cx = c.Center.X, cy = c.Y + c.Height * 0.36, m = Math.Min(c.Width, c.Height);
        double pulse = _locked ? g.Pulse(2) : g.Pulse(0.4) * 0.4;
        // warning triangle
        double tr = m * 0.28;
        var tri = new[] { new Point(cx, cy - tr), new Point(cx + tr * 0.95, cy + tr * 0.7), new Point(cx - tr * 0.95, cy + tr * 0.7) };
        g.PolyFill(tri, g.P.Primary, 0.08 + 0.3 * pulse, g.P.Primary, 3);
        if (g.Glow) g.Poly(tri, g.P.Primary, 9, 0.12 + 0.1 * pulse, true);
        g.Text("!", cx, cy - tr * 0.42, tr * 1.1 / g.Scale, g.P.Primary, Align.Center, true, 0.6 + 0.4 * pulse);
        // security level pips
        double py = cy + tr * 0.85;
        g.Text("SECURITY LEVEL", cx, py, 10, g.P.Text, Align.Center, false, 0.7);
        for (int i = 1; i <= 5; i++)
        {
            double x = cx + (i - 3) * g.U(34);
            var pr = new Rect(x - g.U(14), py + g.U(16), g.U(28), g.U(22));
            g.Fill(pr, g.P.Primary, i <= _level ? 0.6 : 0.07); g.Frame(pr, g.P.Primary, 1, 0.8);
            g.TextIn(pr, i.ToString(), 11, i <= _level ? g.P.Background : g.P.Primary, Align.Center, true);
        }
        // countdown
        double cdY = py + g.U(48);
        string cd = _counting ? $"{(int)_countdown / 60:00}:{_countdown % 60:00.0}" : "--:--.-";
        g.Text(cd, cx, cdY, 34, _counting && _countdown < 10 ? g.P.Warning : g.P.Primary, Align.Center, true);
        g.Text(_counting ? "LOCKDOWN COUNTDOWN" : "COUNTDOWN IDLE", cx, cdY + g.U(40), 9, g.P.Text, Align.Center, false, 0.7);
        // verification scan
        if (_verify < 1)
        {
            var sr = new Rect(c.X + c.Width * 0.15, c.Bottom - g.U(40), c.Width * 0.7, g.U(26));
            g.Bar(sr, _verify, g.P.Accent, "BIOMETRIC VERIFICATION", $"{_verify * 100:0}%");
        }
        if (_threatShown && c.Height > g.U(300)) { g.Text(_threatTxt, cx, c.Bottom - g.U(18), 9, g.P.Warning, Align.Center, true, 0.5 + 0.5 * g.Pulse(2)); }
    }

    void DrawPanel(Gfx g, int i, Rect r)
    {
        switch (i)
        {
            case 0:
                {
                    var c = g.Panel(r, "ACCESS CONTROL");
                    int cols = c.Width > g.U(220) ? 2 : 1; int rowsN = (8 + cols - 1) / cols;
                    double gap = g.U(3), bh = Math.Min(g.U(g.Touch ? 30 : 22), (c.Height - gap * (rowsN - 1)) / rowsN), bw = (c.Width - gap * (cols - 1)) / cols;
                    for (int k = 0; k < 8; k++)
                        g.Button("door:" + k, new Rect(c.X + (k % cols) * (bw + gap), c.Y + (k / cols) * (bh + gap), bw, bh), _doorNames[k] + (_doors[k] ? " [LOCK]" : " [OPEN]"), _doors[k], true, _doors[k] ? null : g.P.Secondary, bw < g.U(120) ? 7 : 8);
                    break;
                }
            case 1:
                {
                    var c = g.Panel(r, "SYSTEM INTEGRITY");
                    double lh = Math.Min(g.U(26), c.Height / 6);
                    for (int k = 0; k < 6; k++) g.Bar(new Rect(c.X, c.Y + k * lh, c.Width, lh - g.U(3)), _integrity[k], g.P.Primary, _intNames[k], $"{_integrity[k] * 100:0}%");
                    break;
                }
            case 2:
                {
                    var c = g.Panel(r, "CAMERA GRID");
                    int cols = 3, rowsN = 2; double gap = g.U(3);
                    double cw = (c.Width - gap * 2) / 3, ch = (c.Height - gap) / 2;
                    for (int k = 0; k < 6; k++)
                    {
                        var rc = new Rect(c.X + (k % cols) * (cw + gap), c.Y + (k / cols) * (ch + gap), cw, ch);
                        g.Fill(rc, 0xFF200808); g.Frame(rc, g.P.Primary, 1, 0.5);
                        using (g.Clip(rc))
                        {
                            double x = rc.X + rc.Width * (0.2 + 0.6 * ((g.Time * 0.1 + k * 0.17) % 1));
                            g.Fill(new Rect(x, rc.Y + rc.Height * 0.4, rc.Width * 0.08, rc.Height * 0.45), g.P.Primary, 0.6);
                            g.Disc(x + rc.Width * 0.04, rc.Y + rc.Height * 0.33, rc.Width * 0.05, g.P.Primary, 0.6);
                            for (int s = 0; s < 3; s++) g.Fill(new Rect(rc.X, rc.Y + Rng.NextDouble() * rc.Height, rc.Width, 1), 0xFFFFFFFF, 0.06);
                        }
                        g.Text($"C{k + 1}", rc.X + 3, rc.Y + 2, 7, g.P.Accent, Align.Left, true);
                        if (_locked) g.Disc(rc.Right - g.U(5), rc.Y + g.U(5), g.U(2.5), 0xFFFF3030, g.Pulse(2));
                    }
                    break;
                }
            case 3:
                {
                    var c = g.Panel(r, "EMERGENCY COMMUNICATIONS");
                    double lh = g.U(12.5); int max = Math.Max(1, (int)(c.Height / lh));
                    for (int k = Math.Max(0, _comms.Count - max), n = 0; k < _comms.Count; k++, n++) g.Text(_comms[k], c.X, c.Y + n * lh, 8, _comms[k].Contains("***") || _comms[k].Contains("DENIED") ? g.P.Warning : g.P.Text);
                    break;
                }
            case 4: DrawKeypad(g, r); break;
            case 5:
                {
                    var c = g.Panel(r, "THREAT ASSESSMENT // FICTIONAL");
                    double score = _threatShown ? Math.Min(1, _threatT / 2) * (0.45 + 0.4 * C.Sim.ThreatIndex + (_locked ? 0.15 : 0)) : 0;
                    g.Gauge(c.X + Math.Min(c.Height, c.Width * 0.45) / 2, c.Center.Y, Math.Min(c.Height, c.Width * 0.45) / 2 - g.U(2), score * 100, 100, "THREAT", "INDEX");
                    double tx = c.X + Math.Min(c.Height, c.Width * 0.45) + g.U(10);
                    string[] k = { "VECTOR", "INTENT", "RANGE", "ETA" }; string[] v = _threatShown ? new[] { "UNKNOWN", "HOSTILE", $"{Rng.Next(2, 9)} KM", $"{Rng.Next(1, 20)} MIN" } : new[] { "--", "--", "--", "--" };
                    for (int n = 0; n < 4; n++) g.KeyVal(tx, c.Y + n * g.U(15), c.Right - tx, k[n], v[n], null, 8.5);
                    if (!_threatShown) g.Text("RUN THREAT ASSESSMENT", tx, c.Bottom - g.U(14), 8, g.P.Text, Align.Left, false, 0.6);
                    break;
                }
        }
    }

    void DrawKeypad(Gfx g, Rect r)
    {
        var c = g.Panel(r, "ACCESS CODE");
        double dh = g.U(26);
        var disp = new Rect(c.X, c.Y, c.Width, dh);
        g.Fill(disp, g.P.Primary, 0.08); g.Frame(disp, g.P.Primary, 1, 0.6);
        g.TextIn(disp, _code.Length == 0 ? "- - - -" : string.Join(" ", _code.Select(_ => "*")) + (_code.Length < 4 ? " _" : ""), 14, g.P.Accent, Align.Center, true);
        double lh = g.U(11);
        g.Text(_codeMsg.Length > 38 && c.Width < g.U(260) ? _codeMsg[..38] : _codeMsg, c.X, c.Y + dh + g.U(2), 7.5, g.P.Text, Align.Left, false, 0.7);
        double top = c.Y + dh + lh + g.U(6), gap = g.U(3);
        double bh = (c.Bottom - top - gap * 3) / 4, bw = (c.Width - gap * 2) / 3;
        string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "CLR", "0", "ENT" };
        for (int k = 0; k < 12; k++) g.Button("key:" + keys[k], new Rect(c.X + (k % 3) * (bw + gap), top + (k / 3) * (bh + gap), bw, bh), keys[k], false, true, keys[k] == "ENT" ? g.P.Accent : null, 11);
    }

    public override string? OnModuleButton(string id)
    {
        if (id.StartsWith("door:"))
        {
            int k = int.Parse(id[5..]); _doors[k] = !_doors[k]; Comm($"{_doorNames[k]} {(_doors[k] ? "SEALED" : "OPENED")}"); C.Audio.Play("mech");
            return $"{_doorNames[k]} {(_doors[k] ? "LOCKED" : "UNLOCKED")}";
        }
        if (id.StartsWith("key:")) return Press(id[4..]);
        return null;
    }

    string? Press(string k)
    {
        if (k == "CLR") { _code = ""; _codeMsg = "CLEARED"; return null; }
        if (k == "ENT")
        {
            if (_code == DemoCode) { _codeMsg = "OVERRIDE ACCEPTED"; _code = ""; if (_locked) return Release("OVERRIDE CODE"); Comm("OVERRIDE ACCEPTED"); C.Audio.Confirm(); return "OVERRIDE ACCEPTED"; }
            _codeMsg = "INVALID CODE — ATTEMPT LOGGED"; Comm("INVALID OVERRIDE ATTEMPT"); _code = ""; C.Audio.Error(); C.Events.Trigger("unauth_access"); return "INVALID CODE";
        }
        if (_code.Length < 4) _code += k;
        return null;
    }

    public override bool TryKey(string combo)
    {
        if (combo.Length == 1 && char.IsDigit(combo[0])) { var m = Press(combo); if (m != null) C.Toast(m); C.Audio.Play("click"); return true; }
        if (combo == "Enter") { var m = Press("ENT"); if (m != null) C.Toast(m); return true; }
        if (combo == "Back") { Press("CLR"); return true; }
        return false;
    }
}
