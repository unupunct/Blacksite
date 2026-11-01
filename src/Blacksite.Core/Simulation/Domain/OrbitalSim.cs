namespace Blacksite.Core.Simulation.Domain;

public enum OrbType { Active, Research, Debris, Unknown, Station }

public sealed class OrbObject
{
    public string Id = "", Name = "";
    public OrbType Type;
    public double Alt, AltTarget;       // km
    public double Incl, Raan, Phase;    // radians
    public double Ecc;
    public double Lat, Lon;             // degrees (ground track)
    public int Priority = 3;            // 1 highest
    public bool Tracked, SignalLost, Acquired = true;
    public int Station = -1;            // assigned tracking station
    public double Confidence, Snr, LastUpdate, Elevation, Range;
    public string Status = "NOMINAL";
    public readonly List<(double lat, double lon)> Trail = new();
    public double Period => 2 * Math.PI * Math.Sqrt(Math.Pow(OrbitalSim.Re + Alt, 3) / OrbitalSim.Mu);   // seconds
    public double Velocity => Math.Sqrt(OrbitalSim.Mu / (OrbitalSim.Re + Alt));                         // km/s (circular)
}

public sealed class GroundStation
{
    public string Id = "", Name = ""; public double Lat, Lon; public bool Online = true;
    public double Bandwidth, Latency, Load; public int Links;
}

public sealed class OrbMission { public string Name = ""; public double Progress; public double Readiness; public string State = "ACTIVE"; }

/// <summary>
/// Fictional orbital-surveillance model. Period and velocity follow Kepler (so a manoeuvre changes them consistently),
/// ground tracks come from inclination/phase/Earth rotation, link quality follows slant range and elevation.
/// </summary>
public sealed class OrbitalSim : DomainSim
{
    public const double Re = 6371, Mu = 398600.4, TimeScale = 60, EarthRate = 7.292115e-5;
    public readonly List<OrbObject> Objects = new();
    public readonly List<GroundStation> Stations = new();
    public readonly List<OrbMission> Missions = new();
    public readonly RingBuffer TrackedCount = new(120), Bandwidth = new(120), Latency = new(120), Quality = new(120);
    public readonly double[] SensorHealth = new double[6];
    public static readonly string[] SensorNames = { "PHASED-ARRAY RADAR", "OPTICAL TELESCOPE", "LASER RANGING", "RF INTERCEPT", "SPACE-BASED IR", "WEATHER FEED" };
    public int Selected, Alerts;
    public double Readiness = 0.94;
    public string Conjunction = "";

    public OrbitalSim() { Reset(0); }

    static readonly (string n, double la, double lo)[] St =
    {
        ("KESTREL RIDGE", 34.6, -106.2), ("NORDHEIM", 69.4, 20.9), ("PAMPA BASE", -31.4, -64.2), ("OBSIDIAN ISLE", -21.2, 55.5),
        ("TASMAN POINT", -42.9, 147.3), ("SAHEL ARRAY", 15.5, 32.5), ("ALTAI SITE", 50.1, 87.0), ("CAPE HALCYON", 28.5, -80.6),
        ("SKARDALEN", 78.2, 15.6), ("ATOLL SEVEN", -7.3, 72.4), ("MAUNA RIDGE", 19.8, -155.5), ("TUNDRA NORTH", 64.8, -147.7),
    };

