namespace Blacksite.Core.Simulation.Domain;

public sealed class DeepCraft
{
    public string Id = "", Mission = "";
    public double DistAu, SpeedKms, FreqMhz, RxDbm, Snr, Degradation, LastContact, WindowPeriod, WindowOffset, WindowLen, Acq;
    public double Az, El;           // direction from Earth (deg)
    public int Dish = -1; public bool Loss, InWindow; public string Status = "";
    public double DataRate;
    public double LightTimeS => DistAu * 499.0048;
}

public sealed class Dish
{
    public string Id = ""; public double Az, El, AzT, ElT, Err, GainDb = 74, Sync = 1; public bool Online = true, Manual; public int Craft = -1; public double Rx;
}

public sealed class Channel
{
    public string Name = ""; public double FreqMhz, BwKhz, SigDbm, NoiseDbm; public string Status = "SEARCH", Receiver = "", Dir = "RX", Enc = "AES-256"; public int Craft = -1; public bool Muted;
}

public sealed class Anomaly { public string Id = ""; public double FreqMhz, DriftHz, Conf; public string Class = ""; public double T; }

/// <summary>
/// Fictional deep-space comms model. Received power follows a real link budget (free-space path loss over distance and
/// frequency, antenna gain reduced by pointing error), light-time follows distance, the spectrum is built from the channels.
/// </summary>
public sealed class CommsSim : DomainSim
{
    public readonly List<DeepCraft> Craft = new();
    public readonly List<Dish> Dishes = new();
    public readonly List<Channel> Channels = new();
    public readonly List<Anomaly> Anomalies = new();
    public readonly List<string> Decode = new(), History = new(), Schedule = new();
    public const int Bins = 256;
    public readonly float[] Spectrum = new float[Bins];
    public readonly RingBuffer SnrH = new(160), NoiseH = new(160), RxH = new(160);
    public double CenterMhz = 8420, SpanMhz = 12;
    public int SelDish, SelCraft, SelChannel;
    public bool Scanning, Decoding, Linked = true;
    public double ScanPos, DecodeProg, NoiseFloorDbm = -160;
    // unknown transmission scenario
    public bool UnknownActive; public double UnkT; public int UnkPhase = -1;
    public double UnkFreq = 8431.2; public string UnkMessage = "";
    double _decT; int _decStep; double _drift;
    public static readonly string[] Stations = { "MOJAVE COMPLEX", "MADRID-X COMPLEX", "CANBERRA-X COMPLEX" };

    public CommsSim() { Reset(0); }

