using Blacksite.Core.Audio;
using Blacksite.Core.Configuration;
using Blacksite.Core.Events;
using Blacksite.Core.Presets;
using Blacksite.Core.Simulation;
using Blacksite.Core.Simulation.Domain;
using Blacksite.Core.Themes;
using Xunit;

namespace Blacksite.Tests;

public class CoreTests
{
    [Fact]
    public void Catalog_has_ten_unique_presets_with_palettes()
    {
        Assert.Equal(15, PresetCatalog.All.Count);
        Assert.Equal(15, PresetCatalog.All.Select(p => p.Id).Distinct().Count());
        Assert.All(PresetCatalog.All, p => Assert.NotEqual(p.Palette.Background, p.Palette.Primary));
    }

    [Fact]
    public void Settings_roundtrip_through_versioned_envelope()
    {
        var s = new AppSettings { MasterVolume = 0.33, Graphics = GraphicsProfile.High, LastPresetId = "reactor" };
        s.CustomPresets.Add(new CustomPreset { Name = "MINE", BasePresetId = "cyber", Primary = "#FF00FF" });
        s.Sequences.Add(new CinematicSequence { Name = "S", Steps = { new SequenceStep { EventId = "breach", DelaySeconds = 3 } } });
        var json = ConfigStore.Serialize(s, "Test");
        Assert.Contains("BLACKSITE_CONFIG", json);
        var back = ConfigStore.Parse(json);
        Assert.Equal(0.33, back.MasterVolume);
        Assert.Equal(GraphicsProfile.High, back.Graphics);
        Assert.Equal("MINE", back.CustomPresets[0].Name);
        Assert.Equal("breach", back.Sequences[0].Steps[0].EventId);
    }

    [Fact]
    public void Parse_rejects_newer_version_and_foreign_files()
    {
        Assert.Throws<InvalidDataException>(() => ConfigStore.Parse("{\"format\":\"BLACKSITE_CONFIG\",\"version\":99,\"settings\":{}}"));
        Assert.Throws<InvalidDataException>(() => ConfigStore.Parse("{\"format\":\"OTHER\",\"version\":1}"));
    }

    [Fact]
    public void Parse_fills_in_missing_shortcuts_for_old_files()
    {
        var s = ConfigStore.Parse("{\"format\":\"BLACKSITE_CONFIG\",\"version\":1,\"settings\":{\"Shortcuts\":{\"Mute\":\"N\"}}}");
        Assert.Equal("N", s.Shortcuts["Mute"]);
        Assert.Equal("F11", s.Shortcuts["Fullscreen"]);
    }