    protected override void OnReset()
    {
        Objects.Clear(); Stations.Clear(); Missions.Clear();
        for (int i = 0; i < St.Length; i++) Stations.Add(new GroundStation { Id = $"GS-{i + 1:00}", Name = St[i].n, Lat = St[i].la, Lon = St[i].lo, Bandwidth = 40 + Rng.Next(60) });
        string[] an = { "AEGIS", "HALCYON", "OBSIDIAN", "VANGUARD", "NOCTURNE", "SPECTRE", "ORACLE", "TALON", "WARDEN", "MERIDIAN", "KESTREL", "LUMEN", "SENTINEL", "ARGUS" };
        for (int i = 0; i < an.Length; i++) Add(OrbType.Active, $"{an[i]}-{i + 1}", 450 + Rng.Next(1300), 20 + Rng.Next(75), 1 + i % 3);
        string[] rn = { "SOLSTICE", "AURORA-R", "PRISM", "CAIRN", "ELYSIUM", "NIMBUS-X" };
        for (int i = 0; i < rn.Length; i++) Add(OrbType.Research, rn[i], 380 + Rng.Next(500), 28 + Rng.Next(60), 4);
        for (int i = 0; i < 26; i++) Add(OrbType.Debris, $"DEB-{10400 + Rng.Next(500)}", 500 + Rng.Next(1500), 10 + Rng.Next(90), 5);
        int unk = 2 + Scenario % 3;
        for (int i = 0; i < unk; i++) { var o = Add(OrbType.Unknown, $"UNK-{Rng.Next(100, 999)}", 700 + Rng.Next(900), 40 + Rng.Next(50), 2); o.Status = "UNCLASSIFIED"; }
        for (int i = 0; i < 4; i++) Add(OrbType.Station, new[] { "BASTION", "CITADEL", "MERIDIAN STN", "WAYPOINT" }[i], 400 + i * 30, 51.6, 3);
        Objects[0].Tracked = true;
        string[] mn = { "DEEP SURVEY", "DEBRIS CATALOGUE", "LEO PATROL", "CONJUNCTION SCREEN", "UNKNOWN ID", "RELAY SUPPORT" };
        foreach (var m in mn) Missions.Add(new OrbMission { Name = m, Progress = Rng.NextDouble() * 0.6, Readiness = 0.85 + Rng.NextDouble() * 0.14 });
        for (int i = 0; i < SensorHealth.Length; i++) SensorHealth[i] = 0.9 + Rng.NextDouble() * 0.1;
        Selected = 0; Alerts = 0; Conjunction = "";
        for (int i = 0; i < 120; i++) OnStep(0.5);
        int inView = Objects.FindIndex(o => o.Type == OrbType.Active && o.Station >= 0);
        if (inView >= 0) { Selected = inView; Objects[0].Tracked = false; Objects[inView].Tracked = true; }
        Note($"SCENARIO {Scenario + 1} LOADED — {Objects.Count} OBJECTS CATALOGUED (FICTIONAL)");
    }

    OrbObject Add(OrbType t, string name, double alt, double inclDeg, int prio)
    {
        var o = new OrbObject { Name = name, Id = $"BS-{24000 + Objects.Count * 7 + Rng.Next(7):00000}", Type = t, Alt = alt, AltTarget = alt, Incl = inclDeg * Math.PI / 180,
            Raan = Rng.NextDouble() * Math.PI * 2, Phase = Rng.NextDouble() * Math.PI * 2, Ecc = t == OrbType.Debris ? Rng.NextDouble() * 0.02 : Rng.NextDouble() * 0.004, Priority = prio };
        Objects.Add(o); return o;
    }

    public (double lat, double lon) Predict(OrbObject o, double secondsAhead)
    {
        double t = (Time + secondsAhead) * TimeScale, u = o.Phase + 2 * Math.PI * t / o.Period;
        double lat = Math.Asin(Math.Sin(o.Incl) * Math.Sin(u));
        double lon = o.Raan + Math.Atan2(Math.Cos(o.Incl) * Math.Sin(u), Math.Cos(u)) - EarthRate * t;
        return (lat * 180 / Math.PI, ((lon * 180 / Math.PI) % 360 + 540) % 360 - 180);
    }

    /// <summary>Central angle (deg) between two surface points.</summary>
    public static double Arc(double la1, double lo1, double la2, double lo2)
    {
        double a = la1 * Math.PI / 180, b = la2 * Math.PI / 180, d = (lo2 - lo1) * Math.PI / 180;
        return Math.Acos(Math.Clamp(Math.Sin(a) * Math.Sin(b) + Math.Cos(a) * Math.Cos(b) * Math.Cos(d), -1, 1)) * 180 / Math.PI;
    }

    public static double Bearing(double la1, double lo1, double la2, double lo2)
    {
        double a = la1 * Math.PI / 180, b = la2 * Math.PI / 180, d = (lo2 - lo1) * Math.PI / 180;
        return (Math.Atan2(Math.Sin(d) * Math.Cos(b), Math.Cos(a) * Math.Sin(b) - Math.Sin(a) * Math.Cos(b) * Math.Cos(d)) * 180 / Math.PI + 360) % 360;
    }

