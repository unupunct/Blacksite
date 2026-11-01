namespace Blacksite.Core.Simulation.Domain;

public enum Cond { Stable, Hypoxia, Tachycardia, AFib, Arrest, Recovery }

public sealed class Patient
{
    public string Name = "", Role = ""; public int Age; public double BaseHr, BaseSpo2, BaseRr, BaseTemp, BaseSys, BaseDia;
}

public sealed class MedEquip { public string Name = ""; public double Health = 1, Power; public bool Online = true; public int MaintDays; public string State = "READY"; }
public sealed class CrewStatus { public string Name = ""; public double Hr; public string State = "OK"; public string Location = ""; }
public sealed class MedEvent { public double T; public string Text = ""; public int Kind; }   // 0 info 1 warn 2 critical 3 intervention

/// <summary>
/// Fictional spacecraft medical-bay model (not medical advice or real monitoring). Cabin air drives the patient:
/// low O2 lowers SpO2, high CO2 raises respiration, low SpO2 raises heart rate. ECG/pleth/resp waveforms are synthesised from the state.
/// </summary>
public sealed class MedicalSim : DomainSim
{
    public readonly List<Patient> Patients = new();
    public int Selected;
    public Cond Condition;
    public double Hr, Spo2, Rr, Temp, Sys, Dia, EtCo2 = 38;
    // cabin
    public double O2 = 20.9, Co2 = 650, Pressure = 101.3, Humidity = 44, CabinTemp = 22, Circulation = 18, Filter = 0.86, O2Reserve = 0.82, EmergReserve = 1.0, SetTemp = 22, SetO2 = 20.9;
    public bool EmergO2, LeakActive;
    public readonly RingBuffer Ecg = new(700), Pleth = new(700), Resp = new(700), HrH = new(240), SpoH = new(240), CoH = new(240), O2H = new(240), TempH = new(240);
    public readonly List<MedEquip> Equipment = new();
    public readonly List<CrewStatus> Crew = new();
    public readonly List<MedEvent> Timeline = new();
    public readonly string[] Meds = { "EPINEPHRINE (SIM)", "ANALGESIC-A (SIM)", "SALINE 500ML (SIM)", "ANTIBIOTIC-K (SIM)", "SEDATIVE-Z (SIM)", "O2 MASK KIT" };
    public readonly int[] MedCount = new int[6];
    public int Alert;                 // 0 normal .. 4 critical
    public double Countdown;          // emergency response countdown seconds (<=0 means none)
    public bool Scanning; public double ScanProgress; public int Region = 1, Layer;
    public readonly List<string> ScanHistory = new();
    public string ScanFinding = "";
    public bool BeatFlag; public long Beats;
    double _ph, _rrPh, _arrT, _next = 0.8, _recT; double _t100;
    public double HrvMs = 50;
    public readonly string[] Diag = new string[8]; public double DiagProg = 1;
    public static readonly string[] DiagNames = { "OXYGEN GENERATOR", "CO2 SCRUBBER", "PRESSURE HULL", "CIRCULATION FANS", "THERMAL LOOP", "FILTER BANK", "O2 RESERVE", "MONITOR LINK" };
    static readonly string[] RegionNames = { "HEAD", "THORAX", "ABDOMEN", "PELVIS / LIMBS", "WHOLE BODY" };
    static readonly string[] LayerNames = { "SKELETAL", "MUSCULAR", "VASCULAR", "NERVOUS", "ORGANS" };
    public static string RegionName(int i) => RegionNames[i % 5]; public static string LayerName(int i) => LayerNames[i % 5];

    public MedicalSim() { Reset(0); }

