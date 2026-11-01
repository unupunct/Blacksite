using Avalonia;
using Blacksite.Core.Audio;
using Blacksite.Core.Simulation.Domain;
using Blacksite.UI.Rendering;

namespace Blacksite.UI.Modules;

/// <summary>PRESET 13 – DEEP SPACE COMMUNICATION ARRAY. Fictional radio-astronomy / deep-space comms facility.</summary>
public sealed class CommArrayModule : DeckModule
{
    readonly CommsSim _s = new();
    readonly Waterfall _wf = new(256, 110);
    double _wfT, _sweep, _alertT = 40, _blipT, _typed;
    int _lastPhase = -1, _trkFirst, _chFirst; bool _unkHandled;
    double _msgShow; string _incoming = "";
    Rect _specR;

    public override string[] ModuleActions => new[] { "SCAN", "TUNE-", "TUNE+", "DECODE", "UNKNOWN", "PLAYBACK", "CHANNEL", "LOSS", "RECOVER" };

    protected override void BuildPanels()
    {
        Def("spec", "A  SIGNAL SPECTRUM ANALYZER / WATERFALL", Zone.Main, 1, DrawSpectrum);
        Def("array", "B  RADIO TELESCOPE ARRAY", Zone.Left, 1.5, DrawArray);
        Def("track", "C  DEEP SPACE TRACKING", Zone.Left, 1.6, DrawTracking);
        Def("sky", "SKY CHART / ANTENNA POINTING", Zone.Left, 1.2, DrawSky);
        Def("term", "D  SIGNAL DECODING TERMINAL", Zone.Right, 1.3, DrawTerminal);
        Def("chan", "E  COMMUNICATION CHANNELS", Zone.Right, 1.4, DrawChannels);
        Def("intel", "F  SIGNAL INTELLIGENCE", Zone.Right, 1.1, DrawIntel);
        Def("mission", "G  DEEP SPACE MISSION CONTROL", Zone.Bottom, 1.6, DrawMission);
        Def("snr", "LINK BUDGET HISTORY", Zone.Bottom, 1, DrawGraphs);
        Def("log", "CONTACT HISTORY", Zone.Bottom, 1.1, DrawLog);
    }

    protected override string? DoAction(string id)
    {
        string m = ""; int c = _s.SelCraft;
        switch (id)
        {
            case "SCAN": m = _s.Scan(); C.Audio.Play("scan", SoundCategory.Ui); break;
            case "TUNE-": m = _s.Tune(-_s.SpanMhz / 4); C.Audio.Play("tick", SoundCategory.Ui, 0.8f); break;
            case "TUNE+": m = _s.Tune(_s.SpanMhz / 4); C.Audio.Play("tick", SoundCategory.Ui, 0.8f); break;
            case "DECODE": m = _s.StartDecode(); C.Audio.Play("handshake", SoundCategory.Event); break;
            case "UNKNOWN": C.Events.Trigger("unknown_tx"); m = "UNKNOWN TRANSMISSION SEQUENCE"; break;
            case "PLAYBACK": m = Playback(); break;
            case "CHANNEL": m = _s.SelectChannel(_s.SelChannel + 1); C.Audio.Play("deepbeep", SoundCategory.Ui, 0.8f); break;
            case "LOSS": m = _s.Loss(c); C.Audio.Play("triple", SoundCategory.Alarm); break;
            case "RECOVER": m = _s.Recover(c); C.Audio.Play("access", SoundCategory.Ui); break;
        }
        return m;
    }

    string Playback()
    {
        var cr = _s.Craft[_s.SelCraft];
        string[] msgs = { "TELEMETRY FRAME COMPLETE. ALL SUBSYSTEMS NOMINAL.", "IMAGE DATA PRODUCT 4412 RECEIVED (SIMULATED).", "ATTITUDE CORRECTION APPLIED. RESUMING CRUISE.", "RADIATION ENVIRONMENT WITHIN LIMITS. COMMENCING SURVEY." };
        _incoming = $"{cr.Id}: {msgs[Rng.Next(msgs.Length)]}"; _msgShow = 7; _typed = 0;
        _s.Decode.Add("INCOMING TRANSMISSION — " + _incoming); C.Audio.Play("tone", SoundCategory.Event, 0.7f); C.Audio.Play("static", SoundCategory.Event, 0.5f);
        return "PLAYING INCOMING TRANSMISSION";
    }

