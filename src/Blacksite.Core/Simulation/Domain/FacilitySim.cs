namespace Blacksite.Core.Simulation.Domain;

public sealed class Room
{
    public string Id = "", Name = "", Type = ""; public int Level, Col, Zone, Priority = 3; public double LoadKw, Temp = 21, Occupancy; public bool Powered = true, Locked; public string Status = "NOMINAL";
    public int Bus;      // 0..3
}

public sealed class Infra { public string Name = ""; public double Health = 1; public int Bus; public bool Failed; public string State = "NOMINAL"; }
public sealed class Person { public string Id = "", Name = "", Dept = ""; public int Clearance; public int Room; public string Duty = "ON DUTY", Task = ""; public double MoveT; public readonly List<string> History = new(); }
public sealed class Incident { public string Id = "", Location = "", Status = "OPEN", Team = "--", Desc = ""; public int Priority; public double Detected; }
public sealed class Checkpoint { public string Id = ""; public int Zone; public string State = "CLOSED"; public int RoomA, RoomB; }

/// <summary>
/// Fictional underground-facility model. Rooms draw power through four buses; when supply is short the battery covers the gap
/// and then low-priority loads are shed, which in turn degrades cooling, ventilation, cameras and doors.
/// </summary>
public sealed class FacilitySim : DomainSim
{
    public const int Levels = 8;
    public static readonly string[] LevelNames = { "L0 SURFACE ANNEX", "L1 ACCESS & SECURITY", "L2 ADMINISTRATION", "L3 RESEARCH LABS", "L4 DATA & COMMS", "L5 MEDICAL & HABITAT", "L6 POWER STATION", "L7 REACTOR CHAMBER" };
    public static readonly string[] ZoneNames = { "ZONE A", "ZONE B", "ZONE C", "ZONE D" };
    public readonly List<Room> Rooms = new();
    public readonly List<Infra> Systems = new();
    public readonly List<Person> People = new();
    public readonly List<Incident> Incidents = new();
    public readonly List<Checkpoint> Checkpoints = new();
    public readonly List<string> Terminal = new();
    public readonly RingBuffer SupplyH = new(240), DemandH = new(240), BattH = new(240), TempH = new(240), Co2H = new(240), VoltH = new(240);
    public int SelLevel = 3, SelRoom;
    public bool GridOnline = true, Lockdown;
    public double GridKw = 2600, GenKw0, GenKw1, Fuel0 = 0.9, Fuel1 = 0.9, Battery = 0.95, BatteryMwh = 6;
    public bool Gen0On, Gen1On;
    public double Supply, Demand, Voltage = 480, Freq = 60, Current;
    public readonly double[] BusAlloc = { 1, 1, 1, 1 };
    public readonly double[] BusLoad = new double[4];
    public double EnvTemp = 21, EnvCo2 = 520, EnvHum = 42, EnvPress = 101.3, EnvO2 = 20.9, Airflow = 100, Water = 0.92;
    // breach scenario
    public int BreachPhase = -1; public double BreachT; public int BreachRoom = -1;
    public string ResponseTeam = "--"; public double TeamEta;
    public int SecurityLevel = 1;
    public double CoolSet = 1, VentSet = 1;
    double _brChk;
    static readonly string[] ZoneAssign = { "A", "A", "B", "B", "C", "C", "D", "D" };

    public FacilitySim() { Reset(0); }