    protected override void OnReset()
    {
        Patients.Clear(); Equipment.Clear(); Crew.Clear(); Timeline.Clear(); ScanHistory.Clear();
        (string n, string r, int a, double hr, double sp, double rr, double t, double s, double d)[] p =
        {
            ("CDR A. VOSS", "COMMANDER", 44, 62, 98.5, 14, 36.7, 118, 76), ("DR M. OKAFOR", "FLIGHT SURGEON", 39, 68, 98, 15, 36.6, 112, 72),
            ("ENG L. HAAS", "CHIEF ENGINEER", 51, 72, 97, 16, 36.8, 126, 82), ("SPC R. TANAKA-LEE", "MISSION SPECIALIST", 33, 58, 99, 13, 36.5, 108, 68),
        };
        foreach (var x in p) Patients.Add(new Patient { Name = x.n, Role = x.r, Age = x.a, BaseHr = x.hr, BaseSpo2 = x.sp, BaseRr = x.rr, BaseTemp = x.t, BaseSys = x.s, BaseDia = x.d });
        (string n, int m)[] eq = { ("BODY SCANNER MK-IV", 90), ("DEFIBRILLATOR (SIM)", 30), ("VENTILATOR", 60), ("MEDICATION CABINET", 120), ("VITALS MONITOR", 45), ("SURGICAL ROBOT ARM", 75), ("INFUSION PUMP", 40), ("UV STERILISER", 55) };
        foreach (var e in eq) Equipment.Add(new MedEquip { Name = e.n, Health = 0.88 + Rng.NextDouble() * 0.12, MaintDays = e.m, Power = 40 + Rng.Next(300) });
        string[] cn = { "CDR A. VOSS", "DR M. OKAFOR", "ENG L. HAAS", "SPC R. TANAKA-LEE", "PLT J. MARCHETTI", "TECH S. BRANDT" }; string[] loc = { "BRIDGE", "MED BAY", "ENGINEERING", "LAB", "BRIDGE", "CARGO" };
        for (int i = 0; i < 6; i++) Crew.Add(new CrewStatus { Name = cn[i], Hr = 62 + Rng.Next(20), Location = loc[i] });
        for (int i = 0; i < 6; i++) MedCount[i] = 4 + Rng.Next(12);
        Condition = Cond.Stable; Selected = Math.Min(Selected, 3); O2 = 20.9; Co2 = 650; Pressure = 101.3; Humidity = 44; CabinTemp = 22; Filter = 0.86; O2Reserve = 0.82; EmergReserve = 1; EmergO2 = false; LeakActive = false; SetTemp = 22; SetO2 = 20.9;
        Alert = 0; Countdown = 0; Scanning = false; ScanProgress = 0; ScanFinding = ""; DiagProg = 1;
        for (int i = 0; i < Diag.Length; i++) Diag[i] = "NOMINAL";
        var pt = Patients[Selected]; Hr = pt.BaseHr; Spo2 = pt.BaseSpo2; Rr = pt.BaseRr; Temp = pt.BaseTemp; Sys = pt.BaseSys; Dia = pt.BaseDia;
        for (int i = 0; i < 1500; i++) OnStep(0.1);
        Timeline.Clear(); Ev("MEDICAL BAY ONLINE — PATIENT " + pt.Name, 0);
    }

    void Ev(string s, int kind) { Timeline.Add(new MedEvent { T = Time, Text = s, Kind = kind }); if (Timeline.Count > 120) Timeline.RemoveAt(0); Note(s, kind >= 2); }

