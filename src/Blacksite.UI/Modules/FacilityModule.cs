using Avalonia;
using Blacksite.Core.Audio;
using Blacksite.Core.Simulation.Domain;
using Blacksite.Core.Themes;
using Blacksite.UI.Rendering;

namespace Blacksite.UI.Modules;

/// <summary>PRESET 15 – UNDERGROUND FACILITY COMMAND. A fictional classified installation.</summary>
public sealed class FacilityModule : DeckModule
{
    readonly FacilitySim _s = new();
    bool _report; double _alertT = 60; int _selPerson, _perFirst, _incFirst, _lastBreach = -1;
    Rect _ovR; readonly List<(double x, double y, double r)> _rock = new();
    double _camT;

    public override string[] ModuleActions => new[] { "LOCKDOWN", "GENERATOR", "GRID", "FAIL", "RESTORE", "ACCESS", "BREACH", "RESOLVE", "REPORT" };

    protected override void OnInit() { var r = new Random(3); for (int i = 0; i < 260; i++) _rock.Add((r.NextDouble(), r.NextDouble(), 0.4 + r.NextDouble() * 1.4)); }

    protected override void BuildPanels()
    {
        Def("overview", "A  FACILITY OVERVIEW — CROSS-SECTION", Zone.Main, 1, DrawOverview);
        Def("security", "B  FACILITY SECURITY", Zone.Left, 1.3, DrawSecurity);
        Def("camera", "SECURITY CAMERA", Zone.Left, 1.1, DrawCamera);
        Def("room", "ROOM / SECTION DETAIL", Zone.Left, 1.0, DrawRoom);
        Def("env", "C  ENVIRONMENTAL CONTROL", Zone.Left, 1.2, DrawEnvironment);
        Def("power", "D  POWER DISTRIBUTION", Zone.Right, 1.9, DrawPower);
        Def("infra", "E  FACILITY INFRASTRUCTURE", Zone.Right, 1.1, DrawInfra);
        Def("people", "F  PERSONNEL MANAGEMENT", Zone.Right, 1.4, DrawPeople);
        Def("incidents", "G  INCIDENT MANAGEMENT", Zone.Right, 1.1, DrawIncidents);
        Def("term", "H  FACILITY OPERATIONS TERMINAL", Zone.Bottom, 1.7, DrawTerminal);
        Def("graphs", "POWER & ENVIRONMENT HISTORY", Zone.Bottom, 1.4, DrawGraphs);
    }

    // ---------------------------------------------------------------- actions
    protected override string? DoAction(string id)
    {
        string m = "";
        switch (id)
        {
            case "LOCKDOWN": m = _s.ToggleLockdown(); C.Audio.Play(_s.Lockdown ? "alarm" : "access", _s.Lockdown ? SoundCategory.Alarm : SoundCategory.Ui, 0.8f); if (_s.Lockdown) C.Audio.Play("mech", SoundCategory.Event); break;
            case "GENERATOR": { int i = !_s.Gen0On ? 0 : !_s.Gen1On ? 1 : 0; m = _s.StartGenerator(i); C.Audio.Play("surge", SoundCategory.Event); break; }
            case "GRID": m = _s.GridFail(); C.Audio.Play(_s.GridOnline ? "access" : "triple", _s.GridOnline ? SoundCategory.Ui : SoundCategory.Alarm); break;
            case "FAIL": { var ok = Enumerable.Range(0, _s.Systems.Count).Where(i => !_s.Systems[i].Failed).ToList(); m = ok.Count == 0 ? "ALL SYSTEMS ALREADY FAILED" : _s.Fail(ok[Rng.Next(ok.Count)]); C.Audio.Play("error", SoundCategory.Event); break; }
            case "RESTORE": m = _s.RestoreAll(); C.Audio.Play("access", SoundCategory.Event); break;
            case "ACCESS": { var cp = Rng.Next(_s.Checkpoints.Count); m = _s.Access(_selPerson, cp); C.Audio.Play(m.Contains("GRANTED") ? "access" : "error"); break; }
            case "BREACH": m = _s.TriggerBreach(); C.Events.Trigger("facility_breach"); break;
            case "RESOLVE": m = _s.ResolveBreach(); C.Events.Clear(); C.Audio.Play("chime", SoundCategory.Event); break;
            case "REPORT": _report = !_report; m = _report ? "FACILITY REPORT (FICTIONAL)" : "REPORT CLOSED"; C.Audio.Play("chime"); break;
        }
        return m;
    }

    protected override bool IsActionActive(string a) => a switch { "LOCKDOWN" => _s.Lockdown, "GENERATOR" => _s.Gen0On || _s.Gen1On, "GRID" => !_s.GridOnline, "BREACH" => _s.BreachPhase >= 0, "REPORT" => _report, _ => false };

    protected override void Tick(double dt, double anim)
    {
        _s.Step(dt); _camT += anim;
        if (_s.BreachPhase != _lastBreach)
        {
            _lastBreach = _s.BreachPhase;
            switch (_s.BreachPhase)
            {
                case 1: C.Audio.Play("alarm", SoundCategory.Alarm, 0.7f); break;
                case 3: C.Audio.Play("scan", SoundCategory.Event, 0.6f); break;
                case 4: C.Audio.Play("mech", SoundCategory.Event); C.Audio.Play("access", SoundCategory.Event, 0.6f); break;
                case 5: C.Audio.Play("deepbeep", SoundCategory.Event); break;
                case 6: C.Audio.Play("chime", SoundCategory.Event); C.Audio.Say("Response team deployed."); break;
                case 7: C.Audio.Play("triple", SoundCategory.Alarm, 0.6f); break;
            }
        }
        _alertT -= dt * Cfg.AlertFrequency;
        if (_alertT <= 0 && Cfg.AlertFrequency > 0.05) { _alertT = 80 + Rng.NextDouble() * 100; if (_s.BreachPhase < 0 && Rng.Next(3) == 0) DoAction("BREACH"); else if (_s.BreachPhase < 0) DoAction("FAIL"); }
    }

