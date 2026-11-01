namespace Blacksite.Core.Configuration;

public enum GraphicsProfile { Low, Balanced, High }
public enum WindowMode { Windowed, Maximized, BorderlessFullscreen, ExclusiveFullscreen }
public enum Orientation { Auto, Landscape, Portrait }

public sealed class CustomPreset
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "CUSTOM";
    public string BasePresetId { get; set; } = "space";
    public string? Primary { get; set; }
    public string? Secondary { get; set; }
    public string? Background { get; set; }
    public string? SoundProfile { get; set; }
}

public sealed class SequenceStep
{
    public string EventId { get; set; } = "";
    public double DelaySeconds { get; set; } = 5;
}

public sealed class CinematicSequence
{
    public string Name { get; set; } = "NEW SEQUENCE";
    public List<SequenceStep> Steps { get; set; } = new();
}

public sealed class PanelLayout
{
    public List<string> Hidden { get; set; } = new();
    public List<string> Order { get; set; } = new();
    public string? Accent { get; set; }
    public double SimSpeed { get; set; } = 1.0;
    public double AlertFrequency { get; set; } = 1.0;
    public double AnimSpeed { get; set; } = 1.0;
}

public sealed class MonitorAssignment
{
    public int MonitorIndex { get; set; }
    public string PresetId { get; set; } = "space";
    public bool Fullscreen { get; set; } = true;
}

/// <summary>All user-tunable state. Serialised as JSON; shared between platforms.</summary>
public sealed class AppSettings
{
    // Appearance
    public string? PrimaryOverride { get; set; }
    public string? SecondaryOverride { get; set; }
    public string? BackgroundOverride { get; set; }
    public double InterfaceBrightness { get; set; } = 1.0;
    public double AnimationSpeed { get; set; } = 1.0;
    public double AnimationIntensity { get; set; } = 1.0;
    public double HudScale { get; set; } = 1.0;
    public double HudOpacity { get; set; } = 1.0;
    public double FontScale { get; set; } = 1.0;
    public int UiDensity { get; set; } = 1; // 0 compact, 1 normal, 2 relaxed
    public int MilitaryLayout { get; set; } = 0;   // 0 phantom, 1 shadow, 2 titan
    public int HackerStyle { get; set; } = 0;   // 0 phosphor CRT, 1 cyber neon, 2 amber ops
    public bool Hologram { get; set; } = true;
    public bool Glow { get; set; } = true;
    public bool Scanlines { get; set; } = true;
    public bool Crt { get; set; } = false;
    public bool Glitch { get; set; } = true;
    public bool Flicker { get; set; } = false;
    public bool ReducedMotion { get; set; } = false;
    public GraphicsProfile Graphics { get; set; } = GraphicsProfile.Balanced;
    public bool GraphicsChosenByUser { get; set; } = false;
    public bool ShowHud { get; set; } = true;
    public bool ShowDiagnostics { get; set; } = false;

    // Audio
    public double MasterVolume { get; set; } = 0.7;
    public double UiVolume { get; set; } = 0.65;
    public double AmbientVolume { get; set; } = 0.3;
    public double AlarmVolume { get; set; } = 0.9;
    public double VoiceVolume { get; set; } = 0.9;
    public double EventVolume { get; set; } = 0.9;
    public bool MuteUi { get; set; } = false;
    public bool MuteAmbientCat { get; set; } = false;
    public bool MuteAlarm { get; set; } = false;
    public bool MuteEvent { get; set; } = false;
    public bool Muted { get; set; } = false;
    public bool AmbientAudio { get; set; } = true;
    public bool RandomAmbientEvents { get; set; } = true;
    public bool ContinuousBackgroundAudio { get; set; } = false;
    public string? AudioDevice { get; set; }

    // Simulation / events
    public double EventFrequency { get; set; } = 1.0;   // multiplier
    public double EventIntensity { get; set; } = 1.0;   // 0..2
    public double UpdateIntervalMs { get; set; } = 100;
    public bool AutoEvents { get; set; } = true;

    // Presets
    public string LastPresetId { get; set; } = "space";
    public List<string> Favorites { get; set; } = new();
    public List<string> PresetOrder { get; set; } = new();
    public List<CustomPreset> CustomPresets { get; set; } = new();
    public List<CinematicSequence> Sequences { get; set; } = new();
    public Dictionary<string, PanelLayout> PanelLayouts { get; set; } = new();
    public bool AutoRotate { get; set; } = false;
    public int AutoRotateSeconds { get; set; } = 45;
    public bool RestoreLastPreset { get; set; } = true;

    // Modes
    public bool AmbientMode { get; set; } = false;
    public int ScreenSaverMinutes { get; set; } = 0; // 0 = off
    public bool KeepScreenOn { get; set; } = false;
    public double ScreenBrightness { get; set; } = -1; // -1 = system
    public Orientation Orientation { get; set; } = Orientation.Auto;
    public bool BatteryAware { get; set; } = true;
    public bool HeatWarningAccepted { get; set; } = false;

    // Windows
    public WindowMode WindowMode { get; set; } = WindowMode.Windowed;
    public bool AlwaysOnTop { get; set; } = false;
    public bool NoBorders { get; set; } = false;
    public bool TrayIcon { get; set; } = false;
    public int MonitorIndex { get; set; } = 0;
    public double WindowX { get; set; } = -1;
    public double WindowY { get; set; } = -1;
    public double WindowWidth { get; set; } = 1280;
    public double WindowHeight { get; set; } = 760;
    public bool StartWithWindows { get; set; } = false;
    public bool StartFullscreen { get; set; } = false;
    public bool StartMinimized { get; set; } = false;
    public bool StartInAmbient { get; set; } = false;
    public bool RestoreMonitorConfig { get; set; } = true;
    public List<MonitorAssignment> CommandCenter { get; set; } = new();

    public Dictionary<string, string> Shortcuts { get; set; } = DefaultShortcuts();

    public static Dictionary<string, string> DefaultShortcuts()
    {
        var d = new Dictionary<string, string>
        {
            ["Fullscreen"] = "F11", ["Escape"] = "Escape", ["Ambient"] = "F12",
            ["Action"] = "Space", ["Mute"] = "M", ["Hud"] = "H", ["Settings"] = "Ctrl+S",
            ["Diagnostics"] = "D", ["Presets"] = "P", ["Events"] = "E", ["Visual"] = "V",
        };
        for (int i = 1; i <= 10; i++) d["Preset" + i] = "F" + i;
        for (int i = 11; i <= 15; i++) d["Preset" + i] = "Ctrl+F" + (i - 10);
        return d;
    }
}
