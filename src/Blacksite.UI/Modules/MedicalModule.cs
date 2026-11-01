using Avalonia;
using Blacksite.Core.Audio;
using Blacksite.Core.Simulation.Domain;
using Blacksite.UI.Rendering;

namespace Blacksite.UI.Modules;

/// <summary>PRESET 14 – ADVANCED MEDICAL LIFE SUPPORT. Entirely fictional; not medical advice or real monitoring.</summary>
public sealed class MedicalModule : DeckModule
{
    readonly MedicalSim _s = new();
    bool _report; double _alarmT, _alertT = 60, _beat;
    int _crewFirst, _eqFirst;

    public override string[] ModuleActions => new[] { "PATIENT", "SCAN", "DIAG", "EMERGENCY", "OXYGEN", "RECOVER", "REPORT" };

    protected override void BuildPanels()
    {
        Def("monitor", "A  HUMAN LIFE SUPPORT MONITOR", Zone.Main, 1, DrawMonitor);
        Def("life", "B  LIFE SUPPORT SYSTEM", Zone.Left, 1.6, DrawLifeSupport);
        Def("scan", "C  MEDICAL SCANNER", Zone.Left, 1.7, DrawScanner);
        Def("equip", "D  MEDICAL EQUIPMENT STATUS", Zone.Right, 1.2, DrawEquipment);
        Def("timeline", "E  PATIENT STATUS TIMELINE", Zone.Right, 1.3, DrawTimeline);
        Def("emerg", "F  EMERGENCY RESPONSE", Zone.Right, 1.4, DrawEmergency);
        Def("trend", "VITAL SIGN TRENDS", Zone.Bottom, 1.4, DrawTrends);
        Def("crew", "CREW STATUS", Zone.Bottom, 1, DrawCrew);
        Def("diag", "LIFE SUPPORT DIAGNOSTICS", Zone.Bottom, 1, DrawDiag);
        Def("meds", "MEDICATION STORAGE (SIM)", Zone.Bottom, 0.9, DrawMeds);
    }

    protected override string? DoAction(string id)
    {
        string m = "";
        switch (id)
        {
            case "PATIENT": m = _s.SelectPatient(_s.Selected + 1); C.Audio.Play("deepbeep"); break;
            case "SCAN": m = _s.StartScan(); C.Audio.Play("scan", SoundCategory.Event); break;
            case "DIAG": m = _s.Diagnostics(); C.Audio.Play("handshake", SoundCategory.Ui); break;
            case "EMERGENCY": m = _s.Emergency(); C.Events.Trigger("medical_emergency"); break;
            case "OXYGEN": m = _s.ToggleEmergO2(); C.Audio.Play("mech"); break;
            case "RECOVER": m = _s.Recover(); C.Audio.Play(_s.Condition == Cond.Recovery ? "defib" : "error", SoundCategory.Event); break;
            case "REPORT": _report = !_report; m = _report ? "MEDICAL REPORT (FICTIONAL)" : "REPORT CLOSED"; C.Audio.Play("chime"); break;
        }
        return m;
    }

    protected override bool IsActionActive(string a) => a switch { "SCAN" => _s.Scanning, "DIAG" => _s.DiagProg < 1, "EMERGENCY" => _s.Condition != Cond.Stable, "OXYGEN" => _s.EmergO2, "RECOVER" => _s.Condition == Cond.Recovery, "REPORT" => _report, _ => false };