    static uint StatusColor(Gfx g, string s) => s switch { "NOMINAL" => 0xFF30D060, "OFFLINE" => 0xFF607080, "LOCKED" => g.P.Warning, "ALERT" => g.P.Warning, "BREACH" => 0xFFFF3030, _ => g.P.Primary };

    // ---------------------------------------------------------------- A: cross-section
    void DrawOverview(Gfx g, Rect r)
    {
        _ovR = r; g.AddHit("fc:ov", r);
        double surfH = r.Height * 0.075, bandH = (r.Height - surfH) / FacilitySim.Levels, left = r.X + g.U(112), width = r.Right - left - g.U(26);
        // sky + surface
        g.Fill(new Rect(r.X, r.Y, r.Width, surfH), 0xFF0A1420, 0.9);
        g.Line(r.X, r.Y + surfH, r.Right, r.Y + surfH, g.P.Secondary, 2, 0.9);
        g.Line(r.X + r.Width * 0.62, r.Y + surfH, r.X + r.Width * 0.62, r.Y + g.U(3), g.P.Text, 1, 0.5); g.Disc(r.X + r.Width * 0.62, r.Y + g.U(3), g.U(2), 0xFFFF3030, g.Pulse(1));
        g.Text("SURFACE  //  CLASSIFIED SITE (FICTIONAL)", r.X + g.U(4), r.Y + g.U(3), 7.5, g.P.Text, Align.Left, false, 0.6);
        // rock strata
        var rock = new Rect(r.X, r.Y + surfH, r.Width, r.Height - surfH); g.Fill(rock, 0xFF14120E, 0.9);
        if (g.Quality != Core.Configuration.GraphicsProfile.Low) foreach (var (x, y, rd) in _rock) g.Disc(rock.X + x * rock.Width, rock.Y + y * rock.Height, g.U(rd), 0xFF3A3224, 0.5);
        // shafts
        double sx1 = left - g.U(10), sx2 = r.Right - g.U(14);
        g.Fill(new Rect(sx1 - g.U(3), rock.Y, g.U(6), rock.Height), g.P.Primary, 0.12); g.Fill(new Rect(sx2 - g.U(3), rock.Y, g.U(6), rock.Height), g.P.Primary, 0.12);
        double ey = rock.Y + (0.5 + 0.5 * Math.Sin(AnimT * 0.4)) * (rock.Height - g.U(14)); g.Fill(new Rect(sx1 - g.U(4), ey, g.U(8), g.U(12)), g.P.Accent, 0.9);
        double ey2 = rock.Y + (0.5 + 0.5 * Math.Sin(AnimT * 0.31 + 2)) * (rock.Height - g.U(14)); g.Fill(new Rect(sx2 - g.U(4), ey2, g.U(8), g.U(12)), g.P.Accent, 0.9);
        var centers = new Dictionary<int, Point>();
        for (int lv = 0; lv < FacilitySim.Levels; lv++)
        {
            double by = rock.Y + lv * bandH; bool sel = lv == _s.SelLevel;
            var band = new Rect(r.X + g.U(2), by + g.U(1), r.Width - g.U(4), bandH - g.U(2));
            g.Fill(band, sel ? g.P.Primary : 0xFF000000, sel ? 0.08 : 0.22); g.Frame(band, g.P.Primary, 1, sel ? 0.7 : 0.2);
            string name = FacilitySim.LevelNames[lv];
            g.Text(name.Split(' ')[0], r.X + g.U(6), by + bandH / 2 - g.U(10), 10, sel ? g.P.Accent : g.P.Text, Align.Left, true);
            g.Text(string.Join(" ", name.Split(' ').Skip(1)), r.X + g.U(6), by + bandH / 2 + g.U(2), 6.5, g.P.Text, Align.Left, false, 0.6);
            g.AddHit("fc:lv:" + lv, new Rect(r.X, by, g.U(104), bandH));
            double cw = width / 5;
            for (int i = 0; i < _s.Rooms.Count; i++)
            {
                var rm = _s.Rooms[i]; if (rm.Level != lv) continue;
                var rc = new Rect(left + rm.Col * cw + g.U(3), by + g.U(4), cw - g.U(6), bandH - g.U(8)); bool rs = i == _s.SelRoom;
                uint col = StatusColor(g, rm.Status); bool breach = rm.Status == "BREACH";
                g.Fill(rc, col, breach ? 0.25 + 0.3 * g.Pulse(3) : 0.10); g.Frame(rc, col, rs ? 2 : 1, rs ? 1 : 0.6);
                if (rs) g.Brackets(rc.Inflate(g.U(2)), g.P.Accent, 7, 1.5);
                RoomIcon(g, rm, rc, col);
                g.Text(rm.Name.Length > 17 ? rm.Name[..17] : rm.Name, rc.X + g.U(3), rc.Y + g.U(2), 6.5, rs ? g.P.Accent : g.P.Text, Align.Left, rs, 0.9);
                if (rm.Occupancy > 0) for (int k = 0; k < Math.Min(6, (int)rm.Occupancy); k++) g.Disc(rc.Right - g.U(5) - k * g.U(4.5), rc.Bottom - g.U(5), g.U(1.5), 0xFFFFFFFF, 0.8);
                if (rm.Locked) g.Text("LOCK", rc.Right - g.U(3), rc.Y + g.U(2), 6, g.P.Warning, Align.Right, true);
                if (!rm.Powered) g.Line(rc.X, rc.Y, rc.Right, rc.Bottom, 0xFF607080, 1, 0.5);
                g.AddHit("fc:rm:" + i, rc); centers[i] = rc.Center;
            }
        }
        // power cables between consecutive levels
        for (int lv = 0; lv < FacilitySim.Levels - 1; lv++)
        {
            var a = _s.Rooms.Where(x => x.Level == lv).ToList(); var b = _s.Rooms.Where(x => x.Level == lv + 1).ToList(); if (a.Count == 0 || b.Count == 0) continue;
            double y1 = rock.Y + (lv + 1) * bandH - g.U(3), y2 = y1 + g.U(6), x = sx1 + g.U(0);
            bool live = a[0].Powered && b[0].Powered; g.Line(sx1 + g.U(6), y1, sx1 + g.U(6), y2, live ? g.P.Warning : 0xFF405060, 1.2, live ? 0.8 : 0.5);
            if (live && g.Quality != Core.Configuration.GraphicsProfile.Low) g.Disc(sx1 + g.U(6), y1 + ((AnimT * 3 + lv) % 1) * g.U(6), g.U(1.4), 0xFFFFFFFF, 0.9);
        }
        if (_s.Lockdown) { g.Frame(r.Deflate(g.U(2)), 0xFFFF3030, g.U(4), 0.2 + 0.25 * g.Pulse(1.6)); g.Text("FACILITY LOCKDOWN", r.Center.X, r.Y + g.U(3), 12, 0xFFFF5050, Align.Center, true, 0.6 + 0.4 * g.Pulse(2)); }
        if (_s.BreachPhase >= 1 && _s.BreachPhase < 8) g.Text($"SECURITY LEVEL {_s.SecurityLevel}  —  BREACH PHASE {_s.BreachPhase}/7", r.Right - g.U(6), r.Y + g.U(3), 9, 0xFFFF5050, Align.Right, true, 0.5 + 0.5 * g.Pulse(3));
        else g.Text($"SECURITY LEVEL {_s.SecurityLevel}", r.Right - g.U(6), r.Y + g.U(3), 8.5, _s.SecurityLevel > 1 ? g.P.Warning : g.P.Primary, Align.Right, true);
    }