    protected override void OnReset()
    {
        Rooms.Clear(); Systems.Clear(); People.Clear(); Incidents.Clear(); Checkpoints.Clear(); Terminal.Clear();
        Lockdown = false; GridOnline = true; Gen0On = Gen1On = false; GenKw0 = GenKw1 = 0; Fuel0 = Fuel1 = 0.9; Battery = 0.95; BreachPhase = -1; BreachRoom = -1; SecurityLevel = 1; ResponseTeam = "--";
        for (int i = 0; i < 4; i++) BusAlloc[i] = 1;
        (string n, string t, int lv, int col, double kw, int pr, int bus)[] r =
        {
            ("SURFACE GATEHOUSE", "SEC", 0, 1, 25, 2, 0), ("VEHICLE BAY", "STORAGE", 0, 3, 40, 4, 3),
            ("MAIN CHECKPOINT", "SEC", 1, 0, 35, 1, 0), ("SECURITY OPS", "CTRL", 1, 2, 60, 1, 0), ("ARMORY", "SEC", 1, 4, 20, 2, 0),
            ("ADMIN OFFICES", "CTRL", 2, 1, 55, 4, 3), ("CONTROL ROOM ALPHA", "CTRL", 2, 3, 90, 1, 1),
            ("QUANTUM LAB", "LAB", 3, 0, 220, 3, 1), ("MATERIALS LAB", "LAB", 3, 2, 180, 3, 2), ("BIOSAFETY LAB", "LAB", 3, 4, 160, 3, 2),
            ("DATA CENTER", "DATA", 4, 1, 420, 2, 1), ("COMMS ROOM", "COMMS", 4, 3, 85, 1, 0), ("ARCHIVE VAULT", "STORAGE", 4, 4, 45, 3, 3),
            ("MEDICAL BAY", "MED", 5, 0, 70, 1, 0), ("HABITAT BLOCK", "HAB", 5, 2, 130, 3, 3), ("CAFETERIA", "HAB", 5, 4, 50, 5, 3),
            ("MAIN POWER HALL", "POWER", 6, 1, 150, 1, 2), ("BATTERY BANK", "POWER", 6, 3, 40, 1, 2), ("MAINTENANCE SHOP", "MAINT", 6, 4, 60, 4, 3),
            ("REACTOR CHAMBER", "REACTOR", 7, 2, 300, 1, 2), ("COOLANT PLANT", "MAINT", 7, 0, 210, 1, 2),
        };
        for (int i = 0; i < r.Length; i++)
            Rooms.Add(new Room { Id = $"R-{r[i].lv}{i + 1:00}", Name = r[i].n, Type = r[i].t, Level = r[i].lv, Col = r[i].col, LoadKw = r[i].kw, Priority = r[i].pr, Bus = r[i].bus, Zone = (r[i].lv) / 2, Occupancy = Rng.Next(0, 6) });
        string[] sn = { "WATER SUPPLY", "COOLING LOOPS", "AIR HANDLING", "WASTE MANAGEMENT", "COMMS UPLINK", "NETWORK CORE", "MAINTENANCE ROBOTS", "FIRE SUPPRESSION" }; int[] sb = { 3, 2, 1, 3, 0, 1, 3, 0 };
        for (int i = 0; i < sn.Length; i++) Systems.Add(new Infra { Name = sn[i], Bus = sb[i], Health = 0.9 + Rng.NextDouble() * 0.1 });
        string[] fn = { "ARIA", "BENNETT", "CASTELLANOS", "DUMAS", "EKSTROM", "FARRELL", "GUNDERSEN", "HASHIMOTO", "IVANOVA", "JOHANSSON", "KAPOOR", "LINDQVIST", "MBEKI", "NAKAMURA", "OLSEN", "PETROVIC", "QUINN", "ROSSI", "SANTOS", "TREMBLAY" };
        string[] dp = { "SECURITY", "RESEARCH", "OPERATIONS", "MEDICAL", "ENGINEERING", "DATA", "ADMIN" }; string[] tk = { "PATROL", "EXPERIMENT 7B", "SYSTEM CHECK", "SHIFT HANDOVER", "MAINTENANCE", "DATA AUDIT", "SAMPLE ANALYSIS", "STANDBY" };
        for (int i = 0; i < fn.Length; i++)
            People.Add(new Person { Id = $"P-{4000 + i * 13}", Name = $"{(char)('A' + i)}. {fn[i]}", Dept = dp[i % dp.Length], Clearance = 1 + (i * 3) % 5, Room = Rng.Next(Rooms.Count), Task = tk[i % tk.Length], Duty = i % 6 == 5 ? "OFF DUTY" : "ON DUTY" });
        for (int i = 1; i < Rooms.Count; i++) if (Rooms[i].Level != Rooms[i - 1].Level || i % 3 == 0) Checkpoints.Add(new Checkpoint { Id = $"CP-{Checkpoints.Count + 1:00}", Zone = Rooms[i].Zone, RoomA = i - 1, RoomB = i });
        SelLevel = 3; SelRoom = 7;
        for (int i = 0; i < 240; i++) OnStep(0.1);
        Terminal.Clear(); Note("FACILITY CONTROL SYSTEM v9.4 — INITIALISATION COMPLETE (FICTIONAL)"); Note($"SCENARIO {Scenario + 1}: {Rooms.Count} ROOMS, {People.Count} PERSONNEL, {Checkpoints.Count} CHECKPOINTS");
    }

