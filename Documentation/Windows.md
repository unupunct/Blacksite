# Windows guide

## Installing
* **Installer** – run `BLACKSITE-Setup.exe`. It installs per user (no administrator rights) to
  `%LOCALAPPDATA%\Programs\BLACKSITE`, adds a Start-menu entry (desktop shortcut optional) and an uninstaller.
* **Portable** – unzip `BLACKSITE-Portable.zip` anywhere. The `portable.flag` file keeps settings in `Data\` next to the exe.
* **Single exe** – `BLACKSITE.exe` is self-contained (≈ 100 MB, includes the .NET runtime).
* **Framework-dependent** – `BLACKSITE-FrameworkDependent.zip`; install the .NET 10 Desktop Runtime first.
Windows SmartScreen may warn about the unsigned executable; choose *More info → Run anyway* if you trust the source.

## Window modes
Windowed · Maximized · Borderless fullscreen · "Exclusive" fullscreen (implemented as borderless fullscreen – on
Windows 10/11 this is promoted to independent-flip, giving the latency benefits of exclusive mode without display mode
switches; true display-mode changes are intentionally not performed) · always-on-top · optional borderless window ·
tray icon · minimise to taskbar. Per-monitor-v2 DPI awareness is enabled; the interface scales with the
window size and HUD scale setting (tested from 640×400 to 3840×2160 and 21:9).

## Multi-monitor
See `Customization.md`. Command-line options: `--preset <id> --monitor <n> --fullscreen --instance --portable --startup`.
Preset ids: space, hacker, surveillance, jarvis, lockdown, satellite, reactor, cyber, deepspace, tactical.

## Start with Windows
Off by default. Turning it on shows a consent dialog; it then writes a single per-user value
`HKCU\Software\Microsoft\Windows\CurrentVersion\Run\BLACKSITE` (no admin rights). The uninstaller removes it.
Related options: start fullscreen, start minimised, restore monitor configuration.

## Audio
NAudio WaveOut with selectable output device (SETTINGS → AUDIO). Voice announcements use the installed Windows
(SAPI) voices – fully offline.

## Performance
Diagnostics panel (D) shows FPS, frame time, memory, CPU, profile, resolution, device, OS, audio status.
On older PCs choose LOW; the screen saver and ambient mode further reduce load.
