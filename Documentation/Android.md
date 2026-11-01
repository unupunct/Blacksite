# Android guide

## Installing
1. Copy `BLACKSITE-release.apk` to the device (USB, cloud drive, `adb install`).
2. Open it from a file manager; allow *Install unknown apps* for that app when asked.
3. Launch BLACKSITE. The first launch shows the operating notice (heat / battery / burn-in) once.

`adb install -r BLACKSITE-release.apk` also works. The release APK is signed with the local
`keystore/blacksite.keystore` created by `scripts/build-android.ps1`; keep that file if you want upgrades to install over
earlier builds. Debug and release builds use different signatures, so uninstall one before installing the other.

## Wall-display setup
* SETTINGS → MODES: *Ambient mode* (automatic events, preset rotation, reduced update rate), *Keep screen on*,
  *Screen saver after N min* (dims and moves the clock to protect the panel), *Brightness*, *Orientation* lock.
* Immersive full-screen is always on (swipe from an edge to reveal the system bars briefly).
* *Battery-aware*: below 20 % and not charging, graphics drop to LOW automatically and recover when charging.
* Use a quality charger/cable, avoid enclosed mounts and direct sun, keep brightness low, and consider a charge limit.
* *Start on device boot (best effort)* registers a boot receiver. Android 10+ and many manufacturers (Xiaomi, Huawei,
  Samsung battery optimisation...) block apps from starting activities in the background; BLACKSITE does **not**
  claim this works on every device. If it does not, add the app to the "auto-start" / "unrestricted battery" lists
  manually, or use a kiosk launcher.

## Optimisation notes
* LOW profile: 24 fps, no glow/scanlines/trails, fewer stars/segments, slower simulation tick; recommended for
  devices with < 3.5 GB RAM or ≤ 4 cores (detected automatically on first run).
* Rendering and simulation pause when the app is backgrounded; audio focus is respected (playback pauses if another app
  takes focus).
* Memory: all visuals are procedural – no bitmaps or video are loaded; audio is synthesised (a few MB).
* Voice announcements use the device's installed text-to-speech engine (works offline if an offline voice is installed).

## Known limitations
* Speech *recognition* in the AI preset is simulated (no microphone access).
* Brightness override affects only BLACKSITE's window.
* Screen rotation lock uses sensor-landscape / sensor-portrait.
