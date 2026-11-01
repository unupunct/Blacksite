namespace Blacksite.Core.Simulation.Domain;

public sealed class ParticleTrack { public double Angle, Curvature, Length, Energy; public int Charge; public string Kind = ""; public int Layer; }

public sealed class DetectionEvent
{
    public string Id = ""; public double Time, TotalEnergy; public List<ParticleTrack> Tracks = new(); public string Class = ""; public bool Candidate;
    public double Age;
}

public sealed class QTask { public string Name = ""; public double Progress; public bool Done; public int Qubits; }

/// <summary>
/// Fictional quantum lab model. Field strength curves tracks (r ~ pT/B), temperature depends on cooling and load,
/// coherence depends on temperature and field noise, anomaly rate depends on stability.
/// </summary>
public sealed class QuantumSim : DomainSim
{
    public static readonly string[] Experiments = { "QC-VQE GROUND STATE", "PD-HADRON COLLISION", "QC-SURFACE CODE 17", "PD-MUON G-2 RING", "EM-SUPERCONDUCTOR TC", "QC-ENTANGLEMENT SWAP" };
    public int Experiment;
    public bool Running, Acquiring, Cooling = true, Shutdown, Quench;
    public double Progress, Field = 3.8, TempSet = 4.0, PressureLog = -7, EnergyTeV = 6.5, IntervalMs = 250, Sensitivity = 0.7, DurationS = 90;
    public int Qubits = 128;
    public double ProcTempMk = 14, Coherence = 180, Fidelity = 99.92, CoolEff = 0.9, Power = 340, Stability = 0.97, ErrorRate = 0.002, CoolantK = 4.0, Vacuum = 1e-7;
    public long Cycles; public int ErrCorrCycles;
    public double ContainmentField;
    public readonly List<QTask> Tasks = new();
    public readonly List<DetectionEvent> Events = new();
    public readonly List<string> Analysis = new();
    public readonly List<string> History = new();
    public readonly RingBuffer TempH = new(240), CohH = new(240), PowerH = new(240), CountH = new(240), StabH = new(240), FidH = new(240);
    public readonly double[] Spectrum = new double[64];       // energy histogram (GeV bins)
    public readonly double[] Freq = new double[96];           // frequency spectrum of the readout signal
    public long ParticleCount; public int EventCount; public double Variance, Mean;
    public bool Anomaly; double _acc, _anomT, _wMean, _wM2; long _n;
    public readonly bool[] Instruments = new bool[8];
    public static readonly string[] InstNames = { "PIXEL TRACKER", "CALORIMETER", "MUON CHAMBERS", "CRYOSTAT", "SQUID ARRAY", "MAGNET PSU", "DAQ CLUSTER", "VACUUM LINE" };
    public readonly double[] Health = new double[8];

    public QuantumSim() { Reset(0); }

    protected override void OnReset()
    {
        Running = Acquiring = Shutdown = Quench = Anomaly = false; Cooling = true; Progress = 0; Field = 3.8; TempSet = 4.0; EnergyTeV = 6.5; Cycles = 0; ParticleCount = 0; EventCount = 0; _n = 0; _wMean = _wM2 = 0;
        Events.Clear(); Analysis.Clear(); History.Clear(); Tasks.Clear(); Array.Clear(Spectrum); ProcTempMk = 14; Stability = 0.97; ContainmentField = Field;
        string[] tn = { "VQE ITERATION 14/60", "STATE TOMOGRAPHY", "RANDOMIZED BENCHMARKING", "SYNDROME DECODING", "CALIBRATION SWEEP", "ENTANGLEMENT WITNESS" };
        foreach (var n in tn) Tasks.Add(new QTask { Name = n, Qubits = 16 + Rng.Next(100), Progress = Rng.NextDouble() * 0.7 });
        for (int i = 0; i < Instruments.Length; i++) { Instruments[i] = true; Health[i] = 0.9 + Rng.NextDouble() * 0.1; }
        for (int i = 0; i < Spectrum.Length; i++) Spectrum[i] = 3 + 40 * Math.Exp(-i / 9.0) * (0.7 + 0.6 * Rng.NextDouble());   // baseline background run
        for (int i = 0; i < 240; i++) OnStep(0.1);
        Note($"FACILITY INITIALISED — EXPERIMENT {Experiments[Experiment]}");
    }