    protected override bool IsActionActive(string a) => a switch { "SCAN" => _s.Scanning, "DECODE" => _s.Decoding, "UNKNOWN" => _s.UnknownActive, "LOSS" => _s.Craft[_s.SelCraft].Loss, _ => false };

    protected override void Tick(double dt, double anim)
    {
        _s.Step(dt);
        bool unkEvt = C.Events.Active.Any(e => e.Def.Id == "unknown_tx");
        if (unkEvt && !_s.UnknownActive && !_unkHandled) { _unkHandled = true; _s.TriggerUnknown(); }
        if (!unkEvt) _unkHandled = false;
        double fast = _s.UnknownActive && _s.UnkPhase >= 1 && _s.UnkPhase < 5 ? 4 : 1;
        _sweep = (_sweep + anim * 70 * fast) % 360;
        _wfT += anim; if (_wfT > 0.1) { _wfT = 0; _wf.Push(_s.Spectrum, 3.0f); }
        // cinematic sequence audio / ambience control
        int ph = _s.UnknownActive ? _s.UnkPhase : -1;
        if (ph != _lastPhase)
        {
            _lastPhase = ph;
            switch (ph)
            {
                case 0: C.Audio.AmbientDuck = 0.1f; C.Audio.Play("static", SoundCategory.Event, 0.6f); break;
                case 1: C.Audio.Play("radarpulse", SoundCategory.Event); break;
                case 2: C.Audio.Play("deepbeep", SoundCategory.Event); break;
                case 3: C.Audio.Play("scan", SoundCategory.Event, 0.7f); break;
                case 4: C.Audio.Play("triple", SoundCategory.Alarm); break;
                case 5: C.Audio.Play("handshake", SoundCategory.Event); break;
                case 6: C.Audio.Play("tone", SoundCategory.Event); break;
                case 7: C.Audio.Play("tone", SoundCategory.Event); C.Audio.Play("tick", SoundCategory.Event); break;
                case 8: _msgShow = 9; _typed = 0; _incoming = _s.UnkMessage; C.Audio.Play("chime", SoundCategory.Event); C.Audio.Say("We are here. We have been listening."); break;
                case -1: C.Audio.AmbientDuck = 1f; break;
            }
        }
        if (_s.UnknownActive && _s.UnkPhase >= 1 && _s.UnkPhase < 5) { _blipT -= dt; if (_blipT <= 0) { _blipT = 0.8; C.Audio.Play("radarpulse", SoundCategory.Event, 0.3f); } }
        if (_s.UnknownActive && _s.UnkPhase == 7) { _blipT -= dt; if (_blipT <= 0) { _blipT = 0.6; C.Audio.Play(Rng.Next(2) == 0 ? "beep" : "deepbeep", SoundCategory.Event, 0.5f); } }
        _msgShow = Math.Max(0, _msgShow - dt); _typed += dt * 22;
        _alertT -= dt * Cfg.AlertFrequency;
        if (_alertT <= 0 && Cfg.AlertFrequency > 0.05) { _alertT = 70 + Rng.NextDouble() * 90; if (!_s.UnknownActive && Rng.Next(3) == 0) C.Events.Trigger("unknown_tx"); else DoActionQuiet(); }
    }

    void DoActionQuiet() { var c = Rng.Next(_s.Craft.Count); if (_s.Craft[c].Status == "LOCKED") { _s.Loss(c); C.Toast($"LOSS OF SIGNAL: {_s.Craft[c].Id}", true); C.Audio.Play("triple", SoundCategory.Alarm); _s.Craft[c].Acq = 0; } }

