using Avalonia;
using Avalonia.Media;
using Blacksite.Core.Audio;
using Blacksite.UI.Rendering;

namespace Blacksite.UI.Shell;

/// <summary>KILL SWITCH (erase data / terminate connection / deploy drones, chosen per preset) and SELF-DESTRUCT (countdown, detonation, reboot).
/// Purely visual and audible — nothing on disk or in the user's settings is touched.</summary>
public sealed partial class BlacksiteView
{
    enum CrisisPhase { None, Kill, Countdown, Boom }
    CrisisPhase _crPhase;
    int _crKind;                       // 0 erase data, 1 terminate connection, 2 deploy drones, 3 emergency shutdown
    double _crT;
    int _crTickSec = -1;
    const double KillSeconds = 6.5, CountSeconds = 10, BoomSeconds = 7.5;

    static (int kind, string label) KillFor(string id) => id switch
    {
        "hacker" or "jarvis" or "quantum" or "medical" => (0, "ERASE DATA"),
        "lockdown" or "tactical" or "orbital" or "facility" => (2, "DEPLOY DRONES"),
        "reactor" => (3, "EMERGENCY SHUTDOWN"),
        _ => (1, "TERMINATE CONNECTION"),
    };

    bool CrisisActive => _crPhase != CrisisPhase.None;

    static double Hs(double i) { double v = Math.Sin(i * 12.9898 + 4.1414) * 43758.5453; return v - Math.Floor(v); }

    void HandleCrisis(string id)
    {
        switch (id)
        {
            case "cr:kill" when !CrisisActive:
                _crKind = KillFor(PresetId).kind; _crPhase = CrisisPhase.Kill; _crT = 0;
                Ctx.Audio.Play(_crKind == 1 ? "handshake" : _crKind == 2 ? "radarpulse" : "alarm", SoundCategory.Alarm);
                AddToast("KILL SWITCH ENGAGED — " + KillFor(PresetId).label, true);
                break;
            case "cr:sd" when !CrisisActive:
                _crPhase = CrisisPhase.Countdown; _crT = 0; _crTickSec = -1;
                Ctx.Audio.Play("alarm", SoundCategory.Alarm); Ctx.Audio.Say("Self destruct sequence initiated.");
                AddToast("SELF-DESTRUCT ARMED — SIMULATION ONLY", true);
                break;
            case "cr:abort" when _crPhase == CrisisPhase.Countdown:
                _crPhase = CrisisPhase.None; Ctx.Audio.Play("confirm", SoundCategory.Event); Ctx.Audio.Say("Self destruct aborted.");
                AddToast("SELF-DESTRUCT ABORTED", false);
                break;
        }
    }

    /// <summary>Escape aborts an armed countdown; returns true when it consumed the key.</summary>
    bool CrisisEscape()
    {
        if (_crPhase != CrisisPhase.Countdown) return false;
        HandleCrisis("cr:abort"); return true;
    }

    void UpdateCrisis(double dt)
    {
        if (_crPhase == CrisisPhase.None) return;
        _crT += dt;
        switch (_crPhase)
        {
            case CrisisPhase.Kill:
                if (_crT >= KillSeconds)
                {
                    _crPhase = CrisisPhase.None; _glitch = 0.8;
                    Ctx.Audio.Play(_crKind == 2 ? "access" : "chime", SoundCategory.Event);
                    AddToast(KillFor(PresetId).label + " — COMPLETE (SIMULATED)", false);
                }
                else if (_crKind == 2 && (int)(_crT * 2) != (int)((_crT - dt) * 2) && _crT < 4.5) Ctx.Audio.Play("whoosh", SoundCategory.Event);
                else if (_crKind != 2 && (int)(_crT * 3) != (int)((_crT - dt) * 3) && _crT < 5) Ctx.Audio.Play("tick", SoundCategory.Event);
                break;
            case CrisisPhase.Countdown:
                {
                    int sec = (int)Math.Ceiling(CountSeconds - _crT);
                    if (sec != _crTickSec)
                    {
                        _crTickSec = sec;
                        Ctx.Audio.Play(sec <= 3 ? "triple" : "tick", SoundCategory.Alarm);
                        if (sec == 10 || sec <= 5 && sec >= 1) Ctx.Audio.Say(sec.ToString());
                        if (sec == 6) Ctx.Audio.Play("alarm", SoundCategory.Alarm);
                    }
                    if (_crT >= CountSeconds)
                    {
                        _crPhase = CrisisPhase.Boom; _crT = 0; _glitch = 1;
                        Ctx.Audio.Play("surge", SoundCategory.Alarm); Ctx.Audio.Play("error", SoundCategory.Alarm); Ctx.Audio.Play("static", SoundCategory.Event);
                    }
                    break;
                }
            case CrisisPhase.Boom:
                if (_crT >= BoomSeconds)
                {
                    _crPhase = CrisisPhase.None; _booting = true; _bootT = 0;
                    AddToast("SYSTEM RESTORED — SELF-DESTRUCT WAS A SIMULATION", false);
                }
                break;
        }
    }

