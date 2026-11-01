using Avalonia;
using Blacksite.Core.Audio;
using Blacksite.Core.Simulation.Domain;
using Blacksite.Core.Themes;
using Blacksite.UI.Rendering;

namespace Blacksite.UI.Modules;

/// <summary>PRESET 12 – QUANTUM RESEARCH FACILITY. Fictional laboratory instrumentation.</summary>
public sealed class QuantumModule : DeckModule
{
    readonly QuantumSim _s = new();
    bool _report; double _alertT = 25, _tickT;
    double[] _bloch = new double[16];
    int _evFirst;
    static readonly (double x, double y, double z)[] Ico = BuildIco();
    static readonly (int a, int b)[] IcoEdges = BuildEdges();

    public override string[] ModuleActions => new[] { "START", "COLLIDE", "ACQUIRE", "ANALYZE", "ANOMALY", "COOLING", "SHUTDOWN", "RESET", "REPORT" };

    static (double, double, double)[] BuildIco()
    {
        double t = (1 + Math.Sqrt(5)) / 2; var v = new List<(double, double, double)>();
        foreach (var (a, b) in new[] { (-1.0, t), (1.0, t), (-1.0, -t), (1.0, -t) }) { v.Add((a, b, 0)); v.Add((0, a, b)); v.Add((b, 0, a)); }
        return v.Select(p => { double n = Math.Sqrt(p.Item1 * p.Item1 + p.Item2 * p.Item2 + p.Item3 * p.Item3); return (p.Item1 / n, p.Item2 / n, p.Item3 / n); }).ToArray();
    }
    static (int, int)[] BuildEdges()
    {
        var e = new List<(int, int)>();
        for (int i = 0; i < Ico.Length; i++) for (int j = i + 1; j < Ico.Length; j++)
            { double dx = Ico[i].x - Ico[j].x, dy = Ico[i].y - Ico[j].y, dz = Ico[i].z - Ico[j].z; if (Math.Sqrt(dx * dx + dy * dy + dz * dz) < 1.15) e.Add((i, j)); }
        return e.ToArray();
    }

    protected override void BuildPanels()
    {
        Def("core", "A  QUANTUM CORE VISUALIZATION", Zone.Main, 1, DrawCore);
        Def("proc", "B  QUANTUM PROCESSOR STATUS", Zone.Left, 1.15, DrawProcessor);
        Def("tasks", "ACTIVE COMPUTATIONAL TASKS", Zone.Left, 0.8, DrawTasks);
        Def("ctl", "D  EXPERIMENT CONTROL", Zone.Left, 1.8, DrawControl);
        Def("det", "C  PARTICLE DETECTOR", Zone.Right, 1.7, DrawDetector);
        Def("spec", "F  ENERGY SPECTRUM", Zone.Right, 1.1, DrawSpectrum);
        Def("hist", "MEASUREMENT HISTORY", Zone.Right, 0.9, DrawHistory);
        Def("term", "E  ANALYSIS TERMINAL", Zone.Bottom, 1.5, DrawTerminal);
        Def("events", "DETECTION EVENTS", Zone.Bottom, 1.1, DrawEvents);
        Def("fac", "G  FACILITY STATUS", Zone.Bottom, 1.2, DrawFacility);
    }

    protected override string? DoAction(string id)
    {
        string m = "";
        switch (id)
        {
            case "START": m = _s.Running ? "EXPERIMENT ALREADY RUNNING" : _s.StartExperiment(); C.Audio.Play("surge", SoundCategory.Event, 0.6f); break;
            case "COLLIDE": m = _s.Collide(); C.Audio.Play("tick", SoundCategory.Event); C.Audio.Play("handshake", SoundCategory.Ui, 0.5f); break;
            case "ACQUIRE": m = _s.Acquire(); C.Audio.Play("scan", SoundCategory.Ui, 0.6f); break;
            case "ANALYZE": m = _s.RunAnalysis(); C.Audio.Play("handshake", SoundCategory.Ui); _report = false; break;
            case "ANOMALY": m = _s.TriggerAnomaly(); C.Events.Trigger("quantum_anomaly"); break;
            case "COOLING": m = _s.ToggleCooling(); C.Audio.Play("mech"); break;
            case "SHUTDOWN": m = _s.EmergencyShutdown(); C.Audio.Play("alarm", SoundCategory.Alarm, 0.7f); break;
            case "RESET": _s.Reset(_s.Scenario); m = "EXPERIMENT RESET"; C.Audio.Play("chime", SoundCategory.Event); break;
            case "REPORT": _report = !_report; if (_report && _s.Analysis.Count == 0) _s.RunAnalysis(); m = _report ? "SCIENTIFIC REPORT" : "REPORT CLOSED"; C.Audio.Play("chime", SoundCategory.Ui); break;
        }
        return m;
    }

