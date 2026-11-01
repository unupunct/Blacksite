using Blacksite.Core.Configuration;

namespace Blacksite.Core.Services;

public sealed record MonitorInfo(int Index, string Name, int Width, int Height, double Scale, bool Primary);

/// <summary>Everything the shared UI needs from the host OS. Each head implements what it can; the rest is no-op.</summary>
public interface IPlatformServices
{
    string PlatformName { get; }
    string DeviceModel { get; }
    string OsVersion { get; }
    bool IsDesktop { get; }
    bool IsTouchFirst { get; }

    /// <summary>Directory for settings, backups, exports.</summary>
    string DataDirectory { get; }

    // Window management (desktop)
    IReadOnlyList<MonitorInfo> Monitors { get; }
    void ApplyWindowMode(WindowMode mode, int monitorIndex);
    void SetAlwaysOnTop(bool on);
    void SetTray(bool on);
    void MoveToMonitor(int monitorIndex);
    void OpenNewInstance(string presetId, int monitorIndex, bool fullscreen);
    void SetStartWithWindows(bool on);
    void Quit();
    bool IsFullscreen { get; }

    // Mobile
    void ApplyImmersive(bool on);
    void SetKeepScreenOn(bool on);
    void SetBrightness(double value01);       // -1 restores system
    void ApplyOrientation(Orientation o);
    double? BatteryLevel { get; }
    bool? IsCharging { get; }

    // Files (import/export without a dialog dependency on the shared project)
    Task<string?> PickTextFileAsync(string title);
    Task<bool> SaveTextFileAsync(string suggestedName, string content);

    double? ProcessCpuPercent { get; }
    long ProcessMemoryBytes { get; }
    long TotalMemoryBytes { get; }
}
