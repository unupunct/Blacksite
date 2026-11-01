using Avalonia;
using Blacksite.UI.Rendering;

namespace Blacksite.UI.Shell.Overlays;

/// <summary>Radial menu (long-press / touch) or context menu list (right-click).</summary>
public sealed class MenuOverlay : Overlay
{
    readonly Point _at;
    readonly bool _radial;
    double _t;

    public MenuOverlay(BlacksiteView v, Point at, bool radial) : base(v) { _at = at; _radial = radial; }

    List<(string id, string label, bool active)> Items()
    {
        var l = new List<(string, string, bool)>();
        foreach (var a in V.Module.Actions) l.Add(("ctx:act:" + a, a, V.Module.IsActive(a)));
        l.Add(("ctx:top:presets", "PRESETS", false));
        l.Add(("ctx:top:events", "EVENTS", false));
        l.Add(("ctx:top:settings", "SETTINGS", false));
        l.Add(("ctx:top:ambient", "AMBIENT", C.Settings.AmbientMode));
        l.Add(("ctx:top:mute", "MUTE", C.Settings.Muted));
        if (C.Platform.IsDesktop) l.Add(("ctx:top:fs", "FULLSCREEN", C.Platform.IsFullscreen));
        l.Add(("ctx:hud", "HUD", C.Settings.ShowHud));
        return l;
    }

    public override void Draw(Gfx g, Rect b)
    {
        _t = Math.Min(1, _t + 0.08);
        g.Fill(b, 0xFF000000, 0.35);
        g.AddHit("ov:blocker", b);
        var items = Items();
        if (_radial)
        {
            double bw = g.U(g.Touch ? 92 : 84), bh = g.U(g.Touch ? 40 : 32);
            double ring = Math.Max(g.U(118), items.Count * g.U(16)) * (0.4 + 0.6 * _t);
            double cx = Math.Clamp(_at.X, ring + bw / 2, b.Width - ring - bw / 2), cy = Math.Clamp(_at.Y, ring + bh / 2, b.Height - ring - bh / 2);
            g.GlowCircle(cx, cy, g.U(22), g.P.Primary, 1.6);
            g.Circle(cx, cy, ring, g.P.Primary, 1, 0.15, g.U(4));
            g.Disc(cx, cy, g.U(4), g.P.Primary);
            for (int i = 0; i < items.Count; i++)
            {
                double a = -Math.PI / 2 + i * Math.PI * 2 / items.Count;
                double x = cx + Math.Cos(a) * ring, y = cy + Math.Sin(a) * ring;
                g.Line(cx, cy, x, y, g.P.Primary, 1, 0.12);
                g.Button(items[i].id, new Rect(x - bw / 2, y - bh / 2, bw, bh), items[i].label, items[i].active, true, null, 9);
            }
        }
        else
        {
            double w = g.U(190), ih = g.U(30), h = ih * items.Count + g.U(10);
            double x = Math.Min(_at.X, b.Width - w - g.U(6)), y = Math.Min(_at.Y, b.Height - h - g.U(6));
            var r = new Rect(x, y, w, h);
            g.Fill(r, g.P.Background, 0.95); g.Frame(r, g.P.Primary, 1, 0.8); g.Brackets(r, g.P.Primary);
            for (int i = 0; i < items.Count; i++)
                g.Button(items[i].id, new Rect(x + g.U(5), y + g.U(5) + i * ih, w - g.U(10), ih - g.U(3)), items[i].label, items[i].active, true, null, 10);
        }
    }

    public override void OnButton(string id)
    {
        base.OnButton(id);
        if (!id.StartsWith("ctx:")) return;
        V.CloseOverlay();
        if (id == "ctx:hud") { C.Settings.ShowHud = !C.Settings.ShowHud; return; }
        V.Invoke(id[4..]);
    }
}
