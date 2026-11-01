using Avalonia;
using Blacksite.Core.Simulation;
using Blacksite.UI.Rendering;

namespace Blacksite.UI.Modules;

/// <summary>PRESET 07 – starship engineering room. All measurements are simulated.</summary>
public sealed class ReactorModule : ModuleBase
{
    readonly double[] _alloc = { 0.28, 0.20, 0.18, 0.12, 0.22 };
    static readonly string[] BusNames = { "PROPULSION", "SHIELDS", "LIFE SUPPORT", "COMMS", "SENSORS" };
    double _diag = 1, _warnT, _unstableCool;
    readonly List<string> _log = new();

    ReactorState R => C.Sim.Reactor;

    public override string[] Actions => new[] { "COOLING", "BALANCE", "OVERLOAD", "DIAG", "FAIL", "SCRAM" };
    public override bool IsActive(string a) => a switch { "COOLING" => R.Cooling, "OVERLOAD" => R.Overload, "DIAG" => _diag < 1, "SCRAM" => R.Scram, _ => false };
    public override string PrimaryAction => "DIAG";

    void Log(string s) { _log.Add($"{DateTime.Now:HH:mm:ss} {s}"); if (_log.Count > 7) _log.RemoveAt(0); }

    public override string? OnAction(string id)
    {
        switch (id)
        {
            case "COOLING": R.Cooling = !R.Cooling; C.Audio.Play("mech"); Log("COOLING " + (R.Cooling ? "ENGAGED" : "DISENGAGED")); return "COOLING " + (R.Cooling ? "ACTIVE" : "OFF");
            case "BALANCE":
                {
                    double demand = Demand(), sum = _alloc.Sum();
                    for (int i = 0; i < _alloc.Length; i++) _alloc[i] = Math.Round(R.Power / 100.0 * 0.9 / _alloc.Length, 3) * 1.0 + 0.0;
                    Log("ENERGY BALANCED"); R.Stability = Math.Min(1, R.Stability + 0.1); return "ENERGY DISTRIBUTION BALANCED";
                }
            case "OVERLOAD": R.Overload = !R.Overload; if (R.Overload) { Log("!! OVERLOAD INITIATED"); C.Events.Trigger("power_fluct"); } else Log("OVERLOAD CLEARED"); return R.Overload ? "OVERLOAD TRIGGERED" : "OVERLOAD CLEARED";
            case "DIAG": _diag = 0; Log("DIAGNOSTICS STARTED"); return "SYSTEM DIAGNOSTICS RUNNING";
            case "FAIL":
                {
                    var ok = Enumerable.Range(0, R.SubsystemOk.Length).Where(i => R.SubsystemOk[i]).ToList();
                    if (ok.Count == 0) { C.Audio.Error(); return "ALL SUBSYSTEMS ALREADY FAILED"; }
                    int k = ok[Rng.Next(ok.Count)]; R.SubsystemOk[k] = false; Log("FAILURE: " + ReactorState.SubsystemNames[k]);
                    C.Events.Trigger("reactor_unstable"); return ReactorState.SubsystemNames[k] + " FAILURE";
                }
            case "SCRAM":
                if (R.Scram) { C.Sim.ResetReactor(); Log("REACTOR RESTART SEQUENCE"); return "REACTOR RESTARTED"; }
                R.Scram = true; R.Overload = false; Log("EMERGENCY SHUTDOWN (SCRAM)"); C.Events.Trigger("reactor_unstable"); return "EMERGENCY SHUTDOWN";
        }
        return null;
    }

    double Demand() => _alloc.Sum() * 100;

    public override void Update(double dt)
    {
        if (_diag < 1)
        {
            _diag = Math.Min(1, _diag + dt / 6);
            if (_diag >= 1) { int bad = R.SubsystemOk.Count(b => !b); Log(bad == 0 ? "DIAGNOSTICS: ALL NOMINAL" : $"DIAGNOSTICS: {bad} FAULT(S)"); C.Toast(bad == 0 ? "DIAGNOSTICS COMPLETE — NOMINAL" : $"DIAGNOSTICS: {bad} FAULT(S) FOUND", bad > 0); }
        }
        _unstableCool -= dt;
        if (R.Stability < 0.35 && _unstableCool <= 0 && !R.Scram) { C.Events.Trigger("reactor_unstable"); _unstableCool = 15; }
        _warnT += dt;
    }

    public override void Draw(Gfx g, Rect area)
    {
        var lay = Layout.Adaptive(g, area, 6, 0.4);
        DrawCore(g, lay.Main);
        for (int i = 0; i < lay.Panels.Count; i++) DrawPanel(g, i, lay.Panels[i]);
    }