    // ---------------------------------------------------------------- A: spectrum + waterfall
    void DrawSpectrum(Gfx g, Rect r)
    {
        const float _gain = 3.0f;
        _specR = r; g.AddHit("cm:spec", r);
        double bh = g.U(g.Touch ? 24 : 19), bw = Math.Min(g.U(70), (r.Width * 0.56 - g.U(4) * 5) / 6);
        string[] ids = { "cm:t-", "cm:t+", "cm:s-", "cm:s+", "cm:scan", "cm:clr" }; string[] lab = { "TUNE -", "TUNE +", "SPAN -", "SPAN +", _s.Scanning ? "SCANNING" : "SCAN", "CLEAR" };
        for (int i = 0; i < 6; i++) g.Button(ids[i], new Rect(r.X + i * (bw + g.U(4)), r.Y, bw, bh), lab[i], i == 4 && _s.Scanning, true, null, bw < g.U(54) ? 6.5 : 7.5);
        g.Text($"CF {_s.CenterMhz:0.000} MHZ  SPAN {_s.SpanMhz:0.0}  RBW {(_s.SpanMhz / CommsSim.Bins * 1000):0.0}K", r.Right - g.U(2), r.Y + g.U(4), 8, g.P.Primary, Align.Right, true);
        double sh = (r.Height - bh - g.U(6)) * 0.4;
        var sp = new Rect(r.X, r.Y + bh + g.U(3), r.Width, sh);
        g.Spectrum(sp, _s.Spectrum, g.P.Primary, null, null, 8, true);
        double lo = _s.CenterMhz - _s.SpanMhz / 2, step = _s.SpanMhz / CommsSim.Bins;
        // noise floor line and markers
        double nf = sp.Bottom - 2 - (sp.Height - 6) * 0.12; g.Dc.DrawLine(g.Pn(g.P.Warning, 1, 0.5, g.U(3)), new Point(sp.X, nf), new Point(sp.Right, nf)); g.Text($"NOISE FLOOR {_s.NoiseFloorDbm:0.0} DBM", sp.X + g.U(3), nf - g.U(10), 7, g.P.Warning, Align.Left, false, 0.8);
        foreach (var ch in _s.Channels)
        {
            double x = sp.X + (ch.FreqMhz - lo) / _s.SpanMhz * sp.Width; if (x < sp.X || x > sp.Right) continue;
            g.PolyFill(new[] { new Point(x, sp.Y + g.U(2)), new Point(x - g.U(3), sp.Y - g.U(3) + g.U(8)), new Point(x + g.U(3), sp.Y - g.U(3) + g.U(8)) }, g.P.Accent, 0.9);
            g.Text(ch.Name, x, sp.Y + g.U(10), 7, g.P.Accent, Align.Center, true, 0.9);
        }
        if (_s.UnknownActive && _s.UnkPhase >= 2)
        {
            double x = sp.X + (_s.UnkFreq - lo) / _s.SpanMhz * sp.Width;
            if (x > sp.X && x < sp.Right) { g.GlowLine(x, sp.Y, x, sp.Bottom, g.P.Warning, 1.4, 0.8); g.Text($"UNK {_s.UnkFreq:0.000}", x + g.U(3), sp.Y + g.U(2), 8, g.P.Warning, Align.Left, true); }
        }
        // frequency labels and SNR readout
        for (int i = 0; i <= 8; i++) g.Text($"{lo + _s.SpanMhz * i / 8:0.00}", sp.X + sp.Width * i / 8, sp.Bottom + g.U(1), 6.5, g.P.Text, Align.Center, false, 0.55);
        var peak = Enumerable.Range(0, CommsSim.Bins).OrderByDescending(i => _s.Spectrum[i]).First();
        double pf = lo + peak * step, snr = (_s.Spectrum[peak] - 0.08) / 0.08 * 3;
        g.Text($"PEAK {pf:0.000} MHZ   SNR {Math.Max(0, snr):0.0} DB   {(_s.Spectrum[peak] > 0.8 ? "CLASS: NARROWBAND CARRIER" : _s.Spectrum[peak] > 0.3 ? "CLASS: SIGNAL CANDIDATE" : "CLASS: NOISE")}", sp.X + g.U(4), sp.Bottom + g.U(10), 8, _s.Spectrum[peak] > 0.3 ? g.P.Accent : g.P.Text, Align.Left, true, 0.9);
        // waterfall
        var wf = new Rect(r.X, sp.Bottom + g.U(22), r.Width, r.Bottom - sp.Bottom - g.U(22));
        _wf.Draw(g, wf); g.Frame(wf, g.P.Primary, 1, 0.35);
        for (int i = 1; i < 8; i++) g.Line(wf.X + wf.Width * i / 8, wf.Y, wf.X + wf.Width * i / 8, wf.Bottom, g.P.Primary, 1, 0.07);
        g.Text("WATERFALL  T-" + (int)(110 * 0.1) + "S", wf.X + g.U(3), wf.Y + g.U(2), 7.5, 0xFFFFFFFF, Align.Left, true, 0.7);
        if (_s.Scanning) { double x = wf.X + wf.Width * _s.ScanPos; g.GlowLine(x, wf.Y, x, wf.Bottom, g.P.Accent, 1.2, 0.8); }
    }