    void RoomIcon(Gfx g, Room rm, Rect rc, uint col)
    {
        double cx = rc.Center.X, cy = rc.Center.Y + g.U(3), s = Math.Min(rc.Width, rc.Height) * 0.28;
        switch (rm.Type)
        {
            case "REACTOR": g.Plasma(cx, cy, s * 1.5, rm.Powered ? g.P.Warning : 0xFF405060, 4, rm.Powered ? 0.9 : 0.2); g.Circle(cx, cy, s * 1.1, col, 1.2, 0.8); break;
            case "POWER": g.Poly(new[] { new Point(cx + s * 0.2, cy - s), new Point(cx - s * 0.4, cy + s * 0.1), new Point(cx, cy + s * 0.1), new Point(cx - s * 0.2, cy + s) }, col, 1.6, 0.9); break;
            case "DATA": for (int i = 0; i < 3; i++) g.Frame(new Rect(cx - s + i * s * 0.75, cy - s * 0.8, s * 0.6, s * 1.6), col, 1, 0.7); break;
            case "MED": g.Fill(new Rect(cx - s * 0.15, cy - s * 0.7, s * 0.3, s * 1.4), col, 0.8); g.Fill(new Rect(cx - s * 0.7, cy - s * 0.15, s * 1.4, s * 0.3), col, 0.8); break;
            case "LAB": g.Circle(cx, cy, s * 0.7, col, 1.2, 0.8); g.Line(cx - s, cy, cx + s, cy, col, 1, 0.6); break;
            case "SEC": g.Frame(new Rect(cx - s * 0.7, cy - s * 0.8, s * 1.4, s * 1.5), col, 1.2, 0.8); g.Line(cx - s * 0.7, cy, cx + s * 0.7, cy, col, 1, 0.6); break;
            case "COMMS": g.Line(cx, cy + s, cx, cy - s * 0.8, col, 1.4, 0.8); g.Arc(cx, cy - s * 0.8, s * 0.7, 200, 140, col, 1.2, 0.8); break;
            default: g.Frame(new Rect(cx - s * 0.8, cy - s * 0.6, s * 1.6, s * 1.2), col, 1, 0.5); break;
        }
    }

