# Customization guide

Open **Settings** (Ctrl+S, the CFG/SETTINGS button, or the radial/context menu).

| Tab | What you can change |
|-----|---------------------|
| DISPLAY | Graphics profile (LOW / BALANCED / HIGH, with a device recommendation), primary & secondary accent, background theme, interface brightness, HUD opacity/scale, font size, UI density, animation speed & intensity, scanlines, CRT vignette, glitch, flicker, reduced motion, HUD and diagnostics visibility |
| AUDIO | Master / interface / ambient / alarm / voice volume, mute, ambient loop, random ambient events, continuous background audio, output device (Windows), test sounds & voice |
| SIMULATION | Update interval, random events on/off, event frequency, random event intensity |
| MODES | Ambient mode, automatic preset rotation + interval, screen saver, restore last preset, start in ambient; Android: keep screen on, brightness, orientation, battery-aware mode |
| SYSTEM | Windows: window mode, monitor, windowed size, borderless, always-on-top, tray, start with Windows (consent dialog), start fullscreen/minimised, multi-monitor command centre. Android: device info |
| CONTROLS | Rebind every shortcut |
| DATA | Export/import settings, export/import presets & sequences, backup, restore previous, reset |
| ABOUT | Version, platform, operating notice |

## Deck presets (11–15): panels and options
Press **PANELS** in the action bar. For each panel: SHOW/HIDE and `<` `>` reorder. Sliders: data update speed (0.25–4x), animation speed, alert frequency (0 = no random alerts). **ACCENT** cycles the preset colour; **SAVE LAYOUT** / **RESTORE LAYOUT** persist or reset it. Settings are stored per preset in `BLACKSITE_CONFIG.json` (`PanelLayouts`) and are kept when switching presets. Tap a panel title to expand it full-screen; on narrow screens the panel stack scrolls by dragging.

## Audio categories
Settings → AUDIO has separate volumes for interface sounds, ambient, alarms, voice and **cinematic events**, plus mute switches per category (interface / ambient / alarms / events). Each new preset has its own ambience and action-specific sounds (radar pulse, handshake, ECG beep, defibrillator, access-control tones, deep-space tones...).

## Presets
Open the **Preset Manager** (P). Cards show a live procedural preview, colours, sound profile, favourite star and
reorder arrows. `+ CUSTOM` clones the current preset: rename it (type on a keyboard), change its base interface,
primary/secondary colour and sound profile. Presets and sequences export to `BLACKSITE_PRESETS.json`
and import on either platform.

## Cinematic sequences
**Events panel (E)** → trigger any of the 17 events manually, or build a sequence: `+ NEW`, tap an event on the left,
`ADD STEP`, tune the delay with `-`/`+`, `PLAY`. Sequences are saved in the configuration file.

## Multi-monitor command centre (Windows)
SYSTEM → *Monitor n preset* assigns a preset per display; *Launch configured command center* opens one window per
assignment (fullscreen on its monitor). With *Restore monitor configuration* enabled the extra windows reopen at start-up.
Each extra window has its own settings copy, so the main window's configuration is never overwritten.

## Configuration file
`BLACKSITE_CONFIG.json` is plain JSON. Colours are `#RRGGBB`. Unknown fields are ignored and missing ones take defaults,
so hand-edited and older files keep working. Location – Windows installer: `%APPDATA%\BLACKSITE`; portable:
`Data\` next to the exe; Android: app-private storage (use *Export* to move files off the device).