    protected override void OnReset()
    {
        Craft.Clear(); Dishes.Clear(); Channels.Clear(); Anomalies.Clear(); Decode.Clear(); History.Clear(); Schedule.Clear();
        UnknownActive = false; UnkPhase = -1; UnkT = 0; Scanning = Decoding = false; CenterMhz = 8420; SpanMhz = 12; DecodeProg = 0; UnkMessage = "";
        (string id, string m, double d, double v, double f)[] cr =
        {
            ("HELIOS FAR-1", "OUTER HELIOSPHERE", 158.2, 16.9, 8420), ("PILGRIM-2", "INTERSTELLAR PRECURSOR", 134.6, 15.4, 8415), ("ORPHEUS PROBE", "KUIPER SURVEY", 52.3, 14.1, 8425),
            ("CALLISTO LANDER", "JOVIAN MOONS", 5.4, 0.0, 8432), ("AMBER-3", "SOLAR OBSERVER", 0.98, 1.2, 2295), ("TETHYS ORBITER", "SATURN SYSTEM", 9.9, 0.0, 8428),
            ("NOVA RELAY", "LAGRANGE RELAY", 0.01, 0.3, 2290), ("KEPLER-X", "EXOPLANET TELESCOPE", 0.07, 0.1, 8436),
        };
        for (int i = 0; i < cr.Length; i++)
            Craft.Add(new DeepCraft { Id = cr[i].id, Mission = cr[i].m, DistAu = cr[i].d, SpeedKms = cr[i].v, FreqMhz = cr[i].f, Az = Rng.NextDouble() * 360, El = 15 + Rng.NextDouble() * 60,
                WindowPeriod = 180 + Rng.Next(240), WindowOffset = Rng.Next(300), WindowLen = 70 + Rng.Next(60), DataRate = 200 + Rng.Next(1800) });
        for (int i = 0; i < 8; i++) Dishes.Add(new Dish { Id = $"DSA-{i + 1:00}", Az = 120 + i * 20, El = 45, AzT = 120 + i * 20, ElT = 45, Craft = i < Craft.Count ? i : -1 });
        for (int i = 0; i < 8; i++) Craft[i].Dish = i;
        for (int i = 0; i < 8; i++) Channels.Add(new Channel { Name = $"CH-{i + 1:00}", FreqMhz = Craft[i].FreqMhz, BwKhz = i % 3 == 0 ? 2 : 0.5, Craft = i, Receiver = Dishes[i].Id, Dir = i % 4 == 3 ? "DUPLEX" : "RX", Enc = i % 2 == 0 ? "AES-256" : i % 3 == 0 ? "PQ-KEM" : "NONE" });
        for (int i = 0; i < 900; i++) OnStep(0.2);
        Anomalies.Add(new Anomaly { Id = "SIG-3318", FreqMhz = 8416.4, DriftHz = -0.12, Conf = 0.62, Class = "NATURAL (PULSAR-LIKE)", T = 0 });
        Anomalies.Add(new Anomaly { Id = "SIG-4471", FreqMhz = 8423.7, DriftHz = 0.34, Conf = 0.48, Class = "TERRESTRIAL RFI", T = 0 });
        Decode.Add("--- DECODER READY ---"); Decode.Add("SIGNAL PROCESSING PIPELINE ONLINE."); Decode.Add("MATCHED FILTER BANK LOADED (8 CHANNELS)."); Decode.Add("AWAITING OPERATOR COMMAND.");
        Note($"ARRAY ONLINE — 8 DISHES, {Craft.Count} SPACECRAFT IN CATALOGUE (FICTIONAL)");
        Scenario = Scenario;
    }

    static double Fspl(double dKm, double fMhz) => 20 * Math.Log10(Math.Max(1, dKm)) + 20 * Math.Log10(fMhz) + 32.44;