    static double Gauss(Random r) { double u = 1 - r.NextDouble(), v = r.NextDouble(); return Math.Sqrt(-2 * Math.Log(u)) * Math.Cos(2 * Math.PI * v); }

    protected override void OnStep(double dt)
    {
        // magnet & containment
        double fTarget = Shutdown ? 0 : Field;
        ContainmentField = Approach(ContainmentField, fTarget, Shutdown ? 0.6 : 1.2, dt);
        double load = (Running ? 0.5 : 0.1) + (Acquiring ? 0.25 : 0) + Math.Abs(ContainmentField) / 14 * 0.4;
        // cooling: temperature setpoint reachable only with cooling and healthy cryostat
        double coolCap = Cooling && !Shutdown ? Health[3] : 0.1;
        CoolEff = Approach(CoolEff, Clamp(coolCap * (1 - Math.Abs(CoolantK - TempSet) / 30.0), 0.05, 1), 0.5, dt);
        CoolantK = Approach(CoolantK, Cooling && !Shutdown ? TempSet + (1 - CoolEff) * 20 + load * 1.5 : 77, 0.25, dt);
        ProcTempMk = Approach(ProcTempMk, 9 + (CoolantK - 4) * 2.2 + load * 8 + Noise(0.15), 0.8, dt);
        Vacuum = Math.Pow(10, Approach(Math.Log10(Vacuum), PressureLog + Noise(0.01), 0.3, dt));
        // stability depends on field, temperature and vacuum; anomalies reduce it
        double stabT = 1 - Math.Max(0, ProcTempMk - 20) / 60 - Math.Max(0, ContainmentField - 10) / 12 - Math.Max(0, Math.Log10(Vacuum) + 4) / 6 - (Anomaly ? 0.35 : 0) - (Quench ? 0.5 : 0);
        Stability = Approach(Stability, Clamp(stabT, 0.05, 1), 0.8, dt);
        Coherence = Approach(Coherence, Clamp(240 - (ProcTempMk - 10) * 9 - (1 - Stability) * 160, 6, 260), 0.8, dt);
        Fidelity = Approach(Fidelity, Clamp(99.98 - (1 - Stability) * 6 - Math.Max(0, ProcTempMk - 16) * 0.05, 90, 99.99), 0.8, dt);
        ErrorRate = (100 - Fidelity) / 100 * 3 + Noise(0.0001);
        Power = Approach(Power, 180 + load * 520 + (Cooling ? 90 : 0) + (Quench ? 300 : 0), 1.0, dt);
        Cycles += (long)(dt * (Running ? 1.4e6 : 2e5) * Stability); ErrCorrCycles += (int)(dt * 800);
        if (Anomaly) { _anomT -= dt; if (_anomT <= 0) { Anomaly = false; Note("ANOMALY CLEARED — STABILITY RECOVERING"); } }
        Quench = Quench && ContainmentField > 1 ? Quench : false;
        // experiment progress & tasks
        if (Running)
        {
            Progress += dt / DurationS * Stability;
            if (Progress >= 1) { Progress = 1; Running = false; Acquiring = false; Note($"EXPERIMENT COMPLETE: {Experiments[Experiment]}  ({EventCount} EVENTS)"); Alert2("EXPERIMENT COMPLETE"); }
        }
        foreach (var t in Tasks) { if (!t.Done) { t.Progress += dt * 0.01 * (Running ? 2 : 1) * Stability; if (t.Progress >= 1) { t.Done = true; t.Progress = 1; Note($"TASK COMPLETE: {t.Name}"); } } else if (Rng.NextDouble() < dt * 0.02) { t.Done = false; t.Progress = 0; t.Name = t.Name; } }
        // detector events: rate ~ energy * sensitivity (Poisson); curvature ~ 1/(pT*B)
        _acc += dt;
        if (Acquiring && Running && !Shutdown)
        {
            double rate = (0.3 + EnergyTeV * 0.1) * (0.4 + Sensitivity) * 1000.0 / Math.Max(60, IntervalMs) * 0.9;
            int k = Poisson(rate * dt);
            for (int i = 0; i < k; i++) MakeEvent();
        }
        foreach (var e in Events) e.Age += dt;
        Events.RemoveAll(e => e.Age > 10);
        // readout frequency spectrum: 1/f noise + resonances (qubit lines shift with temperature)
        for (int i = 0; i < Freq.Length; i++)
        {
            double f = (i + 1.0) / Freq.Length, noise = 0.08 / Math.Sqrt(f) * (0.8 + 0.4 * Rng.NextDouble());
            double s = 0;
            foreach (var (c, w, a) in new[] { (0.22 + (ProcTempMk - 14) * 0.0008, 0.012, 0.9), (0.47, 0.01, 0.7 * Stability), (0.71 - (ProcTempMk - 14) * 0.0005, 0.014, 0.6) }) s += a * Math.Exp(-Math.Pow((f - c) / w, 2));
            Freq[i] = Approach(Freq[i], noise * (1 + (1 - Stability) * 3) + s * Coherence / 260, 6, dt);
        }
        for (int i = 0; i < Instruments.Length; i++) { Health[i] = Clamp(Health[i] + Noise(0.0008) + (Instruments[i] ? 0.0003 : -0.01), 0, 1); }
        if ((int)(Time * 10) != (int)((Time - dt) * 10))
        {
            TempH.Push((float)ProcTempMk); CohH.Push((float)Coherence); PowerH.Push((float)Power); CountH.Push((float)(ParticleCount % 100000)); StabH.Push((float)Stability); FidH.Push((float)Fidelity);
        }
    }