    public int RoomsOnLevel(int lv) => Rooms.Count(r => r.Level == lv);

    protected override void OnStep(double dt)
    {
        // generators ramp (8 s)
        GenKw0 = Approach(GenKw0, Gen0On && Fuel0 > 0 ? 1200 : 0, 0.4, dt); GenKw1 = Approach(GenKw1, Gen1On && Fuel1 > 0 ? 1200 : 0, 0.4, dt);
        if (Gen0On) Fuel0 = Math.Max(0, Fuel0 - dt * 0.0008 * GenKw0 / 1200); if (Gen1On) Fuel1 = Math.Max(0, Fuel1 - dt * 0.0008 * GenKw1 / 1200);
        Supply = (GridOnline ? GridKw : 0) + GenKw0 + GenKw1;
        // demand from powered rooms (bus allocation scales that bus; shedding removes rooms)
        Array.Clear(BusLoad);
        foreach (var r in Rooms) if (r.Powered) BusLoad[r.Bus] += r.LoadKw * BusAlloc[r.Bus] * (r.Locked ? 1.0 : 1.0) * (1 + 0.05 * Math.Sin(Time * 0.3 + r.Level));
        Demand = BusLoad.Sum() + 60;
        double deficit = Demand - Supply;
        if (deficit > 0)
        {
            Battery = Math.Max(0, Battery - deficit / 1000.0 * dt / 3600.0 / BatteryMwh * 60);       // MWh draw (time-compressed)
            if (Battery <= 0.001)
                foreach (var r in Rooms.OrderByDescending(r => r.Priority).Where(r => r.Powered && r.Priority > 1).Take(1)) { if (Rng.NextDouble() < dt * 2) { r.Powered = false; Note($"LOAD SHED: {r.Name} (P{r.Priority})", true); } }
        }
        else
        {
            Battery = Math.Min(1, Battery + Math.Min(-deficit, 300) / 1000.0 * dt / 3600.0 / BatteryMwh * 60);
            if (Battery > 0.15) foreach (var r in Rooms.Where(r => !r.Powered && r.Bus >= 0).OrderBy(r => r.Priority).Take(1)) if (Rng.NextDouble() < dt * 0.5 && Supply > Demand + r.LoadKw) { r.Powered = true; Note($"LOAD RESTORED: {r.Name}"); }
        }
        double ratio = Demand / Math.Max(1, Supply + (Battery > 0 ? 400 : 0));
        Voltage = Approach(Voltage, 480 * (1 - Math.Max(0, ratio - 0.9) * 0.25) + Noise(0.4), 2, dt); Freq = Approach(Freq, 60 - Math.Max(0, ratio - 1) * 3 + Noise(0.01), 3, dt);
        Current = Math.Min(Demand, Supply + 400) * 1000 / (Math.Sqrt(3) * Math.Max(100, Voltage) * 0.92);
        // infrastructure: depends on its bus; failed systems drive the environment
        foreach (var s in Systems)
        {
            bool busOk = Rooms.Any(r => r.Bus == s.Bus && r.Powered) && (Supply + (Battery > 0 ? 1 : 0)) > 0;
            s.Health = s.Failed || !busOk ? Approach(s.Health, 0, 0.3, dt) : Approach(s.Health, 0.97, 0.05, dt) + Noise(0.0005);
            s.State = s.Failed ? "FAILED" : !busOk ? "NO POWER" : s.Health < 0.7 ? "DEGRADED" : "NOMINAL";
        }
        double cool = Math.Min(1.15, Systems[1].Health * CoolSet), air = Math.Min(1.15, Systems[2].Health * VentSet), water = Systems[0].Health;
        foreach (var r in Rooms)
        {
            double heat = r.Powered ? r.LoadKw / 40.0 : 0;
            r.Temp = Approach(r.Temp, 17 + heat * 1.3 + (1 - cool) * 28, 0.05, dt) + Noise(0.01);
            r.Occupancy = People.Count(p => p.Room == Rooms.IndexOf(r) && p.Duty == "ON DUTY");
            r.Status = !r.Powered ? "OFFLINE" : BreachRoom == Rooms.IndexOf(r) && BreachPhase >= 0 ? "BREACH" : r.Locked ? "LOCKED" : r.Temp > 38 ? "ALERT" : "NOMINAL";
        }
        var lvRooms = Rooms.Where(r => r.Level == SelLevel).ToList();
        EnvTemp = Approach(EnvTemp, lvRooms.Count == 0 ? 21 : lvRooms.Average(r => r.Temp), 0.5, dt);
        EnvCo2 = Approach(EnvCo2, 450 + People.Count * 12 + (1 - air) * 3200, 0.1, dt); EnvO2 = Approach(EnvO2, 20.9 - (1 - air) * 3.5, 0.05, dt);
        EnvHum = Approach(EnvHum, 42 + (1 - cool) * 18, 0.05, dt); Airflow = Approach(Airflow, 100 * air, 0.3, dt); Water = Approach(Water, water, 0.2, dt); EnvPress = Approach(EnvPress, 101.3 - (1 - air) * 1.2, 0.1, dt);
        // personnel movement and access logging
        foreach (var p in People)
        {
            p.MoveT -= dt;
            if (p.MoveT <= 0 && p.Duty == "ON DUTY" && !Lockdown)
            {
                p.MoveT = 12 + Rng.NextDouble() * 25;
                int to = Rng.Next(Rooms.Count); var room = Rooms[to];
                if (room.Powered && p.Clearance >= room.Zone / 2 + 1 && !room.Locked) { p.Room = to; p.History.Add($"{DateTime.Now:HH:mm:ss} {room.Name}"); if (p.History.Count > 8) p.History.RemoveAt(0); }
            }
        }
        foreach (var c in Checkpoints) c.State = Lockdown || Rooms[c.RoomA].Locked && Rooms[c.RoomB].Locked ? "LOCKED" : Rooms[c.RoomA].Powered ? (c.State == "OPEN" && Rng.NextDouble() < dt * 0.4 ? "CLOSED" : c.State) : "FAIL-SAFE";
        RunBreach(dt);
        SecurityLevel = Lockdown ? 5 : BreachPhase >= 1 ? 4 : Incidents.Any(i => i.Status != "RESOLVED") ? 2 : 1;
        if ((int)(Time * 10) != (int)((Time - dt) * 10))
        {
            SupplyH.Push((float)Supply); DemandH.Push((float)Demand); BattH.Push((float)(Battery * 100)); TempH.Push((float)Rooms.Average(r => r.Temp)); Co2H.Push((float)EnvCo2); VoltH.Push((float)Voltage);
        }
        if (Rng.NextDouble() < dt * 0.15) Terminal.Add($"{DateTime.Now:HH:mm:ss} [{new[] { "ENV", "PWR", "SEC", "NET", "MNT" }[Rng.Next(5)]}] {new[] { "CHECK OK", "SENSOR POLL COMPLETE", "SCHEDULE UPDATED", "SYNC ACK", "LOG ROTATED" }[Rng.Next(5)]}");
        while (Terminal.Count > 200) Terminal.RemoveAt(0);
    }