    /// <summary>Camera shake applied to the whole frame near detonation.</summary>
    Matrix CrisisShake()
    {
        double k = 0;
        if (_crPhase == CrisisPhase.Countdown) { double left = CountSeconds - _crT; k = left < 3 ? (3 - left) / 3 * 6 : 0; }
        else if (_crPhase == CrisisPhase.Boom) k = Math.Max(0, 1 - _crT / 2.2) * 16;
        if (k <= 0 || Ctx.Settings.ReducedMotion) return Matrix.Identity;
        double t = _crT * 60;
        return Matrix.CreateTranslation(Math.Sin(t * 1.7) * k + Math.Sin(t * 3.1) * k * 0.4, Math.Cos(t * 2.3) * k);
    }

    // ----------------------------------------------------------------- buttons (drawn inside the bottom action bar)
    void DrawCrisisButtons(Gfx g, Rect r)
    {
        var (_, label) = KillFor(PresetId);
        double h = r.Height - g.U(12), y = r.Y + g.U(6), gap = g.U(6);
        bool narrow = r.Width < g.U(700);
        double kw = narrow ? r.Width * 0.22 : g.U(Math.Clamp(label.Length * 8.5 + 40, 130, 220)), sw = narrow ? r.Width * 0.22 : g.U(150);
        double x = r.Right - g.U(8) - sw;
        double pulse = 0.5 + 0.5 * Math.Sin(_animTime * 3.2);
        string kl = narrow ? "KILL" : label, sl = narrow ? "DESTRUCT" : "SELF DESTRUCT";
        var sd = new Rect(x, y, sw, h); var kr = new Rect(x - gap - kw, y, kw, h);
        g.Fill(kr.Inflate(g.U(2)), 0xFFFF3030, 0.05 + 0.07 * pulse);
        g.Button("cr:kill", kr, kl, false, true, 0xFFFF4A3A, narrow ? 8 : 10);
        g.Fill(sd.Inflate(g.U(2)), 0xFFFF2020, 0.06 + 0.10 * pulse);
        g.Button("cr:sd", sd, sl, false, true, 0xFFFF2A2A, narrow ? 8 : 10);
    }

    double CrisisReserve(Gfx g, Rect r)
    {
        var (_, label) = KillFor(PresetId);
        bool narrow = r.Width < g.U(700);
        return narrow ? r.Width * 0.44 + g.U(22) : g.U(Math.Clamp(label.Length * 8.5 + 40, 130, 220)) + g.U(150) + g.U(26);
    }

    // ----------------------------------------------------------------- overlay
    void DrawCrisis(Gfx g, Rect b)
    {
        if (_crPhase == CrisisPhase.None) return;
        g.AddHit("cr:block", b);
        switch (_crPhase)
        {
            case CrisisPhase.Kill: DrawKill(g, b); break;
            case CrisisPhase.Countdown: DrawCountdown(g, b); break;
            case CrisisPhase.Boom: DrawBoom(g, b); break;
        }
    }

    static string Mmss(double s) => $"{(int)(s / 60):00}:{(int)(s % 60):00}.{(int)((s % 1) * 10)}";