    void Alert2(string s) => Note(s, false);

    int Poisson(double lambda) { double l = Math.Exp(-lambda), p = 1; int k = 0; do { k++; p *= Rng.NextDouble(); } while (p > l && k < 60); return k - 1; }

    void MakeEvent()
    {
        var ev = new DetectionEvent { Id = $"EV-{EventCount + 1:000000}", Time = Time };
        int n = 3 + Rng.Next(7);
        double sum = 0;
        string[] kinds = { "e±", "μ±", "π±", "K±", "γ", "p" };
        for (int i = 0; i < n; i++)
        {
            double pt = 0.5 + Math.Abs(Gauss(Rng)) * 3.0 * (0.5 + EnergyTeV / 8);
            var t = new ParticleTrack { Angle = Rng.NextDouble() * Math.PI * 2, Charge = Rng.Next(3) == 0 ? 0 : (Rng.Next(2) == 0 ? 1 : -1), Energy = pt, Kind = kinds[Rng.Next(kinds.Length)] };
            t.Curvature = t.Charge == 0 ? 0 : t.Charge * Math.Max(0.15, ContainmentField) / (pt * 2.5);
            t.Layer = t.Kind == "μ±" ? 4 : t.Kind == "γ" || t.Kind == "e±" ? 3 : 2 + Rng.Next(2);
            t.Length = t.Layer switch { 4 => 1.0, 3 => 0.78, _ => 0.6 };
            sum += pt; ev.Tracks.Add(t);
        }
        ev.TotalEnergy = sum * 12;
        ev.Candidate = ev.TotalEnergy > 120 && ev.Tracks.Count(t => t.Kind == "γ") >= 2;
        ev.Class = ev.Candidate ? "RESONANCE CANDIDATE" : ev.Tracks.Count(t => t.Kind == "μ±") >= 2 ? "DIMUON" : ev.Tracks.Count >= 8 ? "MULTIJET" : "MINBIAS";
        Events.Add(ev); while (Events.Count > 7) Events.RemoveAt(0); EventCount++; ParticleCount += n;
        int bin = (int)Clamp(ev.TotalEnergy / 3, 0, Spectrum.Length - 1); Spectrum[bin] += 1;
        _n++; double d = ev.TotalEnergy - _wMean; _wMean += d / _n; _wM2 += d * (ev.TotalEnergy - _wMean); Mean = _wMean; Variance = _n > 1 ? _wM2 / (_n - 1) : 0;
        if (ev.Candidate) Note($"{ev.Id}: {ev.Class} E={ev.TotalEnergy:0} GeV");
    }

