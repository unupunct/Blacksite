using Blacksite.Core.Configuration;
using Blacksite.Core.Presets;

namespace Blacksite.Core.Audio;

public enum SoundCategory { Ui, Ambient, Alarm, Event }

/// <summary>Platform adapter: plays PCM float mono 22050 Hz pulled from the engine mixer.</summary>
public interface IAudioOutput : IDisposable
{
    string Status { get; }
    IReadOnlyList<string> Devices { get; }
    void Start(AudioEngine engine, string? device);
    void Stop();
}

/// <summary>Offline text-to-speech provided by the OS.</summary>
public interface ISpeechService
{
    bool Available { get; }
    void Speak(string text, double volume, string persona);
}

public sealed class NullAudioOutput : IAudioOutput
{
    public string Status => "No audio device";
    public IReadOnlyList<string> Devices => Array.Empty<string>();
    public void Start(AudioEngine engine, string? device) { }
    public void Stop() { }
    public void Dispose() { }
}

public sealed class NullSpeech : ISpeechService
{
    public bool Available => false;
    public void Speak(string text, double volume, string persona) { }
}

/// <summary>Shared mixer and sound library. Output adapters call <see cref="Mix"/> from their audio thread.</summary>
public sealed class AudioEngine
{
    sealed class Voice
    {
        public float[] Data = null!;
        public int Pos;
        public bool Loop;
        public SoundCategory Cat;
        public float Gain = 1, Target = 1, Slew = 0;
        public bool KillWhenSilent;
        public string Key = "";
    }

    readonly object _lock = new();
    readonly List<Voice> _voices = new();
    readonly Dictionary<string, float[]> _cache = new();
    readonly AppSettings _s;
    readonly Random _rng = new();
    SoundProfile? _profile;
    double _ambTimer = 6;

    public ISpeechService Speech { get; set; } = new NullSpeech();
    public IAudioOutput Output { get; private set; } = new NullAudioOutput();
    public string Status => _s.Muted ? "MUTED" : Output.Status;
    public float Peak { get; private set; }
    /// <summary>True while the app is backgrounded and continuous background audio is off: the mixer outputs silence.</summary>
    public volatile bool Suspended;
    /// <summary>Multiplier for the ambient category (cinematic sequences fade the background out).</summary>
    public volatile float AmbientDuck = 1f;
    float _duckNow = 1f;

    readonly Reverb _verb = new();
    public AudioEngine(AppSettings s) { _s = s; }

    /// <summary>Small Schroeder room reverb (4 damped combs + 2 all-passes) for a more natural, less dry sound.</summary>
    sealed class Reverb
    {
        readonly float[][] _comb; readonly int[] _ci; readonly float[] _damp; readonly float[][] _ap; readonly int[] _ai;
        public Reverb()
        {
            double k = Synth.Rate / 44100.0; int[] cd = { 1557, 1617, 1491, 1422 }, ad = { 225, 556 };
            _comb = cd.Select(d => new float[(int)(d * k)]).ToArray(); _ci = new int[4]; _damp = new float[4];
            _ap = ad.Select(d => new float[(int)(d * k)]).ToArray(); _ai = new int[2];
        }
        public float Process(float x)
        {
            float sum = 0;
            for (int c = 0; c < 4; c++)
            {
                var buf = _comb[c]; float y = buf[_ci[c]];
                _damp[c] = y * 0.7f + _damp[c] * 0.3f;
                buf[_ci[c]] = x * 0.3f + _damp[c] * 0.80f; if (++_ci[c] >= buf.Length) _ci[c] = 0;
                sum += y;
            }
            for (int a = 0; a < 2; a++)
            {
                var buf = _ap[a]; float bo = buf[_ai[a]]; float y = -sum + bo; buf[_ai[a]] = sum + bo * 0.5f; if (++_ai[a] >= buf.Length) _ai[a] = 0; sum = y;
            }
            return sum * 0.25f;
        }
    }

    public void Attach(IAudioOutput output)
    {
        Output.Stop();
        Output = output;
        try { Output.Start(this, _s.AudioDevice); } catch { Output = new NullAudioOutput(); }
    }

