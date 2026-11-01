# Adding presets, modules and sounds

## A new preset (module)
1. **Palette & catalog entry** – add a `PresetInfo` to `Blacksite.Core/Presets/PresetCatalog.cs`
   (id, name, description, `SoundProfile`, `Palette`).
2. **Module class** – create `Blacksite.UI/Modules/MyModule.cs`:
   ```csharp
   public sealed class MyModule : ModuleBase
   {
       public override string[] Actions => new[] { "PING", "SCAN" };           // action-bar buttons
       public override string? OnAction(string id) => id == "PING" ? "PING SENT" : null;   // returns the confirmation toast
       public override void Update(double dt) { }
       public override void Draw(Gfx g, Rect area)
       {
           var lay = Layout.Adaptive(g, area, panelCount: 4);                   // wide: side columns, narrow: stacked
           var c = g.Panel(lay.Main, "TITLE");                                  // returns inner rect
           g.Graph(c, C.Sim.Cpu, g.P.Primary);                                  // C = shared context (sim, audio, events, settings)
       }
   }
   ```
   Use `g.Button/Toggle/Slider` for in-canvas controls (route them via `OnModuleButton` / `OnModuleSlider`) and
   `g.AddHit("id", rect)` + `Tap/Drag/Wheel/DoubleTap` overrides for map-like interaction.
3. **Register** it in `ModuleFactory.Create`.
4. Raise the preset limit if needed: shortcuts `Preset1..10` are generated in `AppSettings.DefaultShortcuts`.
5. Run `dotnet test` – the smoke tests iterate `PresetCatalog.All` over ten resolutions and press every action.

## A new deck preset (dense multi-panel)
Derive from `DeckModule` instead of `ModuleBase`: implement `BuildPanels()` (`Def(id, title, Zone, weight, drawDelegate)`), `ModuleActions` and `DoAction(id)` (return the toast text; play your own sound – deck presets set `CustomSounds`), and drive state from a `DomainSim` subclass in `Blacksite.Core/Simulation/Domain` (`OnStep`, `OnReset`, operator methods that change state). Use `PanelButton/PanelSlider/PanelTap/PanelDrag/PanelWheel` hooks for in-panel controls and `AfterDraw` for modal overlays. Instruments available on `Gfx`: `Scope`, `Spectrum`, `Waterfall`, `Table`, `KvGrid`, `HMeter`, `Led`, `DishIcon`, `Body`, `Gauge`, `Graph`, `Radar`, `Globe`, `Plasma`. Add the `PresetInfo` (and a `Sound­Profile` + ambient in `Synth.Ambient`) and register in `ModuleFactory`.

## A new sound effect
`Blacksite.Core/Audio/Synth.cs`: add a generator returning `float[]` (mono, 22 050 Hz), add its name to `Synth.Names`
and to the `ByName` switch. Play it with `C.Audio.Play("name", SoundCategory.Ui|Alarm|Ambient)`.
All audio is synthesised – never bundle copyrighted effects. Ambient loops per `SoundProfile` live in `Synth.Ambient`
(frequencies are snapped to whole cycles so the 8 s loop tiles seamlessly).

## A new cinematic event
Append an `EventDef` to `EventCatalog.All` (title, message, severity, duration, sound, effect, optional countdown/voice).
Optionally add state changes in `BlacksiteView.OnEventStarted`. It appears automatically in the Events panel, the
sequence editor and the random event pool.

## A new simulation signal
Add a `RingBuffer` and fields to `SimulationEngine`, update them in `Step` (derive from existing values so the data stays
coherent), push to the buffer in the history block.

## A new setting
Add a property to `AppSettings` (it is serialised automatically), then a row in `SettingsOverlay` (`Tog/Sld/Cho/Act`).