    // ---------------------------------------------------------------- B: telescope array
    void DrawArray(Gfx g, Rect r)
    {
        double bh = g.U(g.Touch ? 26 : 19);
        var grid = new Rect(r.X, r.Y, r.Width, r.Height - bh - g.U(24));
        int cols = 4, rows = 2; double cw = grid.Width / cols, ch = grid.Height / rows;
        for (int i = 0; i < _s.Dishes.Count; i++)
        {
            var d = _s.Dishes[i]; var cell = new Rect(grid.X + (i % cols) * cw, grid.Y + (i / cols) * ch, cw, ch); bool sel = i == _s.SelDish;
            g.DishIcon(cell.Center.X, cell.Y + ch * 0.36, Math.Min(cw, ch) * 0.68, d.Az, d.El, d.Online ? g.P.Primary : 0xFF506070, sel, d.Online, d.Sync);
            g.Text(d.Id, cell.Center.X, cell.Y + ch * 0.64, 8, sel ? g.P.Accent : g.P.Text, Align.Center, true);
            g.Text($"{d.Az:0}° / {d.El:0}°", cell.Center.X, cell.Y + ch * 0.64 + g.U(10), 7, g.P.Text, Align.Center, false, 0.7);
            g.Text(d.Manual ? "MANUAL" : d.Craft >= 0 ? "TRK " + _s.Craft[d.Craft].Id.Split(' ')[0] : "STOW", cell.Center.X, cell.Y + ch * 0.64 + g.U(19), 6.5, d.Manual ? g.P.Warning : g.P.Primary, Align.Center, false, 0.85);
            g.AddHit("cm:dish:" + i, cell);
        }
        var dd = _s.Dishes[_s.SelDish];
        g.Text($"SYNC {dd.Sync * 100:0}%  ERR {dd.Err:0.00}°  GAIN {dd.GainDb - 10 * Math.Log10(1 / Math.Max(0.01, dd.Sync)):0.0} DBI  RX {(dd.Online ? "ONLINE" : "OFFLINE")}  ARRAY {(_s.Dishes.Count(x => x.Online && x.Sync > 0.7))}/{_s.Dishes.Count} COHERENT", r.X + g.U(2), grid.Bottom + g.U(3), 7.5, dd.Sync > 0.7 ? g.P.Primary : g.P.Warning, Align.Left, true);
        double by = r.Bottom - bh, bw = (r.Width - g.U(3) * 5) / 6;
        string[] ids = { "cm:az-", "cm:az+", "cm:el-", "cm:el+", "cm:trk", "cm:asg" }; string[] lab = { "AZ -", "AZ +", "EL -", "EL +", "TRACK", "ASSIGN" };
        for (int i = 0; i < 6; i++) g.Button(ids[i], new Rect(r.X + i * (bw + g.U(3)), by, bw, bh), lab[i], false, true, null, bw < g.U(40) ? 6.5 : 7.5);
    }

    // ---------------------------------------------------------------- C: tracking table
    static string Lt(double s) { var t = TimeSpan.FromSeconds(s); return t.TotalHours >= 1 ? $"{(int)t.TotalHours}H{t.Minutes:00}M" : $"{t.Minutes}M{t.Seconds:00}S"; }

