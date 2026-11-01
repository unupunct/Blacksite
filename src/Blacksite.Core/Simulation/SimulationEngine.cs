namespace Blacksite.Core.Simulation;

public sealed class Satellite
{
    public string Name = "";
    public double Inclination;   // radians
    public double Raan;          // radians
    public double Period;        // seconds (simulated)
    public double Phase;         // radians
    public double AltitudeKm;
    public double Lat, Lon;      // degrees, updated per step
    public bool Acquired = true;
    public double Signal;        // 0..1
}

public sealed class Camera
{
    public int Id;
    public string Name = "";
    public bool Online = true;
    public bool Recording = true;
    public double Motion;        // 0..1 current motion level
    public double Interference;  // 0..1
    public int Scene;            // procedural scene seed
}

public sealed class NetNode
{
    public string Name = "";
    public string Ip = "";
    public double X, Y;          // normalised map position
    public double Health = 1;
    public bool UnderAttack;
    public bool Firewalled;
}

public sealed class SimThreat
{
    public int FromNode, ToNode;
    public double Progress;
    public double Speed;
    public string Kind = "";
    public bool Blocked;
}

public sealed class ReactorState
{
    public double Power = 62;          // % output
    public double Target = 62;
    public double Temperature = 540;   // simulated degrees
    public double Pressure = 38;
    public double Coolant = 80;
    public double Stability = 0.96;
    public bool Cooling;
    public bool Overload;
    public bool Scram;
    public bool[] SubsystemOk = { true, true, true, true, true, true };
    public static readonly string[] SubsystemNames = { "COOLANT PUMP", "FUEL FEED", "CONTAINMENT", "TURBINE", "PLASMA INJ", "RELAY GRID" };
}

public sealed class ShipState
{
    public double X = 120.4, Y = -45.2, Z = 8.9;     // light-years, fictional
    public double Speed = 0.12;                       // fraction of fictional cruise
    public double Fuel = 0.78, Oxygen = 0.97, Hull = 0.94, Engine = 0.88;
    public bool Warp;
    public string Destination = "KEPLER STATION";
}

/// <summary>
/// Shared fictional data engine. Everything here is invented and must never be shown as real measurement.
/// Values are coupled (e.g. reactor power drives temperature, which drives stability).
/// </summary>
public sealed class SimulationEngine
{
    readonly Random _rng;
    public double Time { get; private set; }
    public double Speed { get; set; } = 1.0;

    public const int HistoryLen = 240;
    public readonly RingBuffer Cpu = new(HistoryLen), Mem = new(HistoryLen), NetIn = new(HistoryLen),
        NetOut = new(HistoryLen), Power = new(HistoryLen), Temp = new(HistoryLen), Signal = new(HistoryLen),
        Packets = new(HistoryLen), ThreatLevel = new(HistoryLen), Radiation = new(HistoryLen);

    public readonly List<Satellite> Satellites = new();
    public readonly List<Camera> Cameras = new();
    public readonly List<NetNode> Nodes = new();
    public readonly List<SimThreat> Threats = new();
    public readonly ReactorState Reactor = new();
    public readonly ShipState Ship = new();

    // Environment (fictional)
    public double EnvTemp = 21.4, EnvHumidity = 44, EnvPressure = 1013, EnvRadiation = 0.12;
    // System
    public double CpuLoad = 0.3, MemLoad = 0.45, BandwidthIn = 120, BandwidthOut = 60, PacketRate = 2400;
    public double SignalStrength = 0.82;
    public double ThreatIndex = 0.12;     // 0..1
    public int Alerts;

    public int DefenseNodeHits;
    public bool FirewallActive;

    static readonly string[] SatNames = { "AEGIS-1", "HALCYON", "OBSIDIAN", "VANGUARD-4", "NOCTURNE", "SPECTRE", "ORACLE", "TALON-7", "WARDEN", "MERIDIAN", "KESTREL", "LUMEN-2" };
    static readonly string[] NodeNames = { "ARCTIC-GW", "NORD-01", "ATLAS", "HELIOS", "SIERRA", "PAMPAS", "LAGOS-X", "CAIRN", "URAL-9", "INDUS", "KANTO-3", "MEKONG", "TASMAN", "OCEANIA", "ANDES", "SAHEL" };