    void DrawCore(Gfx g, Rect r)
    {
        var c = g.Panel(r, "REACTOR CORE");
        double cx = c.Center.X, cy = c.Center.Y - g.U(8), rad = Math.Min(c.Width, c.Height * 0.9) * 0.42;
        double p = R.Power / 100.0, st = R.Stability;
        g.HoloBase(c, cx, c.Bottom - g.U(26), rad * 1.7);
        uint col = st < 0.4 ? g.P.Warning : g.P.Primary;
        // energy rings
        for (int i = 0; i < 5; i++)
        {
            double rr = rad * (0.35 + i * 0.16), dir = i % 2 == 0 ? 1 : -1, sp = (20 + i * 15) * (0.2 + p) * dir;
            for (int s = 0; s < 4; s++) g.Arc(cx, cy, rr, g.Time * sp + s * 90, 55 + 15 * p, i == 2 ? g.P.Secondary : (i == 4 ? g.P.Accent : col), 2 + i * 0.3, 0.35 + 0.5 * p);
        }
        // coils
        for (int i = 0; i < 12; i++)
        {
            double a = i * Math.PI / 6 + g.Time * 0.2 * p, x1 = cx + Math.Cos(a) * rad * 1.05, y1 = cy + Math.Sin(a) * rad * 1.05;
            g.Fill(new Rect(x1 - g.U(3), y1 - g.U(3), g.U(6), g.U(6)), col, 0.4 + 0.4 * Math.Sin(g.Time * 3 + i));
        }
        // core
        double flick = R.Scram ? 0.05 : 0.8 + 0.2 * Math.Sin(g.Time * (8 + 20 * (1 - st)));
        double cr = rad * (0.14 + 0.1 * p) * flick + rad * 0.04;
        var br = new Avalonia.Media.RadialGradientBrush { GradientStops = { new Avalonia.Media.GradientStop(g.Col(0xFFFFFFFF, 0.95 * flick), 0), new Avalonia.Media.GradientStop(g.Col(col, 0.5 * flick), 0.35), new Avalonia.Media.GradientStop(g.Col(col, 0), 1) } };
        g.Dc.DrawEllipse(br, null, new Point(cx, cy), cr * 3.4, cr * 3.4);
        g.Plasma(cx, cy, cr * 3.0, col, 2, flick);
        g.GlowCircle(cx, cy, cr, g.P.Accent, 2);
        g.HoloShimmer(new Rect(cx - rad * 1.25, cy - rad * 1.25, rad * 2.5, rad * 2.5), null, true);
        // readouts
        g.Text($"{R.Power:0}%", cx, cy + rad * 1.08, 28, col, Align.Center, true);
        g.Text(R.Scram ? "OFFLINE — SCRAM" : R.Overload ? "OVERLOAD" : st < 0.4 ? "UNSTABLE" : R.Cooling ? "NOMINAL / COOLING" : "NOMINAL", cx, cy + rad * 1.08 + g.U(34), 10, R.Scram || R.Overload || st < 0.4 ? g.P.Warning : g.P.Text, Align.Center, true, R.Scram || R.Overload ? 0.5 + 0.5 * g.Pulse(2.5) : 1);
        g.Bar(new Rect(c.X + g.U(6), c.Bottom - g.U(20), c.Width - g.U(12), g.U(18)), st, st < 0.5 ? g.P.Warning : g.P.Primary, "CORE STABILITY", $"{st * 100:0}%");
    }