    protected override void OnStep(double dt)
    {
        _drift += dt;
        // dish slewing and pointing
        for (int i = 0; i < Dishes.Count; i++)
        {
            var d = Dishes[i];
            if (!d.Manual && d.Craft >= 0) { d.AzT = Craft[d.Craft].Az; d.ElT = Craft[d.Craft].El; }
            double daz = Wrap(d.AzT - d.Az), del = d.ElT - d.El;
            d.Az = (d.Az + Math.Clamp(daz, -3 * dt, 3 * dt) + 360) % 360; d.El += Math.Clamp(del, -2 * dt, 2 * dt);
            d.Err = Math.Sqrt(Wrap(Craft.Count > 0 && d.Craft >= 0 ? Craft[d.Craft].Az - d.Az : 0) * Math.Pow(Math.Cos(d.El * Math.PI / 180), 2) * Wrap(Craft.Count > 0 && d.Craft >= 0 ? Craft[d.Craft].Az - d.Az : 0) + (d.Craft >= 0 ? Math.Pow(Craft[d.Craft].El - d.El, 2) : 0));
            d.Sync = Approach(d.Sync, d.Online ? Math.Exp(-Math.Pow(d.Err / 1.5, 2)) : 0, 2, dt);
        }
        // craft motion and link budget
        for (int i = 0; i < Craft.Count; i++)
        {
            var c = Craft[i];
            c.DistAu += c.SpeedKms * dt * 60 / 149.6e6;                                   // time-compressed: 1 sim s = 1 min
            c.Az = (c.Az + dt * 0.015 * (1 + i * 0.1)) % 360; c.El = Clamp(c.El + Math.Sin(Time * 0.01 + i) * dt * 0.02, 5, 85);
            c.InWindow = (Time + c.WindowOffset) % c.WindowPeriod < c.WindowLen;
            var d = c.Dish >= 0 ? Dishes[c.Dish] : null;
            double gain = d == null ? 0 : d.GainDb - 10 * Math.Log10(1 / Math.Max(0.01, d.Sync));
            double rx = 13 + 48 + gain - Fspl(c.DistAu * 149.6e6, c.FreqMhz) + 30;      // dBm
            if (c.Loss || d == null || !d.Online) rx = -200;
            if (!c.InWindow) rx -= 15;
            c.RxDbm = Approach(c.RxDbm == 0 ? -170 : c.RxDbm, rx, 3, dt);
            double noise = -174 + 10 * Math.Log10(10.0) + 3;                               // 10 Hz carrier loop, 3 dB NF
            c.Snr = Math.Max(0, c.RxDbm - noise);
            c.Degradation = Clamp(1 - c.Snr / 25, 0, 1);
            c.Acq = c.Snr > 6 ? Math.Min(1, c.Acq + dt * 0.25) : Math.Max(0, c.Acq - dt * 0.5);
            string st = c.Loss ? "SIGNAL LOST" : !c.InWindow ? "NO WINDOW" : c.Acq >= 1 ? "LOCKED" : c.Acq > 0.05 ? "ACQUIRING" : "SEARCHING";
            if (st != c.Status && c.Status != "") { if (st == "LOCKED") Note($"LOCK: {c.Id} SNR {c.Snr:0.0} DB"); if (st == "SIGNAL LOST") Note($"LOSS OF SIGNAL: {c.Id}", true); }
            c.Status = st; if (st == "LOCKED") c.LastContact = Time;
        }
        // channels derive from their spacecraft
        foreach (var ch in Channels)
        {
            var c = ch.Craft >= 0 ? Craft[ch.Craft] : null;
            ch.NoiseDbm = -160 + Noise(0.4) + (Anomalies.Count > 3 ? 1 : 0);
            ch.SigDbm = c == null ? ch.NoiseDbm : c.RxDbm;
            ch.Status = ch.Muted ? "MUTED" : c == null ? "SEARCH" : c.Status == "LOCKED" ? "LOCKED" : c.Status == "SIGNAL LOST" ? "FAULT" : c.Status == "ACQUIRING" ? "ACQUIRE" : "NOISE";
            if (c != null) ch.Receiver = c.Dish >= 0 ? Dishes[c.Dish].Id : "--";
        }
        BuildSpectrum(dt);
        // scan sweep
        if (Scanning)
        {
            ScanPos += dt / 8;
            if (ScanPos >= 1) { ScanPos = 0; Scanning = false; Note($"SCAN COMPLETE — {Anomalies.Count} CANDIDATE(S) ON RECORD"); }
            else if (Rng.NextDouble() < dt * 0.25 && Anomalies.Count < 12)
            {
                var a = new Anomaly { Id = $"SIG-{Rng.Next(1000, 9999)}", FreqMhz = CenterMhz - SpanMhz / 2 + ScanPos * SpanMhz, DriftHz = Noise(0.8), Conf = 0.4 + Rng.NextDouble() * 0.5, T = Time,
                    Class = new[] { "NATURAL (PULSAR-LIKE)", "TERRESTRIAL RFI", "SPACECRAFT HARMONIC", "UNCLASSIFIED NARROWBAND" }[Rng.Next(4)] };
                Anomalies.Add(a); Note($"CANDIDATE {a.Id} @ {a.FreqMhz:0.000} MHZ — {a.Class}");
            }
        }
        // decode state machine
        if (Decoding)
        {
            _decT += dt; var c = Craft[SelCraft];
            if (c.Status != "LOCKED" && _decStep < 99) { if (_decT > 1.5) { Decode.Add("  CARRIER LOST — DECODER STALLED"); Decoding = false; Note("DECODE ABORTED: NO LOCK", true); } }
            else if (_decT >= 0.9)
            {
                _decT = 0; _decStep++; DecodeProg = Math.Min(1, _decStep / 10.0);
                string[] steps = { "INITIALIZING SIGNAL ANALYSIS...", "CARRIER FREQUENCY DETECTED.", $"SIGNAL STRENGTH {c.RxDbm:0.0} DBM — ABOVE THRESHOLD.", "NOISE FILTER ACTIVE.", "SYMBOL TIMING RECOVERED.", "PATTERN RECOGNITION RUNNING.", "REPEATING SEQUENCE IDENTIFIED.", "FRAME SYNC ACQUIRED.",
                    $"TELEMETRY FRAME {Rng.Next(4000, 9000)}: BUS {27 + Noise(1):0.0}V  TEMP {18 + Noise(3):0.0}C  ATT OK", "DECODING SIMULATION COMPLETE." };
                if (_decStep <= steps.Length) Decode.Add(steps[_decStep - 1]);
                if (_decStep > steps.Length) { Decoding = false; Note($"DECODE COMPLETE: {c.Id}"); }
                while (Decode.Count > 60) Decode.RemoveAt(0);
            }
        }
        RunUnknown(dt);
        if ((int)(Time * 5) != (int)((Time - dt) * 5))
        {
            var c = Craft[SelCraft]; SnrH.Push((float)c.Snr); RxH.Push((float)c.RxDbm); NoiseH.Push((float)(NoiseFloorDbm + Noise(0.5)));
        }
    }