    [Fact]
    public void Store_persists_and_recovers_from_corruption()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bs-test-" + Guid.NewGuid().ToString("N"));
        var store = new ConfigStore(dir);
        var s = new AppSettings { MasterVolume = 0.1 }; store.Save(s);
        s.MasterVolume = 0.2; store.Save(s);                       // creates backup of previous
        Assert.Equal(0.2, store.Load().MasterVolume);
        File.WriteAllText(store.SettingsPath, "{ not json");
        Assert.Equal(0.1, store.Load().MasterVolume);               // falls back to backup
        Directory.Delete(dir, true);
    }

    [Fact]
    public void Preset_pack_import_export()
    {
        var a = new AppSettings(); a.CustomPresets.Add(new CustomPreset { Id = "x1", Name = "ONE" });
        var json = ConfigStore.ExportPresets(a);
        var b = new AppSettings();
        Assert.Equal(1, ConfigStore.ImportPresets(b, json));
        Assert.Equal("ONE", b.CustomPresets.Single().Name);
        Assert.Throws<InvalidDataException>(() => ConfigStore.ImportPresets(b, "{\"format\":\"nope\"}"));
    }

    [Fact]
    public void RingBuffer_wraps_and_reports_range()
    {
        var r = new RingBuffer(4);
        for (int i = 1; i <= 6; i++) r.Push(i);
        Assert.Equal(4, r.Count);
        Assert.Equal(3, r[0]); Assert.Equal(6, r.Last);
        Assert.Equal((3f, 6f), r.Range());
    }

    [Fact]
    public void Simulation_is_deterministic_per_seed_and_stays_in_bounds()
    {
        var a = new SimulationEngine(5); var b = new SimulationEngine(5);
        for (int i = 0; i < 600; i++) { a.Step(0.1); b.Step(0.1); }
        Assert.Equal(a.CpuLoad, b.CpuLoad);
        Assert.InRange(a.CpuLoad, 0, 1); Assert.InRange(a.SignalStrength, 0, 1);
        Assert.InRange(a.Reactor.Stability, 0, 1); Assert.InRange(a.Ship.Oxygen, 0, 1);
        Assert.All(a.Satellites, s => { Assert.InRange(s.Lat, -90, 90); Assert.InRange(s.Lon, -180, 180); });
        Assert.Equal(SimulationEngine.HistoryLen, a.Cpu.Count);
    }

    [Fact]
    public void Reactor_values_are_coupled()
    {
        var sim = new SimulationEngine(1);
        sim.Reactor.Target = 110; for (int i = 0; i < 400; i++) sim.Step(0.1);
        double hot = sim.Reactor.Temperature;
        sim.Reactor.Cooling = true; sim.Reactor.Target = 30; for (int i = 0; i < 800; i++) sim.Step(0.1);
        Assert.True(sim.Reactor.Temperature < hot - 100);
        sim.Reactor.Scram = true; for (int i = 0; i < 600; i++) sim.Step(0.1);
        Assert.True(sim.Reactor.Power < 5);
    }

    [Fact]
    public void Firewall_blocks_most_threats()
    {
        var sim = new SimulationEngine(3) { FirewallActive = true };
        for (int i = 0; i < 40; i++) sim.SpawnThreat();
        Assert.True(sim.Threats.Count(t => t.Blocked) > 20);
    }

    [Fact]
    public void Synth_produces_audible_non_clipping_sounds()
    {
        foreach (var n in Synth.Names)
        {
            var d = Synth.ByName(n);
            Assert.NotEmpty(d);
            Assert.True(d.Max(Math.Abs) > 0.02f, n + " is silent");
            Assert.All(d, v => Assert.False(float.IsNaN(v)));
        }
        foreach (var p in Enum.GetValues<SoundProfile>()) Assert.True(Synth.Ambient(p).Max(Math.Abs) > 0.02f);
    }

    [Fact]
    public void Ambient_loops_tile_seamlessly_and_are_not_hissy()
    {
        foreach (var p in Enum.GetValues<SoundProfile>())
        {
            var b = Synth.Ambient(p);
            Assert.True(Math.Abs(b[0] - b[^1]) < 0.02f, $"{p}: seam jump {Math.Abs(b[0] - b[^1])}");
            double rms = Math.Sqrt(b.Average(v => (double)v * v)), drms = Math.Sqrt(Enumerable.Range(1, b.Length - 1).Average(i => Math.Pow(b[i] - b[i - 1], 2)));
            Assert.True(rms > 0.02, $"{p}: too quiet");
            Assert.True(drms / rms < 0.5, $"{p}: hiss ratio {drms / rms:0.00}");   // pad energy sits at low frequencies (diff/signal ~ 2*pi*f/fs)
            Assert.True(b.Max(Math.Abs) <= 0.5f);
        }
    }

    [Fact]
    public void One_shots_start_and_end_silent_to_avoid_clicks()
    {
        foreach (var n in Synth.Names) { var d = Synth.ByName(n); Assert.True(Math.Abs(d[0]) < 0.02f && Math.Abs(d[^1]) < 0.02f, $"{n} clicks: {d[0]} / {d[^1]}"); }
    }

    [Fact]
    public void Heavy_mixing_never_clips_or_produces_nan()
    {
        var s = new AppSettings { MasterVolume = 1, UiVolume = 1, AlarmVolume = 1, EventVolume = 1, AmbientVolume = 1 }; var e = new AudioEngine(s); e.Warm();
        for (int i = 0; i < 40; i++) { e.Play("alarm", SoundCategory.Alarm); e.Play("defib", SoundCategory.Event); e.Play("surge", SoundCategory.Event); }
        e.SetProfile(SoundProfile.Reactor); Thread.Sleep(600);
        var buf = new float[4096]; float peak = 0;
        for (int k = 0; k < 40; k++) { e.Mix(buf, buf.Length); foreach (var v in buf) { Assert.False(float.IsNaN(v)); peak = Math.Max(peak, Math.Abs(v)); } }
        Assert.True(peak <= 1.0f && peak > 0.2f, $"peak {peak}");
    }

    [Fact]
    public void Mixer_plays_fades_and_respects_mute()
    {
        var s = new AppSettings();
        var e = new AudioEngine(s);
        var buf = new float[2048]; e.Warm();
        e.Play("alarm", SoundCategory.Alarm);
        e.Mix(buf, buf.Length);
        Assert.True(buf.Max(Math.Abs) > 0.01f);
        s.Muted = true; for (int k = 0; k < 40; k++) e.Mix(buf, buf.Length);   // let the reverb tail ring out
        Assert.True(buf.Max(Math.Abs) < 1e-4f);
    }

    [Fact]
    public void Event_engine_runs_events_and_sequences()
    {
        var ev = new EventEngine { Auto = false };
        var started = new List<string>(); ev.Started += a => started.Add(a.Def.Id);
        ev.Trigger("breach", 1);
        Assert.Single(ev.Active);
        for (int i = 0; i < 30; i++) ev.Update(0.05);
        Assert.Empty(ev.Active);
        ev.PlaySequence(new CinematicSequence { Steps = { new SequenceStep { EventId = "lockdown", DelaySeconds = 1 }, new SequenceStep { EventId = "sat_acquired", DelaySeconds = 1 } } });
        for (int i = 0; i < 100; i++) ev.Update(0.05);
        Assert.Contains("lockdown", started); Assert.Contains("sat_acquired", started);
        Assert.Equal(17, EventCatalog.All.Count);
        Assert.Contains(EventCatalog.All, e => e.Id == "unknown_tx" && e.Only == "commarray");
        Assert.Contains(EventCatalog.All, e => e.Id == "facility_breach" && e.Only == "facility");
    }

    [Fact]
    public void Orbital_physics_is_consistent()
    {
        var s = new OrbitalSim(); var o = s.Objects.First(x => x.Type == OrbType.Active);
        double v0 = o.Velocity, p0 = o.Period;
        s.Manoeuvre(s.Objects.IndexOf(o), +400); for (int i = 0; i < 1200; i++) s.Step(0.1);
        Assert.True(o.Alt > 0 && Math.Abs(o.Alt - o.AltTarget) < 5);
        Assert.True(o.Period > p0 && o.Velocity < v0);                       // higher orbit: longer period, lower speed (Kepler)
        Assert.InRange(o.Velocity * o.Period / (2 * Math.PI), OrbitalSim.Re + o.Alt - 1, OrbitalSim.Re + o.Alt + 1);
        var (la, lo) = s.Predict(o, 0); Assert.InRange(Math.Abs(la - o.Lat), 0, 0.01); Assert.InRange(lo, -180, 180);
        var i2 = s.Objects.IndexOf(s.Objects.First(x => x.Station >= 0));
        s.Loss(i2); s.Step(0.1); for (int i = 0; i < 100; i++) s.Step(0.1);
        Assert.Equal("SIGNAL LOST", s.Objects[i2].Status); Assert.True(s.Objects[i2].Snr < 1);
        s.Restore(i2); for (int i = 0; i < 100; i++) s.Step(0.1);
        Assert.NotEqual("SIGNAL LOST", s.Objects[i2].Status);
    }

    [Fact]
    public void Quantum_controls_drive_telemetry()
    {
        var q = new QuantumSim(); double stab0 = q.Stability;
        q.Cooling = false; for (int i = 0; i < 1500; i++) q.Step(0.1);
        Assert.True(q.ProcTempMk > 20);                                      // no cooling -> warmer
        Assert.True(q.Coherence < 180); q.ToggleCooling();
        q.Reset(0); q.Field = 10; q.Collide(); for (int i = 0; i < 100; i++) q.Step(0.1);
        Assert.True(q.EventCount > 0 && q.Events.Any());
        var strong = q.Events.SelectMany(e => e.Tracks).Where(t => t.Charge != 0).Average(t => Math.Abs(t.Curvature));
        q.Reset(0); q.Field = 1; q.Collide(); for (int i = 0; i < 100; i++) q.Step(0.1);
        var weak = q.Events.SelectMany(e => e.Tracks).Where(t => t.Charge != 0).Average(t => Math.Abs(t.Curvature));
        Assert.True(strong > weak);                                          // stronger field curves tracks more
        q.TriggerAnomaly(); for (int i = 0; i < 40; i++) q.Step(0.1);
        Assert.True(q.Stability < stab0);
        q.EmergencyShutdown(); for (int i = 0; i < 400; i++) q.Step(0.1);
        Assert.True(q.ContainmentField < 0.5 && !q.Running);
    }

    [Fact]
    public void Comms_link_budget_follows_distance_and_loss()
    {
        var c = new CommsSim(); var near = c.Craft.OrderBy(x => x.DistAu).First(); var far = c.Craft.OrderByDescending(x => x.DistAu).First();
        for (int i = 0; i < 200; i++) c.Step(0.2);
        Assert.True(near.RxDbm > far.RxDbm);
        Assert.Equal(near.DistAu * 499.0048, near.LightTimeS, 3);
        int idx = c.Craft.IndexOf(near); c.Loss(idx); for (int i = 0; i < 60; i++) c.Step(0.2);
        Assert.Equal("SIGNAL LOST", near.Status);
        c.Recover(idx); for (int i = 0; i < 100; i++) c.Step(0.2);
        Assert.NotEqual("SIGNAL LOST", near.Status);
        var pre = new double[CommsSim.Bins]; c.TriggerUnknown();
        for (int i = 0; i < 200; i++) c.Step(0.1);                           // 20 s -> phase >= 7
        Assert.True(c.UnkPhase >= 7); Assert.Contains(c.Anomalies, a => a.Id == "UNK-0001");
        for (int i = 0; i < 150; i++) c.Step(0.1);
        Assert.False(c.UnknownActive); Assert.Contains(c.Log, l => l.Contains("FINAL MESSAGE"));
    }

    [Fact]
    public void Medical_air_drives_patient_and_emergency_oxygen_helps()
    {
        var m = new MedicalSim(); double spo0 = m.Spo2;
        m.CabinChange("leak", 0); m.CabinChange("o2", -4); for (int i = 0; i < 1500; i++) m.Step(0.1);
        Assert.True(m.O2 < 20.4 || m.Pressure < 100);
        m.Reset(0); m.Emergency(); for (int i = 0; i < 200; i++) m.Step(0.1);
        Assert.NotEqual(Cond.Stable, m.Condition); Assert.True(m.Alert >= 1);
        while (m.Condition != Cond.Arrest) m.Emergency();
        for (int i = 0; i < 300; i++) m.Step(0.1);
        Assert.True(m.Hr < 5 && m.Sys < 5 && m.Alert == 4);
        m.Recover(); for (int i = 0; i < 500; i++) m.Step(0.1);
        Assert.True(m.Hr > 40 && m.Spo2 > 85);
        Assert.InRange(m.Ecg.Count, 100, 700);
        Assert.Contains(m.Timeline, e => e.Text.Contains("DEFIBRILLATION"));
    }

    [Fact]
    public void Facility_power_loss_cascades_to_infrastructure()
    {
        var f = new FacilitySim(); f.GridFail();
        for (int i = 0; i < 6000; i++) f.Step(0.1);
        Assert.True(f.Battery < 0.05 || f.Rooms.Any(r => !r.Powered));      // battery drained, loads shed
        Assert.True(f.Voltage < 480);
        double supplyNoGen = f.Supply; f.StartGenerator(0); f.StartGenerator(1);
        for (int i = 0; i < 300; i++) f.Step(0.1);
        Assert.True(f.Supply > supplyNoGen + 1500);
        f.RestoreAll(); for (int i = 0; i < 600; i++) f.Step(0.1);
        Assert.All(f.Rooms, r => Assert.True(r.Powered)); Assert.True(f.GridOnline);
        f.TriggerBreach(); for (int i = 0; i < 160; i++) f.Step(0.1);
        Assert.True(f.BreachPhase >= 7); Assert.Contains(f.Incidents, i => i.Status != "RESOLVED"); Assert.Contains(f.Checkpoints, c => c.State == "LOCKED");
        f.ToggleLockdown(); f.Step(0.1); Assert.True(f.Lockdown && f.SecurityLevel == 5);
        f.ResolveBreach(); Assert.False(f.Lockdown); Assert.True(f.BreachPhase < 0);
        Assert.StartsWith("ACCESS DENIED", f.Access(f.People.FindIndex(p => p.Clearance == 1), f.Checkpoints.FindIndex(c => c.Zone >= 2)));
    }

    [Fact]
    public void Palette_helpers()
    {
        Assert.Equal(0xFF00DCFFu, Palette.Parse("#00DCFF", 0));
        Assert.Equal(7u, Palette.Parse("garbage", 7));
        Assert.Equal(0x7FFFFFFFu, Palette.WithAlpha(0xFFFFFFFF, 0.5));
    }
}