    void DrawPanel(Gfx g, int i, Rect r)
    {
        switch (i)
        {
            case 0:
                {
                    var c = g.Panel(r, "POWER OUTPUT CONTROL");
                    g.Slider("pwr", new Rect(c.X, c.Y, c.Width, g.U(34)), R.Target / 120.0, "TARGET OUTPUT", $"{R.Target:0}%");
                    double gr = Math.Min(c.Height - g.U(40), c.Width * 0.5) / 2;
                    if (gr > g.U(20)) g.Gauge(c.Center.X, c.Y + g.U(40) + gr, gr - g.U(2), R.Power, 120, "OUTPUT", "%", null, 0.9);
                    break;
                }
            case 1:
                {
                    var c = g.Panel(r, "POWER DISTRIBUTION");
                    double demand = Demand(); bool over = demand > R.Power + 3;
                    g.KeyVal(c.X, c.Y, c.Width, "DEMAND / SUPPLY", $"{demand:0} / {R.Power:0}", over ? g.P.Warning : g.P.Primary, 9);
                    double rh = Math.Min(g.U(g.Touch ? 28 : 22), (c.Height - g.U(16)) / 5);
                    for (int k = 0; k < 5; k++)
                    {
                        double y = c.Y + g.U(15) + k * rh;
                        g.Text(BusNames[k], c.X, y + (rh - g.U(9)) / 2, 8, g.P.Text, Align.Left, false, 0.85);
                        double bx = c.X + c.Width * 0.38;
                        g.Button("bus-:" + k, new Rect(bx, y, g.U(20), rh - g.U(2)), "-", false, true, null, 9);
                        g.Bar(new Rect(bx + g.U(24), y + g.U(2), c.Right - bx - g.U(48), rh - g.U(4)), _alloc[k] * 2.2, g.P.Primary, null, null, 0.85);
                        g.Button("bus+:" + k, new Rect(c.Right - g.U(20), y, g.U(20), rh - g.U(2)), "+", false, true, null, 9);
                    }
                    break;
                }
            case 2:
                {
                    var c = g.Panel(r, "TEMPERATURE");
                    var cols = Layout.Cols(c, g.U(4), 1, 1, 1);
                    g.Meter(cols[0], R.Temperature, 900, "CORE C", null, 0.78);
                    g.Meter(cols[1], R.Temperature * 0.55 + 40, 900, "COOLANT C", g.P.Accent, 0.8);
                    g.Meter(cols[2], R.Temperature * 0.3 + 25, 400, "HULL C", g.P.Secondary, 0.8);
                    break;
                }
            case 3:
                {
                    var c = g.Panel(r, "PRESSURE");
                    var cols = Layout.Cols(c, g.U(4), 1, 1);
                    double gr = Math.Min(cols[0].Width, c.Height) / 2 - g.U(4);
                    g.Gauge(cols[0].Center.X, c.Center.Y, gr, R.Pressure, 60, "PRIMARY", "BAR");
                    g.Gauge(cols[1].Center.X, c.Center.Y, gr, R.Coolant / 100 * 30 + C.Sim.Noise(0.2), 40, "COOLANT", "BAR", g.P.Accent);
                    break;
                }
            case 4:
                {
                    var c = g.Panel(r, "ENERGY GRAPHS");
                    var rows = Layout.Rows(c, g.U(4), 1, 1);
                    g.Graph(rows[0], C.Sim.Power, g.P.Primary, 0, 130, true, "OUTPUT", "%");
                    g.Graph(rows[1], C.Sim.Temp, g.P.Secondary, null, null, true, "CORE TEMP", " C");
                    break;
                }
            case 5: DrawSubsystems(g, r); break;
        }
    }

    void DrawSubsystems(Gfx g, Rect r)
    {
        var c = g.Panel(r, _diag < 1 ? $"SUBSYSTEMS // DIAG {_diag * 100:0}%" : "SUBSYSTEMS / COOLING LOOP");
        int cols = 3, rowsN = 2; double gap = g.U(8);
        double bw = (c.Width - gap * (cols - 1)) / cols, bh = (c.Height - gap) / rowsN * 0.78;
        var centers = new Point[6];
        for (int k = 0; k < 6; k++)
        {
            var rc = new Rect(c.X + (k % cols) * (bw + gap), c.Y + (k / cols) * (bh + gap + c.Height * 0.12), bw, bh);
            centers[k] = rc.Center;
            bool ok = R.SubsystemOk[k]; bool scanning = _diag < 1 && _diag * 6 > k;
            uint col = ok ? g.P.Primary : g.P.Warning;
            g.Fill(rc, col, ok ? 0.08 : 0.2 + 0.2 * g.Pulse(3)); g.Frame(rc, col, 1.2, 0.9);
            g.TextIn(new Rect(rc.X, rc.Y, rc.Width, rc.Height * 0.55), ReactorState.SubsystemNames[k], bw < g.U(80) ? 6.5 : 8, col, Align.Center, true);
            g.TextIn(new Rect(rc.X, rc.Y + rc.Height * 0.45, rc.Width, rc.Height * 0.5), ok ? (scanning && _diag < 1 ? "CHECKED" : "ONLINE") : "FAILED", 7.5, col, Align.Center, false, 0.9);
            g.AddHit("sub:" + k, rc, HitKind.Button);
        }
        // pipes with flow
        for (int k = 0; k < 5; k++)
        {
            var a = centers[k]; var b = centers[k + 1];
            if (k == 2) continue;
            g.Line(a.X + bw * 0.5, a.Y, b.X - bw * 0.5, b.Y, g.P.Accent, 1.5, 0.3);
        }
    }

    public override void OnModuleSlider(string id, double v) { if (id == "pwr") { R.Target = Math.Round(v * 120); if (R.Scram) { R.Scram = false; } } }

    public override string? OnModuleButton(string id)
    {
        if (id.StartsWith("bus"))
        {
            int k = int.Parse(id[5..]); double d = id[3] == '+' ? 0.03 : -0.03;
            _alloc[k] = Math.Clamp(_alloc[k] + d, 0.02, 0.45); return $"{BusNames[k]} {_alloc[k] * 100:0}%";
        }
        if (id.StartsWith("sub:"))
        {
            int k = int.Parse(id[4..]); R.SubsystemOk[k] = !R.SubsystemOk[k]; C.Audio.Play(R.SubsystemOk[k] ? "confirm" : "error");
            Log((R.SubsystemOk[k] ? "REPAIRED: " : "FAILED: ") + ReactorState.SubsystemNames[k]); return ReactorState.SubsystemNames[k] + (R.SubsystemOk[k] ? " REPAIRED" : " FAILED");
        }
        return null;
    }
}