    protected override bool IsActionActive(string a) => a switch { "START" => _s.Running, "ACQUIRE" => _s.Acquiring, "COOLING" => _s.Cooling, "ANOMALY" => _s.Anomaly, "SHUTDOWN" => _s.Shutdown, "REPORT" => _report, _ => false };

    protected override void Tick(double dt, double anim)
    {
        int before = _s.EventCount; bool wasRun = _s.Running; _s.Step(dt);
        if (_s.EventCount > before && (int)(AnimT * 6) % 2 == 0) C.Audio.Play("tick", SoundCategory.Ambient, 0.25f);
        if (wasRun && !_s.Running && _s.Progress >= 1) { C.Toast("EXPERIMENT COMPLETE"); C.Audio.Play("chime", SoundCategory.Event); }
        for (int i = 0; i < _bloch.Length; i++) _bloch[i] += anim * (0.6 + 0.13 * i);
        _alertT -= dt * Cfg.AlertFrequency;
        if (_alertT <= 0 && Cfg.AlertFrequency > 0.05) { _alertT = 45 + Rng.NextDouble() * 50; if (_s.Running && !_s.Anomaly) DoAction("ANOMALY"); }
    }

    // ---------------------------------------------------------------- A: core visualization
    void DrawCore(Gfx g, Rect r)
    {
        double cx = r.Center.X, cy = r.Y + r.Height * 0.46, rad = Math.Min(r.Width, r.Height * 0.78) * 0.42;
        double stab = _s.Stability, load = Math.Clamp(_s.Power / 900, 0, 1), t = AnimT;
        // interference pattern between two coherent sources
        int cols = g.Quality == Core.Configuration.GraphicsProfile.High ? 52 : g.Quality == Core.Configuration.GraphicsProfile.Balanced ? 34 : 18, rows = (int)(cols * r.Height * 0.8 / r.Width) + 1;
        var ar = new Rect(r.X + r.Width * 0.04, r.Y + r.Height * 0.02, r.Width * 0.92, r.Height * 0.8);
        double cw = ar.Width / cols, ch = ar.Height / rows, s1x = ar.Center.X - ar.Width * 0.12, s2x = ar.Center.X + ar.Width * 0.12, sy = ar.Center.Y;
        for (int j = 0; j < rows; j++)
            for (int i = 0; i < cols; i++)
            {
                double x = ar.X + (i + 0.5) * cw, y = ar.Y + (j + 0.5) * ch;
                double r1 = Math.Sqrt((x - s1x) * (x - s1x) + (y - sy) * (y - sy)), r2 = Math.Sqrt((x - s2x) * (x - s2x) + (y - sy) * (y - sy));
                double k = 0.22 / Math.Max(1, g.Scale), v = Math.Cos(k * r1 - t * 2.2) + Math.Cos(k * r2 - t * 2.2); v = v * v / 4;
                double fall = 1 / (1 + (r1 + r2) / (ar.Width * 0.9));
                g.Fill(new Rect(x - cw * 0.34, y - ch * 0.34, cw * 0.68, ch * 0.68), j % 2 == 0 ? g.P.Secondary : g.P.Primary, v * 0.5 * fall * (0.35 + 0.65 * stab));
            }
        // energy field halo
        g.Circle(cx, cy, rad * (1.08 + 0.04 * Math.Sin(t * 3) * load), g.P.Accent, 1, 0.25 + 0.3 * load, g.U(3));
        g.Circle(cx, cy, rad * 1.2, g.P.Primary, 1, 0.12);
        // nested rotating polyhedra
        void Poly3(double scale, double rotA, double rotB, uint col, double a, double w)
        {
            var pts = new Point[Ico.Length]; double[] dz = new double[Ico.Length];
            for (int i = 0; i < Ico.Length; i++)
            {
                var (x, y, z) = Ico[i]; double x1 = x * Math.Cos(rotA) - z * Math.Sin(rotA), z1 = x * Math.Sin(rotA) + z * Math.Cos(rotA);
                double y1 = y * Math.Cos(rotB) - z1 * Math.Sin(rotB), z2 = y * Math.Sin(rotB) + z1 * Math.Cos(rotB);
                pts[i] = new Point(cx + x1 * rad * scale, cy - y1 * rad * scale); dz[i] = z2;
            }
            foreach (var (p, q) in IcoEdges) g.Line(pts[p].X, pts[p].Y, pts[q].X, pts[q].Y, col, w, a * (0.45 + 0.35 * (dz[p] + dz[q])));
            for (int i = 0; i < pts.Length; i++) g.Disc(pts[i].X, pts[i].Y, g.U(1.8), col, a * (0.5 + 0.5 * dz[i]));
        }
        double jit = (1 - stab) * 0.5;
        Poly3(0.95, t * 0.35, 0.6 + Math.Sin(t * 0.2) * 0.2, g.P.Primary, 0.75, 1);
        Poly3(0.62, -t * 0.55, 1.1, g.P.Secondary, 0.85, 1.2);
        Poly3(0.32, t * 0.9 + jit * Math.Sin(t * 40), 0.3, g.P.Accent, 1, 1.4);
        // particle trajectories (helices around the core)
        int np = g.Quality == Core.Configuration.GraphicsProfile.Low ? 8 : 24;
        for (int i = 0; i < np; i++)
        {
            double ph = t * (0.4 + i * 0.03) + i * 2.4, rr = rad * (0.4 + 0.5 * Math.Abs(Math.Sin(ph * 0.5 + i))) * (0.8 + 0.4 * load);
            for (int k = 0; k < 4; k++)
            {
                double p2 = ph - k * 0.12; double rr2 = rad * (0.4 + 0.5 * Math.Abs(Math.Sin(p2 * 0.5 + i))) * (0.8 + 0.4 * load);
                g.Disc(cx + Math.Cos(p2) * rr2, cy + Math.Sin(p2 * 1.0 + i * 0.1) * rr2 * 0.55, g.U(2.2 - k * 0.4), i % 3 == 0 ? g.P.Accent : g.P.Primary, 0.9 - k * 0.2);
            }
        }
        // core glow and status ring
        g.Plasma(cx, cy, rad * 0.26, g.P.Accent, 9, 0.55 + 0.4 * load);
        g.Arc(cx, cy, rad * 0.4, t * 120, 90 + 200 * stab, g.P.Accent, 2, 0.9);
        // probability distribution + qubit Bloch spheres
        double by = r.Bottom - r.Height * 0.17, bh = r.Height * 0.14;
        var pr = new Rect(r.X + r.Width * 0.03, by, r.Width * 0.38, bh);
        int bars = 24; double sig = 2.5 + (1 - stab) * 9;
        double sum = 0; var pv = new double[bars];
        for (int i = 0; i < bars; i++) { double x = i - bars / 2.0 + Math.Sin(t * 0.8) * 3; pv[i] = Math.Exp(-x * x / (2 * sig * sig)) * (1 + 0.4 * Math.Cos(i * 1.3 + t * 2)); sum += pv[i]; }
        for (int i = 0; i < bars; i++) { double h = Math.Clamp(pv[i] / sum * 6, 0, 1) * bh; g.Fill(new Rect(pr.X + i * pr.Width / bars + 1, pr.Bottom - h, pr.Width / bars - 2, h), g.P.Secondary, 0.75); }
        g.Line(pr.X, pr.Bottom, pr.Right, pr.Bottom, g.P.Secondary, 1, 0.6); g.Text("P(|ψ⟩) STATE DISTRIBUTION", pr.X, pr.Y - g.U(10), 7.5, g.P.Text, Align.Left, false, 0.7);
        int nq = 8; double qs = Math.Min(bh * 0.5, r.Width * 0.5 / nq / 2);
        for (int q = 0; q < nq; q++)
        {
            double qx = r.X + r.Width * 0.46 + (q + 0.5) * (r.Width * 0.5 / nq), qy = by + bh * 0.5;
            g.Circle(qx, qy, qs, g.P.Primary, 1, 0.6); g.Dc.DrawEllipse(null, g.Pn(g.P.Primary, 1, 0.3), new Point(qx, qy), qs, qs * 0.3);
            double th = _bloch[q] * 0.7, len = qs * (0.25 + 0.75 * stab); double vx = Math.Sin(th) * Math.Cos(_bloch[q]) * len, vy = -Math.Cos(th) * len * 0.9;
            g.GlowLine(qx, qy, qx + vx, qy + vy, g.P.Accent, 1.4); g.Disc(qx + vx, qy + vy, g.U(2), g.P.Accent);
            g.Text($"Q{q}", qx, qy + qs + g.U(1), 6.5, g.P.Text, Align.Center, false, 0.55);
        }
        // headline readouts
        g.Text($"CYCLES {_s.Cycles:N0}", r.X + g.U(6), r.Y + g.U(3), 9, g.P.Accent, Align.Left, true);
        g.Text(_s.Running ? $"RUNNING  {_s.Progress * 100:0}%" : _s.Shutdown ? "SHUTDOWN" : "IDLE", r.Right - g.U(6), r.Y + g.U(3), 9, _s.Anomaly || _s.Shutdown ? g.P.Warning : g.P.Primary, Align.Right, true);
        g.Text(QuantumSim.Experiments[_s.Experiment], r.Center.X, r.Y + g.U(3), 8.5, g.P.Text, Align.Center, false, 0.8);
        g.Text($"STATE |{(_s.Anomaly ? "DECOHERENT" : stab > 0.9 ? "COHERENT" : "DEGRADED")}⟩   FIDELITY {_s.Fidelity:0.00}%", r.X + g.U(6), r.Y + g.U(16), 8, _s.Anomaly ? g.P.Warning : g.P.Text, Align.Left, false, 0.85);
    }