    void DrawTracking(Gfx g, Rect r)
    {
        var rows = _s.Craft.Select(c => new[] { c.Id, $"{c.DistAu:0.00}", Lt(c.LightTimeS), $"{c.FreqMhz:0}", $"{c.RxDbm:0.0}", c.Status, c.Dish >= 0 ? _s.Dishes[c.Dish].Id : "--", c.Status == "LOCKED" ? "NOW" : $"{Math.Max(0, _s.Time - c.LastContact):0}S AGO", c.InWindow ? "OPEN" : $"{(c.WindowPeriod - (_s.Time + c.WindowOffset) % c.WindowPeriod):0}S", $"{c.Degradation * 100:0}%" }).ToList();
        int vis = g.TableVisibleRows(r); _trkFirst = Math.Clamp(_trkFirst, 0, Math.Max(0, rows.Count - vis));
        g.Table(r, new[] { "SPACECRAFT", "AU", "LIGHT-TIME", "MHZ", "RX DBM", "STATUS", "DISH", "LAST", "WINDOW", "DEGR" }, new[] { 1.45, 0.95, 1, 0.7, 0.85, 1.1, 0.8, 0.9, 0.8, 0.6 }, rows, _trkFirst, _s.SelCraft, "ct:", (ri, ci) =>
            ci == 5 ? (rows[ri][5] == "LOCKED" ? 0xFF30FF70 : rows[ri][5] is "SIGNAL LOST" ? 0xFFFF4040 : g.P.Warning) : ci == 9 ? (_s.Craft[ri].Degradation > 0.6 ? g.P.Warning : (uint?)null) : null, 8);
    }

    // ---------------------------------------------------------------- sky chart
    void DrawSky(Gfx g, Rect r)
    {
        double rad = Math.Min(r.Width, r.Height) / 2 - g.U(6), cx = r.Center.X, cy = r.Center.Y;
        g.Disc(cx, cy, rad, g.P.Background, 0.5);
        for (int i = 1; i <= 3; i++) g.Circle(cx, cy, rad * i / 3, g.P.Primary, 1, 0.2);
        for (int d = 0; d < 360; d += 45) { double a = (d - 90) * Math.PI / 180; g.Line(cx, cy, cx + Math.Cos(a) * rad, cy + Math.Sin(a) * rad, g.P.Primary, 1, 0.1); }
        for (int k = 0; k < 16; k++) { double a = (_sweep - 90 - k * 2.5) * Math.PI / 180; g.Line(cx, cy, cx + Math.Cos(a) * rad, cy + Math.Sin(a) * rad, g.P.Primary, 1.5, 0.28 * (1 - k / 16.0)); }
        g.GlowLine(cx, cy, cx + Math.Cos((_sweep - 90) * Math.PI / 180) * rad, cy + Math.Sin((_sweep - 90) * Math.PI / 180) * rad, g.P.Primary, 1.4);
        Point P(double az, double el) { double a = (az - 90) * Math.PI / 180, z = (90 - el) / 90.0 * rad; return new Point(cx + Math.Cos(a) * z, cy + Math.Sin(a) * z); }
        for (int i = 0; i < _s.Craft.Count; i++) { var c = _s.Craft[i]; var p = P(c.Az, c.El); bool sel = i == _s.SelCraft; g.Disc(p.X, p.Y, g.U(sel ? 3.5 : 2.5), c.Status == "LOCKED" ? g.P.Accent : g.P.Warning, 0.95); if (sel) g.Text(c.Id, p.X + g.U(5), p.Y - g.U(5), 7, g.P.Accent, Align.Left, true); }
        for (int i = 0; i < _s.Dishes.Count; i++) { var d = _s.Dishes[i]; var p = P(d.Az, d.El); g.Line(cx, cy, p.X, p.Y, i == _s.SelDish ? g.P.Accent : g.P.Primary, 1, i == _s.SelDish ? 0.8 : 0.22); g.Frame(new Rect(p.X - g.U(2), p.Y - g.U(2), g.U(4), g.U(4)), i == _s.SelDish ? g.P.Accent : g.P.Primary, 1); }
        if (_s.UnknownActive && _s.UnkPhase >= 1) { var p = P(40 + _s.UnkT * 3, 55); g.Circle(p.X, p.Y, g.U(5 + 2 * g.Pulse(3)), g.P.Warning, 1.3); g.Text("?", p.X + g.U(6), p.Y - g.U(5), 9, g.P.Warning, Align.Left, true); }
        g.Text("N", cx, cy - rad - g.U(1), 8, g.P.Primary, Align.Center, true);
    }

