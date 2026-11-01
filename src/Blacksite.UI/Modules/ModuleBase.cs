using Avalonia;
using Blacksite.Core.Presets;
using Blacksite.UI.Rendering;
using Blacksite.UI.Shell;

namespace Blacksite.UI.Modules;

/// <summary>A full-screen preset. Draws into the content area; the shell supplies chrome, input and events.</summary>
public abstract class ModuleBase
{
    protected BsContext C { get; private set; } = null!;
    public PresetInfo Info { get; private set; } = null!;
    protected Random Rng { get; } = new();

    public void Init(BsContext c, PresetInfo info) { C = c; Info = info; OnInit(); }
    protected virtual void OnInit() { }

    /// <summary>Button ids shown on the action bar (also used by radial and context menus).</summary>
    public abstract string[] Actions { get; }
    public virtual bool IsActive(string action) => false;
    public virtual void Enter() { }
    public virtual void Update(double dt) { }
    public abstract void Draw(Gfx g, Rect area);

    /// <summary>Handle an action; return a confirmation message for the toast.</summary>
    public virtual string? OnAction(string id) => null;
    /// <summary>Module-owned in-canvas buttons (ids not starting with act:/top:). Return a toast message or null.</summary>
    public virtual string? OnModuleButton(string id) => null;
    /// <summary>In-canvas sliders registered by the module (value 0..1).</summary>
    public virtual void OnModuleSlider(string id, double v) { }
    /// <summary>When true the module plays its own action sounds (the shell skips the generic confirmation).</summary>
    public virtual bool CustomSounds => false;
    /// <summary>Presets with their own header bar hide the shell status ticker.</summary>
    public virtual bool NoTicker => false;
    public virtual string PrimaryAction => Actions.Length > 0 ? Actions[0] : "";

    public virtual void Tap(Point p, string? regionId) { }
    public virtual void DoubleTap(Point p, string? regionId) { }
    public virtual bool Drag(Point p, Vector delta, string? regionId) => false;
    public virtual void Wheel(Point p, double delta) { }
    public virtual void Key(string key, string? text) { }
    public virtual bool WantsText => false;
    /// <summary>Optional keys the module consumes without owning the text field (e.g. keypad digits).</summary>
    public virtual bool TryKey(string combo) => false;

    protected void Say(string text) => C.Audio.Say(text);
    protected void Beep() => C.Audio.Play("beep");

    protected static double Hypot(double a, double b) => Math.Sqrt(a * a + b * b);
    protected static double Clamp(double v, double a, double b) => Math.Max(a, Math.Min(b, v));
}