    protected override void Tick(double dt, double anim)
    {
        _s.Step(dt);
        if (_s.BeatFlag && _s.Condition != Cond.Arrest) C.Audio.Play("ecg", SoundCategory.Ambient, _s.Spo2 < 90 ? 0.55f : 0.35f);
        _alarmT -= dt;
        if (_s.Alert >= 3 && _alarmT <= 0) { _alarmT = 1.6; C.Audio.Play("triple", SoundCategory.Alarm, 0.7f); }
        else if (_s.Alert == 2 && _alarmT <= 0) { _alarmT = 4; C.Audio.Play("deepbeep", SoundCategory.Alarm, 0.6f); }
        if (_s.Condition == Cond.Arrest && _alarmT <= 0.8 && _alarmT > 0.7) C.Audio.Play("tick", SoundCategory.Alarm);
        _alertT -= dt * Cfg.AlertFrequency;
        if (_alertT <= 0 && Cfg.AlertFrequency > 0.05) { _alertT = 90 + Rng.NextDouble() * 120; if (_s.Condition == Cond.Stable) DoAction("EMERGENCY"); }
    }

    // ---------------------------------------------------------------- A: monitor
    void DrawMonitor(Gfx g, Rect r)
    {
        var p = _s.Patients[_s.Selected];
        double bh = g.U(g.Touch ? 26 : 20);
        double bw = Math.Min(g.U(70), (r.Width * 0.45) / 4);
        for (int i = 0; i < _s.Patients.Count; i++) g.Button("md:p:" + i, new Rect(r.X + i * (bw + g.U(3)), r.Y, bw, bh), _s.Patients[i].Name.Split(' ')[^1], i == _s.Selected, true, null, bw < g.U(50) ? 6 : 7.5);
        g.Text($"{p.Name}  |  {p.Role}  |  AGE {p.Age}  |  SIMULATED PATIENT", r.Right - g.U(2), r.Y + g.U(5), 8, g.P.Text, Align.Right, false, 0.8);
        uint ecgCol = 0xFF40FF80, plCol = g.P.Primary, rsCol = 0xFFFFD060;
        bool wide = r.Width > g.U(520);
        var body = new Rect(r.X, r.Y + bh + g.U(3), r.Width, r.Height - bh - g.U(3));
        Rect wave = wide ? new Rect(body.X, body.Y, body.Width * 0.66, body.Height - g.U(22)) : new Rect(body.X, body.Y, body.Width, body.Height * 0.52);
        Rect nums = wide ? new Rect(wave.Right + g.U(5), body.Y, body.Width - wave.Width - g.U(5), body.Height - g.U(22)) : new Rect(body.X, wave.Bottom + g.U(4), body.Width, body.Height - wave.Height - g.U(26));
        var rows = Layout.Rows(wave, g.U(4), 1.6, 1, 1);
        bool arrest = _s.Condition == Cond.Arrest;
        g.Scope(rows[0], _s.Ecg, ecgCol, -0.4, 1.25, "ECG  II   25 MM/S", true, 1.6);
        g.Scope(rows[1], _s.Pleth, plCol, -0.05, 1.15, "PLETH  SpO2", true, 1.5);
        g.Scope(rows[2], _s.Resp, rsCol, -1.3, 1.3, "RESP / CAPNOGRAPHY", true, 1.4);
        // numeric blocks
        string[] lab = { "HR", "SpO2", "RESP", "TEMP", "NIBP", "EtCO2" };
        string[] val = { arrest ? "0" : $"{_s.Hr:0}", $"{_s.Spo2:0}", $"{_s.Rr:0}", $"{_s.Temp:0.0}", arrest ? "---/---" : $"{_s.Sys:0}/{_s.Dia:0}", $"{_s.EtCo2:0}" };
        string[] unit = { "BPM", "%", "/MIN", "°C", "MMHG", "MMHG" }; uint[] col = { ecgCol, plCol, rsCol, 0xFFFF9070, 0xFFE0E8F0, 0xFFFFD060 };
        int cols = wide ? 1 : 3; int rws = (6 + cols - 1) / cols; double ch = nums.Height / rws, cw = nums.Width / cols;
        for (int i = 0; i < 6; i++)
        {
            var c = new Rect(nums.X + (i % cols) * cw, nums.Y + (i / cols) * ch, cw - g.U(2), ch - g.U(2));
            bool bad = i == 0 && (_s.Hr > 130 || _s.Hr < 45) || i == 1 && _s.Spo2 < 92;
            g.Fill(c, col[i], bad ? 0.2 + 0.2 * g.Pulse(2.5) : 0.05); g.Frame(c, bad ? g.P.Warning : col[i], 1, 0.5);
            g.Text(lab[i], c.X + g.U(5), c.Y + g.U(2), 8, col[i], Align.Left, true, 0.9);
            g.Text(unit[i], c.Right - g.U(4), c.Y + g.U(3), 7, g.P.Text, Align.Right, false, 0.6);
            g.Text(val[i], c.Center.X, c.Y + c.Height * 0.3, Math.Clamp(c.Height * 0.55 / g.Scale, 12, 40), bad ? g.P.Warning : col[i], Align.Center, true);
        }
        // status line
        double sy = body.Bottom - g.U(18);
        bool conn = true;
        g.Led(body.X + g.U(5), sy + g.U(9), conn ? 1 : 3); g.Text("MONITOR LINK", body.X + g.U(13), sy + g.U(3), 7.5, g.P.Text, Align.Left, false, 0.8);
        g.Led(body.X + g.U(95), sy + g.U(9), _s.EmergO2 ? 2 : 1); g.Text(_s.EmergO2 ? "O2 DELIVERY: EMERGENCY" : "O2 DELIVERY: CABIN AIR", body.X + g.U(103), sy + g.U(3), 7.5, _s.EmergO2 ? g.P.Warning : g.P.Text, Align.Left, _s.EmergO2, 0.9);
        string cond = _s.Condition switch { Cond.Stable => "STABLE", Cond.Hypoxia => "HYPOXIA (SIM)", Cond.Tachycardia => "TACHYCARDIA (SIM)", Cond.AFib => "ARRHYTHMIA (SIM)", Cond.Arrest => "CARDIAC ARREST (SIM)", _ => "RECOVERING" };
        uint cc = _s.Condition == Cond.Stable ? 0xFF30FF70 : _s.Condition == Cond.Recovery ? g.P.Primary : 0xFFFF4040;
        g.Text($"CONDITION: {cond}   HRV {_s.HrvMs:0} MS   BEATS {_s.Beats}", body.Right - g.U(3), sy + g.U(3), 8, cc, Align.Right, true, 0.5 + 0.5 * (cc == 0xFFFF4040 ? g.Pulse(2.5) : 1));
        if (_s.Alert >= 2)
        {
            var br = new Rect(wave.X + wave.Width * 0.15, wave.Y + g.U(18), wave.Width * 0.7, g.U(20)); g.Fill(br, 0xFF000000, 0.7); g.Frame(br, _s.Alert >= 3 ? 0xFFFF3030 : g.P.Warning, 1.5, 0.5 + 0.5 * g.Pulse(3));
            g.TextIn(br, _s.Condition == Cond.Arrest ? "!! CARDIAC ARREST ALARM (SIMULATED) !!" : _s.Spo2 < 90 ? "!! LOW SpO2 ALARM !!" : _s.Hr > 130 ? "!! HIGH HEART RATE !!" : "ALERT LEVEL " + _s.Alert, 9, _s.Alert >= 3 ? 0xFFFF5050 : g.P.Warning, Align.Center, true);
        }
    }

