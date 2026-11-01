using Android.Content;
using Android.Media;
using Android.Speech.Tts;
using Blacksite.Core.Audio;

namespace Blacksite.Android;

/// <summary>Streams the shared mixer into an AudioTrack and cooperates with Android audio focus.</summary>
public sealed class AndroidAudioOutput : Java.Lang.Object, IAudioOutput, AudioManager.IOnAudioFocusChangeListener
{
    readonly Context _ctx;
    AudioTrack? _track;
    Thread? _thread;
    volatile bool _run, _focus = true;
    AudioFocusRequestClass? _req;
    public string Status { get; private set; } = "Starting";
    public IReadOnlyList<string> Devices => Array.Empty<string>();

    public AndroidAudioOutput(Context ctx) { _ctx = ctx; }

    public void Start(AudioEngine engine, string? device)
    {
        Stop();
        try
        {
            var am = (AudioManager)_ctx.GetSystemService(Context.AudioService)!;
            var attrs = new AudioAttributes.Builder()!.SetUsage(AudioUsageKind.Media)!.SetContentType(AudioContentType.Sonification)!.Build()!;
            _req = new AudioFocusRequestClass.Builder(AudioFocus.GainTransientMayDuck)!.SetAudioAttributes(attrs)!.SetOnAudioFocusChangeListener(this)!.Build();
            am.RequestAudioFocus(_req!);
            int min = AudioTrack.GetMinBufferSize(Synth.Rate, ChannelOut.Mono, Encoding.Pcm16bit);
            _track = new AudioTrack.Builder()
                .SetAudioAttributes(attrs)!
                .SetAudioFormat(new AudioFormat.Builder()!.SetEncoding(Encoding.Pcm16bit)!.SetSampleRate(Synth.Rate)!.SetChannelMask(ChannelOut.Mono)!.Build()!)!
                .SetBufferSizeInBytes(Math.Max(min, 8192) * 2)!
                .SetTransferMode(AudioTrackMode.Stream)!.Build();
            _track.Play();
            _run = true;
            _thread = new Thread(() =>
            {
                try { global::Android.OS.Process.SetThreadPriority(global::Android.OS.ThreadPriority.UrgentAudio); } catch { }
                var f = new float[2048]; var s = new short[2048];
                while (_run)
                {
                    if (!_focus) { Thread.Sleep(50); continue; }
                    engine.Mix(f, f.Length);
                    for (int i = 0; i < f.Length; i++) s[i] = (short)(Math.Clamp(f[i], -1f, 1f) * 32000);
                    _track?.Write(s, 0, s.Length);
                }
            }) { IsBackground = true, Name = "BlacksiteAudio" };
            _thread.Start();
            Status = "Output: Android AudioTrack";
        }
        catch (Exception e) { Status = "Audio unavailable: " + e.Message; }
    }

    public void OnAudioFocusChange(AudioFocus focusChange)
    {
        _focus = focusChange is AudioFocus.Gain or AudioFocus.GainTransient or AudioFocus.GainTransientMayDuck;
        Status = _focus ? "Output: Android AudioTrack" : "Paused (audio focus lost)";
    }

    public void Stop()
    {
        _run = false;
        try { _thread?.Join(200); _track?.Stop(); _track?.Release(); } catch { }
        _track = null;
        try { if (_req != null) ((AudioManager)_ctx.GetSystemService(Context.AudioService)!).AbandonAudioFocusRequest(_req); } catch { }
    }
}

/// <summary>Offline speech via the device's installed text-to-speech engine (quality and language depend on the device).</summary>
public sealed class AndroidSpeech : Java.Lang.Object, ISpeechService, TextToSpeech.IOnInitListener
{
    TextToSpeech? _tts; bool _ready;
    public bool Available => _ready;
    public AndroidSpeech(Context ctx) { try { _tts = new TextToSpeech(ctx, this); } catch { } }
    public void OnInit(OperationResult status) { _ready = status == OperationResult.Success; try { _tts?.SetLanguage(Java.Util.Locale.Us); } catch { } }

    public void Speak(string text, double volume, string persona)
    {
        if (!_ready || _tts == null) return;
        try
        {
            _tts.SetPitch(persona switch { "TACTICAL" => 0.8f, "CALM" => 0.9f, "ANALYTIC" => 1.05f, _ => 0.85f });
            _tts.SetSpeechRate(persona == "TACTICAL" ? 1.1f : persona == "CALM" ? 0.85f : 1f);
            var p = new Bundle(); p.PutFloat(TextToSpeech.Engine.KeyParamVolume, (float)Math.Clamp(volume, 0, 1));
            _tts.Speak(text, QueueMode.Flush, p, "bs");
        }
        catch { }
    }
}