    // ---------------------------------------------------------------- B: processor
    void DrawProcessor(Gfx g, Rect r)
    {
        var kv = new (string, string, uint?)[]
        {
            ("QUBITS", $"{_s.Qubits}", null), ("COHERENCE T2", $"{_s.Coherence:0} µS", _s.Coherence < 80 ? g.P.Warning : null), ("GATE FIDELITY", $"{_s.Fidelity:0.000}%", _s.Fidelity < 99 ? g.P.Warning : null), ("ERROR RATE", $"{_s.ErrorRate:0.0000}", null),
            ("PROC TEMP", $"{_s.ProcTempMk:0.0} mK", _s.ProcTempMk > 22 ? g.P.Warning : null), ("COOLANT", $"{_s.CoolantK:0.00} K", null), ("ERR CORRECTION", _s.Stability > 0.6 ? "ACTIVE" : "OVERLOAD", _s.Stability > 0.6 ? 0xFF30FF70 : g.P.Warning), ("EC CYCLES", $"{_s.ErrCorrCycles:N0}", null),
            ("COOLING EFF", $"{_s.CoolEff * 100:0}%", null), ("POWER", $"{_s.Power:0} KW", null), ("STABILITY", $"{_s.Stability * 100:0.0}%", _s.Stability < 0.6 ? g.P.Warning : null), ("VACUUM", $"{_s.Vacuum:0.0e+0} PA", null),
        };
        double used = g.KvGrid(r, kv, r.Width > g.U(300) ? 2 : 1, 8.5);
        var m = new Rect(r.X, r.Y + used + g.U(2), r.Width, r.Bottom - r.Y - used - g.U(2));
        if (m.Height > g.U(26)) g.HMeter(new Rect(m.X, m.Y, m.Width, Math.Min(m.Height, g.U(26))), _s.Stability * 100, 0, 100, "SYSTEM STABILITY", $"{_s.Stability * 100:0}%", _s.Stability < 0.6 ? g.P.Warning : g.P.Primary, 60);
    }

