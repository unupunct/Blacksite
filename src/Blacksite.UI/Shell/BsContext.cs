using Blacksite.Core.Audio;
using Blacksite.Core.Configuration;
using Blacksite.Core.Events;
using Blacksite.Core.Presets;
using Blacksite.Core.Services;
using Blacksite.Core.Simulation;
using Blacksite.Core.Themes;

namespace Blacksite.UI.Shell;

/// <summary>Everything a module or overlay may touch. One instance per window.</summary>
public sealed class BsContext
{
    public AppSettings Settings { get; }
    public ConfigStore Store { get; }
    public SimulationEngine Sim { get; }
    public EventEngine Events { get; }
    public AudioEngine Audio { get; }
    public IPlatformServices Platform { get; }

    public Action<string, bool>? ToastHandler;
    public Action<string>? SwitchPreset;
    public Action<string>? OpenOverlay;
    public string? StartPresetOverride { get; set; }
    /// <summary>VIS dock controls: freeze animation clock / pause data clock. SimDt is the data step modules should use.</summary>
    public bool AnimFrozen, DataPaused;
    public double SimDt;

    public BsContext(AppSettings s, ConfigStore store, IPlatformServices platform, AudioEngine audio)
    {
        Settings = s; Store = store; Platform = platform; Audio = audio;
        Sim = new SimulationEngine();
        Events = new EventEngine();
    }

    public void Toast(string msg, bool warn = false) => ToastHandler?.Invoke(msg, warn);
    public void Save() => Store.Save(Settings);

    /// <summary>Effective palette: preset colours with user overrides applied.</summary>
    public Palette PaletteFor(PresetInfo info, CustomPreset? custom = null)
    {
        var p = info.Palette;
        uint pri = Palette.Parse(custom?.Primary ?? Settings.PrimaryOverride ?? "", p.Primary);
        uint sec = Palette.Parse(custom?.Secondary ?? Settings.SecondaryOverride ?? "", p.Secondary);
        uint bg = Palette.Parse(custom?.Background ?? Settings.BackgroundOverride ?? "", p.Background);
        if (pri == p.Primary && sec == p.Secondary && bg == p.Background) return p;
        return p with { Primary = pri, Secondary = sec, Background = bg, Panel = Palette.Lerp(bg, pri, 0.07), Accent = Palette.Lerp(pri, 0xFFFFFFFF, 0.5) };
    }

    public IReadOnlyList<(string id, string name, PresetInfo info, CustomPreset? custom)> AllPresets()
    {
        var list = new List<(string, string, PresetInfo, CustomPreset?)>();
        foreach (var p in PresetCatalog.All) list.Add((p.Id, p.Name, p, null));
        foreach (var c in Settings.CustomPresets)
        {
            var b = PresetCatalog.Get(c.BasePresetId);
            list.Add(("c:" + c.Id, c.Name, b, c));
        }
        var order = Settings.PresetOrder;
        if (order.Count > 0)
            list = list.OrderBy(x => { int i = order.IndexOf(x.Item1); return i < 0 ? 1000 + list.IndexOf(x) : i; }).ToList();
        return list;
    }
}