    void DrawKill(Gfx g, Rect b)
    {
        double p = Math.Clamp(_crT / KillSeconds, 0, 1), t = _crT;
        uint col = _crKind == 0 ? 0xFFFF5A3C : _crKind == 1 ? 0xFFFFB02E : _crKind == 2 ? 0xFF38D8FF : 0xFFFF6A28;
        g.Fill(b, 0xFF000000, 0.5 * Math.Min(1, t * 3) * (p > 0.94 ? (1 - p) / 0.06 : 1));
        double pw = Math.Min(b.Width - g.U(24), g.U(760)), ph = Math.Min(b.Height - g.U(24), g.U(360));
        var r = new Rect(b.Center.X - pw / 2, b.Center.Y - ph / 2, pw, ph);
        g.Fill(r, 0xFF05080C, 0.92); g.Frame(r, col, 2, 0.9); g.Frame(r.Deflate(g.U(4)), col, 1, 0.3);
        string title = KillFor(PresetId).label;
        double hb = g.U(30); g.Fill(new Rect(r.X, r.Y, r.Width, hb), col, 0.16 + 0.1 * Math.Sin(t * 8));
        g.Text("KILL SWITCH // " + title, r.X + g.U(12), r.Y + g.U(8), g.U(13), col, Align.Left, true);
        g.Text("SIMULATED", r.Right - g.U(12), r.Y + g.U(9), g.U(10), col, Align.Right, false, 0.7);
        var body = new Rect(r.X + g.U(14), r.Y + hb + g.U(12), r.Width - g.U(28), r.Height - hb - g.U(66));
        using (g.Clip(body))
        {
            if (_crKind == 0) KillErase(g, body, p, t, col);
            else if (_crKind == 1) KillTerminate(g, body, p, t, col);
            else if (_crKind == 2) KillDrones(g, body, p, t, col);
            else KillShutdown(g, body, p, t, col);
        }
        var bar = new Rect(r.X + g.U(14), r.Bottom - g.U(44), r.Width - g.U(28), g.U(12));
        g.Fill(bar, col, 0.12); g.Frame(bar, col, 1, 0.7); g.Fill(new Rect(bar.X + 1, bar.Y + 1, (bar.Width - 2) * p, bar.Height - 2), col, 0.85);
        string st = p < 0.9 ? $"IN PROGRESS  {p * 100:0}%" : p < 1 ? "FINALISING…" : "COMPLETE";
        g.Text(st, r.X + g.U(14), r.Bottom - g.U(26), g.U(11), col, Align.Left, true);
        g.Text($"T+{t:0.0}s", r.Right - g.U(14), r.Bottom - g.U(26), g.U(11), g.P.Text, Align.Right, false, 0.7);
        if (p > 0.82) { double a = Math.Min(1, (p - 0.82) / 0.06); g.TextIn(new Rect(body.X, body.Center.Y - g.U(26), body.Width, g.U(52)), KillDone(), g.U(30), col, Align.Center, true, a); }
    }

    string KillDone() => _crKind switch { 0 => "DATA ERASED", 1 => "CONNECTION TERMINATED", 2 => "DRONES DEPLOYED", _ => "SYSTEM SHUT DOWN" };

    void KillErase(Gfx g, Rect body, double p, double t, uint col)
    {
        int cols = 36, rows = 9; double cw = body.Width / cols, chh = Math.Min(g.U(15), (body.Height - g.U(46)) / rows);
        int pass = Math.Min(3, 1 + (int)(p * 3)); double pp = p * 3 % 1;
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
            {
                double th = (x / (double)cols * 0.6 + Hs(x * 31 + y * 7) * 0.4);
                bool wiped = pp > th && pass > 0; double fl = pp > th - 0.06 && pp < th + 0.02 ? 1 : 0;
                var c = new Rect(body.X + x * cw + 1, body.Y + y * chh + 1, cw - 2, chh - 2);
                if (pass > 1 || wiped) g.Fill(c, pass >= 3 && wiped ? 0xFF101418 : 0xFF3A4048, 0.9);
                else g.Fill(c, col, 0.25 + 0.2 * Hs(x * 13 + y + Math.Floor(t * 6)));
                if (fl > 0) g.Fill(c, 0xFFFFFFFF, 0.9);
            }
        double ly = body.Y + rows * chh + g.U(8);
        g.Text($"OVERWRITE PASS {pass}/3   PATTERN {(pass == 1 ? "0x00" : pass == 2 ? "0xFF" : "RANDOM")}", body.X, ly, g.U(11), col, Align.Left, true);
        string[] files = { "/var/db/records.dat", "/home/ops/keys.vault", "/srv/telemetry/archive.bin", "/etc/blacksite/config.sys", "/data/mission/logs.enc", "/mnt/core/cache.img" };
        for (int i = 0; i < 3; i++) g.Text($"shred -vfz {files[((int)(t * 5) + i) % files.Length]}  ... {(Hs(i + Math.Floor(t * 5)) < 0.8 ? "OK" : "WIPED")}", body.X, ly + g.U(16 + i * 13), g.U(9.5), g.P.Text, Align.Left, false, 0.55 + 0.1 * i);
    }

