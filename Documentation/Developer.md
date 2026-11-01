# Developer guide

## Toolchain
* .NET SDK 10 (10.0.403 used)
* Android: JDK 17 (Microsoft OpenJDK), Android SDK platform 36 + build-tools, `dotnet workload install android`
* Windows installer: Inno Setup 6

The repository's scripts look in `.tools\` first (private copies; nothing is installed system-wide):
`.tools\dotnet`, `.tools\jdk`, `.tools\android-sdk`. To recreate them see `scripts\setup-tools.ps1`.

## Solution map
`src\Blacksite.Core` · `src\Blacksite.UI` · `src\Blacksite.Desktop` · `src\Blacksite.Android` · `src\Blacksite.Snap` ·
`tests\Blacksite.Tests` · `installer\BLACKSITE.iss` · `scripts\*.ps1` · `tools\make-icons.ps1` (logo/icons generator)

## Everyday commands
```powershell
dotnet build src\Blacksite.Desktop
dotnet run --project src\Blacksite.Desktop
dotnet test tests\Blacksite.Tests                     # core + headless UI smoke tests (~minutes)
dotnet run --project src\Blacksite.Snap -- out all 1600 900 8        # PNG of every preset (headless Skia)
dotnet run --project src\Blacksite.Snap -- out space 1600 900 8 ev=breach   # with an event
dotnet run --project src\Blacksite.Snap -- out hacker 390 844 8 settings    # phone layout with overlay
```
`Snap` arguments: `<outDir> <preset|all|a,b> [w] [h] [seconds] [overlay|ev=ID|click=ID,...] [low|balanced|high]`.

## Conventions
* Shared code never references a platform; new OS needs go through `IPlatformServices`.
* Modules draw with `Gfx` only and keep per-frame allocations small (cached brushes/pens/text; `RingBuffer`).
* Everything user-visible must be fictional and offline. No network, camera, microphone access.
* Every action button returns a confirmation string; sound/animation/toast are handled by the shell.
* Add a smoke-test-friendly design: no module may throw for any window size ≥ 320×240.

## Releasing
Bump versions in `Blacksite.Desktop.csproj`, `Blacksite.Android.csproj`, `installer\BLACKSITE.iss`, then run both build
scripts. Android upgrades require the same keystore (`keystore\`, not in source control).