    // ---------------------------------------------------------------- B: security
    void DrawSecurity(Gfx g, Rect r)
    {
        double zh = g.U(46);
        for (int z = 0; z < 4; z++)
        {
            double w = (r.Width - g.U(3) * 3) / 4; var rc = new Rect(r.X + z * (w + g.U(3)), r.Y, w, zh);
            bool br = _s.BreachRoom >= 0 && _s.Rooms[_s.BreachRoom].Zone == z && _s.BreachPhase >= 2; uint col = _s.Lockdown || br ? 0xFFFF3030 : g.P.Primary;
            g.Fill(rc, col, br ? 0.2 + 0.2 * g.Pulse(3) : 0.07); g.Frame(rc, col, 1, 0.6);
            g.Text(FacilitySim.ZoneNames[z], rc.Center.X, rc.Y + g.U(3), 7.5, col, Align.Center, true);
            g.Text($"CLR {z + 1}+", rc.Center.X, rc.Y + g.U(14), 7, g.P.Text, Align.Center, false, 0.7);
            int pr = _s.People.Count(p => p.Duty == "ON DUTY" && _s.Rooms[p.Room].Zone == z); g.Text($"{pr} PERS", rc.Center.X, rc.Y + g.U(25), 7, g.P.Text, Align.Center, false, 0.8);
            g.Text(br ? "ALARM" : _s.Lockdown ? "LOCKED" : "SECURE", rc.Center.X, rc.Y + g.U(35), 6.5, br ? 0xFFFF5050 : _s.Lockdown ? g.P.Warning : 0xFF30D060, Align.Center, true);
        }
        var rows = _s.Checkpoints.Select((c, i) => new[] { c.Id, $"ZONE {c.Zone + 1}", c.State, _s.Rooms[c.RoomA].Name.Split(' ')[0], $"{(_s.Rooms[c.RoomA].Powered ? "CAM OK" : "CAM OFF")}", _s.Rooms[c.RoomA].Occupancy > 0 ? "MOTION" : "CLEAR" }).ToList();
        var t = new Rect(r.X, r.Y + zh + g.U(4), r.Width, r.Height - zh - g.U(4));
        g.Table(t, new[] { "CHECKPOINT", "ZONE", "DOOR", "AREA", "CAMERA", "SENSOR" }, new[] { 1.1, 0.9, 0.9, 1, 0.9, 0.9 }, rows, 0, -1, "cp:", (ri, ci) => ci == 2 ? (rows[ri][2] == "OPEN" ? 0xFF30D060 : rows[ri][2] == "LOCKED" ? 0xFFFF5050 : g.P.Warning) : ci == 5 && rows[ri][5] == "MOTION" ? g.P.Accent : null, 7.5);
    }

    // ---------------------------------------------------------------- camera
    void DrawCamera(Gfx g, Rect r)
    {
        var rm = _s.Rooms[_s.SelRoom]; double bh = g.U(g.Touch ? 24 : 18);
        g.Button("fc:cam-", new Rect(r.X, r.Y, g.U(30), bh), "<", false, true, null, 8); g.Button("fc:cam+", new Rect(r.X + g.U(34), r.Y, g.U(30), bh), ">", false, true, null, 8);
        g.Text($"CAM {rm.Id}  {rm.Name}", r.X + g.U(72), r.Y + g.U(4), 8, g.P.Accent, Align.Left, true);
        var v = new Rect(r.X, r.Y + bh + g.U(3), r.Width, r.Height - bh - g.U(3)); g.Fill(v, 0xFF03080A);
        using (g.Clip(v))
        {
            if (!rm.Powered) { for (int i = 0; i < 18; i++) g.Fill(new Rect(v.X, v.Y + Rng.NextDouble() * v.Height, v.Width, g.U(1 + Rng.NextDouble() * 2)), 0xFFFFFFFF, 0.08); g.Text("NO SIGNAL — POWER LOSS", v.Center.X, v.Center.Y - g.U(6), 10, g.P.Warning, Align.Center, true); }
            else
            {
                uint tint = 0xFF40FF90; double hz = v.Y + v.Height * 0.45;
                g.Fill(new Rect(v.X, hz, v.Width, v.Bottom - hz), tint, 0.05);
                for (int i = 0; i <= 8; i++) g.Line(v.Center.X + (i - 4) * v.Width * 0.02, hz, v.Center.X + (i - 4) * v.Width * 0.30, v.Bottom, tint, 1, 0.12);
                for (int i = 1; i < 6; i++) { double y = hz + (v.Bottom - hz) * Math.Pow(i / 6.0, 1.7); g.Line(v.X, y, v.Right, y, tint, 1, 0.1); }
                for (int i = 0; i < 4; i++) { double x = v.X + v.Width * (0.12 + i * 0.22); g.Fill(new Rect(x, hz - v.Height * (rm.Type == "DATA" ? 0.34 : 0.14), v.Width * 0.1, v.Height * (rm.Type == "DATA" ? 0.34 : 0.14)), tint, 0.16); g.Frame(new Rect(x, hz - v.Height * (rm.Type == "DATA" ? 0.34 : 0.14), v.Width * 0.1, v.Height * (rm.Type == "DATA" ? 0.34 : 0.14)), tint, 1, 0.4); }
                int n = (int)Math.Min(5, rm.Occupancy) + (_s.BreachRoom == _s.SelRoom && _s.BreachPhase >= 1 ? 1 : 0);
                for (int i = 0; i < n; i++)
                {
                    double px = v.X + v.Width * (0.15 + 0.7 * ((AnimT * 0.05 * (1 + i * 0.3) + i * 0.37) % 1)), py = hz + v.Height * 0.12 + i * v.Height * 0.04; bool intr = i == n - 1 && _s.BreachRoom == _s.SelRoom && _s.BreachPhase >= 1;
                    uint pc = intr ? 0xFFFF4040 : tint; g.Fill(new Rect(px - g.U(2.5), py - g.U(14), g.U(5), g.U(11)), pc, 0.8); g.Disc(px, py - g.U(17), g.U(2.6), pc, 0.9);
                    if (intr) { g.Frame(new Rect(px - g.U(8), py - g.U(24), g.U(16), g.U(28)), 0xFFFF4040, 1.5, 0.9); g.Text("INTRUDER", px - g.U(8), py - g.U(34), 7, 0xFFFF5050, Align.Left, true); }
                }
                for (int i = 0; i < 14; i++) g.Fill(new Rect(v.X + Rng.NextDouble() * v.Width, v.Y + Rng.NextDouble() * v.Height, g.U(1.2), g.U(1.2)), tint, 0.3);
            }
        }
        g.Text(DateTime.Now.ToString("HH:mm:ss"), v.X + g.U(4), v.Bottom - g.U(11), 7.5, 0xFF40FF90, Align.Left, false, 0.9); g.Disc(v.Right - g.U(8), v.Y + g.U(7), g.U(3), 0xFFFF3030, g.Pulse(1) > 0.4 ? 1 : 0.2); g.Text("REC", v.Right - g.U(14), v.Y + g.U(2), 7, 0xFFFF5050, Align.Right, true);
        g.Frame(v, g.P.Primary, 1, 0.4);
    }

