using Blacksite.Core.Configuration;

namespace Blacksite.Core.Events;

public enum Severity { Info, Notice, Warning, Critical }
public enum EventEffect { None, Banner, Flash, Glitch, Pulse, Blackout }

public sealed record EventDef(
    string Id, string Title, string Message, Severity Severity, double DefaultDuration,
    string Sound, EventEffect Effect, double Countdown = 0, string? Voice = null, string? Only = null);

public sealed class ActiveEvent
{
    public EventDef Def = null!;
    public double Elapsed;
    public double Duration;
    public double CountdownLeft => Def.Countdown > 0 ? Math.Max(0, Def.Countdown - Elapsed) : 0;
    public double Progress => Math.Clamp(Elapsed / Duration, 0, 1);
    public double Fade => Math.Min(1, Math.Min(Elapsed / 0.4, (Duration - Elapsed) / 0.6));
}

public static class EventCatalog
{
    public static readonly IReadOnlyList<EventDef> All = new[]
    {
        new EventDef("incoming_tx", "INCOMING TRANSMISSION", "Encrypted channel open. Source: UNKNOWN.", Severity.Notice, 6, "radio", EventEffect.Banner, 0, "Incoming transmission."),
        new EventDef("sat_acquired", "SATELLITE ACQUIRED", "Link established. Telemetry stream active.", Severity.Info, 5, "confirm", EventEffect.Banner, 0, "Satellite connection established."),
        new EventDef("unknown_obj", "UNKNOWN OBJECT DETECTED", "Unidentified contact on bearing 047. Tracking.", Severity.Warning, 8, "ping", EventEffect.Pulse, 0, "Unknown object detected."),
        new EventDef("breach", "SECURITY BREACH", "Intrusion in sector 7. Containment protocols engaged.", Severity.Critical, 9, "alarm", EventEffect.Flash, 0, "Warning. Security breach detected."),
        new EventDef("reactor_unstable", "REACTOR INSTABILITY", "Core variance exceeding tolerance. Stabilise.", Severity.Critical, 10, "alarm", EventEffect.Glitch, 0, "Warning. Reactor instability."),
        new EventDef("lockdown", "SYSTEM LOCKDOWN", "All access sealed. Awaiting authorisation.", Severity.Critical, 12, "alarm", EventEffect.Flash, 10, "System lockdown initiated."),
        new EventDef("unauth_access", "UNAUTHORIZED ACCESS", "Credential mismatch. Origin traced to node 14.", Severity.Warning, 7, "error", EventEffect.Glitch, 0, "Warning. Unauthorized access detected."),
        new EventDef("emergency_comms", "EMERGENCY COMMUNICATIONS", "Priority broadcast on all channels.", Severity.Warning, 8, "radio", EventEffect.Banner, 0, "Emergency communications."),
        new EventDef("nav_recalc", "NAVIGATION RECALCULATION", "Course deviation detected. Computing new vector.", Severity.Notice, 7, "beep", EventEffect.Banner, 5, "Navigation recalculating."),
        new EventDef("power_fluct", "POWER FLUCTUATION", "Bus voltage unstable. Rerouting.", Severity.Warning, 5, "mech", EventEffect.Blackout, 0, "Power fluctuation."),
        new EventDef("mission_orders", "INCOMING MISSION ORDERS", "New directive received. Review and acknowledge.", Severity.Notice, 7, "radio", EventEffect.Banner, 0, "Incoming mission orders."),
        new EventDef("deep_signal", "DEEP-SPACE SIGNAL DETECTED", "Narrowband emission, origin beyond catalogued range.", Severity.Notice, 9, "ping", EventEffect.Pulse, 0, "Deep space signal detected."),
        new EventDef("orbital_alert", "ORBITAL CONJUNCTION WARNING", "Close approach predicted. Screening manoeuvre options.", Severity.Warning, 10, "triple", EventEffect.Pulse, 0, "Orbital conjunction warning.", "orbital"),
        new EventDef("quantum_anomaly", "EXPERIMENTAL ANOMALY", "Decoherence spike and field instability detected.", Severity.Warning, 10, "surge", EventEffect.Glitch, 0, "Experimental anomaly detected.", "quantum"),
        new EventDef("unknown_tx", "UNKNOWN TRANSMISSION DETECTED", "Narrowband repeating signal. Origin unresolved.", Severity.Warning, 30, "tone", EventEffect.Glitch, 0, "Unknown transmission detected.", "commarray"),
        new EventDef("medical_emergency", "MEDICAL EMERGENCY (SIMULATED)", "Crew member vital signs critical. Response team to bay.", Severity.Critical, 14, "alarm", EventEffect.Flash, 0, "Medical emergency in the medical bay.", "medical"),
        new EventDef("facility_breach", "FACILITY SECURITY BREACH", "Unauthorised entry in monitored zone. Response required.", Severity.Critical, 30, "alarm", EventEffect.Flash, 0, "Warning. Facility security breach.", "facility"),
    };

    public static EventDef? Get(string id) => All.FirstOrDefault(e => e.Id == id);
}

/// <summary>Runs cinematic events and sequences; raises callbacks the UI/audio hook into.</summary>
public sealed class EventEngine
{
    readonly Random _rng = new();
    readonly List<ActiveEvent> _active = new();
    public IReadOnlyList<ActiveEvent> Active => _active;

    public event Action<ActiveEvent>? Started;
    public event Action<ActiveEvent>? Ended;

    // sequence playback
    CinematicSequence? _seq; int _seqIdx; double _seqTimer;
    public string? PlayingSequence => _seq?.Name;

    double _autoTimer = 12;
    public string CurrentPreset { get; set; } = "";
    public bool Auto { get; set; } = true;
    public double Frequency { get; set; } = 1.0;

    public ActiveEvent? Primary => _active.Count > 0 ? _active[^1] : null;

    public ActiveEvent Trigger(string id, double? duration = null)
    {
        var def = EventCatalog.Get(id) ?? EventCatalog.All[0];
        _active.RemoveAll(a => a.Def.Id == def.Id);
        var ev = new ActiveEvent { Def = def, Duration = duration ?? def.DefaultDuration };
        _active.Add(ev);
        Started?.Invoke(ev);
        return ev;
    }

    public void Clear()
    {
        foreach (var a in _active.ToArray()) Ended?.Invoke(a);
        _active.Clear(); _seq = null;
    }

    public void PlaySequence(CinematicSequence seq)
    {
        if (seq.Steps.Count == 0) return;
        _seq = seq; _seqIdx = 0; _seqTimer = 0;
    }

    public void Update(double dt)
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var a = _active[i];
            a.Elapsed += dt;
            if (a.Elapsed >= a.Duration) { _active.RemoveAt(i); Ended?.Invoke(a); }
        }

        if (_seq != null)
        {
            _seqTimer -= dt;
            if (_seqTimer <= 0)
            {
                if (_seqIdx >= _seq.Steps.Count) _seq = null;
                else
                {
                    var step = _seq.Steps[_seqIdx++];
                    Trigger(step.EventId);
                    _seqTimer = Math.Max(0.5, step.DelaySeconds);
                }
            }
        }
        else if (Auto && Frequency > 0.01)
        {
            _autoTimer -= dt;
            if (_autoTimer <= 0)
            {
                var pool = EventCatalog.All.Where(e => e.Only == null || e.Only == CurrentPreset).ToList();
                var def = pool[_rng.Next(pool.Count)];
                Trigger(def.Id);
                _autoTimer = (14 + _rng.NextDouble() * 22) / Frequency;
            }
        }
    }
}