    void DrawTasks(Gfx g, Rect r)
    {
        double lh = Math.Min(g.U(22), r.Height / Math.Max(1, _s.Tasks.Count));
        for (int i = 0; i < _s.Tasks.Count; i++) { var t = _s.Tasks[i]; g.Bar(new Rect(r.X, r.Y + i * lh, r.Width, lh - g.U(2)), t.Progress, t.Done ? g.P.Secondary : g.P.Primary, $"{t.Name}  [{t.Qubits}Q]", t.Done ? "DONE" : $"{t.Progress * 100:0}%"); }
    }

    // ---------------------------------------------------------------- D: experiment control
    void DrawControl(Gfx g, Rect r)
    {
        double bh = g.U(g.Touch ? 28 : 22), gap = g.U(3);
        g.Button("qx:next", new Rect(r.X, r.Y, r.Width, bh), "EXPERIMENT: " + QuantumSim.Experiments[_s.Experiment] + "  >", false, true, null, 8);
        bool two = r.Width > g.U(290);
        var sl = new (string id, string lab, double v01, string val)[]
        {
            ("qx:field", "MAGNETIC FIELD", _s.Field / 14, $"{_s.Field:0.00} T"), ("qx:temp", "TEMPERATURE SETPOINT", (_s.TempSet - 2) / 38, $"{_s.TempSet:0.0} K"),
            ("qx:press", "VACUUM PRESSURE", (_s.PressureLog + 9) / 6, $"1E{_s.PressureLog:0.0} PA"), ("qx:energy", "ENERGY INPUT", (_s.EnergyTeV - 0.5) / 13.1, $"{_s.EnergyTeV:0.0} TEV"),
            ("qx:interval", "MEASUREMENT INTERVAL", (_s.IntervalMs - 50) / 950, $"{_s.IntervalMs:0} MS"), ("qx:sens", "DETECTOR SENSITIVITY", (_s.Sensitivity - 0.1) / 0.9, $"{_s.Sensitivity * 100:0}%"),
            ("qx:dur", "EXPERIMENT DURATION", (_s.DurationS - 30) / 270, $"{_s.DurationS:0} S"),
        };
        int rows = two ? (sl.Length + 1) / 2 : sl.Length; double top = r.Y + bh + g.U(4), areaH = r.Height - bh - g.U(4) - bh - g.U(8), rh = Math.Min(g.U(52), areaH / rows);
        double cw = two ? (r.Width - g.U(8)) / 2 : r.Width;
        for (int i = 0; i < sl.Length; i++)
        {
            int col = two ? i % 2 : 0, row = two ? i / 2 : i;
            g.Slider(sl[i].id, new Rect(r.X + col * (cw + g.U(8)), top + row * rh, cw, rh - g.U(2)), Math.Clamp(sl[i].v01, 0, 1), sl[i].lab, sl[i].val);
        }
        double by = r.Bottom - bh; double bw = (r.Width - gap * 3) / 4;
        g.Button("qx:start", new Rect(r.X, by, bw, bh), _s.Running ? "STOP" : "START", _s.Running, true, null, 8);
        g.Button("qx:acq", new Rect(r.X + (bw + gap), by, bw, bh), _s.Acquiring ? "ACQ: ON" : "ACQ: OFF", _s.Acquiring, true, null, 8);
        g.Button("qx:cool", new Rect(r.X + 2 * (bw + gap), by, bw, bh), _s.Cooling ? "COOL: ON" : "COOL: OFF", _s.Cooling, true, null, 8);
        g.Button("qx:scram", new Rect(r.X + 3 * (bw + gap), by, bw, bh), "E-SHUTDOWN", _s.Shutdown, true, g.P.Warning, 8);
    }