    // ---------------------------------------------------------------- room detail
    void DrawRoom(Gfx g, Rect r)
    {
        var rm = _s.Rooms[_s.SelRoom]; var kv = new (string, string, uint?)[]
        {
            ("ROOM", rm.Id, null), ("NAME", rm.Name, g.P.Accent), ("LEVEL", FacilitySim.LevelNames[rm.Level].Split(' ')[0], null), ("ZONE", FacilitySim.ZoneNames[rm.Zone], null),
            ("STATUS", rm.Status, StatusColor(g, rm.Status)), ("POWER BUS", $"BUS {(char)('A' + rm.Bus)}", null), ("LOAD", rm.Powered ? $"{rm.LoadKw * _s.BusAlloc[rm.Bus]:0} KW" : "0 KW", null), ("PRIORITY", "P" + rm.Priority, null),
            ("TEMPERATURE", $"{rm.Temp:0.0}°C", rm.Temp > 32 ? g.P.Warning : null), ("OCCUPANCY", $"{rm.Occupancy:0}", null),
        };
        double used = g.KvGrid(r, kv, r.Width > g.U(240) ? 2 : 1, 8.5);
        double by = r.Y + used + g.U(4), bh = Math.Min(g.U(g.Touch ? 28 : 21), r.Bottom - by);
        if (bh > g.U(12)) { double bw = (r.Width - g.U(3)) / 2; g.Button("fc:lock", new Rect(r.X, by, bw, bh), rm.Locked ? "UNLOCK ROOM" : "LOCK ROOM", rm.Locked, true, g.P.Warning, 7.5); g.Button("fc:pow", new Rect(r.X + bw + g.U(3), by, bw, bh), rm.Powered ? "CUT POWER" : "RESTORE POWER", !rm.Powered, true, null, 7.5); }
    }

    // ---------------------------------------------------------------- C: environment
    void DrawEnvironment(Gfx g, Rect r)
    {
        var kv = new (string, string, uint?)[]
        {
            ("LEVEL", FacilitySim.LevelNames[_s.SelLevel].Split(' ')[0], null), ("TEMPERATURE", $"{_s.EnvTemp:0.0}°C", _s.EnvTemp > 30 ? g.P.Warning : null), ("HUMIDITY", $"{_s.EnvHum:0}%", null), ("PRESSURE", $"{_s.EnvPress:0.0} KPA", null),
            ("OXYGEN", $"{_s.EnvO2:0.0}%", _s.EnvO2 < 19.5 ? g.P.Warning : null), ("CO2", $"{_s.EnvCo2:0} PPM", _s.EnvCo2 > 1500 ? g.P.Warning : null), ("AIRFLOW", $"{_s.Airflow:0}%", _s.Airflow < 60 ? g.P.Warning : null), ("WATER SUPPLY", $"{_s.Water * 100:0}%", _s.Water < 0.5 ? g.P.Warning : null),
        };
        double used = g.KvGrid(new Rect(r.X, r.Y, r.Width, r.Height), kv, 2, 8.5);
        double y = r.Y + used + g.U(3), bh = Math.Min(g.U(g.Touch ? 28 : 21), r.Height - used - g.U(3));
        string alarms = _s.EnvTemp > 30 ? "HIGH TEMPERATURE" : _s.EnvCo2 > 1500 ? "CO2 HIGH" : _s.EnvO2 < 19.5 ? "LOW OXYGEN" : "NO ALARMS";
        if (bh > g.U(12)) { g.Text("ALARMS: " + alarms, r.X, y, 8, alarms == "NO ALARMS" ? 0xFF30D060 : g.P.Warning, Align.Left, true); }
        double by = r.Bottom - bh; if (by > y + g.U(10))
        {
            double bw = (r.Width - g.U(3) * 3) / 4;
            g.Button("fc:cool-", new Rect(r.X, by, bw, bh), "COOL -", false, true, null, 7.5); g.Button("fc:cool+", new Rect(r.X + (bw + g.U(3)), by, bw, bh), "COOL +", false, true, null, 7.5);
            g.Button("fc:vent-", new Rect(r.X + 2 * (bw + g.U(3)), by, bw, bh), "VENT -", false, true, null, 7.5); g.Button("fc:vent+", new Rect(r.X + 3 * (bw + g.U(3)), by, bw, bh), "VENT +", false, true, null, 7.5);
            g.Text($"COOLING {_s.CoolSet * 100:0}%   VENTILATION {_s.VentSet * 100:0}%", r.X, by - g.U(11), 7.5, g.P.Text, Align.Left, false, 0.7);
        }
    }