    /// <summary>Elevation (deg) and slant range (km) of object seen from a station.</summary>
    public static (double el, double range) Look(OrbObject o, GroundStation s)
    {
        double g = Arc(s.Lat, s.Lon, o.Lat, o.Lon) * Math.PI / 180, r = Re + o.Alt;
        double range = Math.Sqrt(Re * Re + r * r - 2 * Re * r * Math.Cos(g));
        double el = Math.Atan2(Math.Cos(g) - Re / r, Math.Sin(g)) * 180 / Math.PI;
        return (el, range);
    }
    protected override void OnStep(double dt)
    {
        double tsDt = dt * TimeScale;
        foreach (var o in Objects)
        {
            if (Math.Abs(o.Alt - o.AltTarget) > 0.2) o.Alt += Math.Sign(o.AltTarget - o.Alt) * Math.Min(Math.Abs(o.AltTarget - o.Alt), 4.0 * dt);   // manoeuvre: 4 km/s of sim time
            if (o.Type == OrbType.Debris) o.Alt -= 0.0004 * dt;                                                                                            // slow decay
            o.Phase += 2 * Math.PI * tsDt / o.Period;
            var (la, lo) = Predict(o, 0); o.Lat = la; o.Lon = lo;
            if (o.Tracked || o == Objects[Selected]) { if (o.Trail.Count == 0 || (int)(Time * 4) != (int)((Time - dt) * 4)) { o.Trail.Add((la, lo)); if (o.Trail.Count > 90) o.Trail.RemoveAt(0); } }
            // best station: highest elevation above 5 deg
            double bestEl = -90, bestRange = 0; int bs = -1;
            for (int i = 0; i < Stations.Count; i++)
            {
                if (!Stations[i].Online) continue;
                var (el, rg) = Look(o, Stations[i]);
                if (el > bestEl) { bestEl = el; bestRange = rg; bs = i; }
            }
            bool vis = bestEl > 3 && !o.SignalLost;
            if (vis && o.Station < 0) { o.Station = bs; }
            else if (o.Station >= 0)
            {
                var (el, rg) = Look(o, Stations[o.Station]);
                if (el < 3 || !Stations[o.Station].Online) o.Station = vis ? bs : -1; else { bestEl = Math.Max(el, 0); bestRange = rg; }
            }
            o.Elevation = bestEl; o.Range = bestRange;
            double snr = vis ? Clamp(34 - 20 * Math.Log10(Math.Max(300, bestRange) / 1000.0) - (o.Type == OrbType.Debris ? 12 : 0) + bestEl * 0.08 + Noise(0.4), 0, 45) : Approach(o.Snr, 0, 2, dt);
            o.Snr = Approach(o.Snr, snr, 2.5, dt);
            o.Confidence = Clamp(o.Snr / 40.0 + (o.Tracked ? 0.12 : 0), 0, 0.99);
            if (vis) o.LastUpdate = Time;
            o.Status = o.SignalLost ? "SIGNAL LOST" : !vis && o.Station < 0 ? "NO CONTACT" : o.Type == OrbType.Unknown ? "UNCLASSIFIED" : Math.Abs(o.Alt - o.AltTarget) > 1 ? "MANOEUVRING" : "NOMINAL";
        }
        // station / link accounting
        foreach (var s in Stations) { s.Links = 0; s.Load = 0; }
        double totalBw = 0, totalLat = 0, q = 0; int nl = 0, trk = 0;
        foreach (var o in Objects)
        {
            if (o.Tracked) trk++;
            if (o.Station >= 0 && (o.Type == OrbType.Active || o.Type == OrbType.Research || o.Type == OrbType.Station) && !o.SignalLost) { Stations[o.Station].Links++; Stations[o.Station].Load += o.Tracked ? 3 : 1; }
        }
        foreach (var s in Stations)
        {
            double cap = s.Bandwidth;
            s.Load = Approach(s.Load, Clamp(s.Load * 7, 0, cap), 1.5, dt);
            var best = Objects.Where(o => o.Station == Stations.IndexOf(s)).OrderBy(o => o.Range).FirstOrDefault();
            s.Latency = Approach(s.Latency, best == null ? 0 : best.Range / 299792.0 * 1000 + 8 + s.Load / cap * 20, 2, dt);
            totalBw += s.Load; totalLat += s.Latency; if (s.Online) nl++;
        }
        foreach (var o in Objects.Where(o => o.Station >= 0)) q += o.Snr;
        TrackedCount.Push(trk); Bandwidth.Push((float)totalBw); Latency.Push((float)(nl == 0 ? 0 : totalLat / nl)); Quality.Push((float)(Objects.Count(o => o.Station >= 0) == 0 ? 0 : q / Objects.Count(o => o.Station >= 0)));
        for (int i = 0; i < SensorHealth.Length; i++) SensorHealth[i] = Clamp(SensorHealth[i] + Noise(0.0015) + (SensorHealth[i] < 0.9 ? 0.0006 : 0), 0.3, 1);
        Readiness = Clamp(SensorHealth.Average() * 0.5 + Stations.Count(s => s.Online) / (double)Stations.Count * 0.5, 0, 1);
        foreach (var m in Missions) { m.Progress = Clamp(m.Progress + dt * 0.002 * (m.State == "ACTIVE" ? 1 : 0), 0, 1); m.Readiness = Approach(m.Readiness, Readiness, 0.2, dt); }
        // conjunction screening (events are produced by physics, not by dice)
        if ((int)(Time * 2) != (int)((Time - dt) * 2))
        {
            var sel = Objects[Selected];
            foreach (var d in Objects.Where(x => x.Type == OrbType.Debris || x.Type == OrbType.Unknown))
                if (Math.Abs(d.Alt - sel.Alt) < 6 && Arc(sel.Lat, sel.Lon, d.Lat, d.Lon) < 1.2 && string.IsNullOrEmpty(Conjunction))
                { Conjunction = $"{sel.Name} / {d.Id}  MISS DIST {Arc(sel.Lat, sel.Lon, d.Lat, d.Lon) * 111:0} KM"; Alerts++; Note("CONJUNCTION WARNING: " + Conjunction, true); }
            if (!string.IsNullOrEmpty(Conjunction) && Rng.NextDouble() < 0.02) Conjunction = "";
        }
    }