    // ---------------------------------------------------------------- B: life support
    void DrawLifeSupport(Gfx g, Rect r)
    {
        double gr = Math.Min(r.Height * 0.4, r.Width / 6.4);
        var gauges = new (string, double, double, string, string, double)[] { ("O2", _s.O2, 26, "%", "", 0), ("CO2", _s.Co2, 5000, "PPM", "", 0.5), ("PRESS", _s.Pressure, 120, "KPA", "", 0.9) };
        for (int i = 0; i < 3; i++)
        {
            var (l, v, mx, u, _, wf) = gauges[i]; double cx = r.X + r.Width * (i + 0.5) / 3;
            bool bad = i == 0 && _s.O2 < 19.5 || i == 1 && _s.Co2 > 2500 || i == 2 && _s.Pressure < 90;
            g.Gauge(cx, r.Y + gr + g.U(2), gr - g.U(2), v, mx, l, u, bad ? g.P.Warning : g.P.Primary, 2);
        }
        double y = r.Y + gr * 2 + g.U(8);
        var kv = new (string, string, uint?)[]
        {
            ("HUMIDITY", $"{_s.Humidity:0}%", null), ("CABIN TEMP", $"{_s.CabinTemp:0.0}°C", null), ("AIR CIRCULATION", $"{_s.Circulation:0} M3/MIN", _s.Circulation < 10 ? g.P.Warning : null), ("FILTER CONDITION", $"{_s.Filter * 100:0}%", _s.Filter < 0.5 ? g.P.Warning : null),
            ("O2 RESERVE", $"{_s.O2Reserve * 100:0}%", null), ("EMERGENCY RESERVE", $"{_s.EmergReserve * 100:0}%", _s.EmergReserve < 0.3 ? g.P.Warning : null), ("O2 TARGET", $"{_s.SetO2:0.0}%", null), ("TEMP SETPOINT", $"{_s.SetTemp:0.0}°C", null),
        };
        double used = g.KvGrid(new Rect(r.X, y, r.Width, r.Bottom - y), kv, r.Width > g.U(300) ? 2 : 1, 8.5);
        double by = y + used + g.U(4), bh = Math.Min(g.U(g.Touch ? 28 : 21), r.Bottom - by);
        if (bh > g.U(12))
        {
            string[] ids = { "md:c:temp-", "md:c:temp+", "md:c:o2-", "md:c:o2+", "md:c:fan", "md:c:leak" }; string[] lab = { "TEMP -", "TEMP +", "O2 -", "O2 +", "FANS +", _s.LeakActive ? "SEAL LEAK" : "SIM LEAK" };
            double bw = (r.Width - g.U(3) * 5) / 6;
            for (int i = 0; i < 6; i++) g.Button(ids[i], new Rect(r.X + i * (bw + g.U(3)), by, bw, bh), lab[i], i == 5 && _s.LeakActive, true, i == 5 ? g.P.Warning : null, bw < g.U(40) ? 6 : 7.5);
        }
    }