    // ---------------------------------------------------------------- D: power
    void DrawPower(Gfx g, Rect r)
    {
        double dh = r.Height * 0.55, w = r.Width;
        var d = new Rect(r.X, r.Y, w, dh);
        // sources column
        var src = new (string name, double kw, bool on, string id)[] { ("EXTERNAL GRID", _s.GridOnline ? _s.GridKw : 0, _s.GridOnline, "fc:grid"), ("GENERATOR 1", _s.GenKw0, _s.Gen0On, "fc:g0"), ("GENERATOR 2", _s.GenKw1, _s.Gen1On, "fc:g1"), ("BATTERY BANK", _s.Battery * 1000, _s.Battery > 0.01, "") };
        double sh = d.Height / 4, sw = w * 0.24; var sp = new Point[4];
        for (int i = 0; i < 4; i++)
        {
            var rc = new Rect(d.X, d.Y + i * sh + g.U(2), sw, sh - g.U(4)); uint c = src[i].on ? (i == 3 ? g.P.Secondary : g.P.Warning) : 0xFF607080;
            g.Fill(rc, c, 0.12); g.Frame(rc, c, 1, 0.7); g.Text(src[i].name, rc.X + g.U(3), rc.Y + g.U(2), 7, g.P.Text, Align.Left, true, 0.9);
            g.Text(i == 3 ? $"{_s.Battery * 100:0}%" : $"{src[i].kw:0} KW", rc.X + g.U(3), rc.Y + g.U(12), 9, c, Align.Left, true);
            if (i == 1 || i == 2) g.Text($"FUEL {(i == 1 ? _s.Fuel0 : _s.Fuel1) * 100:0}%", rc.Right - g.U(3), rc.Y + g.U(2), 6.5, g.P.Text, Align.Right, false, 0.7);
            if (src[i].id != "") g.AddHit(src[i].id, rc, HitKind.Button);
            sp[i] = new Point(rc.Right, rc.Center.Y);
        }
        // main bus
        double bx = d.X + w * 0.36; g.Fill(new Rect(bx - g.U(3), d.Y, g.U(6), d.Height), _s.Supply > 0 ? g.P.Warning : 0xFF607080, 0.7); g.Text("MAIN BUS", bx, d.Y - g.U(1), 6.5, g.P.Text, Align.Center, false, 0.6);
        for (int i = 0; i < 4; i++) { bool on = src[i].on; g.Line(sp[i].X, sp[i].Y, bx - g.U(3), sp[i].Y, on ? g.P.Warning : 0xFF405060, 1.5, 0.8); if (on && g.Quality != Core.Configuration.GraphicsProfile.Low) g.Disc(sp[i].X + ((AnimT * 2 + i * 0.3) % 1) * (bx - sp[i].X), sp[i].Y, g.U(1.6), 0xFFFFFFFF, 0.9); }
        // distribution buses
        double bw = w * 0.56; double bsx = d.X + w * 0.42; double bsh = d.Height / 4;
        for (int b = 0; b < 4; b++)
        {
            var rc = new Rect(bsx, d.Y + b * bsh + g.U(2), bw - g.U(2), bsh - g.U(4));
            double frac = Math.Clamp(_s.BusLoad[b] / Math.Max(1, _s.Supply * 0.4 + 200), 0, 1.2); bool over = frac > 0.95;
            g.Line(bx + g.U(3), rc.Center.Y, rc.X, rc.Center.Y, g.P.Warning, 1.2, 0.7);
            g.Fill(rc, g.P.Primary, 0.07); g.Frame(rc, over ? g.P.Warning : g.P.Primary, 1, 0.6);
            g.Text($"BUS {(char)('A' + b)}  {_s.BusLoad[b]:0} KW  ({_s.BusAlloc[b] * 100:0}%)", rc.X + g.U(3), rc.Y + g.U(2), 7.5, over ? g.P.Warning : g.P.Text, Align.Left, true);
            g.Fill(new Rect(rc.X + g.U(3), rc.Bottom - g.U(7), (rc.Width - g.U(6)) * Math.Min(1, frac), g.U(4)), over ? g.P.Warning : g.P.Primary, 0.75);
            g.Button("fc:bus-:" + b, new Rect(rc.Right - g.U(40), rc.Y + g.U(2), g.U(18), g.U(14)), "-", false, true, null, 7); g.Button("fc:bus+:" + b, new Rect(rc.Right - g.U(20), rc.Y + g.U(2), g.U(18), g.U(14)), "+", false, true, null, 7);
        }
        // readouts
        var kv = new (string, string, uint?)[]
        {
            ("SUPPLY", $"{_s.Supply:0} KW", null), ("DEMAND", $"{_s.Demand:0} KW", _s.Demand > _s.Supply ? g.P.Warning : null), ("VOLTAGE", $"{_s.Voltage:0} V", _s.Voltage < 440 ? g.P.Warning : null), ("CURRENT", $"{_s.Current:0} A", null),
            ("FREQUENCY", $"{_s.Freq:0.00} HZ", _s.Freq < 59 ? g.P.Warning : null), ("BATTERY", $"{_s.Battery * 100:0}%", _s.Battery < 0.2 ? g.P.Warning : null), ("REDUNDANCY", (_s.GridOnline ? 1 : 0) + (_s.Gen0On ? 1 : 0) + (_s.Gen1On ? 1 : 0) >= 2 ? "N+1" : "N", (_s.GridOnline ? 1 : 0) + (_s.Gen0On ? 1 : 0) + (_s.Gen1On ? 1 : 0) >= 2 ? 0xFF30D060 : g.P.Warning), ("LOADS SHED", _s.Rooms.Count(x => !x.Powered).ToString(), _s.Rooms.Any(x => !x.Powered) ? g.P.Warning : null),
        };
        g.KvGrid(new Rect(r.X, r.Y + dh + g.U(4), r.Width, r.Height - dh - g.U(4)), kv, 4, 8);
    }