    // ---------------------------------------------------------------- D: terminal
    void DrawTerminal(Gfx g, Rect r)
    {
        var lines = _s.Decode; double lh = g.U(11.5); int max = Math.Max(1, (int)((r.Height - g.U(14)) / lh)); int start = Math.Max(0, lines.Count - max);
        if (lines.Count == 0) g.Text("AWAITING SIGNAL — PRESS DECODE", r.X, r.Y, 8, g.P.Text, Align.Left, false, 0.5);
        for (int i = start; i < lines.Count; i++) { string l = lines[i]; bool hi = l.StartsWith("DECODED") || l.StartsWith("INCOMING") || l.Contains("REPEATING"); g.Text(l, r.X, r.Y + (i - start) * lh, 7.5, hi ? g.P.Warning : l.StartsWith("---") ? g.P.Accent : g.P.Primary, Align.Left, hi, 0.95); }
        if (_s.Decoding || _s.DecodeProg > 0 && _s.DecodeProg < 1) g.Bar(new Rect(r.X, r.Bottom - g.U(12), r.Width, g.U(11)), _s.DecodeProg, g.P.Accent, null, null);
    }

    // ---------------------------------------------------------------- E: channels
    void DrawChannels(Gfx g, Rect r)
    {
        var rows = _s.Channels.Select(c => new[] { c.Name, $"{c.FreqMhz:0.0}", $"{c.BwKhz:0.0}", $"{c.SigDbm:0.0}", $"{c.NoiseDbm:0.0}", c.Status, c.Receiver, c.Dir, c.Enc }).ToList();
        g.Table(r, new[] { "CH", "MHZ", "BW KHZ", "SIG", "NOISE", "STATUS", "RX", "DIR", "ENC" }, new[] { 0.7, 0.9, 0.8, 0.8, 0.8, 1, 0.8, 0.8, 0.9 }, rows, _chFirst, _s.SelChannel, "cc:", (ri, ci) => ci == 5 ? (rows[ri][5] == "LOCKED" ? 0xFF30FF70 : rows[ri][5] is "FAULT" or "MUTED" ? 0xFFFF4040 : g.P.Warning) : null, 8);
    }

    // ---------------------------------------------------------------- F: intelligence
    void DrawIntel(Gfx g, Rect r)
    {
        var rows = _s.Anomalies.AsEnumerable().Reverse().Select(a => new[] { a.Id, $"{a.FreqMhz:0.000}", $"{a.DriftHz:+0.00;-0.00}", a.Class.Length > 22 ? a.Class[..22] : a.Class, $"{a.Conf * 100:0}%" }).ToList();
        double kh = g.U(40);
        var kv = new (string, string, uint?)[] { ("ANOMALIES", _s.Anomalies.Count.ToString(), null), ("ANALYSIS CONF.", _s.Anomalies.Count == 0 ? "--" : $"{_s.Anomalies.Average(a => a.Conf) * 100:0}%", null), ("INTERRUPTIONS", _s.Craft.Count(c => c.Loss).ToString(), _s.Craft.Any(c => c.Loss) ? g.P.Warning : null), ("FREQ DRIFT", _s.Anomalies.Count == 0 ? "0.00 HZ/S" : $"{_s.Anomalies.Last().DriftHz:+0.00;-0.00} HZ/S", null) };
        g.KvGrid(new Rect(r.X, r.Y, r.Width, kh), kv, 2, 8);
        g.Table(new Rect(r.X, r.Y + kh, r.Width, r.Height - kh), new[] { "ID", "MHZ", "DRIFT", "CLASS", "CONF" }, new[] { 0.9, 1, 0.7, 2, 0.6 }, rows, 0, -1, "ia:", (ri, ci) => ci == 3 && rows[ri][3].StartsWith("UNIDENTIFIED") || ci == 3 && rows[ri][3].StartsWith("UNCLASSIFIED") ? g.P.Warning : null, 7.5);
    }