    new void Note(string s, bool warn = false) { base.Note(s, warn); Terminal.Add($"{DateTime.Now:HH:mm:ss} {s}"); }

    void RunBreach(double dt)
    {
        if (BreachPhase < 0) return;
        BreachT += dt;
        double[] t = { 0, 2, 4, 6, 8, 10, 12, 14 };
        int ph = BreachPhase; for (int i = 0; i < t.Length; i++) if (BreachT >= t[i]) ph = Math.Max(ph, i);
        if (ph != BreachPhase)
        {
            BreachPhase = ph; var room = Rooms[BreachRoom];
            switch (ph)
            {
                case 1: Note("SECURITY ALERT: UNAUTHORISED ENTRY DETECTED", true); break;
                case 2: Note($"ZONE {ZoneNames[room.Zone]} HIGHLIGHTED — {room.Name} ({LevelNames[room.Level]})", true); break;
                case 3: Note($"CAMERAS SWITCHED TO {room.Name}"); SelLevel = room.Level; SelRoom = BreachRoom; break;
                case 4: foreach (var c in Checkpoints.Where(c => c.RoomA == BreachRoom || c.RoomB == BreachRoom || Rooms[c.RoomA].Level == room.Level)) c.State = "LOCKED"; Note("ACCESS CHECKPOINTS LOCKING IN AFFECTED ZONE"); break;
                case 5: Incidents.Insert(0, new Incident { Id = $"INC-{1000 + Incidents.Count + 1}", Priority = 1, Location = room.Name, Detected = Time, Status = "OPEN", Desc = "UNAUTHORISED ENTRY — MOTION AND DOOR SENSORS TRIPPED" }); Note("INCIDENT LOGGED: " + Incidents[0].Id); break;
                case 6: ResponseTeam = "ALPHA TEAM (SECURITY)"; TeamEta = 45; Incidents[0].Team = ResponseTeam; Incidents[0].Status = "RESPONDING"; Note($"RESPONSE TEAM {ResponseTeam} DEPLOYED — ETA 45S"); break;
                case 7: Note("AWAITING OPERATOR DECISION: LOCKDOWN OR RESOLVE", true); break;
            }
        }
        if (BreachPhase >= 6) TeamEta = Math.Max(0, TeamEta - dt * (Lockdown ? 3 : 1));
        if (BreachPhase >= 6 && TeamEta <= 0 && Incidents.Count > 0 && Incidents[0].Status == "RESPONDING") { Incidents[0].Status = "CONTAINED"; Note("RESPONSE TEAM ON SCENE — INCIDENT CONTAINED"); }
    }