    void KillTerminate(Gfx g, Rect body, double p, double t, uint col)
    {
        double ay = body.Y + body.Height * 0.32, ax = body.X + g.U(40), bx = body.Right - g.U(40), mid = (ax + bx) / 2;
        double cut = 0.45; bool cutDone = p > cut; double gapW = cutDone ? Math.Min(g.U(90), (p - cut) * g.U(300)) : 0;
        foreach (var (x, n) in new[] { (ax, "LOCAL NODE"), (bx, "REMOTE NODE") })
        {
            g.Disc(x, ay, g.U(16), col, 0.2); g.Frame(new Rect(x - g.U(16), ay - g.U(16), g.U(32), g.U(32)), col, 2, 0.9);
            g.Text(n, x, ay + g.U(22), g.U(10), col, Align.Center, true);
        }
        double leftEnd = cutDone ? mid - gapW / 2 : bx - g.U(18), rightStart = cutDone ? mid + gapW / 2 : bx;
        g.GlowLine(ax + g.U(18), ay, leftEnd, ay, col, 2, cutDone ? 0.55 : 0.9);
        if (cutDone) g.GlowLine(rightStart, ay, bx - g.U(18), ay, col, 2, 0.4);
        for (int i = 0; i < 9 && !cutDone; i++) { double q = (t * 0.5 + i / 9.0) % 1; g.Disc(ax + g.U(18) + (bx - ax - g.U(36)) * q, ay, g.U(2.5), 0xFFFFFFFF, 0.9); }
        if (cutDone) for (int i = 0; i < 14; i++) { double a = Hs(i) * Math.PI * 2, d = (p - cut) * g.U(150) * (0.3 + Hs(i + 9)); g.Disc(mid + Math.Cos(a) * d, ay + Math.Sin(a) * d, g.U(1.8), 0xFFFFFFFF, Math.Max(0, 1 - (p - cut) * 4)); }
        for (int k = 0; k < 3; k++) { double rr = ((t * 0.9 + k / 3.0) % 1) * g.U(60); if (!cutDone) g.Dc.DrawEllipse(null, g.Pn(col, 1.2, 1 - rr / g.U(60) * 0.9), new Point(ax, ay), rr, rr); }
        string[] ch = { "UPLINK / KU-BAND", "TELEMETRY STREAM", "COMMAND CHANNEL", "VPN TUNNEL 7", "SENSOR FEED", "BACKUP LINK" };
        for (int i = 0; i < ch.Length; i++)
        {
            double th = 0.15 + i * 0.11; bool sev = p > th; double y = body.Y + body.Height * 0.55 + (i % 3) * g.U(18), x = body.X + (i / 3) * body.Width * 0.5;
            g.Text(ch[i], x, y, g.U(10), g.P.Text, Align.Left, false, 0.8);
            g.Text(sev ? "SEVERED" : "ACTIVE", x + body.Width * 0.38, y, g.U(10), sev ? 0xFFFF4040 : 0xFF50FF90, Align.Right, true, sev ? 1 : 0.7 + 0.3 * Math.Sin(t * 8 + i));
        }
        for (int i = 0; i < 5; i++) { double hgt = g.U(6 + i * 4) * (1 - Math.Clamp((p - 0.1) / 0.8, 0, 1)); g.Fill(new Rect(body.Right - g.U(54) + i * g.U(9), body.Bottom - g.U(6) - hgt, g.U(6), hgt), col, 0.8); }
    }