    public SimulationEngine(int seed = 1337)
    {
        _rng = new Random(seed);
        for (int i = 0; i < SatNames.Length; i++)
            Satellites.Add(new Satellite
            {
                Name = SatNames[i],
                Inclination = (25 + _rng.NextDouble() * 65) * Math.PI / 180,
                Raan = _rng.NextDouble() * Math.PI * 2,
                Period = 70 + _rng.NextDouble() * 90,
                Phase = _rng.NextDouble() * Math.PI * 2,
                AltitudeKm = 400 + _rng.Next(0, 1400),
                Signal = 0.5 + _rng.NextDouble() * 0.5,
            });
        for (int i = 0; i < 9; i++)
            Cameras.Add(new Camera { Id = i + 1, Name = $"CAM-{i + 1:00}", Scene = i * 7 + 3, Recording = i % 3 != 2, Online = i != 6 });
        for (int i = 0; i < NodeNames.Length; i++)
            Nodes.Add(new NetNode
            {
                Name = NodeNames[i],
                Ip = $"10.{_rng.Next(1, 250)}.{_rng.Next(1, 250)}.{_rng.Next(2, 250)}",
                X = 0.06 + (i % 8) * 0.12 + _rng.NextDouble() * 0.05,
                Y = 0.2 + (i / 8) * 0.38 + _rng.NextDouble() * 0.22,
            });
        for (int i = 0; i < HistoryLen; i++) Step(0.1, record: true);
        Time = 0;
    }

    public Random Rng => _rng;

    public double Noise(double amp) => (_rng.NextDouble() * 2 - 1) * amp;

    double Smooth(double v, double target, double k, double noise) =>
        v + (target - v) * k + Noise(noise);

    double _histAcc;

    public void Step(double dt, bool record = true, double intensity = 1.0)
    {
        dt *= Speed;
        Time += dt;
        // ---- system & network
        CpuLoad = Math.Clamp(Smooth(CpuLoad, 0.35 + 0.25 * Math.Sin(Time * 0.21) + ThreatIndex * 0.3, 0.05, 0.02 * intensity), 0.02, 0.99);
        MemLoad = Math.Clamp(Smooth(MemLoad, 0.48 + 0.1 * Math.Sin(Time * 0.07), 0.02, 0.005), 0.1, 0.95);
        BandwidthIn = Math.Clamp(Smooth(BandwidthIn, 140 + 90 * Math.Sin(Time * 0.33) + ThreatIndex * 300, 0.05, 8 * intensity), 5, 900);
        BandwidthOut = Math.Clamp(Smooth(BandwidthOut, 70 + 40 * Math.Sin(Time * 0.27 + 1), 0.05, 5), 3, 500);
        PacketRate = BandwidthIn * 17 + BandwidthOut * 9;
        SignalStrength = Math.Clamp(Smooth(SignalStrength, 0.8 + 0.1 * Math.Sin(Time * 0.11), 0.03, 0.01), 0.2, 1);
        ThreatIndex = Math.Clamp(Smooth(ThreatIndex, FirewallActive ? 0.06 : 0.12, 0.01, 0.004 * intensity), 0, 1);

        // ---- environment
        EnvTemp = Smooth(EnvTemp, 21.4 + Math.Sin(Time * 0.03) * 0.9, 0.02, 0.01);
        EnvHumidity = Smooth(EnvHumidity, 44 + Math.Sin(Time * 0.025) * 3, 0.02, 0.05);
        EnvPressure = Smooth(EnvPressure, 1013 + Math.Sin(Time * 0.01) * 2, 0.02, 0.05);
        EnvRadiation = Math.Max(0.05, Smooth(EnvRadiation, 0.12 + Reactor.Power * 0.0006, 0.05, 0.003));

        // ---- satellites
        foreach (var s in Satellites)
        {
            s.Phase += dt * 2 * Math.PI / s.Period;
            double u = s.Phase, i = s.Inclination;
            double lat = Math.Asin(Math.Sin(i) * Math.Sin(u));
            double lon = s.Raan + Math.Atan2(Math.Cos(i) * Math.Sin(u), Math.Cos(u)) - Time * 0.02;
            s.Lat = lat * 180 / Math.PI;
            s.Lon = ((lon * 180 / Math.PI + 540) % 360) - 180;
            s.Signal = Math.Clamp(s.Signal + Noise(0.02), 0.2, 1);
        }

        // ---- cameras
        foreach (var c in Cameras)
        {
            c.Motion = Math.Max(0, c.Motion - dt * 0.4);
            if (c.Online && _rng.NextDouble() < dt * 0.04 * intensity) c.Motion = 0.5 + _rng.NextDouble() * 0.5;
            c.Interference = Math.Clamp(c.Interference * (1 - dt) + (_rng.NextDouble() < dt * 0.05 * intensity ? 0.6 : 0), 0, 1);
        }

        // ---- reactor (coupled)
        var r = Reactor;
        if (r.Scram) r.Target = 0;
        r.Power = Smooth(r.Power, r.Overload ? 118 : r.Target, 0.04, 0.15);
        double heatTarget = 300 + r.Power * 3.6 - (r.Cooling ? 110 : 0) - (r.SubsystemOk[0] ? 0 : -140);
        r.Temperature = Smooth(r.Temperature, heatTarget, 0.03, 0.8);
        r.Pressure = Smooth(r.Pressure, 20 + r.Temperature * 0.034, 0.04, 0.05);
        r.Coolant = Math.Clamp(r.Coolant + (r.Cooling ? -dt * 0.4 : dt * 0.2), 10, 100);
        int bad = r.SubsystemOk.Count(b => !b);
        double instab = Math.Max(0, (r.Temperature - 780) / 400) + bad * 0.08 + (r.Overload ? 0.25 : 0);
        r.Stability = Math.Clamp(Smooth(r.Stability, 1 - instab, 0.05, 0.004 * intensity), 0.05, 1);

        // ---- ship
        var sh = Ship;
        sh.Oxygen = Math.Clamp(sh.Oxygen + Noise(0.0004), 0.6, 1);
        sh.Fuel = Math.Max(0, sh.Fuel - dt * (sh.Warp ? 0.0008 : 0.00002));
        sh.Hull = Math.Clamp(sh.Hull + Noise(0.0002), 0.1, 1);
        sh.Engine = Math.Clamp(Smooth(sh.Engine, sh.Warp ? 0.97 : 0.86, 0.03, 0.004), 0, 1);
        sh.Speed = Smooth(sh.Speed, sh.Warp ? 9.6 : 0.12, 0.05, 0.002);
        sh.X += dt * sh.Speed * 0.9; sh.Y += dt * sh.Speed * 0.3; sh.Z += dt * sh.Speed * 0.05;

        // ---- network threats
        for (int i = Threats.Count - 1; i >= 0; i--)
        {
            var t = Threats[i];
            t.Progress += dt * t.Speed;
            if (t.Progress >= 1)
            {
                var n = Nodes[t.ToNode];
                if (!t.Blocked && !n.Firewalled) { n.Health = Math.Max(0.1, n.Health - 0.12); DefenseNodeHits++; ThreatIndex = Math.Min(1, ThreatIndex + 0.04); }
                n.UnderAttack = false;
                Threats.RemoveAt(i);
            }
        }
        double spawn = (FirewallActive ? 0.15 : 0.5) * intensity;
        if (_rng.NextDouble() < dt * spawn && Threats.Count < 14) SpawnThreat();
        foreach (var n in Nodes) n.Health = Math.Min(1, n.Health + dt * 0.01);

        // ---- history (fixed rate so graphs scroll steadily)
        _histAcc += dt;
        if (record || _histAcc >= 0.1)
        {
            _histAcc = 0;
            Cpu.Push((float)CpuLoad); Mem.Push((float)MemLoad);
            NetIn.Push((float)BandwidthIn); NetOut.Push((float)BandwidthOut);
            Power.Push((float)r.Power); Temp.Push((float)r.Temperature);
            Signal.Push((float)SignalStrength); Packets.Push((float)PacketRate);
            ThreatLevel.Push((float)ThreatIndex); Radiation.Push((float)EnvRadiation);
        }
    }