    // ---------------------------------------------------------------- G: mission control
    void DrawMission(Gfx g, Rect r)
    {
        double lw = Math.Min(r.Width * 0.3, g.U(110));
        for (int i = 0; i < CommsSim.Stations.Length; i++)
        {
            var y = r.Y + i * g.U(26); int on = _s.Dishes.Count(d => d.Online) * (i + 2) / 5; g.Led(r.X + g.U(5), y + g.U(8), 1);
            g.Text(CommsSim.Stations[i].Split(' ')[0], r.X + g.U(14), y, 8, g.P.Text, Align.Left, true); g.Text($"{Math.Min(8, 4 + i * 2 - (i == 1 ? 1 : 0))} DISHES", r.X + g.U(14), y + g.U(10), 7, g.P.Primary, Align.Left, false, 0.8);
        }
        g.Text("SCHEDULED TX", r.X, r.Y + g.U(80), 7.5, g.P.Accent, Align.Left, true);
        g.Text($"{DateTime.Now.AddMinutes(12):HH:mm}  CMD UPLINK", r.X, r.Y + g.U(91), 7, g.P.Text, Align.Left, false, 0.8);
        double tx = r.X + lw, tw = r.Width - lw, lh = Math.Min(g.U(14), r.Height / _s.Craft.Count);
        g.Text("CONTACT WINDOWS (NEXT 10 MIN)  |  ACQUISITION PROGRESS", tx, r.Y - g.U(1), 7, g.P.Text, Align.Left, false, 0.6);
        for (int i = 0; i < _s.Craft.Count; i++)
        {
            var c = _s.Craft[i]; double y = r.Y + g.U(9) + i * lh;
            g.Text(c.Id.Split(' ')[0], tx, y, 7, i == _s.SelCraft ? g.P.Accent : g.P.Text, Align.Left, i == _s.SelCraft);
            var bar = new Rect(tx + g.U(66), y + 1, tw - g.U(66) - g.U(54), lh - g.U(4)); g.Fill(bar, g.P.Primary, 0.07);
            for (double t = 0; t < 600; t += 3) { bool open = (_s.Time + t + c.WindowOffset) % c.WindowPeriod < c.WindowLen; if (open) g.Fill(new Rect(bar.X + bar.Width * t / 600, bar.Y, Math.Max(1, bar.Width * 3 / 600), bar.Height), c.Status == "LOCKED" ? g.P.Accent : g.P.Primary, 0.55); }
            g.Line(bar.X, bar.Y - 1, bar.X, bar.Bottom + 1, 0xFFFFFFFF, 1, 0.8);
            g.Text($"{c.Acq * 100:0}%", bar.Right + g.U(5), y, 7, c.Acq >= 1 ? 0xFF30FF70 : g.P.Warning, Align.Left, true);
        }
    }

    void DrawGraphs(Gfx g, Rect r)
    {
        var rows = Layout.Rows(r, g.U(3), 1, 1);
        g.Graph(rows[0], _s.SnrH, g.P.Accent, 0, null, true, $"SNR {_s.Craft[_s.SelCraft].Id.Split(' ')[0]}", " dB"); g.Graph(rows[1], _s.RxH, g.P.Primary, null, null, true, "RX POWER", " dBm");
    }

    void DrawLog(Gfx g, Rect r)
    {
        double lh = g.U(11.5); int max = Math.Max(1, (int)(r.Height / lh)); int start = Math.Max(0, _s.Log.Count - max);
        for (int i = start; i < _s.Log.Count; i++) { string l = _s.Log[i]; bool w = l.Contains("LOSS") || l.Contains("UNKNOWN") || l.Contains("WARNING"); g.Text(l, r.X, r.Y + (i - start) * lh, 7.5, w ? g.P.Warning : g.P.Text, Align.Left, w, 0.92); }
    }

