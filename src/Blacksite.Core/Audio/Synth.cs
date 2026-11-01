using Blacksite.Core.Presets;

namespace Blacksite.Core.Audio;

/// <summary>Procedural sound generator. All audio is original, synthesised at runtime (no copyrighted assets).</summary>
public static class Synth
{
    public const int Rate = 44100;
    const double Tau = Math.PI * 2;

    static float[] Buf(double seconds) => new float[(int)(seconds * Rate)];

    static float Env(int i, int n, double attack, double release)
    {
        double t = (double)i / Rate, total = (double)n / Rate;
        double a = attack <= 0 ? 1 : Math.Min(1, t / attack);
        double r = release <= 0 ? 1 : Math.Min(1, (total - t) / release);
        return (float)Math.Clamp(Math.Min(a, r), 0, 1);
    }

    public static float[] Tone(double freq, double dur, double vol = 0.5, double attack = 0.005, double release = 0.05, int harmonics = 1)
    {
        var b = Buf(dur);
        for (int i = 0; i < b.Length; i++)
        {
            double t = (double)i / Rate, s = 0;
            for (int h = 1; h <= harmonics; h++) s += Math.Sin(Tau * freq * h * t) / h;
            b[i] = (float)(s * vol) * Env(i, b.Length, attack, release);
        }
        return b;
    }

    static float[] Concat(params float[][] parts)
    {
        var r = new float[parts.Sum(p => p.Length)];
        int o = 0; foreach (var p in parts) { Array.Copy(p, 0, r, o, p.Length); o += p.Length; }
        return r;
    }

    static void Mix(float[] dst, float[] src, int offset, float gain = 1)
    {
        for (int i = 0; i < src.Length && i + offset < dst.Length; i++) dst[i + offset] += src[i] * gain;
    }

    public static float[] Click()
    {
        var b = Buf(0.05); var r = new Random(1);
        for (int i = 0; i < b.Length; i++)
            b[i] = (float)((r.NextDouble() * 2 - 1) * 0.25 + Math.Sin(Tau * 1800 * i / Rate) * 0.3) * Env(i, b.Length, 0.001, 0.04);
        return b;
    }

    public static float[] Beep() => Tone(1320, 0.09, 0.35, 0.004, 0.03);
    public static float[] Confirm() => Concat(Tone(880, 0.07, 0.35), Tone(1320, 0.12, 0.35, 0.004, 0.08));
    public static float[] ErrorSound() => Concat(Tone(180, 0.14, 0.45, 0.005, 0.03, 4), Tone(150, 0.2, 0.45, 0.005, 0.1, 4));

    public static float[] Ping()
    {
        var b = Buf(1.4);
        for (int i = 0; i < b.Length; i++)
        {
            double t = (double)i / Rate;
            b[i] = (float)(Math.Sin(Tau * 1100 * t) * Math.Exp(-t * 4.2) * 0.45 + Math.Sin(Tau * 2210 * t) * Math.Exp(-t * 6) * 0.15);
        }
        return b;
    }

    public static float[] Alarm()
    {
        var b = Buf(1.6);
        double ph = 0;
        for (int i = 0; i < b.Length; i++)
        {
            double t = (double)i / Rate;
            double f = 520 + 380 * (0.5 + 0.5 * Math.Sin(Tau * 1.25 * t - Math.PI / 2));
            ph += Tau * f / Rate;
            double s = Math.Sin(ph) + 0.4 * Math.Sin(ph * 2) + 0.2 * Math.Sin(ph * 3);
            b[i] = (float)(s * 0.3) * Env(i, b.Length, 0.02, 0.05);
        }
        return b;
    }

    public static float[] Radio()
    {
        var b = Buf(0.9); var r = new Random(7); double lp = 0;
        for (int i = 0; i < b.Length; i++)
        {
            double t = (double)i / Rate;
            double n = r.NextDouble() * 2 - 1; lp += (n - lp) * 0.5;
            double burst = t < 0.15 ? 1 : Math.Exp(-(t - 0.15) * 7);
            double chirp = i > Rate * 0.55 ? Math.Sin(Tau * 1700 * t) * Math.Exp(-(t - 0.55) * 18) * 0.5 : 0;
            double beep = t < 0.12 ? Math.Sin(Tau * 2100 * t) * 0.3 : 0;
            b[i] = (float)((lp * 0.35 * burst) + chirp + beep) * Env(i, b.Length, 0.002, 0.05);
        }
        return b;
    }

