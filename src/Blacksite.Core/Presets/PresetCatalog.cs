using Blacksite.Core.Themes;

namespace Blacksite.Core.Presets;

public enum SoundProfile { Space, Terminal, Surveillance, Ai, Lockdown, Satellite, Reactor, Cyber, DeepSpace, Tactical, Orbital, Quantum, DeepComms, Medical, Facility }

public sealed record PresetInfo(
    string Id, int Index, string Name, string Description, SoundProfile Sound, Palette Palette, string[] Actions);

public static class PresetCatalog
{
    static uint C(int r, int g, int b) => Palette.Rgb((byte)r, (byte)g, (byte)b);

    public static readonly IReadOnlyList<PresetInfo> All = new[]
    {
        new PresetInfo("space", 1, "SPACE COMMAND", "Deep-space mission control: planets, orbits, telemetry, launch sequences.", SoundProfile.Space,
            new Palette("Space", C(2,6,18), C(6,16,38), C(0,220,255), C(40,110,255), C(160,240,255), C(200,235,255), C(255,170,40)),
            new[]{"TRACK","LAUNCH","SIGNAL","ZOOM+"}),
        new PresetInfo("hacker", 2, "HACKER TERMINAL", "Cyber operations workstation with live terminals, packets and hex streams.", SoundProfile.Terminal,
            new Palette("Hacker", C(2,8,3), C(8,18,10), C(40,255,90), C(0,170,60), C(190,255,200), C(150,255,170), C(255,200,40)),
            new[]{"SCAN","DECRYPT","THEME","CLEAR"}),
        new PresetInfo("surveillance", 3, "SURVEILLANCE CONTROL", "Multi-camera monitoring room: night vision, thermal, motion alerts.", SoundProfile.Surveillance,
            new Palette("Surveillance", C(8,11,14), C(16,22,28), C(130,170,140), C(70,110,150), C(190,230,200), C(180,200,190), C(255,90,70)),
            new[]{"LAYOUT","NIGHT","THERMAL","RECORD","MOTION"}),
        new PresetInfo("jarvis", 4, "JARVIS AI CORE", "Holographic AI core with voice, diagnostics and analysis sequences.", SoundProfile.Ai,
            new Palette("Jarvis", C(3,8,16), C(10,20,34), C(90,225,255), C(110,150,200), C(255,255,255), C(220,240,255), C(255,170,60)),
            new[]{"ACTIVATE","DIAGNOSE","ANALYZE","PERSONA","SPEAK"}),
        new PresetInfo("lockdown", 5, "SYSTEM LOCKDOWN", "Military emergency command interface. Entirely simulated.", SoundProfile.Lockdown,
            new Palette("Lockdown", C(10,2,3), C(26,10,12), C(255,40,40), C(170,30,30), C(255,150,120), C(255,190,180), C(255,220,60)),
            new[]{"LOCKDOWN","VERIFY","COUNTDOWN","THREAT","CODE"}),
        new PresetInfo("satellite", 6, "SATELLITE INTELLIGENCE", "Geospatial operations: Earth map, orbital tracks, coverage, scanning.", SoundProfile.Satellite,
            new Palette("Satellite", C(2,8,20), C(8,22,44), C(70,170,255), C(0,230,230), C(235,248,255), C(210,232,255), C(255,190,60)),
            new[]{"SCAN","GRID","COVERAGE","REGION"}),
        new PresetInfo("reactor", 7, "REACTOR CONTROL", "Spaceship engineering: power, cooling, overload, subsystem failures.", SoundProfile.Reactor,
            new Palette("Reactor", C(14,6,2), C(30,16,6), C(255,140,20), C(255,215,60), C(70,160,255), C(255,225,190), C(255,60,40)),
            new[]{"COOLING","BALANCE","OVERLOAD","DIAG","FAIL","SCRAM"}),
        new PresetInfo("cyber", 8, "CYBER DEFENSE CENTER", "Global SOC: attack vectors, firewall, threat analysis, reports.", SoundProfile.Cyber,
            new Palette("Cyber", C(2,10,10), C(6,22,24), C(0,235,150), C(0,140,255), C(180,255,230), C(170,240,225), C(255,80,80)),
            new[]{"FIREWALL","ANALYZE","REPORT","MODE","PROTOCOL"}),
        new PresetInfo("deepspace", 9, "DEEP SPACE EXPLORATION", "Starship bridge: navigation, warp, life support, comms.", SoundProfile.DeepSpace,
            new Palette("DeepSpace", C(6,3,18), C(16,10,40), C(170,110,255), C(0,215,240), C(230,210,255), C(225,215,255), C(255,170,70)),
            new[]{"DEST","WARP","HYPER","COMMS","SCAN"}),
        new PresetInfo("tactical", 10, "TACTICAL COMMAND CENTER", "Military operations room: map, units, radar, objectives, intel.", SoundProfile.Tactical,
            new Palette("Tactical", C(7,12,6), C(16,26,12), C(150,175,70), C(235,170,30), C(220,235,160), C(205,220,160), C(255,90,50)),
            new[]{"RADAR","LAYER","OBJECTIVE","INTEL","EVENT"}),
        new PresetInfo("orbital", 11, "ORBITAL DEFENSE COMMAND", "Aerospace surveillance: orbital tracking, radar, traffic, ground-station links, handover.", SoundProfile.Orbital,
            new Palette("Orbital", C(2,6,16), C(6,16,34), C(0,210,255), C(120,160,220), C(255,255,255), C(210,230,245), C(255,176,40)),
            new[]{"ACQUIRE","TRACK","PRIORITY","HANDOVER","LOSS","SCENARIO"}),
        new PresetInfo("quantum", 12, "QUANTUM RESEARCH FACILITY", "Quantum processor, particle detector, experiment control and analysis workstation.", SoundProfile.Quantum,
            new Palette("Quantum", C(6,2,14), C(18,8,34), C(170,110,255), C(0,220,255), C(255,80,220), C(225,215,250), C(255,60,160)),
            new[]{"EXPERIMENT","COLLIDE","ACQUIRE","ANALYZE","ANOMALY","COOLING"}),
        new PresetInfo("commarray", 13, "DEEP SPACE COMMUNICATION ARRAY", "Radio telescope array, spectrum analyzer, waterfall, deep-space tracking and decoding.", SoundProfile.DeepComms,
            new Palette("CommArray", C(2,4,10), C(6,12,26), C(0,200,240), C(60,110,200), C(255,255,255), C(200,225,240), C(255,170,40)),
            new[]{"SCAN","TUNE","DECODE","UNKNOWN","LOSS","CHANNEL"}),
        new PresetInfo("medical", 14, "ADVANCED MEDICAL LIFE SUPPORT", "Spacecraft medical bay: patient monitor, scanner, life support and emergency response (fictional).", SoundProfile.Medical,
            new Palette("Medical", C(3,8,16), C(8,20,34), C(60,170,255), C(80,230,160), C(255,255,255), C(220,238,250), C(255,176,40)),
            new[]{"PATIENT","SCAN","DIAG","EMERGENCY","OXYGEN","RECOVER","REPORT"}),
        new PresetInfo("facility", 15, "UNDERGROUND FACILITY COMMAND", "Classified underground facility: levels, security, power, environment, personnel and incidents.", SoundProfile.Facility,
            new Palette("Facility", C(9,10,11), C(20,22,24), C(255,176,40), C(110,160,90), C(0,210,240), C(205,208,200), C(255,70,60)),
            new[]{"LOCKDOWN","GENERATOR","FAIL","RESTORE","ACCESS","REPORT"}),
    };

    public static PresetInfo Get(string id) => All.FirstOrDefault(p => p.Id == id) ?? All[0];
}