    static double Wrap(double a) { a %= 360; if (a > 180) a -= 360; if (a < -180) a += 360; return a; }

    void BuildSpectrum(double dt)
    {
        double lo = CenterMhz - SpanMhz / 2, step = SpanMhz / Bins;
        for (int i = 0; i < Bins; i++)
        {
            double f = lo + i * step;
            double v = 0.09 + 0.045 * Rng.NextDouble() + 0.02 * Math.Sin(i * 0.05 + Time * 0.2) + 0.1 * Math.Exp(-Math.Pow((i - Bins * 0.05) / 8.0, 2));
            foreach (var ch in Channels)
            {
                if (ch.Muted) continue;
                double w = Math.Max(step * 1.4, ch.BwKhz / 1000.0), amp = Clamp((ch.SigDbm + 175) / 40.0, 0, 1);
                v += amp * 0.9 * Math.Exp(-Math.Pow((f - ch.FreqMhz) / w, 2));
            }
            foreach (var a in Anomalies) v += a.Conf * 0.35 * Math.Exp(-Math.Pow((f - (a.FreqMhz + a.DriftHz * (Time - a.T) * 1e-6 * 50)) / (step * 1.6), 2));
            if (UnknownActive && UnkPhase >= 2)
            {
                double uf = UnkFreq + Math.Sin(UnkT * 0.4) * 0.03, amp = UnkPhase >= 6 ? 0.95 : 0.45 + 0.1 * UnkPhase;
                double pulse = UnkPhase >= 7 ? 0.5 + 0.5 * Math.Sin(UnkT * 7.0) : 1;
                v += amp * pulse * Math.Exp(-Math.Pow((f - uf) / (step * 1.3), 2));
                if (UnkPhase >= 6) v += 0.25 * amp * Math.Exp(-Math.Pow((f - uf - 0.9) / (step * 1.3), 2)) + 0.2 * amp * Math.Exp(-Math.Pow((f - uf + 0.9) / (step * 1.3), 2));
            }
            if (Scanning && Math.Abs(i - ScanPos * Bins) < 2) v += 0.2;
            Spectrum[i] = (float)Approach(Spectrum[i], v, 12, dt);
        }
        NoiseFloorDbm = -160 + Noise(0.3);
    }

    void RunUnknown(double dt)
    {
        if (!UnknownActive) return;
        UnkT += dt;
        double[] t = { 0, 2, 4, 6, 8, 11, 15, 19, 24 };
        int ph = 0; for (int i = 0; i < t.Length; i++) if (UnkT >= t[i]) ph = i;
        if (ph != UnkPhase)
        {
            UnkPhase = ph;
            string[] msg = { "BACKGROUND AMBIENCE FADING", "RADAR ACTIVITY INCREASING", "SIGNAL PEAK APPEARED", "WATERFALL PATTERN CHANGE", "DETECTION WARNING — UNKNOWN TRANSMISSION", "SIGNAL ANALYSIS BEGINS", "MYSTERIOUS REPEATING PATTERN DETECTED", "AUDIO TRANSMISSION PLAYING", "FINAL MESSAGE RECEIVED" };
            Note(msg[ph], ph == 4 || ph == 6);
            if (ph == 5) { Decode.Add("INITIALIZING UNKNOWN SIGNAL ANALYSIS..."); Decode.Add($"CARRIER AT {UnkFreq:0.000} MHZ — NOT IN CATALOGUE."); }
            if (ph == 6) { Decode.Add("REPEATING SEQUENCE: 01 01 02 03 05 08 0D 15 22 37 (PERIOD 4.2 S)"); Anomalies.Add(new Anomaly { Id = "UNK-0001", FreqMhz = UnkFreq, DriftHz = 0.3, Conf = 0.93, Class = "UNIDENTIFIED REPEATING NARROWBAND", T = Time }); }
            if (ph == 7) Decode.Add("MODULATION: PHASE-SHIFT KEYING, 8 BAUD. DECODING...");
            if (ph == 8) { UnkMessage = "WE ARE HERE. WE HAVE BEEN LISTENING. (FICTIONAL TRANSMISSION)"; Decode.Add("DECODED: " + UnkMessage); }
        }
        if (UnkT > 30) { UnknownActive = false; UnkPhase = -1; UnkT = 0; }
    }