    public static float[] Boot()
    {
        var b = Buf(2.6);
        double ph = 0;
        for (int i = 0; i < Rate * 1.6; i++)
        {
            double t = (double)i / Rate;
            ph += Tau * (60 + 520 * t * t) / Rate;
            b[i] += (float)(Math.Sin(ph) * 0.3 * Math.Min(1, t * 4));
        }
        double[] notes = { 440, 554.4, 659.3, 880, 1108.7 };
        for (int k = 0; k < notes.Length; k++) Mix(b, Tone(notes[k], 0.5, 0.22, 0.01, 0.4, 2), (int)(Rate * (1.2 + k * 0.17)));
        return b;
    }

    public static float[] Mech()
    {
        var b = Buf(0.45); var r = new Random(11); double lp = 0;
        for (int i = 0; i < b.Length; i++)
        {
            double t = (double)i / Rate;
            lp += ((r.NextDouble() * 2 - 1) - lp) * 0.08;
            b[i] = (float)(Math.Sin(Tau * 70 * t) * Math.Exp(-t * 14) * 0.7 + lp * Math.Exp(-t * 9) * 0.5);
        }
        return b;
    }

    public static float[] Whoosh()
    {
        var b = Buf(1.8); var r = new Random(5); double lp = 0;
        for (int i = 0; i < b.Length; i++)
        {
            double t = (double)i / Rate, k = Math.Sin(Math.PI * t / 1.8);
            lp += ((r.NextDouble() * 2 - 1) - lp) * (0.02 + 0.3 * k * k);
            b[i] = (float)(lp * 1.4 * k);
        }
        return b;
    }

    public static float[] Static()
    {
        var b = Buf(0.5); var r = new Random(3);
        for (int i = 0; i < b.Length; i++) b[i] = (float)((r.NextDouble() * 2 - 1) * 0.08) * Env(i, b.Length, 0.04, 0.2);
        return b;
    }

    public static float[] RadarPulse()
    {
        var b = Buf(1.6);
        for (int i = 0; i < b.Length; i++)
        {
            double t = (double)i / Rate;
            b[i] = (float)(Math.Sin(Tau * (640 - 180 * Math.Min(1, t * 2)) * t) * Math.Exp(-t * 3.2) * 0.42 + Math.Sin(Tau * 1280 * t) * Math.Exp(-t * 7) * 0.12);
        }
        return b;
    }

    public static float[] Handshake()
    {
        double[] f = { 1200, 1800, 900, 2100, 1500, 2400, 1000, 1900 };
        var parts = f.Select(x => Tone(x, 0.055, 0.28, 0.003, 0.02, 2)).ToArray();
        return Concat(parts);
    }

    public static float[] Ecg() => Tone(1000, 0.085, 0.32, 0.003, 0.03);

    public static float[] Scan()
    {
        var b = Buf(1.6); var r = new Random(9); double ph = 0, lp = 0;
        for (int i = 0; i < b.Length; i++)
        {
            double t = (double)i / Rate; ph += Tau * (300 + 1500 * t / 1.6) / Rate;
            lp += ((r.NextDouble() * 2 - 1) - lp) * 0.2;
            b[i] = (float)((Math.Sin(ph) * 0.25 + lp * 0.08) * Math.Sin(Math.PI * t / 1.6));
        }
        return b;
    }

    public static float[] Tick()
    {
        var b = Buf(0.03); var r = new Random(21);
        for (int i = 0; i < b.Length; i++) b[i] = (float)((r.NextDouble() * 2 - 1) * Math.Exp(-i / (double)Rate * 220) * 0.45);
        return b;
    }

    public static float[] Access() => Concat(Tone(1568, 0.07, 0.3, 0.004, 0.02), Tone(1175, 0.07, 0.3, 0.004, 0.02), Tone(784, 0.14, 0.3, 0.004, 0.08));
    public static float[] Chime() => Concat(Tone(659, 0.16, 0.3, 0.005, 0.12, 2), Tone(880, 0.16, 0.3, 0.005, 0.12, 2), Tone(1318, 0.4, 0.3, 0.005, 0.3, 2));
    public static float[] DeepBeep() => Tone(196, 0.22, 0.45, 0.006, 0.08, 3);
    public static float[] Triple() => Concat(Tone(1760, 0.08, 0.32, 0.003, 0.02), Tone(0.0001, 0.05, 0), Tone(1760, 0.08, 0.32, 0.003, 0.02), Tone(0.0001, 0.05, 0), Tone(1760, 0.08, 0.32, 0.003, 0.02));

