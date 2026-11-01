using System.Speech.Synthesis;
using Blacksite.Core.Audio;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Blacksite.Desktop;

/// <summary>
/// Pulls mixed audio from the shared engine into WASAPI (shared mode, event-driven, MMCSS-scheduled) as 44.1 kHz stereo float,
/// resampled by NAudio to the device mix rate. Falls back to WaveOut. The device can be chosen by name.
/// </summary>
public sealed class WindowsAudioOutput : IAudioOutput
{
    sealed class Provider : ISampleProvider
    {
        readonly AudioEngine _e; float[] _mono = new float[4096];
        public Provider(AudioEngine e) { _e = e; WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(Synth.Rate, 2); }
        public WaveFormat WaveFormat { get; }
        public int Read(float[] buffer, int offset, int count)
        {
            int frames = count / 2;
            if (_mono.Length < frames) _mono = new float[frames];
            _e.Mix(_mono, frames);
            for (int i = 0; i < frames; i++) { buffer[offset + 2 * i] = _mono[i]; buffer[offset + 2 * i + 1] = _mono[i]; }
            return frames * 2;
        }
    }

    IWavePlayer? _out;
    public string Status { get; private set; } = "Starting";

    public IReadOnlyList<string> Devices
    {
        get
        {
            var l = new List<string>();
            try { using var en = new MMDeviceEnumerator(); foreach (var d in en.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)) l.Add(d.FriendlyName); } catch { }
            return l;
        }
    }

    public void Start(AudioEngine engine, string? device)
    {
        Stop();
        var src = new Provider(engine);
        try
        {
            using var en = new MMDeviceEnumerator();
            MMDevice? dev = device != null ? en.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).FirstOrDefault(d => d.FriendlyName == device) : null;
            dev ??= en.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            int rate = dev.AudioClient.MixFormat.SampleRate;
            ISampleProvider chain = rate == Synth.Rate ? src : new WdlResamplingSampleProvider(src, rate);
            var w = new WasapiOut(dev, AudioClientShareMode.Shared, true, 100);
            w.Init(chain); w.Play(); _out = w;
            Status = $"Output: {dev.FriendlyName} (WASAPI {rate / 1000.0:0.#} kHz)";
        }
        catch (Exception e)
        {
            try
            {
                int dn = -1; if (device != null) { for (int i = 0; i < WaveOut.DeviceCount; i++) if (WaveOut.GetCapabilities(i).ProductName == device) dn = i; }
                var w = new WaveOutEvent { DeviceNumber = dn, DesiredLatency = 200, NumberOfBuffers = 4 };
                w.Init(src); w.Play(); _out = w; Status = "Output: WaveOut fallback";
            }
            catch (Exception e2) { Status = "Audio unavailable: " + e2.Message; _out = null; }
        }
    }

    public void Stop() { try { _out?.Stop(); _out?.Dispose(); } catch { } _out = null; }
    public void Dispose() => Stop();
}
/// <summary>Offline speech via the Windows SAPI voices installed on the machine.</summary>
public sealed class WindowsSpeech : ISpeechService
{
    SpeechSynthesizer? _s;
    public bool Available { get; private set; }

    public WindowsSpeech()
    {
        try { _s = new SpeechSynthesizer(); Available = _s.GetInstalledVoices().Count > 0; } catch { Available = false; }
    }

    public void Speak(string text, double volume, string persona)
    {
        if (_s == null || !Available) return;
        try
        {
            _s.SpeakAsyncCancelAll();
            _s.Volume = (int)Math.Clamp(volume * 100, 0, 100);
            _s.Rate = persona switch { "TACTICAL" => 2, "ANALYTIC" => 1, "CALM" => -2, "Lockdown" => 1, _ => 0 };
            var voices = _s.GetInstalledVoices().Where(v => v.Enabled).ToList();
            if (voices.Count > 1)
            {
                int idx = persona switch { "ANALYTIC" => 1, "TACTICAL" => 0, "CALM" => 1, _ => 0 } % voices.Count;
                _s.SelectVoice(voices[idx].VoiceInfo.Name);
            }
            _s.SpeakAsync(text);
        }
        catch { }
    }
}
