using Avalonia;
using Avalonia.Media;
using Blacksite.Core.Configuration;

namespace Blacksite.UI.Rendering;

public sealed partial class Gfx
{
    readonly Random _fx = new();

    /// <summary>Post effects: scanlines, vignette (CRT), flicker, glitch bars.</summary>
    public void PostEffects(Rect r, double glitchAmount)
    {
        if (S.Scanlines && Quality != GraphicsProfile.Low)
        {
            double step = Math.Max(3, U(3));
            var b = Br(0xFF000000, 0.14);
            for (double y = r.Y; y < r.Bottom; y += step) Dc.DrawRectangle(b, null, new Rect(r.X, y, r.Width, 1));
        }
        if (S.Crt && !Reduced)
        {
            var vg = new RadialGradientBrush
            {
                Center = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
                GradientOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
                RadiusX = RelativeScalar.Parse("75%"), RadiusY = RelativeScalar.Parse("75%"),
                GradientStops = { new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.55), new GradientStop(Color.FromArgb(150, 0, 0, 0), 1) }
            };
            Dc.DrawRectangle(vg, null, r);
        }
        if (S.Flicker && !Reduced && _fx.NextDouble() < 0.08)
            Fill(r, 0xFFFFFFFF, 0.02 + _fx.NextDouble() * 0.04);
        if (S.Glitch && !Reduced && Quality != GraphicsProfile.Low && glitchAmount > 0.01)
        {
            int n = 2 + (int)(glitchAmount * 6);
            for (int i = 0; i < n; i++)
            {
                double y = r.Y + _fx.NextDouble() * r.Height, h = U(2 + _fx.NextDouble() * 10 * glitchAmount);
                double off = (_fx.NextDouble() - 0.5) * U(60) * glitchAmount;
                Fill(new Rect(r.X + Math.Max(0, off), y, r.Width * (0.2 + _fx.NextDouble() * 0.6), h), _fx.Next(3) == 0 ? P.Warning : P.Primary, 0.10 + 0.2 * glitchAmount);
            }
        }
    }

    public double FxRand() => _fx.NextDouble();
}