    // ---- operator actions
    public string Tune(double deltaMhz, bool log = true) { CenterMhz = Math.Clamp(CenterMhz + deltaMhz, 1000, 32500); Anomalies.RemoveAll(a => Math.Abs(a.FreqMhz - CenterMhz) > SpanMhz * 2); if (log) Note($"TUNED TO {CenterMhz:0.0} MHZ"); return $"CENTER {CenterMhz:0.0} MHZ"; }
    public string Span(double factor) { SpanMhz = Math.Clamp(SpanMhz * factor, 1, 200); return $"SPAN {SpanMhz:0.0} MHZ"; }
    public string Scan() { Scanning = !Scanning; ScanPos = 0; Note(Scanning ? "SCAN STARTED" : "SCAN STOPPED"); return Scanning ? "SIGNAL SCAN STARTED" : "SCAN STOPPED"; }
    public string StartDecode() { Decoding = true; _decStep = 0; _decT = 0; DecodeProg = 0; Decode.Add($"--- DECODE {Craft[SelCraft].Id} ---"); Note($"DECODE STARTED: {Craft[SelCraft].Id}"); return "DECODING SIMULATION STARTED"; }
    public string SelectChannel(int i) { SelChannel = i % Channels.Count; var ch = Channels[SelChannel]; CenterMhz = ch.FreqMhz; if (ch.Craft >= 0) SelCraft = ch.Craft; Note($"CHANNEL {ch.Name} SELECTED"); return $"{ch.Name} @ {ch.FreqMhz:0.0} MHZ"; }
    public string TriggerUnknown() { UnknownActive = true; UnkT = 0; UnkPhase = -1; UnkFreq = CenterMhz + 1.2; Note("UNKNOWN TRANSMISSION SCENARIO ARMED", true); return "UNKNOWN TRANSMISSION"; }
    public string Loss(int c) { Craft[c].Loss = true; Note($"SIMULATED SIGNAL LOSS: {Craft[c].Id}", true); return $"SIGNAL LOSS: {Craft[c].Id}"; }
    public string Recover(int c) { Craft[c].Loss = false; Dishes.ForEach(d => d.Online = true); Note($"RECOVERY: {Craft[c].Id}"); return $"RECOVERY: {Craft[c].Id}"; }
    public string Point(int dish, double dAz, double dEl) { var d = Dishes[dish]; d.Manual = true; d.AzT = (d.Az + dAz + 360) % 360; d.ElT = Clamp(d.El + dEl, 5, 88); Note($"{d.Id} MANUAL SLEW AZ {d.AzT:0.0} EL {d.ElT:0.0}"); return $"{d.Id} SLEWING"; }
    public string Track(int dish) { var d = Dishes[dish]; d.Manual = false; if (d.Craft < 0) d.Craft = SelCraft; Note($"{d.Id} TRACKING {Craft[d.Craft].Id}"); return $"{d.Id} TRACKING {Craft[d.Craft].Id}"; }
    public string Assign(int dish, int craft) { Dishes[dish].Craft = craft; Craft[craft].Dish = dish; Dishes[dish].Manual = false; Channels.Where(c => c.Craft == craft).ToList().ForEach(c => c.Receiver = Dishes[dish].Id); Note($"{Dishes[dish].Id} ASSIGNED TO {Craft[craft].Id}"); return $"{Dishes[dish].Id} -> {Craft[craft].Id}"; }
}