    protected override void PanelSlider(string id, double v)
    {
        switch (id)
        {
            case "qx:field": _s.Field = Math.Round(v * 14 * 20) / 20; break;
            case "qx:temp": _s.TempSet = Math.Round((2 + v * 38) * 10) / 10; break;
            case "qx:press": _s.PressureLog = Math.Round((-9 + v * 6) * 10) / 10; break;
            case "qx:energy": _s.EnergyTeV = Math.Round((0.5 + v * 13.1) * 10) / 10; break;
            case "qx:interval": _s.IntervalMs = Math.Round((50 + v * 950) / 10) * 10; break;
            case "qx:sens": _s.Sensitivity = Math.Round((0.1 + v * 0.9) * 100) / 100; break;
            case "qx:dur": _s.DurationS = Math.Round((30 + v * 270) / 5) * 5; break;
        }
    }

    protected override string? PanelButton(string id)
    {
        switch (id)
        {
            case "qx:next": C.Audio.Play("deepbeep"); return _s.NextExperiment();
            case "qx:start": return _s.Running ? ((_s.Running = false) ? "" : "EXPERIMENT STOPPED") : DoAction("START");
            case "qx:acq": return DoAction("ACQUIRE");
            case "qx:cool": return DoAction("COOLING");
            case "qx:scram": return DoAction("SHUTDOWN");
            case "qx:col": return DoAction("COLLIDE");
        }
        return null;
    }