    // ---------------------------------------------------------------- C: scanner
    void DrawScanner(Gfx g, Rect r)
    {
        var bodyR = new Rect(r.X, r.Y, r.Width * 0.46, r.Height);
        g.Body(bodyR, _s.Region, _s.Layer, _s.Scanning ? _s.ScanProgress : 0, _s.Condition == Cond.Arrest ? 0 : Math.Sin(_s.Time * (_s.Hr / 60) * 6.28) * 0.5 + 0.5, g.P.Primary);
        double x = bodyR.Right + g.U(8), w = r.Right - x, bh = Math.Min(g.U(g.Touch ? 26 : 20), r.Height / 14);
        g.Text("BODY REGION", x, r.Y, 7.5, g.P.Text, Align.Left, true, 0.7);
        for (int i = 0; i < 5; i++) g.Button("md:r:" + i, new Rect(x, r.Y + g.U(11) + i * (bh + g.U(2)), w, bh), MedicalSim.RegionName(i), _s.Region == i, true, null, 7.5);
        double ly = r.Y + g.U(11) + 5 * (bh + g.U(2)) + g.U(4);
        g.Text("ANATOMICAL LAYER", x, ly, 7.5, g.P.Text, Align.Left, true, 0.7);
        for (int i = 0; i < 5; i++) g.Button("md:l:" + i, new Rect(x, ly + g.U(11) + i * (bh + g.U(2)), w, bh), MedicalSim.LayerName(i), _s.Layer == i, true, g.P.Secondary, 7.5);
        double sy = ly + g.U(11) + 5 * (bh + g.U(2)) + g.U(4);
        g.Text($"SCANNER: {(_s.Scanning ? "SCANNING" : "READY")}", x, sy, 8, _s.Scanning ? g.P.Accent : 0xFF30FF70, Align.Left, true);
        if (sy + g.U(34) < r.Bottom) g.Bar(new Rect(x, sy + g.U(11), w, g.U(14)), _s.ScanProgress, g.P.Accent, null, null);
        if (sy + g.U(48) < r.Bottom && _s.ScanFinding.Length > 0) HelpWrap(g, _s.ScanFinding, x, sy + g.U(30), w, 7.5);
    }