    // ---- operator actions
    public string TriggerBreach()
    {
        BreachRoom = Rng.Next(Rooms.Count); BreachPhase = 0; BreachT = 0; ResponseTeam = "--"; Note("FACILITY SECURITY BREACH SIMULATION INITIATED", true); return "BREACH SIMULATION STARTED";
    }
    public string ResolveBreach()
    {
        if (BreachPhase < 0) return "NO ACTIVE INCIDENT";
        foreach (var i in Incidents.Where(i => i.Status != "RESOLVED")) i.Status = "RESOLVED"; BreachPhase = -1; BreachRoom = -1; Lockdown = false; foreach (var r in Rooms) r.Locked = false; foreach (var c in Checkpoints) c.State = "CLOSED";
        Note("INCIDENT RESOLVED — SYSTEMS RETURNING TO NORMAL"); return "INCIDENT RESOLVED";
    }
    public string ToggleLockdown() { Lockdown = !Lockdown; foreach (var r in Rooms) r.Locked = Lockdown; Note("FACILITY LOCKDOWN " + (Lockdown ? "ENGAGED" : "RELEASED"), Lockdown); return "LOCKDOWN " + (Lockdown ? "ENGAGED" : "RELEASED"); }
    public string StartGenerator(int i) { if (i == 0) Gen0On = !Gen0On; else Gen1On = !Gen1On; Note($"GENERATOR {i + 1} " + ((i == 0 ? Gen0On : Gen1On) ? "STARTING" : "STOPPING")); return $"GENERATOR {i + 1} " + ((i == 0 ? Gen0On : Gen1On) ? "STARTING" : "STOPPING"); }
    public string GridFail() { GridOnline = !GridOnline; Note("EXTERNAL GRID " + (GridOnline ? "RESTORED" : "LOST"), !GridOnline); return "GRID " + (GridOnline ? "RESTORED" : "FAILURE"); }
    public string Fail(int sys) { Systems[sys].Failed = true; Note($"INFRASTRUCTURE FAILURE: {Systems[sys].Name}", true); return $"FAILURE: {Systems[sys].Name}"; }
    public string RestoreAll() { foreach (var s in Systems) s.Failed = false; GridOnline = true; foreach (var r in Rooms) r.Powered = true; Note("ALL SYSTEMS RESTORED"); return "SYSTEMS RESTORED"; }
    public string Adjust(string what, double d)
    {
        if (what == "cool") { CoolSet = Clamp(CoolSet + d, 0.3, 1.3); Note($"COOLING OUTPUT {CoolSet * 100:0}%"); return $"COOLING OUTPUT {CoolSet * 100:0}%"; }
        VentSet = Clamp(VentSet + d, 0.3, 1.3); Note($"VENTILATION {VentSet * 100:0}%"); return $"VENTILATION {VentSet * 100:0}%";
    }