    void KillDrones(Gfx g, Rect body, double p, double t, uint col)
    {
        g.Fill(body, 0xFF02121A, 0.9); g.Frame(body, col, 1, 0.4);
        double cx = body.Center.X, cy = body.Center.Y, R = Math.Min(body.Width, body.Height) * 0.47;
        for (int i = 1; i <= 3; i++) g.Dc.DrawEllipse(null, g.Pn(col, 1, 0.22), new Point(cx, cy), R * i / 3, R * i / 3);
        g.Line(cx - R, cy, cx + R, cy, col, 1, 0.18); g.Line(cx, cy - R, cx, cy + R, col, 1, 0.18);
        double sw = t * 2.6;
        for (int k = 0; k < 26; k++) { double a = sw - k * 0.03; g.Line(cx, cy, cx + Math.Cos(a) * R, cy + Math.Sin(a) * R, col, 2, 0.35 * (1 - k / 26.0)); }
        int n = 12, up = Math.Min(n, (int)(p / 0.7 * n) + (p > 0 ? 1 : 0));
        for (int i = 0; i < n; i++)
        {
            double lt = i / (double)n * 0.55, f = Math.Clamp((p - lt) / 0.38, 0, 1); if (f <= 0) continue;
            double ang = Hs(i + 3) * Math.PI * 2, dist = R * (0.35 + 0.55 * Hs(i * 3 + 1));
            double sx = cx + (i - n / 2.0) * body.Width * 0.05, sy = body.Bottom - g.U(8);
            double tx = cx + Math.Cos(ang) * dist, ty = cy + Math.Sin(ang) * dist, e = 1 - Math.Pow(1 - f, 2);
            double x = sx + (tx - sx) * e, y = sy + (ty - sy) * e;
            for (int k = 1; k < 9; k++) { double ee = Math.Max(0, e - k * 0.045); g.Disc(sx + (tx - sx) * ee, sy + (ty - sy) * ee, g.U(2.2 - k * 0.18), col, 0.45 - k * 0.045); }
            double hd = Math.Atan2(ty - sy, tx - sx);
            var tri = new[] { new Point(x + Math.Cos(hd) * g.U(7), y + Math.Sin(hd) * g.U(7)), new Point(x + Math.Cos(hd + 2.5) * g.U(5), y + Math.Sin(hd + 2.5) * g.U(5)), new Point(x + Math.Cos(hd - 2.5) * g.U(5), y + Math.Sin(hd - 2.5) * g.U(5)) };
            g.PolyFill(tri, 0xFFFFFFFF, 0.95, col, 1);
            if (f >= 1) { double pr = g.U(9 + 3 * Math.Sin(t * 6 + i)); g.Frame(new Rect(tx - pr, ty - pr, pr * 2, pr * 2), 0xFFFF5040, 1.2, 0.9); }
        }
        g.Text($"DRONES AIRBORNE {up}/{n}", body.X + g.U(8), body.Y + g.U(6), g.U(11), col, Align.Left, true);
        g.Text($"SWARM LINK {70 + (int)(p * 29)}%   TARGETS {(int)(p * 9)}", body.X + g.U(8), body.Y + g.U(21), g.U(9.5), g.P.Text, Align.Left, false, 0.7);
    }

    void KillShutdown(Gfx g, Rect body, double p, double t, uint col)
    {
        string[] sys = { "REACTOR CORE", "COOLANT LOOPS", "PLASMA INJECTORS", "POWER GRID", "LIFE SUPPORT BUS", "AUX GENERATORS" };
        for (int i = 0; i < sys.Length; i++)
        {
            double th = 0.1 + i * 0.12, f = 1 - Math.Clamp((p - th) / 0.15, 0, 1); double y = body.Y + i * g.U(26);
            g.Text(sys[i], body.X, y, g.U(11), g.P.Text, Align.Left, false, 0.9);
            var bar = new Rect(body.X + body.Width * 0.38, y + g.U(2), body.Width * 0.5, g.U(11)); g.Frame(bar, col, 1, 0.6); g.Fill(new Rect(bar.X + 1, bar.Y + 1, (bar.Width - 2) * f, bar.Height - 2), col, 0.8);
            g.Text(f <= 0 ? "OFFLINE" : $"{f * 100:0}%", body.Right, y, g.U(10), f <= 0 ? 0xFFFF4040 : col, Align.Right, true);
        }
    }