    protected override void OnStep(double dt)
    {
        var p = Patients[Selected];
        // ---- cabin air
        double crewLoad = 6 * 0.0000045;                       // fractional O2 consumption per second of sim, scaled
        double scrub = (Diag[1] == "FAULT" ? 0.2 : 1.0) * Filter * Circulation / 18.0;
        Co2 += (4.64 * (Patients.Count > 0 ? 1.0 : 0) - scrub * 0.02 * (Co2 - 380)) * dt;
        Co2 = Clamp(Co2, 380, 9000);
        double gen = (Diag[0] == "FAULT" ? 0.1 : 1.0) * 0.00055;
        double toward = EmergO2 ? 24.5 : SetO2;
        O2 += ((toward - O2) * (gen * 60 + (EmergO2 ? 0.08 : 0)) - (EmergO2 ? 0 : 0.0005)) * dt;
        if (EmergO2) EmergReserve = Math.Max(0, EmergReserve - dt * 0.012); else if (O2 < SetO2 - 0.2) O2Reserve = Math.Max(0, O2Reserve - dt * 0.0004);
        if (LeakActive) { Pressure -= dt * 0.12; O2 -= dt * 0.01; if (Pressure < 80) { LeakActive = false; Ev("LEAK SEALED BY AUTOMATIC PATCH", 3); } } else Pressure = Approach(Pressure, 101.3, 0.03, dt);
        O2 = Clamp(O2, 12, 26); Pressure = Clamp(Pressure, 60, 110);
        CabinTemp = Approach(CabinTemp, SetTemp + (Diag[4] == "FAULT" ? 5 : 0) + Noise(0.05), 0.04, dt);
        Humidity = Approach(Humidity, 44 + (CabinTemp - 22) * -1.2, 0.02, dt) + Noise(0.02);
        Filter = Clamp(Filter - dt * 0.00003, 0.2, 1); Circulation = Approach(Circulation, Diag[3] == "FAULT" ? 6 : 18, 0.1, dt);
        // ---- physiology targets
        double hypox = Math.Max(0, 19.5 - O2) * 3.2 + Math.Max(0, 1 - Pressure / 101.3) * 40;
        double spoT = p.BaseSpo2 - hypox; double rrT = p.BaseRr + Math.Max(0, Co2 - 900) / 380.0 + hypox * 0.6; double hrT = p.BaseHr + Math.Max(0, 96 - spoT) * 2.4 + (Co2 - 650) / 500.0;
        double tempT = p.BaseTemp, sysT = p.BaseSys, diaT = p.BaseDia;
        switch (Condition)
        {
            case Cond.Hypoxia: spoT = Math.Min(spoT, 82); rrT += 8; hrT += 28; sysT += 10; break;
            case Cond.Tachycardia: hrT = 152; sysT -= 12; diaT -= 8; break;
            case Cond.AFib: hrT = 118 + Math.Sin(Time) * 18; sysT -= 6; break;
            case Cond.Arrest: hrT = 0; spoT = Math.Max(50, Spo2 - 6); rrT = 0; sysT = 0; diaT = 0; tempT = p.BaseTemp - 0.5; break;
            case Cond.Recovery: hrT = p.BaseHr + 30; spoT = Math.Min(p.BaseSpo2, Spo2 + 3); _recT += dt; if (_recT > 40) { Condition = Cond.Stable; _recT = 0; Ev("RECOVERY COMPLETE — PATIENT STABLE", 3); } break;
        }
        if (Condition != Cond.Stable && EmergO2 && Condition != Cond.Arrest) spoT = Math.Min(p.BaseSpo2, spoT + 12);
        double k = Condition == Cond.Arrest ? 0.9 : 0.35;
        Hr = Approach(Hr, hrT + Noise(0.4), k, dt); Spo2 = Clamp(Approach(Spo2, spoT + Noise(0.1), 0.25, dt), 40, 100); Rr = Clamp(Approach(Rr, rrT, 0.3, dt), 0, 40);
        Temp = Approach(Temp, tempT, 0.02, dt); Sys = Approach(Sys, sysT, 0.3, dt); Dia = Approach(Dia, diaT, 0.3, dt); EtCo2 = Approach(EtCo2, 36 + (Co2 - 650) / 300.0 - (Condition == Cond.Arrest ? 30 : 0), 0.3, dt);
        HrvMs = Approach(HrvMs, Condition == Cond.AFib ? 140 : Condition == Cond.Arrest ? 0 : 50 - (Hr - 70) * 0.2, 0.3, dt);
        // ---- alert level & response countdown (derived, not scripted)
        int level = Condition == Cond.Arrest ? 4 : (Spo2 < 88 || Hr > 140 || Hr < 40 && Hr > 0) ? 3 : (Spo2 < 93 || Hr > 110 || O2 < 19.3 || Co2 > 2500) ? 2 : (Spo2 < 96 || Co2 > 1500) ? 1 : 0;
        if (level != Alert) { if (level > Alert) Ev($"ALERT LEVEL {level}: SpO2 {Spo2:0} HR {Hr:0} O2 {O2:0.0}%", level >= 3 ? 2 : 1); else if (level == 0) Ev("ALERT CLEARED", 0); Alert = level; }
        if (Alert >= 3) { if (Countdown <= 0) Countdown = 60; Countdown = Math.Max(0.01, Countdown - dt); } else Countdown = 0;
        // ---- crew
        for (int i = 0; i < Crew.Count; i++)
        {
            var c = Crew[i]; bool pat = Patients.Count > i && i == Selected;
            c.Hr = pat ? Hr : Approach(c.Hr, 66 + (96 - Spo2) * 0.4 + Noise(2) + (i * 3 % 7), 0.2, dt);
            c.State = pat ? (Alert >= 3 ? "CRITICAL" : Alert >= 1 ? "MONITOR" : "OK") : (O2 < 19 || Co2 > 3000 ? "MONITOR" : "OK");
            if (pat) c.Location = "MED BAY";
        }
        // ---- scanner
        if (Scanning) { ScanProgress += dt / 7; if (ScanProgress >= 1) { Scanning = false; ScanProgress = 1; Finish(); } }
        // ---- diagnostics
        if (DiagProg < 1)
        {
            DiagProg = Math.Min(1, DiagProg + dt / 8);
            if (DiagProg >= 1)
            {
                int bad = 0; double[] vals = { gen > 0.0004 ? 1 : 0.3, scrub, Pressure / 101.3, Circulation / 18, 1 - Math.Abs(CabinTemp - SetTemp) / 6, Filter, O2Reserve, 1 };
                for (int i = 0; i < 8; i++) { Diag[i] = vals[i] > 0.7 ? "NOMINAL" : vals[i] > 0.4 ? "DEGRADED" : "FAULT"; if (Diag[i] != "NOMINAL") bad++; }
                Ev(bad == 0 ? "LIFE-SUPPORT DIAGNOSTICS: ALL NOMINAL" : $"LIFE-SUPPORT DIAGNOSTICS: {bad} ISSUE(S)", bad == 0 ? 0 : 1);
            }
        }
        foreach (var e in Equipment) { e.Health = Clamp(e.Health + Noise(0.0006), 0.5, 1); e.State = !e.Online ? "OFFLINE" : e.Health < 0.7 ? "SERVICE" : "READY"; }
        // ---- waveform synthesis at 100 Hz
        _t100 += dt * 100; int n = (int)_t100; _t100 -= n;
        BeatFlag = false;
        for (int i = 0; i < n; i++) Sample(0.01);
        if ((int)(Time * 10) != (int)((Time - dt) * 10)) { HrH.Push((float)Hr); SpoH.Push((float)Spo2); CoH.Push((float)Co2); O2H.Push((float)O2); TempH.Push((float)Temp); }
    }

