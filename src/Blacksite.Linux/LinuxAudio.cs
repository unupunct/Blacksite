using System.Diagnostics;
using Blacksite.Core.Audio;

namespace Blacksite.Linux;

/// <summary>
/// Streams the shared engine's mix as raw 44.1 kHz stereo float into whichever system player is installed
/// (pacat for PulseAudio / PipeWire-pulse, pw-cat for PipeWire, aplay for ALSA). The pipe's back-pressure paces the pump thread.
/// </summary>
public sealed class LinuxAudioOutput : IAudioOutput
{
    static readonly (string exe, string args, string name)[] Players =
    {
        ("pacat", "--playback --raw --format=float32le --rate=44100 --channels=2 --latency-msec=80 --client-name=BLACKSITE", "PulseAudio / PipeWire (pacat)"),
        ("pw-cat", "--playback --format f32 --rate 44100 --channels 2 --latency 80ms -", "PipeWire (pw-cat)"),
        ("aplay", "-q -t raw -f FLOAT_LE -r 44100 -c 2 --buffer-time=120000 -", "ALSA (aplay)"),
    };

    Process? _proc; Thread? _thread; volatile bool _run;
    public string Status { get; private set; } = "Starting";
    public IReadOnlyList<string> Devices => Array.Empty<string>();

    public void Start(AudioEngine engine, string? device)
    {
        Stop();
        foreach (var (exe, args, name) in Players)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args) { RedirectStandardInput = true, UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
                var p = Process.Start(psi);
                if (p == null) continue;
                Thread.Sleep(250);
                if (p.HasExited) continue;
                _proc = p; _run = true;
                _thread = new Thread(() => Pump(engine, p)) { IsBackground = true, Name = "blacksite-audio", Priority = ThreadPriority.AboveNormal };
                _thread.Start();
                Status = "Output: " + name;
                return;
            }
            catch { }
        }
        Status = "Audio unavailable: install pulseaudio-utils, pipewire-bin or alsa-utils";
    }

    void Pump(AudioEngine engine, Process p)
    {
        const int frames = 1024;
        var mono = new float[frames]; var bytes = new byte[frames * 2 * sizeof(float)]; var stereo = new float[frames * 2];
        try
        {
            var stdin = p.StandardInput.BaseStream;
            while (_run && !p.HasExited)
            {
                engine.Mix(mono, frames);
                for (int i = 0; i < frames; i++) { stereo[2 * i] = mono[i]; stereo[2 * i + 1] = mono[i]; }
                Buffer.BlockCopy(stereo, 0, bytes, 0, bytes.Length);
                stdin.Write(bytes, 0, bytes.Length); stdin.Flush();
            }
        }
        catch { }
        if (_run) Status = "Audio stopped (player exited)";
    }

    public void Stop()
    {
        _run = false;
        try { _proc?.StandardInput.Close(); } catch { }
        try { if (_proc is { HasExited: false }) _proc.Kill(); } catch { }
        try { _thread?.Join(500); } catch { }
        _proc = null; _thread = null;
    }

    public void Dispose() => Stop();
}

/// <summary>Offline speech through espeak-ng / espeak / spd-say when one of them is installed.</summary>
public sealed class LinuxSpeech : ISpeechService
{
    readonly string? _exe;
    Process? _cur;
    public bool Available => _exe != null;

    public LinuxSpeech()
    {
        foreach (var e in new[] { "espeak-ng", "espeak", "spd-say" })
            if (OnPath(e)) { _exe = e; break; }
    }

    static bool OnPath(string exe) =>
        (Environment.GetEnvironmentVariable("PATH") ?? "").Split(':', StringSplitOptions.RemoveEmptyEntries).Any(d => File.Exists(Path.Combine(d, exe)));

    public void Speak(string text, double volume, string persona)
    {
        if (_exe == null || string.IsNullOrWhiteSpace(text)) return;
        try
        {
            try { if (_cur is { HasExited: false }) _cur.Kill(); } catch { }
            int rate = persona switch { "TACTICAL" => 175, "ANALYTIC" => 165, "CALM" => 135, "Lockdown" => 170, _ => 155 };
            int vol = (int)Math.Clamp(volume * 200, 0, 200), pitch = persona switch { "CALM" => 40, "TACTICAL" => 35, _ => 50 };
            var psi = new ProcessStartInfo(_exe) { UseShellExecute = false, CreateNoWindow = true };
            if (_exe == "spd-say") { psi.ArgumentList.Add("-r"); psi.ArgumentList.Add(((rate - 155) / 2).ToString()); psi.ArgumentList.Add(text); }
            else { psi.ArgumentList.Add("-s"); psi.ArgumentList.Add(rate.ToString()); psi.ArgumentList.Add("-a"); psi.ArgumentList.Add(vol.ToString()); psi.ArgumentList.Add("-p"); psi.ArgumentList.Add(pitch.ToString()); psi.ArgumentList.Add(text); }
            _cur = Process.Start(psi);
        }
        catch { }
    }
}
