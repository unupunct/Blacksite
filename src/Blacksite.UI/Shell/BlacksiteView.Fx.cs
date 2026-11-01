using Avalonia;
using Blacksite.Core.Configuration;
using Blacksite.UI.Modules;
using Blacksite.UI.Rendering;

namespace Blacksite.UI.Shell;

public sealed partial class BlacksiteView
{
    /// <summary>Shared atmosphere drawn over every procedural preset: drifting light motes, pulsing corner brackets and a slow light sweep.</summary>
    void DrawAtmosphere(Gfx g, Rect r)
    {
        if (Module is SkinModule || g.Quality == GraphicsProfile.Low || Ctx.Settings.ReducedMotion || r.Width < 200) return;
        double t = g.Time; uint c = g.P.Primary;
        int n = g.Quality == GraphicsProfile.High ? 64 : 30;
        using (g.Clip(r))
        {
            for (int i = 0; i < n; i++)
            {
                double sp = 0.012 + 0.03 * Hs(i + 5), ph = (Hs(i) + t * sp) % 1, x = r.X + (Hs(i * 3 + 1) + Math.Sin(t * 0.25 + i) * 0.015) * r.Width, y = r.Bottom - ph * r.Height;
                double tw = 0.25 + 0.75 * (0.5 + 0.5 * Math.Sin(t * (1 + Hs(i + 9) * 2) + i)), fade = Math.Sin(Math.PI * ph);
                g.Disc(x, y, g.U(0.8 + 1.6 * Hs(i + 2)), c, 0.55 * tw * fade);
                if (i % 7 == 0) g.Disc(x, y, g.U(4), c, 0.08 * tw * fade);
            }
            double sy = r.Y + ((t * 0.07) % 1.3 - 0.15) * r.Height, bh = g.U(60);
            g.Fill(new Rect(r.X, sy - bh, r.Width, bh), c, 0.018); g.Fill(new Rect(r.X, sy - 1, r.Width, 1.5), c, 0.07);
        }
        double a = 0.35 + 0.35 * (0.5 + 0.5 * Math.Sin(t * 2)), L = g.U(16), m = g.U(3);
        foreach (var (x, y, dx, dy) in new[] { (r.X + m, r.Y + m, 1, 1), (r.Right - m, r.Y + m, -1, 1), (r.X + m, r.Bottom - m, 1, -1), (r.Right - m, r.Bottom - m, -1, -1) })
        { g.Line(x, y, x + dx * L, y, c, 1.5, a); g.Line(x, y, x, y + dy * L, c, 1.5, a); }
    }
}