    void Sample(double dt)
    {
        double hr = Math.Max(0, Hr);
        if (Condition == Cond.Arrest)
        {   // fine ventricular fibrillation fading to asystole
            _arrT += dt; double amp = Math.Max(0.05, 0.5 - _arrT * 0.02) * (Hr < 1 ? 1 : 0.3);
            Ecg.Push((float)(amp * (Math.Sin(_arrT * 31) * Math.Sin(_arrT * 7.3) + 0.4 * Math.Sin(_arrT * 53)) * 0.35 + Noise(0.01)));
            Pleth.Push((float)(0.02 + Noise(0.005))); Resp.Push((float)Noise(0.01)); return;
        }
        _arrT = 0;
        double bpm = hr < 20 ? 60 : hr; double period = 60.0 / bpm;
        if (Condition == Cond.AFib) period *= 0.65 + 0.7 * Math.Abs(Math.Sin(Time * 1.7 + Beats));
        _ph += dt / period;
        if (_ph >= 1) { _ph -= 1; Beats++; BeatFlag = true; }
        double ph = _ph, v = 0;
        double G(double c, double w, double a) => a * Math.Exp(-Math.Pow((ph - c) / w, 2));
        v += Condition == Cond.AFib ? Noise(0.04) : G(0.18, 0.035, 0.16);       // P wave (absent in AF)
        v += G(0.30, 0.012, -0.12) + G(0.325, 0.014, 1.0) + G(0.35, 0.013, -0.25);   // Q R S
        v += G(0.58, 0.06, 0.28) * (Condition == Cond.Tachycardia ? 0.6 : 1);      // T wave
        Ecg.Push((float)(v + Noise(0.01)));
        double pp = Math.Pow(Math.Max(0, Math.Sin(Math.PI * ((_ph + 0.9) % 1))), 2.2) * Clamp((Spo2 - 60) / 40, 0.1, 1);
        Pleth.Push((float)(pp * (Sys > 5 ? 1 : 0.1) + Noise(0.005)));
        _rrPh += dt * Math.Max(0, Rr) / 60.0; Resp.Push((float)Math.Sin(_rrPh * 2 * Math.PI) * (float)(0.7 + (Rr - 14) * 0.03));
    }