    // ---- operator actions
    public string StartExperiment() { if (Shutdown) return "SHUTDOWN ACTIVE — RESET FIRST"; Running = true; Progress = 0; Acquiring = true; Note($"EXPERIMENT STARTED: {Experiments[Experiment]}"); return "EXPERIMENT STARTED"; }
    public string Collide() { Shutdown = false; Running = true; Acquiring = true; for (int i = 0; i < 6; i++) MakeEvent(); Note("COLLISION SIMULATION TRIGGERED"); return "COLLISION EVENT GENERATED"; }
    public string Acquire() { Acquiring = !Acquiring; Note("DATA ACQUISITION " + (Acquiring ? "ON" : "OFF")); return "DATA ACQUISITION " + (Acquiring ? "ON" : "OFF"); }
    public string TriggerAnomaly() { Anomaly = true; _anomT = 12; Note("EXPERIMENTAL ANOMALY INJECTED — DECOHERENCE SPIKE", true); return "ANOMALY TRIGGERED"; }
    public string ToggleCooling() { Cooling = !Cooling; Note("CRYOGENIC COOLING " + (Cooling ? "ON" : "OFF")); return "COOLING " + (Cooling ? "ON" : "OFF"); }
    public string EmergencyShutdown() { Shutdown = true; Running = false; Acquiring = false; Quench = ContainmentField > 6; Note("EMERGENCY SHUTDOWN — FIELD RAMP-DOWN" + (Quench ? " (QUENCH)" : ""), true); return "EMERGENCY SHUTDOWN"; }

    public string RunAnalysis()
    {
        Analysis.Clear();
        double sigma = Math.Sqrt(Variance), sig = EventCount < 20 ? 0 : Math.Min(6.0, Spectrum.Skip(30).Take(8).Sum() / Math.Sqrt(Math.Max(1, Spectrum.Take(20).Sum())) * 1.8);
        Analysis.Add($"ANALYSIS {Experiments[Experiment]}  T={Time:0}S");
        Analysis.Add($"  EVENTS {EventCount}   PARTICLES {ParticleCount}");
        Analysis.Add($"  <E> = {Mean:0.0} GEV   SIGMA = {sigma:0.0} GEV   VAR = {Variance:0}");
        Analysis.Add($"  CHI2/NDF = {1 + (1 - Stability) * 4 + Noise(0.05):0.00}");
        Analysis.Add($"  EXCESS SIGNIFICANCE = {sig:0.0} SIGMA (SIMULATED)");
        Analysis.Add($"  COHERENCE {Coherence:0} US   FIDELITY {Fidelity:0.00}%   STABILITY {Stability * 100:0}%");
        Analysis.Add(Stability < 0.6 ? "  FLAG: LOW STABILITY — RESULTS UNRELIABLE" : "  QUALITY: ACCEPTABLE");
        Note("ANALYSIS RUN COMPLETE"); return "ANALYSIS COMPLETE";
    }

    public string NextExperiment() { Experiment = (Experiment + 1) % Experiments.Length; Progress = 0; Running = false; Note("EXPERIMENT SELECTED: " + Experiments[Experiment]); return Experiments[Experiment]; }
}