    // ---------------------------------------------------------------- E, F, G
    void DrawInfra(Gfx g, Rect r)
    {
        double lh = Math.Min(g.U(g.Touch ? 24 : 19), r.Height / _s.Systems.Count);
        for (int i = 0; i < _s.Systems.Count; i++)
        {
            var s = _s.Systems[i]; var rc = new Rect(r.X, r.Y + i * lh, r.Width, lh);
            g.Led(rc.X + g.U(5), rc.Center.Y, s.State == "NOMINAL" ? 1 : s.State == "DEGRADED" ? 2 : 3);
            g.Text(s.Name, rc.X + g.U(13), rc.Y + (lh - g.U(9)) / 2, 7.5, g.P.Text, Align.Left, false, 0.95);
            g.Bar(new Rect(rc.X + r.Width * 0.46, rc.Y + g.U(3), r.Width * 0.3, lh - g.U(6)), s.Health, s.Failed ? 0xFFFF4040 : g.P.Primary);
            g.Text(s.State, rc.Right - g.U(2), rc.Y + (lh - g.U(9)) / 2, 7, s.State == "NOMINAL" ? 0xFF30D060 : g.P.Warning, Align.Right, true);
            g.AddHit("fc:sys:" + i, rc, HitKind.Button);
        }
    }

    void DrawPeople(Gfx g, Rect r)
    {
        var rows = _s.People.Select(p => new[] { p.Id, p.Name, p.Dept, p.Clearance.ToString(), _s.Rooms[p.Room].Name, p.Duty, p.Task }).ToList();
        int vis = g.TableVisibleRows(r, 7.5); _perFirst = Math.Clamp(_perFirst, 0, Math.Max(0, rows.Count - vis));
        g.Table(r, new[] { "ID", "NAME", "DEPT", "CLR", "LOCATION", "DUTY", "TASK" }, new[] { 0.8, 1.2, 1, 0.45, 1.6, 0.9, 1.3 }, rows, _perFirst, _selPerson, "pp:", (ri, ci) => ci == 5 && rows[ri][5] != "ON DUTY" ? g.P.Warning : ci == 3 && _s.People[ri].Clearance >= 4 ? g.P.Accent : null, 7.5);
    }

    void DrawIncidents(Gfx g, Rect r)
    {
        var rows = _s.Incidents.Select(i => new[] { i.Id, "P" + i.Priority, i.Location, $"{Math.Max(0, _s.Time - i.Detected):0}S", i.Status, i.Team, i.Desc }).ToList();
        double th = _s.BreachPhase >= 6 ? g.U(14) : 0;
        g.Table(new Rect(r.X, r.Y, r.Width, r.Height - th), new[] { "INCIDENT", "PRI", "LOCATION", "AGE", "STATUS", "TEAM", "DESCRIPTION" }, new[] { 0.9, 0.4, 1.3, 0.5, 0.9, 1.2, 2.6 }, rows, _incFirst, 0, "in:", (ri, ci) => ci == 4 ? (rows[ri][4] == "RESOLVED" ? 0xFF30D060 : rows[ri][4] == "CONTAINED" ? g.P.Accent : 0xFFFF5050) : null, 7.5);
        if (rows.Count == 0) g.Text("NO ACTIVE INCIDENTS", r.X + g.U(4), r.Y + g.U(24), 8, g.P.Text, Align.Left, false, 0.5);
        if (th > 0) g.Text($"RESPONSE: {_s.ResponseTeam}  ETA {_s.TeamEta:0}S  {(_s.BreachPhase == 7 ? "— AWAITING DECISION: LOCKDOWN / RESOLVE" : "")}", r.X, r.Bottom - th + g.U(2), 7.5, g.P.Warning, Align.Left, true);
    }

    void DrawTerminal(Gfx g, Rect r)
    {
        double lh = g.U(11.5); int max = Math.Max(1, (int)(r.Height / lh)); int start = Math.Max(0, _s.Terminal.Count - max);
        for (int i = start; i < _s.Terminal.Count; i++) { string l = _s.Terminal[i]; bool w = l.Contains("ALERT") || l.Contains("BREACH") || l.Contains("FAIL") || l.Contains("LOSS") || l.Contains("DENIED") || l.Contains("LOCKDOWN") || l.Contains("SHED"); g.Text(l, r.X, r.Y + (i - start) * lh, 7.5, w ? g.P.Warning : g.P.Text, Align.Left, w, 0.92); }
        if ((int)(AnimT * 2) % 2 == 0) g.Text("█", r.X, r.Y + Math.Min(_s.Terminal.Count - start, max - 1) * lh, 7.5, g.P.Primary);
    }

    void DrawGraphs(Gfx g, Rect r)
    {
        var rows = Layout.Rows(r, g.U(3), 1, 1); var a = Layout.Cols(rows[0], g.U(3), 1, 1); var b = Layout.Cols(rows[1], g.U(3), 1, 1);
        g.Graph(a[0], _s.SupplyH, g.P.Warning, 0, null, true, "SUPPLY", " kW"); g.Graph(a[1], _s.DemandH, g.P.Primary, 0, null, true, "DEMAND", " kW");
        g.Graph(b[0], _s.BattH, g.P.Secondary, 0, 100, true, "BATTERY", "%"); g.Graph(b[1], _s.TempH, g.P.Accent, null, null, true, "AVG TEMP", " °C");
    }