    void DrawCountdown(Gfx g, Rect b)
    {
        double left = Math.Max(0, CountSeconds - _crT), frac = left / CountSeconds;
        double flash = 0.5 + 0.5 * Math.Sin(_crT * (4 + (1 - frac) * 14));
        g.Fill(b, 0xFFFF0000, 0.06 + 0.12 * flash * (1 + (1 - frac)));
        // hazard bars top and bottom
        double hb = g.U(26);
        foreach (double yy in new[] { b.Y + (b.Y < 1 ? 0 : 0), b.Bottom - hb })
            for (double x = -hb + (_crT * 60) % (hb * 2); x < b.Width; x += hb * 2)
                g.PolyFill(new[] { new Point(x, yy + hb), new Point(x + hb, yy), new Point(x + hb * 2, yy), new Point(x + hb, yy + hb) }, 0xFFFFC020, 0.55);
        g.Fill(new Rect(b.X, b.Y + hb, b.Width, g.U(24)), 0xFF000000, 0.7);
        g.TextIn(new Rect(b.X, b.Y + hb, b.Width, g.U(24)), "⚠  SELF-DESTRUCT SEQUENCE ACTIVE  ⚠", g.U(14), 0xFFFF3030, Align.Center, true, 0.6 + 0.4 * flash);
        double cx = b.Center.X, cy = b.Center.Y - g.U(34), R = Math.Min(b.Width, b.Height) * 0.22;
        g.Dc.DrawEllipse(g.Br(0xFF000000, 0.62), null, new Point(cx, cy), R * 1.12, R * 1.12);
        g.Dc.DrawEllipse(null, g.Pn(0xFFFF3030, 2, 0.3), new Point(cx, cy), R, R);
        int segs = 60; for (int i = 0; i < segs; i++) { double a0 = -Math.PI / 2 + i / (double)segs * Math.PI * 2; bool on = i / (double)segs < frac; g.Line(cx + Math.Cos(a0) * R * 0.9, cy + Math.Sin(a0) * R * 0.9, cx + Math.Cos(a0) * R, cy + Math.Sin(a0) * R, on ? 0xFFFF3030 : 0xFF501010, 3, on ? 0.95 : 0.5); }
        int sec = (int)Math.Ceiling(left); double pop = 1 + 0.18 * Math.Max(0, 1 - (left % 1) * 4);
        g.TextIn(new Rect(cx - R, cy - R * 0.55, R * 2, R * 1.1), Math.Min(sec, 10).ToString(), R * 0.95 * pop, 0xFFFF4040, Align.Center, true);
        g.TextIn(new Rect(cx - R, cy + R * 0.45, R * 2, g.U(20)), "T-" + Mmss(left), g.U(14), 0xFFFFFFFF, Align.Center, true, 0.8);
        string[] msg = { "CORE MEMORY PURGE", "COOLANT VENTING", "CONTAINMENT FIELD OFFLINE", "WARHEAD PRIMED (SIMULATED)", "EVACUATE ALL PERSONNEL" };
        g.TextIn(new Rect(b.X, cy + R + g.U(8), b.Width, g.U(18)), msg[(int)(_crT * 1.2) % msg.Length], g.U(11), 0xFFFFA0A0, Align.Center, false, 0.9);
        var ab = new Rect(cx - g.U(90), cy + R + g.U(34), g.U(180), g.U(36));
        g.Button("cr:abort", ab, "ABORT  (ESC)", true, true, 0xFF40FF90, 12);
    }