    static void HelpWrap(Gfx g, string t, double x, double y, double w, double fs) => Shell.Overlays.HeatWarningOverlay.Wrap(g, t, x, y, w, fs, g.P.Accent);

    // ---------------------------------------------------------------- D, E, F
    void DrawEquipment(Gfx g, Rect r)
    {
        var rows = _s.Equipment.Select(e => new[] { e.Name, e.State, $"{e.Health * 100:0}%", $"{e.Power:0} W", $"{e.MaintDays} D" }).ToList();
        g.Table(r, new[] { "EQUIPMENT", "STATE", "HEALTH", "POWER", "MAINT" }, new[] { 2.2, 0.9, 0.8, 0.8, 0.7 }, rows, _eqFirst, -1, "eq:", (ri, ci) => ci == 1 ? (rows[ri][1] == "READY" ? 0xFF30FF70 : g.P.Warning) : null, 8);
    }

    void DrawTimeline(Gfx g, Rect r)
    {
        var gh = new Rect(r.X, r.Y, r.Width, r.Height * 0.38);
        var c = Layout.Cols(gh, g.U(3), 1, 1);
        g.Graph(c[0], _s.HrH, 0xFF40FF80, null, null, true, "HR", ""); g.Graph(c[1], _s.SpoH, g.P.Primary, 80, 100, true, "SpO2", "%");
        var ev = new Rect(r.X, gh.Bottom + g.U(4), r.Width, r.Bottom - gh.Bottom - g.U(4));
        double lh = g.U(11.5); int max = Math.Max(1, (int)(ev.Height / lh)); int start = Math.Max(0, _s.Timeline.Count - max);
        for (int i = start; i < _s.Timeline.Count; i++)
        {
            var e = _s.Timeline[i]; uint col = e.Kind switch { 2 => 0xFFFF5050, 1 => g.P.Warning, 3 => g.P.Accent, _ => g.P.Text };
            g.Text($"T+{e.T:000}  {e.Text}", ev.X, ev.Y + (i - start) * lh, 7.5, col, Align.Left, e.Kind >= 2, 0.95);
        }
    }