    // ---------------------------------------------------------------- overlays for the cinematic event
    protected override void AfterDraw(Gfx g, Rect area)
    {
        if (_s.UnknownActive && _s.UnkPhase >= 4 && _s.UnkPhase < 8)
        {
            var br = new Rect(area.Center.X - g.U(190), area.Y + g.U(8), g.U(380), g.U(26)); g.Fill(br, 0xFF000000, 0.7); g.Frame(br, g.P.Warning, 1.5, 0.5 + 0.5 * g.Pulse(2.5));
            g.TextIn(br, new[] { "DETECTION WARNING — UNKNOWN TRANSMISSION", "SIGNAL ANALYSIS IN PROGRESS", "REPEATING PATTERN DETECTED", "RECEIVING AUDIO TRANSMISSION" }[_s.UnkPhase - 4], 9.5, g.P.Warning, Align.Center, true);
        }
        if (_msgShow > 0 && _incoming.Length > 0)
        {
            double w = Math.Min(area.Width - g.U(10), g.U(560)), h = g.U(60); var br = new Rect(area.Center.X - w / 2, area.Bottom - h - g.U(10), w, h);
            g.Fill(br, 0xFF000000, 0.85); g.Frame(br, g.P.Accent, 1.5); g.Brackets(br, g.P.Accent, 10, 2);
            bool final = _s.UnkPhase == 8; g.Text(final ? "FINAL MESSAGE — DECODED" : "INCOMING TRANSMISSION", br.X + g.U(10), br.Y + g.U(6), 8, final ? g.P.Warning : g.P.Primary, Align.Left, true);
            int n = Math.Min(_incoming.Length, (int)_typed); g.Text(_incoming[..n] + (n < _incoming.Length ? "_" : ""), br.X + g.U(10), br.Y + g.U(24), final ? 12 : 10, g.P.Accent, Align.Left, true);
        }
    }

    // ---------------------------------------------------------------- input
    protected override string? PanelButton(string id)
    {
        switch (id)
        {
            case "cm:t-": return DoAction("TUNE-"); case "cm:t+": return DoAction("TUNE+"); case "cm:scan": return DoAction("SCAN");
            case "cm:s-": C.Audio.Play("tick"); return _s.Span(0.5); case "cm:s+": C.Audio.Play("tick"); return _s.Span(2);
            case "cm:clr": _s.Anomalies.Clear(); return "CANDIDATES CLEARED";
            case "cm:az-": C.Audio.Play("mech", SoundCategory.Ui, 0.6f); return _s.Point(_s.SelDish, -6, 0); case "cm:az+": C.Audio.Play("mech", SoundCategory.Ui, 0.6f); return _s.Point(_s.SelDish, 6, 0);
            case "cm:el-": C.Audio.Play("mech", SoundCategory.Ui, 0.6f); return _s.Point(_s.SelDish, 0, -4); case "cm:el+": C.Audio.Play("mech", SoundCategory.Ui, 0.6f); return _s.Point(_s.SelDish, 0, 4);
            case "cm:trk": C.Audio.Play("access"); return _s.Track(_s.SelDish);
            case "cm:asg": C.Audio.Play("handshake"); return _s.Assign(_s.SelDish, _s.SelCraft);
        }
        return null;
    }

    protected override void PanelTap(Point p, string? id)
    {
        if (id == null) return;
        if (id.StartsWith("cm:dish:")) { _s.SelDish = int.Parse(id[8..]); var d = _s.Dishes[_s.SelDish]; C.Toast($"{d.Id} SELECTED  AZ {d.Az:0} EL {d.El:0}"); C.Audio.Play("deepbeep", SoundCategory.Ui, 0.7f); }
        else if (id.StartsWith("ct:")) { _s.SelCraft = int.Parse(id[3..]); C.Toast("TRACKING " + _s.Craft[_s.SelCraft].Id); C.Audio.Play("tick"); }
        else if (id.StartsWith("cc:")) { C.Toast(_s.SelectChannel(int.Parse(id[3..]))); C.Audio.Play("deepbeep", SoundCategory.Ui, 0.7f); }
    }

    protected override bool PanelDrag(Point p, Vector d, string? id)
    {
        if (id == "cm:spec") { _s.Tune(-d.X / Math.Max(1, _specR.Width) * _s.SpanMhz, false); return true; }
        if (id != null && id.StartsWith("ct:")) { _trkFirst = Math.Max(0, _trkFirst - (int)Math.Round(d.Y / 8)); return true; }
        return false;
    }

    protected override bool PanelWheel(Point p, double delta) { if (_specR.Contains(p)) { _s.Span(delta > 0 ? 0.8 : 1.25); return true; } return false; }
}