    /// <summary>Future ground position of a satellite (same model as Step), offset in simulated seconds.</summary>
    public (double lat, double lon) Predict(Satellite s, double offset)
    {
        double u = s.Phase + offset * 2 * Math.PI / s.Period, i = s.Inclination;
        double lat = Math.Asin(Math.Sin(i) * Math.Sin(u));
        double lon = s.Raan + Math.Atan2(Math.Cos(i) * Math.Sin(u), Math.Cos(u)) - (Time + offset) * 0.02;
        return (lat * 180 / Math.PI, ((lon * 180 / Math.PI + 540) % 360) - 180);
    }

    public void SpawnThreat(int? to = null)
    {
        int a = _rng.Next(Nodes.Count), b = to ?? _rng.Next(Nodes.Count);
        if (a == b) b = (b + 1) % Nodes.Count;
        string[] kinds = { "SYN FLOOD", "PORT PROBE", "BRUTE FORCE", "MALFORMED PKT", "DATA EXFIL", "DNS SPOOF" };
        var t = new SimThreat { FromNode = a, ToNode = b, Speed = 0.12 + _rng.NextDouble() * 0.2, Kind = kinds[_rng.Next(kinds.Length)] };
        t.Blocked = FirewallActive && _rng.NextDouble() < 0.8;
        Nodes[b].UnderAttack = true;
        Threats.Add(t);
        Alerts++;
    }

    public void TriggerMotion(int cameraIndex)
    {
        var c = Cameras[cameraIndex % Cameras.Count];
        c.Motion = 1;
    }

    public void ResetReactor()
    {
        var r = Reactor; r.Overload = false; r.Scram = false; r.Target = 62; r.Cooling = false;
        for (int i = 0; i < r.SubsystemOk.Length; i++) r.SubsystemOk[i] = true;
    }
}