    void DrawEmergency(Gfx g, Rect r)
    {
        // alert level bar
        double sw = (r.Width - g.U(4) * 4) / 5, sh = g.U(22);
        uint[] lc = { 0xFF30FF70, 0xFF9AE040, g.P.Warning, 0xFFFF7030, 0xFFFF3030 }; string[] ln = { "NORMAL", "ADVISORY", "CAUTION", "WARNING", "CRITICAL" };
        for (int i = 0; i < 5; i++) { var rc = new Rect(r.X + i * (sw + g.U(4)), r.Y, sw, sh); bool on = i == _s.Alert; g.Fill(rc, lc[i], on ? 0.55 + 0.3 * (i >= 3 ? g.Pulse(2.5) : 0) : 0.08); g.Frame(rc, lc[i], on ? 1.8 : 1, on ? 1 : 0.4); g.TextIn(rc, ln[i], sw < g.U(52) ? 6 : 7.5, on ? 0xFFFFFFFF : lc[i], Align.Center, true); }
        double y = r.Y + sh + g.U(6);
        string cd = _s.Countdown > 0 ? $"{(int)_s.Countdown / 60:00}:{_s.Countdown % 60:00.0}" : "--:--.-";
        g.Text("RESPONSE COUNTDOWN", r.X, y, 7.5, g.P.Text, Align.Left, false, 0.7); g.Text(cd, r.X, y + g.U(10), 24, _s.Countdown > 0 ? 0xFFFF5050 : g.P.Primary, Align.Left, true);
        int ok = _s.Diag.Count(d => d == "NOMINAL"); double avail = ok / 8.0;
        double bx = r.X + r.Width * 0.5, bw = r.Right - bx;
        g.HMeter(new Rect(bx, y, bw, g.U(24)), avail * 100, 0, 100, "LIFE SUPPORT AVAILABILITY", $"{avail * 100:0}%", null, 70);
        g.HMeter(new Rect(bx, y + g.U(28), bw, g.U(24)), _s.EmergReserve * 100, 0, 100, "EMERGENCY O2 RESERVE", $"{_s.EmergReserve * 100:0}%", g.P.Secondary, 30);
        double ky = y + g.U(44) + g.U(14);
        var kv = new (string, string, uint?)[] { ("MEDICAL BAY", _s.Alert >= 3 ? "EMERGENCY MODE" : "STANDBY", _s.Alert >= 3 ? g.P.Warning : 0xFF30FF70), ("RESPONSE TEAM", _s.Alert >= 3 ? "DEPLOYED" : "ON CALL", null), ("EMERGENCY O2", _s.EmergO2 ? "ACTIVE" : "ARMED", _s.EmergO2 ? g.P.Warning : null), ("DEFIB (SIM)", _s.Condition == Cond.Arrest ? "READY" : "STANDBY", null) };
        g.KvGrid(new Rect(r.X, ky, r.Width, r.Bottom - ky), kv, 2, 8);
    }

    // ---------------------------------------------------------------- bottom
    void DrawTrends(Gfx g, Rect r)
    {
        var rows = Layout.Rows(r, g.U(3), 1, 1); var a = Layout.Cols(rows[0], g.U(3), 1, 1); var b = Layout.Cols(rows[1], g.U(3), 1, 1);
        g.Graph(a[0], _s.HrH, 0xFF40FF80, null, null, true, "HEART RATE", " bpm"); g.Graph(a[1], _s.TempH, 0xFFFF9070, null, null, true, "BODY TEMP", " °C");
        g.Graph(b[0], _s.CoH, g.P.Warning, null, null, true, "CABIN CO2", " ppm"); g.Graph(b[1], _s.O2H, g.P.Primary, null, null, true, "CABIN O2", " %");
    }

    void DrawCrew(Gfx g, Rect r)
    {
        var rows = _s.Crew.Select(c => new[] { c.Name, c.Location, $"{c.Hr:0}", c.State }).ToList();
        g.Table(r, new[] { "CREW", "LOCATION", "HR", "STATE" }, new[] { 2, 1.3, 0.6, 1 }, rows, _crewFirst, _s.Selected, "cr:", (ri, ci) => ci == 3 ? (rows[ri][3] == "OK" ? 0xFF30FF70 : rows[ri][3] == "MONITOR" ? g.P.Warning : 0xFFFF4040) : null, 8);
    }

    void DrawDiag(Gfx g, Rect r)
    {
        double lh = Math.Min(g.U(14), r.Height / (MedicalSim.DiagNames.Length + (_s.DiagProg < 1 ? 1 : 0)));
        for (int i = 0; i < MedicalSim.DiagNames.Length; i++)
        {
            string st = _s.DiagProg < 1 && _s.DiagProg * 8 < i ? "PENDING" : _s.Diag[i];
            g.Led(r.X + g.U(5), r.Y + i * lh + lh / 2, st == "NOMINAL" ? 1 : st == "PENDING" ? 0 : st == "DEGRADED" ? 2 : 3);
            g.Text(MedicalSim.DiagNames[i], r.X + g.U(14), r.Y + i * lh + (lh - g.U(9)) / 2, 7.5, g.P.Text, Align.Left, false, 0.9);
            g.Text(st, r.Right - g.U(2), r.Y + i * lh + (lh - g.U(9)) / 2, 7.5, st == "NOMINAL" ? 0xFF30FF70 : g.P.Warning, Align.Right, true);
        }
        if (_s.DiagProg < 1) g.Bar(new Rect(r.X, r.Bottom - lh, r.Width, lh - g.U(2)), _s.DiagProg, g.P.Accent, null, null);
    }

