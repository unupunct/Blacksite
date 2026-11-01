using Avalonia;
using Blacksite.UI.Rendering;

namespace Blacksite.UI.Shell.Overlays;

/// <summary>Full-screen modal layer drawn above the active preset (settings, preset manager, menus...).</summary>
public abstract class Overlay
{
    protected BlacksiteView V { get; }
    protected BsContext C => V.Ctx;
    protected double Scroll;
    protected double ContentHeight, ViewHeight;
    protected Rect ListRect;

    protected Overlay(BlacksiteView v) { V = v; }

    public abstract void Draw(Gfx g, Rect b);
    public virtual void OnButton(string id) { if (id == "ov:close") V.CloseOverlay(); }
    public virtual void OnSlider(string id, double v) { }
    public virtual void Tap(Point p, string? regionId) { if (regionId == "ov:blocker") V.CloseOverlay(); }
    public virtual bool Drag(Point p, Vector d, string? regionId)
    {
        if (regionId == "ov:list") { Scroll = Math.Clamp(Scroll - d.Y, 0, Math.Max(0, ContentHeight - ViewHeight)); return true; }
        return false;
    }
    public virtual void Wheel(Point p, double delta) => Scroll = Math.Clamp(Scroll - delta * 70, 0, Math.Max(0, ContentHeight - ViewHeight));
    public virtual bool WantsText => false;
    public virtual bool Key(string combo, string? text) => false;

    /// <summary>Dims the scene, registers the click-blocker, draws the frame and the close button. Returns inner rect.</summary>
    protected Rect Frame(Gfx g, Rect b, string title, double maxW, double maxH, bool closable = true)
    {
        g.Fill(b, 0xFF000000, 0.62);
        g.AddHit("ov:blocker", b);
        double w = Math.Min(b.Width - g.U(12), g.U(maxW)), h = Math.Min(b.Height - g.U(12), g.U(maxH));
        var r = new Rect(b.Center.X - w / 2, b.Center.Y - h / 2, w, h);
        g.AddHit("ov:frame", r);
        var inner = g.Panel(r, title);
        if (closable)
        {
            double cs = g.U(g.Touch ? 34 : 26);
            g.Button("ov:close", new Rect(r.Right - cs - g.U(4), r.Y + g.U(1), cs, g.U(18)), "X", false, true, null, 9);
        }
        return inner;
    }

    protected void BeginList(Gfx g, Rect list)
    {
        ListRect = list; ViewHeight = list.Height;
        Scroll = Math.Clamp(Scroll, 0, Math.Max(0, ContentHeight - ViewHeight));
        g.AddHit("ov:list", list);
    }

    protected void DrawScrollbar(Gfx g)
    {
        if (ContentHeight <= ViewHeight + 1) return;
        double f = ViewHeight / ContentHeight, th = Math.Max(g.U(20), ListRect.Height * f);
        double y = ListRect.Y + (ListRect.Height - th) * (Scroll / (ContentHeight - ViewHeight));
        g.Fill(new Rect(ListRect.Right + g.U(2), ListRect.Y, g.U(3), ListRect.Height), g.P.Primary, 0.12);
        g.Fill(new Rect(ListRect.Right + g.U(2), y, g.U(3), th), g.P.Primary, 0.8);
    }
}