    void DrawBoom(Gfx g, Rect b)
    {
        double t = _crT, cx = b.Center.X, cy = b.Center.Y, R = Math.Max(b.Width, b.Height);
        double dark = Math.Clamp((t - 1.3) / 1.2, 0, 1);
        g.Fill(b, 0xFF000000, dark * 0.97);
        double fb = Math.Clamp(t / 1.6, 0, 1);
        if (t < 2.2)
        {
            var fire = new RadialGradientBrush
            {
                Center = new RelativePoint(0.5, 0.5, RelativeUnit.Relative), GradientOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative), RadiusX = RelativeScalar.Parse("50%"), RadiusY = RelativeScalar.Parse("50%"),
                GradientStops = { new GradientStop(Color.FromArgb((byte)(255 * (1 - t / 2.2)), 255, 255, 235), 0), new GradientStop(Color.FromArgb((byte)(230 * (1 - t / 2.2)), 255, 170, 40), 0.35), new GradientStop(Color.FromArgb((byte)(160 * (1 - t / 2.2)), 255, 60, 10), 0.7), new GradientStop(Color.FromArgb(0, 80, 0, 0), 1) }
            };
            double fr = R * 0.15 + R * 0.75 * (1 - Math.Pow(1 - fb, 2));
            g.Dc.DrawEllipse(fire, null, new Point(cx, cy), fr, fr);
        }
        if (t < 0.5) g.Fill(b, 0xFFFFFFFF, 1 - t / 0.5);
        for (int k = 0; k < 4; k++) { double st = t - k * 0.22; if (st <= 0 || st > 2.6) continue; double rr = R * 0.9 * (1 - Math.Pow(1 - st / 2.6, 3)); g.Dc.DrawEllipse(null, g.Pn(k % 2 == 0 ? 0xFFFFE0A0 : 0xFFFF7040, 4 - k * 0.7, 1 - st / 2.6), new Point(cx, cy), rr, rr); }
        for (int i = 0; i < 110; i++)
        {
            double a = Hs(i) * Math.PI * 2, sp = (0.25 + Hs(i + 50) * 0.9) * R * 0.7, d = sp * Math.Min(t, 3.2) / 1.6, life = Math.Clamp(1 - t / (2.4 + Hs(i + 7) * 2), 0, 1);
            if (life <= 0) continue;
            double x = cx + Math.Cos(a) * d, y = cy + Math.Sin(a) * d + 40 * t * t * Hs(i + 3);
            g.Line(x, y, x - Math.Cos(a) * g.U(14), y - Math.Sin(a) * g.U(14), i % 3 == 0 ? 0xFFFFFFFF : 0xFFFF9030, 2, life);
        }
        if (t > 0.3 && t < 3) for (int i = 0; i < 9; i++)
            {
                double a = Hs(i + 90) * Math.PI * 2, len = R * 0.55 * Math.Min(1, (t - 0.3) / 0.5) * (0.4 + Hs(i + 20)); double x = cx, y = cy;
                for (int s = 1; s <= 6; s++) { double nx = x + Math.Cos(a + (Hs(i * 7 + s) - 0.5) * 0.9) * len / 6, ny = y + Math.Sin(a + (Hs(i * 7 + s) - 0.5) * 0.9) * len / 6; g.Line(x, y, nx, ny, 0xFFFFFFFF, 1.5, 0.6 * (1 - (t - 0.3) / 2.7)); x = nx; y = ny; }
            }
        if (t > 2.4)
        {
            double a = Math.Clamp((t - 2.4) / 0.8, 0, 1);
            g.TextIn(new Rect(b.X, cy - g.U(60), b.Width, g.U(60)), "SYSTEM DESTROYED", g.U(46), 0xFFFF3030, Align.Center, true, a * (0.85 + 0.15 * Math.Sin(t * 30)));
            g.TextIn(new Rect(b.X, cy + g.U(6), b.Width, g.U(24)), "// SIMULATION ONLY — NO REAL DATA WAS AFFECTED //", g.U(13), 0xFFFFFFFF, Align.Center, false, a * 0.7);
            if (t > 4.5) g.TextIn(new Rect(b.X, cy + g.U(40), b.Width, g.U(20)), "REBOOTING" + new string('.', (int)(t * 3) % 4), g.U(13), 0xFF50FF90, Align.Center, true, 0.9);
            for (double y = b.Y; y < b.Bottom; y += 3) if (Hs(y + Math.Floor(t * 20)) < 0.18) g.Fill(new Rect(b.X, y, b.Width, 1), 0xFFFFFFFF, 0.12);
        }
    }
}