    void DrawMeds(Gfx g, Rect r)
    {
        double lh = Math.Min(g.U(22), r.Height / _s.Meds.Length);
        for (int i = 0; i < _s.Meds.Length; i++) g.Bar(new Rect(r.X, r.Y + i * lh, r.Width, lh - g.U(2)), _s.MedCount[i] / 16.0, _s.MedCount[i] < 4 ? g.P.Warning : g.P.Secondary, _s.Meds[i], $"x{_s.MedCount[i]}");
    }

    protected override void AfterDraw(Gfx g, Rect area)
    {
        if (!_report) return;
        g.Fill(area, 0xFF000000, 0.74); g.AddHit("md:report", area);
        double w = Math.Min(area.Width - g.U(10), g.U(560)), h = Math.Min(area.Height - g.U(10), g.U(300));
        var rr = new Rect(area.Center.X - w / 2, area.Center.Y - h / 2, w, h); var inner = g.Panel(rr, "FICTIONAL MEDICAL REPORT — NOT MEDICAL ADVICE");
        var lines = _s.Report(); double lh = g.U(14); for (int i = 0; i < lines.Length; i++) g.Text(lines[i], inner.X, inner.Y + i * lh, 9, i == 0 || i == lines.Length - 1 ? g.P.Warning : g.P.Text, Align.Left, i == 0);
        g.Text("TAP TO CLOSE", inner.Right, inner.Bottom - g.U(11), 8, g.P.Text, Align.Right, false, 0.6);
    }

    // ---------------------------------------------------------------- input
    protected override string? PanelButton(string id)
    {
        if (id.StartsWith("md:p:")) { C.Audio.Play("deepbeep"); return _s.SelectPatient(int.Parse(id[5..])); }
        if (id.StartsWith("md:r:")) { _s.Region = int.Parse(id[5..]); C.Audio.Play("tick"); return "REGION: " + MedicalSim.RegionName(_s.Region); }
        if (id.StartsWith("md:l:")) { _s.Layer = int.Parse(id[5..]); C.Audio.Play("tick"); return "LAYER: " + MedicalSim.LayerName(_s.Layer); }
        if (id.StartsWith("md:c:"))
        {
            C.Audio.Play(id == "md:c:leak" ? "alarm" : "mech", id == "md:c:leak" ? SoundCategory.Alarm : SoundCategory.Ui, 0.5f);
            return id[5..] switch { "temp-" => _s.CabinChange("temp", -0.5), "temp+" => _s.CabinChange("temp", 0.5), "o2-" => _s.CabinChange("o2", -0.3), "o2+" => _s.CabinChange("o2", 0.3), "fan" => _s.CabinChange("fan", 3), _ => _s.CabinChange("leak", 0) };
        }
        return null;
    }

    protected override void PanelTap(Point p, string? id)
    {
        if (id == "md:report") { _report = false; return; }
        if (id != null && id.StartsWith("body:")) { _s.Region = int.Parse(id[5..]); C.Audio.Play("tick"); C.Toast("REGION: " + MedicalSim.RegionName(_s.Region)); }
        else if (id != null && id.StartsWith("cr:")) { int i = int.Parse(id[3..]); if (i < _s.Patients.Count) { C.Toast(_s.SelectPatient(i)); C.Audio.Play("deepbeep"); } }
    }
}
