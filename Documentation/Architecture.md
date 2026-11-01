# Architecture

## Framework decision

| Option | Android APK | Windows | Custom rendering | Verdict |
|--------|-------------|---------|------------------|---------|
| .NET MAUI | yes | WinUI 3 only, heavy, Windows support has been unstable | needs SkiaSharp on top | rejected – the Windows head is the weakest part, no Linux/Mac escape hatch |
| **Avalonia 12 (Skia)** | yes (`net10.0-android`) | yes (classic desktop, per-monitor DPI, multi-window) | Skia drawing context, same renderer on both | **selected** |
| Native Kotlin + WPF | best per-platform | best | duplicate everything | rejected – violates "one shared identity and logic" |

Avalonia gives one renderer (Skia) on both platforms, so the Hollywood look is pixel-identical, while each head
stays thin and native (window management, audio device, immersive mode, battery...).

## Layers

```
Blacksite.Core        (net10.0)   no UI dependencies – shared by every head and by the tests
  Configuration/  AppSettings, ConfigStore (versioned BLACKSITE_CONFIG.json, backup, import/export)
  Presets/        PresetCatalog – the 10 presets, palettes, sound profiles
  Themes/         Palette (colour system)
  Simulation/     SimulationEngine + RingBuffer – coupled fictional data, history buffers
  Events/         EventCatalog (12 events), EventEngine (timing, countdowns, sequences, auto events)
  Audio/          Synth (procedural sounds), AudioEngine (mixer, categories, fades, ambient), IAudioOutput, ISpeechService
  Simulation/Domain/  isolated per-preset simulations (OrbitalSim, QuantumSim, CommsSim, MedicalSim, FacilitySim) on a DomainSim base
  Services/       IPlatformServices – everything the UI needs from the OS

Blacksite.UI          (net10.0, Avalonia)  shared presentation
  Rendering/      Gfx – immediate-mode drawing + HUD widgets (panels, buttons, gauges, radar, graphs, globe, maps, effects)
                  Layout – adaptive panel layout (wide vs. stacked)
  Modules/        one class per preset + ModuleBase + ModuleFactory
  Shell/          BlacksiteView (render loop, input pipeline, chrome, toasts, event FX), BsContext, Overlays/

Blacksite.Desktop     (net10.0-windows)  window modes, monitors, tray, startup, NAudio output, SAPI speech
Blacksite.Android     (net10.0-android)  immersive mode, brightness, battery, AudioTrack output, TTS, boot receiver
Blacksite.Snap        headless renderer used for visual verification
tests/Blacksite.Tests xunit v3 – core logic + headless UI smoke tests
```

## Presets 11–15 (deck presets)

Each deck preset is a `DeckModule` (zoned dense layout: Main / Left / Right / Bottom columns on wide screens, a scrolling stack with per-panel expand on narrow screens) wrapped around its own `DomainSim`:

| Simulation | Consistency rules |
|------------|-------------------|
| `OrbitalSim` | period & velocity from Kepler (T=2π√(a³/μ)); a manoeuvre changes altitude → period → ground track; SNR from slant range/elevation; handover/loss/restore change station assignment and link state; conjunctions raised from geometry |
| `QuantumSim` | field strength curves tracks (κ ∝ B/pT); temperature from cooling + load; coherence/fidelity/stability from temperature, field, vacuum and anomalies; event rate from energy × sensitivity |
| `CommsSim` | received power from a real link budget (FSPL over distance and frequency, antenna gain reduced by pointing error); light-time = distance × 499 s/AU; spectrum is built from channels/anomalies; scripted unknown-transmission state machine |
| `MedicalSim` | cabin O2/CO2/pressure drive SpO2, respiration and heart rate; ECG/pleth/resp waveforms synthesised from state; alert level and response countdown derived from vitals |
| `FacilitySim` | rooms draw power through 4 buses; deficit → battery → load shedding; failed/unpowered infrastructure degrades cooling/air which changes room temperature, CO2 and O2; breach state machine drives zones, cameras, checkpoints and incidents |

Every sim has `Reset(scenario)`, a speed multiplier (per-preset *data update speed*), history `RingBuffer`s and an activity log. Their tests live in `CoreTests`.

## Rendering model

`BlacksiteView` is one custom `Control`. Every frame it builds a `Gfx` (wrapper over `DrawingContext` with cached
brushes/pens/text), asks the active `ModuleBase` to draw into the content rectangle, then draws chrome, event FX,
toasts, post-effects and the active overlay. Widgets (`Gfx.Button`, `Slider`, `Toggle`...) also register *hit regions*;
pointer input is resolved against the regions of the previous frame, so drawing and interaction can never disagree.

Graphics profiles (`GraphicsProfile`): LOW (24 fps, no glow/scanlines, fewer particles/segments), BALANCED (40 fps),
HIGH (60 fps, trails, extra detail). The recommended profile is derived from RAM/cores (`HardwareProbe`). Rendering
pauses when minimised / backgrounded, ambient mode halves update rates, and a low-battery Android device
automatically drops to LOW.

## Input pipeline

Pointer → hit region (button / slider / region) → tap, double-tap, long-press (radial menu), drag, swipe, pinch
(`PinchGestureRecognizer`), wheel, right-click (context menu). Keyboard → configurable shortcut table → module key hooks.
Every button press flashes, plays a click + confirm sound, calls `ModuleBase.OnAction` and shows its confirmation toast.

## Configuration

`BLACKSITE_CONFIG.json` – `{ "format", "version", "exportedUtc", "sourcePlatform", "settings": {...} }`.
`ConfigStore.Parse` rejects foreign/newer files and is where future migrations go (`version` switch).
Preset/sequence packs use `BLACKSITE_PRESETS`. Files are identical on Android and Windows.