    public static float[] Defib()
    {
        var b = Buf(2.0); double ph = 0;
        for (int i = 0; i < Rate * 1.6; i++) { double t = (double)i / Rate; ph += Tau * (200 + 900 * t / 1.6) / Rate; b[i] = (float)(Math.Sin(ph) * 0.28 * Math.Min(1, t * 4)); }
        Mix(b, Mech(), (int)(Rate * 1.62), 1.3f);
        return b;
    }

    public static float[] DeepTone()
    {
        var b = Buf(2.4);
        for (int i = 0; i < b.Length; i++)
        {
            double t = (double)i / Rate, env = Math.Sin(Math.PI * t / 2.4);
            b[i] = (float)((Math.Sin(Tau * 110 * t) * 0.3 + Math.Sin(Tau * 164.8 * t + Math.Sin(t * 3) * 2) * 0.18 + Math.Sin(Tau * 3520 * t) * 0.03 * Math.Sin(Tau * 6 * t)) * env);
        }
        return b;
    }

    public static float[] Surge()
    {
        var b = Buf(1.2); double ph = 0; var r = new Random(4);
        for (int i = 0; i < b.Length; i++) { double t = (double)i / Rate; ph += Tau * (60 + 340 * Math.Exp(-t * 3)) / Rate; b[i] = (float)((Math.Sin(ph) * 0.4 + (r.NextDouble() - 0.5) * 0.15) * Math.Exp(-t * 2.2)); }
        return b;
    }

    public static float[] ByName(string name) => Declick(ByNameRaw(name));

    static float[] ByNameRaw(string name) => name switch
    {
        "radarpulse" => RadarPulse(), "handshake" => Handshake(), "ecg" => Ecg(), "scan" => Scan(), "tick" => Tick(), "access" => Access(),
        "chime" => Chime(), "deepbeep" => DeepBeep(), "triple" => Triple(), "defib" => Defib(), "tone" => DeepTone(), "surge" => Surge(),
        _ => ByNameBase(name),
    };
    static float[] Declick(float[] d) { int n = Math.Min(d.Length / 4, (int)(Rate * 0.004)); for (int i = 0; i < n; i++) { float g = (float)i / n; d[i] *= g; d[d.Length - 1 - i] *= g; } return d; }

    static float[] ByNameBase(string name) => name switch
    {
        "click" => Click(), "beep" => Beep(), "confirm" => Confirm(), "error" => ErrorSound(),
        "ping" => Ping(), "alarm" => Alarm(), "radio" => Radio(), "boot" => Boot(),
        "mech" => Mech(), "whoosh" => Whoosh(), "static" => Static(), _ => Beep(),
    };

    public static readonly string[] Names = { "click", "beep", "confirm", "error", "ping", "alarm", "radio", "boot", "mech", "whoosh", "static",
        "radarpulse", "handshake", "ecg", "scan", "tick", "access", "chime", "deepbeep", "triple", "defib", "tone", "surge" };