    void Finish()
    {
        string[] findings = { "NO STRUCTURAL ANOMALY DETECTED (SIMULATED)", "MINOR TISSUE VARIANCE — WITHIN SIMULATED NORMAL RANGE", "MILD VASCULAR CONGESTION PATTERN (SIMULATED)", "REFERENCE SCAN STORED FOR COMPARISON" };
        string f = Condition == Cond.Stable ? findings[Rng.Next(findings.Length)] : Condition == Cond.Hypoxia ? "REDUCED OXYGENATION INDICATOR IN SCAN REGION (SIMULATED)" : "CARDIAC ACTIVITY INDICATOR ABNORMAL (SIMULATED)";
        ScanFinding = $"{RegionName(Region)} / {LayerName(Layer)}: {f}";
        ScanHistory.Add($"{DateTime.Now:HH:mm:ss} {Patients[Selected].Name.Split(' ')[^1]} {ScanFinding}"); if (ScanHistory.Count > 20) ScanHistory.RemoveAt(0);
        Ev("SCAN COMPLETE: " + ScanFinding, 0);
    }

    // ---- operator actions
    public string SelectPatient(int i) { Selected = i % Patients.Count; Condition = Cond.Stable; Countdown = 0; var p = Patients[Selected]; Hr = p.BaseHr; Spo2 = p.BaseSpo2; Ev("PATIENT SELECTED: " + p.Name, 0); return "PATIENT: " + p.Name; }
    public string StartScan() { if (Scanning) return "SCAN ALREADY RUNNING"; Scanning = true; ScanProgress = 0; ScanFinding = ""; Ev($"SCAN STARTED: {RegionName(Region)} / {LayerName(Layer)}", 3); return "BODY SCAN STARTED"; }
    public string Diagnostics() { DiagProg = 0; Ev("LIFE-SUPPORT DIAGNOSTICS STARTED", 3); return "DIAGNOSTICS RUNNING"; }
    public string Emergency()
    {
        Condition = Scenario % 3 == 0 ? Cond.Hypoxia : Scenario % 3 == 1 ? Cond.AFib : Cond.Arrest; Scenario++;
        Ev("MEDICAL EMERGENCY (SIMULATED): " + Condition.ToString().ToUpper(), 2); return "EMERGENCY: " + Condition.ToString().ToUpper();
    }
    public string ToggleEmergO2() { EmergO2 = !EmergO2; Ev("EMERGENCY OXYGEN " + (EmergO2 ? "ACTIVATED" : "STOPPED"), 3); return "EMERGENCY OXYGEN " + (EmergO2 ? "ON" : "OFF"); }
    public string Recover()
    {
        if (Condition == Cond.Stable) return "NO RECOVERY NEEDED";
        if (Condition == Cond.Arrest) { Ev("DEFIBRILLATION (SIMULATED) DELIVERED — 200 J", 3); Hr = 40; Spo2 = Math.Max(Spo2, 70); }
        Condition = Cond.Recovery; _recT = 0; EmergO2 = true; Ev("RECOVERY SEQUENCE STARTED", 3); return "RECOVERY SEQUENCE STARTED";
    }
    public string CabinChange(string what, double delta)
    {
        switch (what) { case "temp": SetTemp = Clamp(SetTemp + delta, 16, 30); return $"CABIN SETPOINT {SetTemp:0.0} C"; case "o2": SetO2 = Clamp(SetO2 + delta, 17, 24); return $"O2 TARGET {SetO2:0.0}%"; case "leak": LeakActive = !LeakActive; Ev(LeakActive ? "HULL LEAK SIMULATED" : "LEAK SEALED", 2); return LeakActive ? "LEAK SIMULATED" : "LEAK SEALED"; case "fan": Circulation = Clamp(Circulation + delta, 4, 30); return $"CIRCULATION {Circulation:0} M3/MIN"; }
        return "";
    }
    public string[] Report()
    {
        var p = Patients[Selected];
        return new[] { $"MEDICAL REPORT (FICTIONAL) — {DateTime.Now:yyyy-MM-dd HH:mm}", $"PATIENT {p.Name} ({p.Role}, {p.Age})", $"CONDITION {Condition.ToString().ToUpper()}  ALERT LEVEL {Alert}", $"HR {Hr:0}  SpO2 {Spo2:0}%  RR {Rr:0}  TEMP {Temp:0.0}C  BP {Sys:0}/{Dia:0}",
            $"CABIN O2 {O2:0.0}%  CO2 {Co2:0} PPM  P {Pressure:0.0} KPA", $"EMERGENCY O2 {EmergReserve * 100:0}%  O2 RESERVE {O2Reserve * 100:0}%", $"LAST SCAN: {(ScanFinding.Length > 0 ? ScanFinding : "NONE")}", "NOT MEDICAL ADVICE. SIMULATED DATA." };
    }
}