    protected override void AfterDraw(Gfx g, Rect area)
    {
        if (!_report) return;
        g.Fill(area, 0xFF000000, 0.74); g.AddHit("fc:report", area);
        double w = Math.Min(area.Width - g.U(10), g.U(600)), h = Math.Min(area.Height - g.U(10), g.U(280)); var rr = new Rect(area.Center.X - w / 2, area.Center.Y - h / 2, w, h);
        var inner = g.Panel(rr, "FACILITY REPORT — SIMULATED DATA"); var lines = _s.Report(); double lh = g.U(14);
        for (int i = 0; i < lines.Length; i++) g.Text(lines[i], inner.X, inner.Y + i * lh, 9, i == 0 ? g.P.Accent : g.P.Text, Align.Left, i == 0);
        g.Text("TAP TO CLOSE", inner.Right, inner.Bottom - g.U(11), 8, g.P.Text, Align.Right, false, 0.6);
    }

    // ---------------------------------------------------------------- input
    protected override string? PanelButton(string id)
    {
        var p = id.Split(':');
        switch (id)
        {
            case "fc:cam-": _s.SelRoom = (_s.SelRoom + _s.Rooms.Count - 1) % _s.Rooms.Count; _s.SelLevel = _s.Rooms[_s.SelRoom].Level; C.Audio.Play("tick"); return "CAMERA " + _s.Rooms[_s.SelRoom].Id;
            case "fc:cam+": _s.SelRoom = (_s.SelRoom + 1) % _s.Rooms.Count; _s.SelLevel = _s.Rooms[_s.SelRoom].Level; C.Audio.Play("tick"); return "CAMERA " + _s.Rooms[_s.SelRoom].Id;
            case "fc:lock": { var rm = _s.Rooms[_s.SelRoom]; rm.Locked = !rm.Locked; C.Audio.Play("mech"); return $"{rm.Name} {(rm.Locked ? "LOCKED" : "UNLOCKED")}"; }
            case "fc:pow": { var rm = _s.Rooms[_s.SelRoom]; rm.Powered = !rm.Powered; C.Audio.Play(rm.Powered ? "access" : "error"); return $"{rm.Name} {(rm.Powered ? "POWER RESTORED" : "POWER CUT")}"; }
            case "fc:cool-": C.Audio.Play("mech", SoundCategory.Ui, 0.6f); return _s.Adjust("cool", -0.1); case "fc:cool+": C.Audio.Play("mech", SoundCategory.Ui, 0.6f); return _s.Adjust("cool", 0.1);
            case "fc:vent-": C.Audio.Play("mech", SoundCategory.Ui, 0.6f); return _s.Adjust("vent", -0.1); case "fc:vent+": C.Audio.Play("mech", SoundCategory.Ui, 0.6f); return _s.Adjust("vent", 0.1);
            case "fc:grid": return DoAction("GRID"); case "fc:g0": C.Audio.Play("surge", SoundCategory.Event); return _s.StartGenerator(0); case "fc:g1": C.Audio.Play("surge", SoundCategory.Event); return _s.StartGenerator(1);
        }
        if (id.StartsWith("fc:bus")) { C.Audio.Play("deepbeep", SoundCategory.Ui, 0.7f); return _s.Allocate(int.Parse(p[2]), id[6] == '+' ? 0.1 : -0.1); }
        if (id.StartsWith("fc:sys:")) { var s = _s.Systems[int.Parse(p[2])]; if (s.Failed) { s.Failed = false; C.Audio.Play("access"); return s.Name + " RESTORED"; } C.Audio.Play("error"); return _s.Fail(int.Parse(p[2])); }
        return null;
    }

    protected override void PanelTap(Point p, string? id)
    {
        if (id == null) return;
        if (id == "fc:report") { _report = false; return; }
        if (id.StartsWith("fc:rm:")) { _s.SelRoom = int.Parse(id[6..]); _s.SelLevel = _s.Rooms[_s.SelRoom].Level; var rm = _s.Rooms[_s.SelRoom]; C.Toast($"{rm.Name} — {rm.Status}"); C.Audio.Play("deepbeep", SoundCategory.Ui, 0.7f); }
        else if (id.StartsWith("fc:lv:")) { _s.SelLevel = int.Parse(id[6..]); var first = _s.Rooms.FindIndex(x => x.Level == _s.SelLevel); if (first >= 0) _s.SelRoom = first; C.Toast(FacilitySim.LevelNames[_s.SelLevel]); C.Audio.Play("tick"); }
        else if (id.StartsWith("pp:")) { _selPerson = int.Parse(id[3..]); C.Toast($"{_s.People[_selPerson].Name} — CLEARANCE {_s.People[_selPerson].Clearance}"); C.Audio.Play("tick"); }
        else if (id.StartsWith("cp:")) { var r2 = _s.Access(_selPerson, int.Parse(id[3..])); C.Toast(r2, r2.Contains("DENIED")); C.Audio.Play(r2.Contains("GRANTED") ? "access" : "error"); }
    }

    protected override bool PanelDrag(Point p, Vector d, string? id)
    {
        if (id != null && id.StartsWith("pp:")) { _perFirst = Math.Max(0, _perFirst - (int)Math.Round(d.Y / 8)); return true; }
        return false;
    }

    protected override bool PanelWheel(Point p, double delta) { _perFirst = Math.Max(0, _perFirst - (int)Math.Round(delta * 2)); return false; }
}
