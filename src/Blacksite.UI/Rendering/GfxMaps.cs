using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Blacksite.Core.Configuration;
using Blacksite.Core.Themes;

namespace Blacksite.UI.Rendering;

public sealed partial class Gfx
{
    readonly Dictionary<(PlanetKind, uint, uint, int), PlanetSurface> _surfaces = new();
    WriteableBitmap? _flat;

    PlanetSurface Surf(PlanetKind k, uint b, uint band, int seed)
    {
        bool hi = Quality == GraphicsProfile.High;
        var key = (k, b, band, seed + (hi ? 100000 : 0));
        if (!_surfaces.TryGetValue(key, out var s)) _surfaces[key] = s = new PlanetSurface(k, b, band, seed, hi);
        return s;
    }

    bool HiQ => Quality != GraphicsProfile.Low;

    void DrawBmp(WriteableBitmap bmp, Rect dest)
    {
        using (Dc.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality }))
            Dc.DrawImage(bmp, new Rect(0, 0, bmp.PixelSize.Width, bmp.PixelSize.Height), dest);
    }

    void Halo(double cx, double cy, double rad, uint col, double alpha)
    {
        var br = new RadialGradientBrush
        {
            Center = new RelativePoint(0.5, 0.5, RelativeUnit.Relative), GradientOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            RadiusX = RelativeScalar.Parse("50%"), RadiusY = RelativeScalar.Parse("50%"),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.0), new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.78),
                new GradientStop(Col(col, alpha), 0.84), new GradientStop(Color.FromArgb(0, 0, 0, 0), 1.0),
            }
        };
        Dc.DrawEllipse(br, null, new Point(cx, cy), rad * 1.3, rad * 1.3);
    }

    public void Image(WriteableBitmap bmp, Rect dest, double opacity = 1)
    {
        using (Dc.PushOpacity(Math.Clamp(opacity * HudOpacity, 0, 1))) DrawBmp(bmp, dest);
    }

    /// <summary>Soft procedural nebula backdrop (cached sprite).</summary>
    public void Nebula(Rect r, uint colA, uint colB, int seed, double opacity = 0.6, double drift = 0)
    {
        if (!HiQ) return;
        var bmp = Sprites.Nebula(colA, colB, seed);
        double off = Reduced ? 0 : Math.Sin(Time * 0.02 + seed) * r.Width * 0.03 + drift;
        using (Dc.PushOpacity(opacity * HudOpacity))
        using (Dc.PushClip(r))
            DrawBmp(bmp, new Rect(r.X - r.Width * 0.05 + off, r.Y - r.Height * 0.05, r.Width * 1.1, r.Height * 1.1));
    }

    /// <summary>Animated plasma ball (two counter-rotating noise layers).</summary>
    public void Plasma(double cx, double cy, double rad, uint col, int seed, double intensity = 1)
    {
        if (!HiQ) { Disc(cx, cy, rad * 0.5, col, 0.5 * intensity); return; }
        var bmp = Sprites.Plasma(col, seed);
        var dest = new Rect(cx - rad, cy - rad, rad * 2, rad * 2);
        using (Dc.PushOpacity(Math.Clamp(intensity, 0, 1) * HudOpacity))
        {
            Rotated(cx, cy, Time * 14, () => DrawBmp(bmp, dest));
            Rotated(cx, cy, -Time * 23 + 90, () => { using (Dc.PushOpacity(0.7)) DrawBmp(bmp, new Rect(cx - rad * 0.8, cy - rad * 0.8, rad * 1.6, rad * 1.6)); });
        }
    }

    /// <summary>Flat map: baked Earth surface with pan/zoom, plus an optional graticule.</summary>
    public void FlatMap(Rect r, double cLon, double cLat, double zoom, uint land, bool grid, bool labels = true)
    {
        using var _ = Clip(r);
        Fill(r, P.Background, 0.6);
        var earth = Surf(PlanetKind.Earth, 0, 0, 1);
        if (earth.Ready)
        {
            _flat ??= BuildFlat(earth);
            double spanLon = 360 / zoom, spanLat = 180 / zoom;
            double sx0 = (cLon - spanLon / 2 + 180) / 360 * earth.TW, sy0 = (90 - (cLat + spanLat / 2)) / 180 * earth.TH;
            double sw = spanLon / 360 * earth.TW, sh = spanLat / 180 * earth.TH;
            // clip the source rectangle to the texture and shrink the destination to match
            double cx0 = Math.Max(0, sx0), cy0 = Math.Max(0, sy0), cx1 = Math.Min(earth.TW, sx0 + sw), cy1 = Math.Min(earth.TH, sy0 + sh);
            if (cx1 > cx0 && cy1 > cy0)
            {
                var dest = new Rect(r.X + (cx0 - sx0) / sw * r.Width, r.Y + (cy0 - sy0) / sh * r.Height, (cx1 - cx0) / sw * r.Width, (cy1 - cy0) / sh * r.Height);
                using (Dc.PushOpacity(0.9 * HudOpacity))
                using (Dc.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality }))
                    Dc.DrawImage(_flat, new Rect(cx0, cy0, cx1 - cx0, cy1 - cy0), dest);
                Fill(r, land, 0.16);   // theme tint
            }
        }
        if (grid)
        {
            for (int lon = -180; lon <= 180; lon += 30)
            {
                var a = World.Project(r, lon, 90, cLon, cLat, zoom); var b = World.Project(r, lon, -90, cLon, cLat, zoom);
                Line(a.X, a.Y, b.X, b.Y, P.Primary, 1, lon == 0 ? 0.35 : 0.16);
                if (labels) Text(lon + "°", a.X + 2, r.Y + 2, 7, P.Primary, Align.Left, false, 0.6);
            }
            for (int lat = -90; lat <= 90; lat += 30)
            {
                var a = World.Project(r, -180, lat, cLon, cLat, zoom); var b = World.Project(r, 180, lat, cLon, cLat, zoom);
                Line(a.X, a.Y, b.X, b.Y, P.Primary, 1, lat == 0 ? 0.35 : 0.16);
                if (labels) Text(lat + "°", r.X + 2, a.Y + 1, 7, P.Primary, Align.Left, false, 0.6);
            }
        }
    }

    static WriteableBitmap BuildFlat(PlanetSurface s)
    {
        var bmp = new WriteableBitmap(new PixelSize(s.TW, s.TH), new Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul);
        var px = new int[s.TW * s.TH];
        for (int i = 0; i < px.Length; i++) { uint a = s.Albedo[i]; px[i] = (int)(0xFF000000 | (((a >> 16) & 0xFF) * 3 / 4 << 16) | (((a >> 8) & 0xFF) * 3 / 4 << 8) | ((a & 0xFF) * 3 / 4)); }
        using var fb = bmp.Lock(); System.Runtime.InteropServices.Marshal.Copy(px, 0, fb.Address, px.Length);
        return bmp;
    }

    /// <summary>Lit Earth globe (clouds, ocean glint, night-side city lights, atmosphere) with optional graticule.</summary>
    public void Globe(double cx, double cy, double rad, double rotLon, double tilt, uint ocean, uint land, bool grid)
    {
        var earth = Surf(PlanetKind.Earth, 0, 0, 1);
        var bmp = earth.Render(Time, rotLon, tilt, (int)(rad * 2 * PixelScale), Time * 0.012, Quality == GraphicsProfile.High);
        if (bmp != null)
        {
            if (HiQ) Halo(cx, cy, rad, 0xFF6EB4FF, 0.45);
            DrawBmp(bmp, new Rect(cx - rad, cy - rad, rad * 2, rad * 2));
        }
        else Dc.DrawEllipse(Br(ocean), null, new Point(cx, cy), rad, rad);
        if (grid)
        {
            for (int lon = -180; lon < 180; lon += 30) Meridian(cx, cy, rad, lon, rotLon, tilt, 0.14);
            for (int lat = -60; lat <= 60; lat += 30) Parallel(cx, cy, rad, lat, rotLon, tilt, 0.14);
        }
        Circle(cx, cy, rad, P.Primary, 1.2, 0.5);
    }

    void Meridian(double cx, double cy, double rad, double lon, double rot, double tilt, double a)
    {
        _tmp.Clear();
        for (int lat = -90; lat <= 90; lat += 6)
        {
            var (p, z) = World.Ortho(cx, cy, rad, lon, lat, rot, tilt);
            if (z > 0) _tmp.Add(p); else { if (_tmp.Count > 1) Poly(_tmp, P.Primary, 1, a); _tmp.Clear(); }
        }
        if (_tmp.Count > 1) Poly(_tmp, P.Primary, 1, a);
    }

    void Parallel(double cx, double cy, double rad, double lat, double rot, double tilt, double a)
    {
        _tmp.Clear();
        for (int lon = -180; lon <= 180; lon += 6)
        {
            var (p, z) = World.Ortho(cx, cy, rad, lon, lat, rot, tilt);
            if (z > 0) _tmp.Add(p); else { if (_tmp.Count > 1) Poly(_tmp, P.Primary, 1, a); _tmp.Clear(); }
        }
        if (_tmp.Count > 1) Poly(_tmp, P.Primary, 1, a);
    }

    /// <summary>Lit, textured planet. Kind defaults to Gas for ringed bodies, Rocky otherwise.</summary>
    public void Planet(double cx, double cy, double rad, uint baseCol, uint bandCol, double spin, bool ring, double ringTilt = 0.35, PlanetKind? kind = null, int seed = 1)
    {
        var k = kind ?? (ring ? PlanetKind.Gas : PlanetKind.Rocky);
        if (ring) RingHalf(cx, cy, rad, baseCol, ringTilt, true);
        var surf = Surf(k, baseCol, bandCol, seed);
        var bmp = surf.Render(Time, spin * 57.3 + 40, 16, (int)(rad * 2 * PixelScale), spin * 0.05, Quality == GraphicsProfile.High);
        if (bmp != null)
        {
            if (HiQ) Halo(cx, cy, rad, k == PlanetKind.Earth ? 0xFF6EB4FF : Palette.Lerp(baseCol, 0xFFFFFFFF, 0.5), 0.3);
            DrawBmp(bmp, new Rect(cx - rad, cy - rad, rad * 2, rad * 2));
        }
        else Dc.DrawEllipse(Br(baseCol, 0.8), null, new Point(cx, cy), rad, rad);
        if (ring) RingHalf(cx, cy, rad, baseCol, ringTilt, false);
    }

    void RingHalf(double cx, double cy, double rad, uint c, double tilt, bool back)
    {
        for (int k = 0; k < 3; k++)
        {
            double rr = rad * (1.45 + k * 0.17);
            var g = new StreamGeometry();
            using (var ctx = g.Open())
            {
                double a0 = back ? Math.PI : 0, a1 = a0 + Math.PI;
                ctx.BeginFigure(new Point(cx + Math.Cos(a0) * rr, cy + Math.Sin(a0) * rr * tilt), false);
                ctx.ArcTo(new Point(cx + Math.Cos(a1) * rr, cy + Math.Sin(a1) * rr * tilt), new Size(rr, rr * tilt), 0, false, SweepDirection.Clockwise);
                ctx.EndFigure(false);
            }
            Dc.DrawGeometry(null, Pn(Palette.Lerp(c, 0xFFFFFFFF, 0.3), rad * 0.05, 0.45 - k * 0.1), g);
        }
    }
}