    public string Allocate(int bus, double d) { BusAlloc[bus] = Clamp(BusAlloc[bus] + d, 0.3, 1.3); Note($"BUS {(char)('A' + bus)} ALLOCATION {BusAlloc[bus] * 100:0}%"); return $"BUS {(char)('A' + bus)} {BusAlloc[bus] * 100:0}%"; }
    public string Access(int person, int cp)
    {
        var p = People[person]; var c = Checkpoints[cp % Checkpoints.Count]; bool ok = !Lockdown && c.State != "LOCKED" && p.Clearance >= c.Zone + 1;
        p.History.Add($"{DateTime.Now:HH:mm:ss} {c.Id} {(ok ? "GRANTED" : "DENIED")}"); if (ok) { c.State = "OPEN"; p.Room = c.RoomB; }
        Note($"ACCESS {(ok ? "GRANTED" : "DENIED")}: {p.Name} @ {c.Id} (CLR {p.Clearance} / ZONE {c.Zone + 1})", !ok); return $"ACCESS {(ok ? "GRANTED" : "DENIED")}: {p.Name}";
    }
    public string[] Report() => new[]
    {
        $"FACILITY REPORT (FICTIONAL) — {DateTime.Now:yyyy-MM-dd HH:mm}", $"SECURITY LEVEL {SecurityLevel}   LOCKDOWN {(Lockdown ? "YES" : "NO")}   OPEN INCIDENTS {Incidents.Count(i => i.Status != "RESOLVED")}",
        $"SUPPLY {Supply:0} KW   DEMAND {Demand:0} KW   BATTERY {Battery * 100:0}%   GEN {(Gen0On ? "1 " : "")}{(Gen1On ? "2" : "")}", $"VOLTAGE {Voltage:0} V   FREQ {Freq:0.00} HZ   CURRENT {Current:0} A",
        $"UNPOWERED ROOMS {Rooms.Count(r => !r.Powered)}   FAILED SYSTEMS {Systems.Count(s => s.Failed)}", $"PERSONNEL ON DUTY {People.Count(p => p.Duty == "ON DUTY")}/{People.Count}", "ALL DATA SIMULATED.",
    };
}