    /// <summary>
    /// Seamless 12 s ambient bed: a slowly breathing pad of detuned tonal partials (no noise bed, no hiss), a very quiet
    /// low-passed "air" layer, and a soft rhythmic element per profile. Every frequency is a whole number of cycles per loop, so it tiles perfectly.
    /// </summary>
    public static float[] Ambient(SoundProfile p)
    {
        const double dur = 12.0;
        var b = Buf(dur);
        (double root, double[] ratios, double air, double hum, double pulseEvery, double pulseHz, double shimmer) c = p switch
        {
            SoundProfile.Space => (55, new[] { 1, 1.5, 2, 3.0 }, 0.010, 0, 0, 0, 0),
            SoundProfile.Terminal => (82.4, new[] { 1, 1.5, 2, 3.0 }, 0.006, 0, 3, 1400, 0),
            SoundProfile.Surveillance => (49, new[] { 1, 1.26, 1.5 }, 0.010, 0.05, 0, 0, 0),
            SoundProfile.Ai => (110, new[] { 1, 1.5, 2, 2.5, 3.0 }, 0.004, 0, 0, 0, 0.04),
            SoundProfile.Lockdown => (55, new[] { 1, 1.06, 1.5 }, 0.008, 0.06, 4, 55, 0),
            SoundProfile.Satellite => (73.4, new[] { 1, 1.5, 2, 2.25 }, 0.006, 0, 0, 0, 0.02),
            SoundProfile.Reactor => (62, new[] { 1.0, 2, 3, 4 }, 0.014, 0.12, 0, 0, 0),
            SoundProfile.Cyber => (98, new[] { 1, 1.5, 1.78 }, 0.005, 0, 2, 1800, 0),
            SoundProfile.DeepSpace => (41.2, new[] { 1, 1.5, 2, 3.0 }, 0.012, 0, 0, 0, 0),
            SoundProfile.Tactical => (65.4, new[] { 1, 1.335, 1.5 }, 0.010, 0.04, 0, 0, 0),
            SoundProfile.Orbital => (58.3, new[] { 1, 1.5, 2, 2.667 }, 0.006, 0, 4, 880, 0.02),
            SoundProfile.Quantum => (92.5, new[] { 1, 1.5, 2, 2.5, 3.0, 4.5 }, 0.003, 0, 0, 0, 0.08),
            SoundProfile.DeepComms => (36.7, new[] { 1, 1.5, 2 }, 0.022, 0, 0, 0, 0),
            SoundProfile.Medical => (130.8, new[] { 1, 1.25, 1.5, 2 }, 0.003, 0, 1.2, 70, 0),
            _ => (48, new[] { 1.0, 2, 3 }, 0.015, 0.1, 0, 0, 0),
        };
        double Snap(double hz) => Math.Max(1.0 / dur, Math.Round(hz * dur) / dur);
        var rnd = new Random((int)p * 31 + 7);
        var parts = new List<(double f, double ph, double lfo, double lph, double amp)>();
        for (int i = 0; i < c.ratios.Length; i++)
            for (int d = -1; d <= 1; d += 2)                                         // two slightly detuned copies -> slow beating
                parts.Add((Snap(c.root * c.ratios[i] + d * 0.17), rnd.NextDouble() * Tau, Snap(0.05 + 0.04 * i), rnd.NextDouble() * Tau, 1.0 / (1 + i * 0.9)));
        // quiet air layer: low-passed noise, crossfaded so it tiles
        var air = new float[b.Length]; double lp = 0, lp2 = 0;
        for (int i = 0; i < air.Length; i++) { double n = rnd.NextDouble() * 2 - 1; lp += (n - lp) * 0.02; lp2 += (lp - lp2) * 0.02; air[i] = (float)lp2; }
        int xf = Rate; for (int i = 0; i < xf; i++) { float a = (float)i / xf; air[i] = air[i] * a + air[air.Length - xf + i] * (1 - a); }
        for (int i = 0; i < b.Length; i++)
        {
            double t = (double)i / Rate, s = 0;
            foreach (var q in parts) s += Math.Sin(Tau * q.f * t + q.ph) * q.amp * (0.62 + 0.38 * Math.Sin(Tau * q.lfo * t + q.lph));
            s *= 0.16;
            if (c.hum > 0) s += c.hum * 0.12 * (Math.Sin(Tau * Snap(60) * t) + 0.5 * Math.Sin(Tau * Snap(120) * t) + 0.25 * Math.Sin(Tau * Snap(180) * t));
            if (c.shimmer > 0) s += c.shimmer * 0.06 * Math.Sin(Tau * Snap(c.root * 8.02) * t) * (0.5 + 0.5 * Math.Sin(Tau * Snap(0.5) * t));
            s += air[i] * c.air * 14;
            if (c.pulseEvery > 0)
            {   // soft periodic element (sonar / heartbeat / data tick), phase-locked to the loop
                double ph = (t % c.pulseEvery), env = Math.Exp(-ph * (c.pulseHz < 200 ? 9 : 14)) * Math.Min(1, ph * 400);
                s += Math.Sin(Tau * c.pulseHz * ph) * env * (c.pulseHz < 200 ? 0.07 : 0.025);
            }
            b[i] = (float)s;
        }
        float peak = b.Max(Math.Abs); if (peak > 0) { float g = 0.42f / peak; for (int i = 0; i < b.Length; i++) b[i] *= g; }
        return b;
    }
}