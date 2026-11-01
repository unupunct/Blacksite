using Avalonia;
using Avalonia.Media;
using Blacksite.Core.Configuration;
using Blacksite.Core.Themes;

namespace Blacksite.UI.Rendering;

public sealed partial class Gfx
{
    static IBrush Vertical(params (Color c, double at)[] stops)
    {
        var b = new LinearGradientBrush { StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative) };
        foreach (var (c, at) in stops) b.GradientStops.Add(new GradientStop(c, at));
        return b;
    }

    /// <summary>Hologram projector: volumetric beam, glowing pad and expanding pulse rings. Draw BEFORE the projected content.</summary>
    public void HoloBase(Rect r, double cx, double baseY, double w, uint? col = null)
    {
        if (!S.Hologram || Quality == GraphicsProfile.Low) return;
        uint c = col ?? P.Primary;
        var top = r.Y + U(6);
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(new Point(cx - w * 0.5, baseY), true);
            ctx.LineTo(new Point(cx + w * 0.5, baseY)); ctx.LineTo(new Point(cx + w * 0.98, top)); ctx.LineTo(new Point(cx - w * 0.98, top));
            ctx.EndFigure(true);
        }
        double flick = Reduced ? 1 : 0.9 + 0.1 * Math.Sin(Time * 23) * Math.Sin(Time * 5.3);
        Dc.DrawGeometry(Vertical((Color.FromArgb(0, 0, 0, 0), 0), (Col(c, 0.045 * flick), 0.5), (Col(c, 0.20 * flick), 1)), null, g);
        // beam edges
        Line(cx - w * 0.5, baseY, cx - w * 0.98, top, c, 1, 0.18); Line(cx + w * 0.5, baseY, cx + w * 0.98, top, c, 1, 0.18);
        // pad
        var glow = new RadialGradientBrush
        {
            Center = new RelativePoint(0.5, 0.5, RelativeUnit.Relative), GradientOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            RadiusX = RelativeScalar.Parse("50%"), RadiusY = RelativeScalar.Parse("50%"),
            GradientStops = { new GradientStop(Col(c, 0.55 * flick), 0), new GradientStop(Col(c, 0.12), 0.6), new GradientStop(Color.FromArgb(0, 0, 0, 0), 1) }
        };
        Dc.DrawEllipse(glow, null, new Point(cx, baseY), w * 0.62, w * 0.12);
        for (int k = 0; k < 3; k++)
        {
            double rx = w * (0.38 + 0.15 * k);
            Dc.DrawEllipse(null, Pn(c, 1.2, 0.6 - 0.15 * k, U(4 + k)), new Point(cx, baseY), rx, rx * 0.17);
        }
        double t = (Time * 0.45) % 1; double pr = w * (0.3 + 0.75 * t);
        Dc.DrawEllipse(null, Pn(c, 1.4, 0.55 * (1 - t)), new Point(cx, baseY), pr, pr * 0.17);
        // rising particles in the beam
        if (!Reduced)
            for (int i = 0; i < 14; i++)
            {
                double ph = (Time * (0.12 + 0.03 * (i % 4)) + i * 0.173) % 1, px = cx + (Math.Sin(i * 12.9) * 0.85) * (w * (0.4 + 0.5 * ph)), py = baseY - ph * (baseY - top);
                Disc(px, py, U(0.9 + (i % 3) * 0.4), c, 0.55 * Math.Sin(Math.PI * ph));
            }
    }

    /// <summary>Hologram shimmer over projected content: sweeping scan band, fine interference lines, flicker and glitch slices.</summary>
    public void HoloShimmer(Rect r, uint? col = null, bool round = false)
    {
        if (!S.Hologram) return;
        uint c = col ?? P.Primary;
        using var clip = round ? Dc.PushGeometryClip(new EllipseGeometry(r)) : Clip(r);
        double t = (Time * 0.2) % 1.2 - 0.1, y = r.Y + t * r.Height, bh = U(34);
        Dc.DrawRectangle(Vertical((Color.FromArgb(0, 0, 0, 0), 0), (Col(c, 0.13), 0.85), (Col(c, 0.30), 0.97), (Color.FromArgb(0, 0, 0, 0), 1)), null, new Rect(r.X, y - bh, r.Width, bh));
        if (Quality != GraphicsProfile.Low)
        {
            double step = Math.Max(3, U(3.5)), off = (Time * 14) % step;
            var br = Br(c, 0.045);
            for (double yy = r.Y + off; yy < r.Bottom; yy += step) Dc.DrawRectangle(br, null, new Rect(r.X, yy, r.Width, 1));
        }
        if (Reduced) return;
        if (FxRand() < 0.025) Fill(r, c, 0.03 + 0.03 * FxRand());
        if (S.Glitch && FxRand() < 0.015)
        {
            double gy = r.Y + FxRand() * r.Height; Fill(new Rect(r.X + FxRand() * r.Width * 0.3, gy, r.Width * (0.2 + 0.5 * FxRand()), U(1.5 + 4 * FxRand())), c, 0.16);
        }
    }
}