    // ---------------------------------------------------------------- C: detector
    void DrawDetector(Gfx g, Rect r)
    {
        double bh = g.U(g.Touch ? 26 : 20);
        g.Button("qx:col", new Rect(r.X, r.Y, g.U(130), bh), "TRIGGER COLLISION", false, true, g.P.Accent, 8);
        var ev = _s.Events.LastOrDefault();
        g.Text(ev == null ? "NO EVENT" : $"{ev.Id}  {ev.Class}  E={ev.TotalEnergy:0} GEV", r.Right, r.Y + g.U(4), 8, ev != null && ev.Candidate ? g.P.Warning : g.P.Text, Align.Right, true);
        var a = new Rect(r.X, r.Y + bh + g.U(2), r.Width, r.Height - bh - g.U(2));
        double cx = a.Center.X, cy = a.Center.Y, R = Math.Min(a.Width, a.Height) / 2 - g.U(3);
        (double a0, double a1, string name, uint col)[] layers = { (0.07, 0.10, "BEAM PIPE", g.P.Text), (0.16, 0.30, "SILICON TRACKER", g.P.Primary), (0.34, 0.46, "TRANSITION", g.P.Secondary), (0.52, 0.62, "ECAL", g.P.Accent), (0.66, 0.78, "HCAL", g.P.Secondary), (0.82, 0.94, "MUON SYSTEM", g.P.Primary) };
        int li = 0;
        foreach (var l in layers)
        {
            int segs = 8 + li * 4; double gapA = 0.5;
            for (int s = 0; s < segs; s++)
            {
                double st = s * 360.0 / segs + gapA, sw = 360.0 / segs - gapA * 2;
                g.Arc(cx, cy, R * (l.a0 + l.a1) / 2, st, sw, l.col, R * (l.a1 - l.a0) * 0.55 / g.Scale * 0.5, 0.2 + 0.07 * (s % 2));
            }
            g.Circle(cx, cy, R * l.a1, l.col, 1, 0.35);
            if (li > 0) g.Text(l.name, cx + R * (l.a0 + l.a1) / 2 * 0.707 + g.U(2), cy - R * (l.a0 + l.a1) / 2 * 0.707 - g.U(4), 6, l.col, Align.Left, false, 0.6);
            li++;
        }
        // field direction hint
        g.Text($"B = {_s.ContainmentField:0.0} T ⊙", a.X + g.U(2), a.Bottom - g.U(11), 7.5, g.P.Text, Align.Left, false, 0.7);
        // tracks from recent events
        foreach (var e in _s.Events)
        {
            double fade = Math.Clamp(1 - e.Age / 10, 0, 1);
            foreach (var t in e.Tracks)
            {
                var pts = new List<Point> { new Point(cx, cy) }; double th = t.Angle, x = cx, y = cy, len = R * t.Length * Math.Min(1, e.Age * 3 / (0.3 + t.Length));
                double step = len / 22, k = t.Curvature * 1.6;
                for (int i = 0; i < 22; i++) { th += k * step / R * 6; x += Math.Cos(th) * step; y += Math.Sin(th) * step; pts.Add(new Point(x, y)); }
                uint col = t.Kind switch { "μ±" => 0xFF60FFA0, "γ" => 0xFFFFE070, "e±" => 0xFFFF70C0, "p" => 0xFFFFA060, _ => g.P.Accent };
                g.Poly(pts, col, e.Candidate ? 1.8 : 1.2, 0.85 * fade);
                if (e.Age * 3 > 0.3 + t.Length) g.Disc(pts[^1].X, pts[^1].Y, g.U(2), col, fade);
            }
            g.Disc(cx, cy, g.U(5 * Math.Max(0, 1 - e.Age)), 0xFFFFFFFF, 0.9);
            if (e.Candidate && fade > 0.3) g.Text(e.Id, cx + R * 0.55, cy + R * 0.7, 7.5, g.P.Warning, Align.Left, true, fade);
        }
        // sweep
        g.Arc(cx, cy, R, AnimT * 40, 30, g.P.Primary, 1.2, 0.35);
        g.Text(_s.Acquiring && _s.Running ? "TRIGGER: ARMED" : "TRIGGER: STANDBY", a.Right - g.U(2), a.Bottom - g.U(11), 7.5, _s.Acquiring ? 0xFF30FF70 : g.P.Text, Align.Right, true, 0.85);
    }