    // ---- operator actions (each changes state that the displays derive from)
    public string Acquire(int i)
    {
        var o = Objects[i]; o.Acquired = true; o.SignalLost = false;
        if (o.Station < 0) return $"{o.Name}: NO STATION IN VIEW — AWAITING PASS";
        Note($"ACQUIRED {o.Name} VIA {Stations[o.Station].Id}"); return $"{o.Name} ACQUIRED — {Stations[o.Station].Name}";
    }

    public string Track(int i) { var o = Objects[i]; o.Tracked = !o.Tracked; if (o.Tracked) Selected = i; Note($"TRACKING {(o.Tracked ? "STARTED" : "STOPPED")}: {o.Name}"); return $"TRACKING {(o.Tracked ? "STARTED" : "STOPPED")}: {o.Name}"; }
    public string CyclePriority(int i) { var o = Objects[i]; o.Priority = o.Priority % 5 + 1; Note($"PRIORITY {o.Name} -> P{o.Priority}"); return $"{o.Name} PRIORITY P{o.Priority}"; }

    public string Handover(int i)
    {
        var o = Objects[i]; double best = -90; int bs = -1;
        for (int k = 0; k < Stations.Count; k++) { if (k == o.Station || !Stations[k].Online) continue; var (el, _) = Look(o, Stations[k]); if (el > best) { best = el; bs = k; } }
        if (bs < 0 || best < 5) return "HANDOVER REFUSED — NO ALTERNATE STATION IN VIEW";
        string from = o.Station >= 0 ? Stations[o.Station].Id : "--"; o.Station = bs; Note($"HANDOVER {o.Name}: {from} -> {Stations[bs].Id}"); return $"HANDOVER {from} -> {Stations[bs].Id}";
    }

    public string Loss(int i) { var o = Objects[i]; o.SignalLost = true; Note($"SIGNAL LOSS: {o.Name}", true); return $"SIMULATED SIGNAL LOSS: {o.Name}"; }
    public string Restore(int i) { var o = Objects[i]; o.SignalLost = false; foreach (var s in Stations) s.Online = true; Note($"LINK RESTORED: {o.Name}"); return $"CONNECTION RESTORED: {o.Name}"; }
    public string Manoeuvre(int i, double dAlt) { var o = Objects[i]; o.AltTarget = Clamp(o.AltTarget + dAlt, 300, 2500); Note($"MANOEUVRE {o.Name} TARGET {o.AltTarget:0} KM"); return $"{o.Name} -> {o.AltTarget:0} KM"; }
}