    float[] Get(string key)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var d)) return d;
        }
        var made = key.StartsWith("amb:") ? Synth.Ambient(Enum.Parse<SoundProfile>(key[4..])) : Synth.ByName(key);
        lock (_lock) { _cache[key] = made; }
        return made;
    }

    public void Warm() { foreach (var n in Synth.Names) Get(n); foreach (var p in Enum.GetValues<SoundProfile>()) Get("amb:" + p); }

    float CategoryVolume(SoundCategory c) => (float)(_s.Muted ? 0 : _s.MasterVolume * c switch
    {
        SoundCategory.Ui => _s.MuteUi ? 0 : _s.UiVolume,
        SoundCategory.Ambient => _s.MuteAmbientCat ? 0 : _s.AmbientVolume,
        SoundCategory.Event => _s.MuteEvent ? 0 : _s.EventVolume,
        _ => _s.MuteAlarm ? 0 : _s.AlarmVolume,
    });

    public void Play(string name, SoundCategory cat = SoundCategory.Ui, float gain = 1f)
    {
        if (_s.Muted) return;
        float[]? data; lock (_lock) { _cache.TryGetValue(name, out data); }
        if (data == null) { Task.Run(() => Get(name)); return; }
        lock (_lock)
        {
            if (_voices.Count(v => !v.Loop) > 24) _voices.RemoveAll(v => !v.Loop && v.Pos > 0 && _voices.Count > 24);
            _voices.Add(new Voice { Data = data, Cat = cat, Gain = gain, Target = gain, Key = name });
        }
    }

    public void Click() => Play("click");
    public void Confirm() => Play("confirm");
    public void Error() => Play("error");

    public void Say(string text)
    {
        if (_s.Muted) return;
        Speech.Speak(text, _s.MasterVolume * _s.VoiceVolume, _profile?.ToString() ?? "");
    }

    public void Say(string text, string persona)
    {
        if (_s.Muted) return;
        Speech.Speak(text, _s.MasterVolume * _s.VoiceVolume, persona);
    }

    public void SetProfile(SoundProfile p)
    {
        _profile = p;
        SyncAmbient();
    }

    /// <summary>Crossfades the ambient loop to match current profile and ambient toggle.</summary>
    public void SyncAmbient()
    {
        if (_profile == null) return;
        string key = "amb:" + _profile;
        bool want = _s.AmbientAudio && !_s.Muted;
        Task.Run(() =>
        {
            var data = Get(key);
            lock (_lock)
            {
                foreach (var v in _voices.Where(v => v.Cat == SoundCategory.Ambient && v.Key != key)) { v.Target = 0; v.Slew = 1f / (Synth.Rate * 1.5f); v.KillWhenSilent = true; }
                var cur = _voices.FirstOrDefault(v => v.Cat == SoundCategory.Ambient && v.Key == key && !v.KillWhenSilent);
                if (cur == null && want)
                    _voices.Add(new Voice { Data = data, Loop = true, Cat = SoundCategory.Ambient, Gain = 0, Target = 1, Slew = 1f / (Synth.Rate * 1.5f), Key = key });
                else if (cur != null && !want) { cur.Target = 0; cur.Slew = 1f / (Synth.Rate * 0.8f); cur.KillWhenSilent = true; }
            }
        });
    }

    /// <summary>Randomised ambient blips tied to the active profile.</summary>
    public void Update(double dt)
    {
        if (!_s.RandomAmbientEvents || _s.Muted || !_s.AmbientAudio) return;
        _ambTimer -= dt;
        if (_ambTimer > 0) return;
        _ambTimer = 5 + _rng.NextDouble() * 12;
        string[] pool = (_profile) switch
        {
            SoundProfile.Terminal or SoundProfile.Cyber => new[] { "beep", "tick", "click" },
            SoundProfile.Reactor => new[] { "mech", "beep" },
            SoundProfile.Lockdown => new[] { "beep", "mech" },
            SoundProfile.Tactical => new[] { "radio", "ping", "beep" },
            SoundProfile.Space or SoundProfile.Satellite or SoundProfile.DeepSpace => new[] { "ping", "beep", "whoosh" },
            SoundProfile.Orbital => new[] { "radarpulse", "deepbeep", "handshake" },
            SoundProfile.Quantum => new[] { "tick", "beep", "surge" },
            SoundProfile.DeepComms => new[] { "tone", "deepbeep", "ping" },
            SoundProfile.Medical => new[] { "ecg", "mech", "chime" },
            SoundProfile.Facility => new[] { "mech", "access", "deepbeep" },
            _ => new[] { "beep", "ping" },
        };
        Play(pool[_rng.Next(pool.Length)], SoundCategory.Ambient, 0.35f);
    }

    /// <summary>Called by the output adapter's audio thread.</summary>
    public void Mix(float[] buffer, int count)
    {
        Array.Clear(buffer, 0, count);
        if (Suspended) return;
        float peak = 0;
        lock (_lock)
        {
            _duckNow += (AmbientDuck - _duckNow) * 0.0008f * Math.Max(1, count / 256);
            float cu = CategoryVolume(SoundCategory.Ui), ca = CategoryVolume(SoundCategory.Ambient) * _duckNow, cl = CategoryVolume(SoundCategory.Alarm), ce = CategoryVolume(SoundCategory.Event);
            for (int vi = _voices.Count - 1; vi >= 0; vi--)
            {
                var v = _voices[vi];
                float cv = v.Cat == SoundCategory.Ui ? cu : v.Cat == SoundCategory.Ambient ? ca : v.Cat == SoundCategory.Event ? ce : cl;
                int n = count;
                for (int i = 0; i < n; i++)
                {
                    if (v.Pos >= v.Data.Length)
                    {
                        if (v.Loop) v.Pos = 0; else { v.Pos = int.MaxValue; break; }
                    }
                    if (v.Slew > 0) { if (v.Gain < v.Target) v.Gain = Math.Min(v.Target, v.Gain + v.Slew); else if (v.Gain > v.Target) v.Gain = Math.Max(v.Target, v.Gain - v.Slew); }
                    buffer[i] += v.Data[v.Pos++] * v.Gain * cv * 0.7f;
                }
                if (v.Pos == int.MaxValue || (v.KillWhenSilent && v.Gain <= 0.0001f)) _voices.RemoveAt(vi);
            }
        }
        for (int i = 0; i < count; i++)
        {
            float dry = buffer[i];
            float x = dry * 0.88f + _verb.Process(dry) * 0.20f;
            float ax = Math.Abs(x);
            if (ax > 0.75f) x = Math.Sign(x) * (0.75f + 0.25f * MathF.Tanh((ax - 0.75f) / 0.25f));   // transparent soft-knee limiter above 0.75
            buffer[i] = x;
            float a = Math.Abs(x); if (a > peak) peak = a;
        }
        Peak = peak;
    }
}