    // ---------------------------------------------------------------- F: spectrum
    void DrawSpectrum(Gfx g, Rect r)
    {
        var rows = Layout.Rows(r, g.U(4), 1, 1);
        double max = Math.Max(10, _s.Spectrum.Max());
        var e = _s.Spectrum.Select(v => (float)(v / max)).ToList();
        g.Spectrum(rows[0], e, g.P.Secondary, "0 GEV", "190 GEV  ENERGY DISTRIBUTION", 8, false);
        // histogram bars overlay style
        var f = _s.Freq.Select(v => (float)Math.Clamp(v / 1.2, 0, 1)).ToList();
        g.Spectrum(rows[1], f, g.P.Accent, "0 MHZ", "READOUT FREQUENCY SPECTRUM", 10, true);
        g.Text($"σ = {Math.Sqrt(_s.Variance):0.0}  <E> = {_s.Mean:0.0}  N = {_s.EventCount}", r.Right - g.U(3), r.Y + g.U(2), 7.5, g.P.Text, Align.Right, true, 0.8);
    }

    void DrawHistory(Gfx g, Rect r)
    {
        var rows = Layout.Rows(r, g.U(3), 1, 1); var a = Layout.Cols(rows[0], g.U(3), 1, 1); var b = Layout.Cols(rows[1], g.U(3), 1, 1);
        g.Graph(a[0], _s.TempH, g.P.Warning, null, null, true, "PROC TEMP", " mK"); g.Graph(a[1], _s.CohH, g.P.Primary, null, null, true, "COHERENCE", " µs");
        g.Graph(b[0], _s.StabH, g.P.Accent, 0, 1, true, "STABILITY"); g.Graph(b[1], _s.PowerH, g.P.Secondary, null, null, true, "POWER", " kW");
    }

    // ---------------------------------------------------------------- E: terminal, events, facility
    void DrawTerminal(Gfx g, Rect r)
    {
        var lines = new List<string>(); lines.AddRange(_s.Log.TakeLast(60)); if (_s.Analysis.Count > 0) { lines.Add("---- ANALYSIS ----"); lines.AddRange(_s.Analysis); }
        double lh = g.U(11.5); int max = Math.Max(1, (int)(r.Height / lh)); int start = Math.Max(0, lines.Count - max);
        for (int i = start; i < lines.Count; i++) { string l = lines[i]; bool w = l.Contains("ANOMALY") || l.Contains("SHUTDOWN") || l.Contains("FLAG"); g.Text(l, r.X, r.Y + (i - start) * lh, 7.5, w ? g.P.Warning : l.StartsWith("  ") || l.StartsWith("--") ? g.P.Accent : g.P.Text, Align.Left, false, 0.92); }
        if ((int)(AnimT * 2) % 2 == 0) g.Text("█", r.X, r.Y + Math.Min(lines.Count - start, max - 1) * lh, 7.5, g.P.Primary);
    }

    void DrawEvents(Gfx g, Rect r)
    {
        var rows = _s.Events.AsEnumerable().Reverse().Select(e => new[] { e.Id[3..], e.Class[..Math.Min(10, e.Class.Length)], $"{e.TotalEnergy:0}", e.Tracks.Count.ToString(), $"{e.Age:0}s" }).ToList();
        g.Table(r, new[] { "ID", "CLASS", "GEV", "TRK", "AGE" }, new[] { 1, 1.7, 0.8, 0.6, 0.6 }, rows, _evFirst, -1, "ev:", (ri, ci) => ci == 1 && rows[ri][1].StartsWith("RESONANCE") ? g.P.Warning : null);
    }

    void DrawFacility(Gfx g, Rect r)
    {
        double lh = Math.Min(g.U(15), r.Height / 5); int cols = r.Width > g.U(280) ? 2 : 1;
        double cw = r.Width / cols;
        for (int i = 0; i < _s.Instruments.Length; i++)
        {
            int c = i % cols, row = i / cols; double x = r.X + c * cw, y = r.Y + row * lh; if (y + lh > r.Bottom + 1) break;
            double h = _s.Health[i]; g.Led(x + g.U(5), y + lh / 2, !_s.Instruments[i] ? 0 : h > 0.8 ? 1 : h > 0.55 ? 2 : 3);
            g.Text(QuantumSim.InstNames[i], x + g.U(13), y + (lh - g.U(9)) / 2, 7.5, g.P.Text, Align.Left, false, 0.9);
            g.Text($"{h * 100:0}%", x + cw - g.U(4), y + (lh - g.U(9)) / 2, 7.5, h < 0.6 ? g.P.Warning : g.P.Primary, Align.Right, true);
            g.AddHit("qi:" + i, new Rect(x, y, cw, lh), HitKind.Button);
        }
        double ey = r.Y + lh * ((_s.Instruments.Length + cols - 1) / cols) + g.U(3);
        if (ey + g.U(12) < r.Bottom)
        {
            string safety = _s.Shutdown ? "SAFE — SHUTDOWN" : _s.Anomaly ? "CAUTION — ANOMALY" : _s.Quench ? "QUENCH" : "NORMAL";
            g.Text($"CONTAINMENT {_s.ContainmentField:0.00} T   LAB 21.{(int)(_s.Time * 3 % 9)}°C   SAFETY: {safety}", r.X, ey, 7.5, _s.Anomaly || _s.Shutdown ? g.P.Warning : g.P.Primary, Align.Left, true);
        }
    }

    protected override void AfterDraw(Gfx g, Rect area)
    {
        if (!_report) return;
        g.Fill(area, 0xFF000000, 0.72); g.AddHit("qr:report", area);
        double w = Math.Min(area.Width - g.U(10), g.U(620)), h = Math.Min(area.Height - g.U(10), g.U(380));
        var rr = new Rect(area.Center.X - w / 2, area.Center.Y - h / 2, w, h); var inner = g.Panel(rr, "SCIENTIFIC REPORT — SIMULATED DATA");
        var lines = new List<string> { $"EXPERIMENT:  {QuantumSim.Experiments[_s.Experiment]}", $"STATUS:      {(_s.Running ? "RUNNING" : _s.Progress >= 1 ? "COMPLETE" : "IDLE")}   PROGRESS {_s.Progress * 100:0}%", $"PARAMETERS:  B={_s.Field:0.00} T   T={_s.TempSet:0.0} K   E={_s.EnergyTeV:0.0} TEV   SENS={_s.Sensitivity * 100:0}%", "" };
        lines.AddRange(_s.Analysis); lines.Add(""); lines.Add($"STABILITY {_s.Stability * 100:0}%   COHERENCE {_s.Coherence:0} US   POWER {_s.Power:0} KW"); lines.Add("ALL VALUES ARE SIMULATED. TAP ANYWHERE TO CLOSE.");
        double lh = g.U(13.5); for (int i = 0; i < lines.Count && (i + 1) * lh < inner.Height; i++) g.Text(lines[i], inner.X, inner.Y + i * lh, 9, lines[i].StartsWith("STATUS") || lines[i].Contains("FLAG") ? g.P.Warning : g.P.Text, Align.Left, i < 1);
    }

    protected override void PanelTap(Point p, string? id)
    {
        if (id == "qr:report") { _report = false; return; }
        if (id != null && id.StartsWith("qi:")) { int i = int.Parse(id[3..]); _s.Instruments[i] = !_s.Instruments[i]; C.Audio.Play(_s.Instruments[i] ? "confirm" : "error"); C.Toast($"{QuantumSim.InstNames[i]} {(_s.Instruments[i] ? "ENABLED" : "DISABLED")}"); }
    }
}
