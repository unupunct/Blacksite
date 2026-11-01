using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Blacksite.Core.Configuration;
using Blacksite.Core.Simulation;
using Blacksite.Core.Themes;

namespace Blacksite.UI.Rendering;

/// <summary>Scrolling bitmap waterfall (time on the vertical axis, frequency on the horizontal axis).</summary>
public sealed class Waterfall
{
    readonly int _w, _h; readonly int[] _px; readonly WriteableBitmap _bmp;
    public Waterfall(int w, int h)
    {
        _w = w; _h = h; _px = new int[w * h];
        _bmp = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul);
        for (int i = 0; i < _px.Length; i++) _px[i] = unchecked((int)0xFF020610);
    }

    static int Ramp(float v)
    {
        v = Math.Clamp(v, 0, 1);
        (float p, int r, int g, int b)[] k = { (0, 2, 6, 16), (0.25f, 8, 34, 90), (0.5f, 0, 170, 220), (0.75f, 230, 245, 255), (1f, 255, 190, 60) };
        for (int i = 1; i < k.Length; i++)
            if (v <= k[i].p) { float t = (v - k[i - 1].p) / (k[i].p - k[i - 1].p); return unchecked((int)(0xFF000000 | ((uint)(k[i - 1].r + (k[i].r - k[i - 1].r) * t) << 16) | ((uint)(k[i - 1].g + (k[i].g - k[i - 1].g) * t) << 8) | (uint)(k[i - 1].b + (k[i].b - k[i - 1].b) * t))); }
        return unchecked((int)0xFFFFBE3C);
    }

    public void Push(IReadOnlyList<float> row, float gain = 1.2f)
    {
        Buffer.BlockCopy(_px, 0, _px, _w * 4, (_h - 1) * _w * 4);
        for (int x = 0; x < _w; x++) _px[x] = Ramp(row[x * row.Count / _w] * gain);
    }

    public void Draw(Gfx g, Rect r)
    {
        using (var fb = _bmp.Lock()) Marshal.Copy(_px, 0, fb.Address, _px.Length);
        using (g.Dc.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.MediumQuality }))
            g.Dc.DrawImage(_bmp, new Rect(0, 0, _w, _h), r);
    }
}

public sealed partial class Gfx
{
    /// <summary>Oscilloscope / monitor trace of the most recent samples.</summary>
    public void Scope(Rect r, RingBuffer buf, uint c, double min, double max, string? label = null, bool grid = true, double thickness = 1.5)
    {
        Fill(r, 0xFF000000, 0.35); Frame(r, c, 1, 0.3);
        if (grid)
        {
            for (int i = 1; i < 4; i++) Line(r.X, r.Y + r.Height * i / 4, r.Right, r.Y + r.Height * i / 4, c, 1, 0.08);
            for (int i = 1; i < 10; i++) Line(r.X + r.Width * i / 10, r.Y, r.X + r.Width * i / 10, r.Bottom, c, 1, 0.06);
        }
        int n = buf.Count; if (n < 2) return;
        int step = Math.Max(1, n * 1 / Math.Max(2, (int)r.Width)); var pts = new List<Point>(Math.Min(n, (int)r.Width + 2));
        for (int i = 0; i < n; i += step)
            pts.Add(new Point(r.X + r.Width * i / (n - 1), r.Bottom - 2 - (r.Height - 4) * Math.Clamp((buf[i] - min) / (max - min), 0, 1)));
        if (Glow) Poly(pts, c, thickness * 3, 0.12);
        Poly(pts, c, thickness, 0.95);
        // sweep dot at the leading edge
        var last = pts[^1]; Disc(last.X, last.Y, U(2.4), 0xFFFFFFFF, 0.9);
        if (label != null) Text(label, r.X + U(4), r.Y + U(2), 8.5, c, Align.Left, true, 0.9);
    }

    /// <summary>Line spectrum with fill, frequency ticks and optional peak labels.</summary>
    public void Spectrum(Rect r, IReadOnlyList<float> data, uint c, string? unitLeft = null, string? unitRight = null, double ticks = 8, bool peaks = true)
    {
        Fill(r, 0xFF000000, 0.4); Frame(r, c, 1, 0.3);
        for (int i = 1; i < 5; i++) Line(r.X, r.Y + r.Height * i / 5, r.Right, r.Y + r.Height * i / 5, c, 1, 0.07);
        for (int i = 0; i <= ticks; i++) { double x = r.X + r.Width * i / ticks; Line(x, r.Y, x, r.Bottom, c, 1, 0.07); Line(x, r.Bottom - U(4), x, r.Bottom, c, 1, 0.5); }
        int n = data.Count; var pts = new List<Point>(n);
        for (int i = 0; i < n; i++) pts.Add(new Point(r.X + r.Width * i / (n - 1), r.Bottom - 2 - (r.Height - 6) * Math.Clamp(data[i], 0, 1)));
        if (Quality != GraphicsProfile.Low) { var area = new List<Point>(n + 2) { new Point(r.X, r.Bottom) }; area.AddRange(pts); area.Add(new Point(r.Right, r.Bottom)); PolyFill(area, c, 0.14); }
        if (Glow) Poly(pts, c, 3.4, 0.12);
        Poly(pts, c, 1.2, 0.95);
        if (peaks)
        {
            int lastPk = -99;
            for (int i = 3; i < n - 3; i++)
                if (data[i] > 0.38f && data[i] >= data[i - 1] && data[i] > data[i + 1] && data[i] > data[i - 3] + 0.05f && i - lastPk > n / 24)
                { lastPk = i; Line(pts[i].X, pts[i].Y - U(3), pts[i].X, pts[i].Y - U(9), P.Warning, 1, 0.9); }
        }
        if (unitLeft != null) Text(unitLeft, r.X + U(3), r.Bottom - U(11), 7.5, P.Text, Align.Left, false, 0.6);
        if (unitRight != null) Text(unitRight, r.Right - U(3), r.Bottom - U(11), 7.5, P.Text, Align.Right, false, 0.6);
    }

    public void Led(double x, double y, int state, double r = 3.5)   // 0 off, 1 ok, 2 warn, 3 fail
    {
        uint c = state == 1 ? 0xFF30FF70 : state == 2 ? P.Warning : state == 3 ? 0xFFFF3030 : 0xFF506070;
        Disc(x, y, U(r), c, state == 0 ? 0.5 : state == 3 ? 0.35 + 0.65 * Pulse(2.5, x) : 0.85);
        if (state != 0 && Glow) Disc(x, y, U(r * 2), c, 0.12);
    }

    /// <summary>Key/value readout laid out in columns. Returns the height used.</summary>
    public double KvGrid(Rect r, IReadOnlyList<(string k, string v, uint? col)> items, int cols = 2, double fs = 8.5)
    {
        double lh = U(fs + 5.5), cw = r.Width / cols;
        int rows = (items.Count + cols - 1) / cols;
        for (int i = 0; i < items.Count; i++)
        {
            double x = r.X + (i % cols) * cw, y = r.Y + (i / cols) * lh;
            if (y + lh > r.Bottom + 1) break;
            Text(items[i].k, x, y, fs - 0.5, P.Text, Align.Left, false, 0.55);
            Text(items[i].v, x + cw - U(6), y, fs, items[i].col ?? P.Primary, Align.Right, true);
        }
        return rows * lh;
    }

    /// <summary>Horizontal labelled meter with optional coloured zones.</summary>
    public void HMeter(Rect r, double v, double min, double max, string label, string value, uint? col = null, double warnLo = double.NaN, double warnHi = double.NaN)
    {
        uint c = col ?? P.Primary; double f = Math.Clamp((v - min) / (max - min), 0, 1);
        bool warn = (!double.IsNaN(warnLo) && v < warnLo) || (!double.IsNaN(warnHi) && v > warnHi);
        uint vc = warn ? P.Warning : c;
        Text(label, r.X, r.Y, 8, P.Text, Align.Left, false, 0.7); Text(value, r.Right, r.Y, 8.5, vc, Align.Right, true);
        var t = new Rect(r.X, r.Y + U(12), r.Width, Math.Clamp(r.Height - U(12), U(5), U(8)));
        Fill(t, c, 0.09); Frame(t, c, 1, 0.35);
        Fill(new Rect(t.X + 1, t.Y + 1, (t.Width - 2) * f, t.Height - 2), vc, 0.7);
        for (int i = 1; i < 10; i++) Line(t.X + t.Width * i / 10, t.Bottom - U(3), t.X + t.Width * i / 10, t.Bottom, c, 1, 0.5);
    }

    /// <summary>Selectable data table. Rows register region hits "{idPrefix}{index}".</summary>
    public void Table(Rect r, string[] headers, double[] weights, IReadOnlyList<string[]> rows, int first, int selected, string idPrefix, Func<int, int, uint?>? colorOf = null, double fs = 8.5)
    {
        double rh = U(fs + 6.5), total = weights.Sum();
        var cx = new double[weights.Length + 1]; cx[0] = r.X;
        for (int i = 0; i < weights.Length; i++) cx[i + 1] = cx[i] + r.Width * weights[i] / total;
        Fill(new Rect(r.X, r.Y, r.Width, rh), P.Primary, 0.12);
        for (int i = 0; i < headers.Length; i++) Text(headers[i], cx[i] + U(3), r.Y + U(2), fs - 1, P.Primary, Align.Left, true, 0.9);
        int vis = Math.Max(1, (int)((r.Height - rh) / rh));
        for (int k = 0; k < vis; k++)
        {
            int idx = first + k; if (idx >= rows.Count) break;
            var rr = new Rect(r.X, r.Y + rh * (k + 1), r.Width, rh);
            if (idx == selected) { Fill(rr, P.Accent, 0.16); Frame(rr, P.Accent, 1, 0.6); }
            else if (k % 2 == 1) Fill(rr, P.Primary, 0.035);
            for (int i = 0; i < headers.Length && i < rows[idx].Length; i++)
            {
                uint col = colorOf?.Invoke(idx, i) ?? (idx == selected ? P.Accent : P.Text);
                string s = rows[idx][i]; double maxW = cx[i + 1] - cx[i] - U(5);
                if (TextWidth(s, fs * Scale * S.FontScale) > maxW && s.Length > 3) s = s[..Math.Max(2, (int)(s.Length * maxW / TextWidth(s, fs * Scale * S.FontScale)) - 1)] + "…";
                Text(s, cx[i] + U(3), rr.Y + U(2.5), fs, col, Align.Left, idx == selected, idx == selected ? 1 : 0.88);
            }
            AddHit(idPrefix + idx, rr);
        }
        if (rows.Count > vis) { double f = (double)first / Math.Max(1, rows.Count - vis); Fill(new Rect(r.Right - U(2), r.Y + rh, U(2), r.Height - rh), P.Primary, 0.1); Fill(new Rect(r.Right - U(2), r.Y + rh + (r.Height - rh - U(14)) * f, U(2), U(14)), P.Primary, 0.8); }
    }

    public int TableVisibleRows(Rect r, double fs = 8.5) => Math.Max(1, (int)((r.Height - U(fs + 6.5)) / U(fs + 6.5)));

    /// <summary>Radio telescope dish seen from the side; az/el drive the visual tilt/rotation.</summary>
    public void DishIcon(double cx, double cy, double size, double az, double el, uint c, bool selected, bool online, double sync)
    {
        double s = size, tilt = (90 - el) * Math.PI / 180;
        uint col = online ? c : 0xFF506070;
        // base & pedestal
        Fill(new Rect(cx - s * 0.18, cy + s * 0.25, s * 0.36, s * 0.1), col, 0.5);
        Line(cx, cy + s * 0.25, cx, cy - s * 0.05, col, 2, 0.8);
        // rotate the dish bowl around the pivot
        double pivotY = cy - s * 0.05;
        Rotated(cx, pivotY, -(el - 45) * 0.9 + Math.Sin(az * Math.PI / 180) * 6, () =>
        {
            var g = new StreamGeometry();
            using (var ctx = g.Open())
            {
                ctx.BeginFigure(new Point(cx - s * 0.45, pivotY - s * 0.12), false);
                ctx.ArcTo(new Point(cx + s * 0.45, pivotY - s * 0.12), new Size(s * 0.5, s * 0.38), 0, false, SweepDirection.CounterClockwise);
                ctx.EndFigure(false);
            }
            Dc.DrawGeometry(Br(col, 0.12), Pn(col, 1.8, 0.95), g);
            Line(cx, pivotY - s * 0.12, cx, pivotY - s * 0.55, col, 1, 0.8);              // feed support
            Disc(cx, pivotY - s * 0.55, U(2.2), selected ? P.Accent : col);
        });
        if (selected) GlowCircle(cx, cy, s * 0.62, P.Accent, 1.2, 0.9, U(3));
        if (online && sync > 0.05) { Led(cx + s * 0.5, cy + s * 0.3, sync > 0.8 ? 1 : 2, 3); }
    }

    /// <summary>Stylised front view anatomical outline with selectable regions and layer rendering.</summary>
    public void Body(Rect r, int region, int layer, double scan, double beat, uint c, Func<string, bool>? scanActive = null)
    {
        double h = r.Height * 0.95, cx = r.Center.X, top = r.Y + r.Height * 0.025, w = h * 0.34;
        uint outline = Palette.WithAlpha(c, 0.9);
        double headR = h * 0.065, neck = top + headR * 2.0, shoulder = neck + h * 0.045, chest = shoulder + h * 0.2, waist = chest + h * 0.14, hip = waist + h * 0.1, knee = hip + h * 0.23, foot = top + h;
        double sw = w * 0.5;
        // silhouette
        var torso = new[] { new Point(cx - sw, shoulder), new Point(cx + sw, shoulder), new Point(cx + sw * 0.9, chest), new Point(cx + sw * 0.7, waist), new Point(cx + sw * 0.78, hip), new Point(cx - sw * 0.78, hip), new Point(cx - sw * 0.7, waist), new Point(cx - sw * 0.9, chest) };
        PolyFill(torso, c, 0.07, c, 1.4, 0.85);
        Circle(cx, top + headR, headR, c, 1.4, 0.85); Line(cx - headR * 0.35, top + headR * 2, cx - headR * 0.35, shoulder, c, 1.2, 0.7); Line(cx + headR * 0.35, top + headR * 2, cx + headR * 0.35, shoulder, c, 1.2, 0.7);
        foreach (int sd in new[] { -1, 1 })
        {
            Poly(new[] { new Point(cx + sd * sw, shoulder), new Point(cx + sd * (sw + w * 0.28), chest + h * 0.05), new Point(cx + sd * (sw + w * 0.38), waist + h * 0.08) }, c, 1.6, 0.75);
            Poly(new[] { new Point(cx + sd * sw * 0.75, hip), new Point(cx + sd * sw * 0.62, knee), new Point(cx + sd * sw * 0.55, foot) }, c, 1.8, 0.75);
            Poly(new[] { new Point(cx + sd * sw * 0.15, hip), new Point(cx + sd * sw * 0.18, knee), new Point(cx + sd * sw * 0.2, foot) }, c, 1.8, 0.75);
        }
        // layers
        switch (layer % 5)
        {
            case 0: // skeletal
                Line(cx, neck, cx, hip, 0xFFE8F4FF, 2, 0.55);
                for (int i = 0; i < 6; i++) { double y = shoulder + h * 0.03 + i * h * 0.026; double rw = sw * (0.78 - i * 0.07); Poly(new[] { new Point(cx - rw, y + h * 0.02), new Point(cx - rw * 0.4, y), new Point(cx, y - h * 0.004), new Point(cx + rw * 0.4, y), new Point(cx + rw, y + h * 0.02) }, 0xFFE8F4FF, 1.2, 0.45); }
                Circle(cx, hip - h * 0.02, sw * 0.55, 0xFFE8F4FF, 1.4, 0.4); Line(cx - sw, shoulder, cx + sw, shoulder, 0xFFE8F4FF, 2, 0.5);
                break;
            case 1: // muscular
                for (int i = 0; i < 14; i++) { double y = shoulder + i * h * 0.02; Line(cx - sw * 0.85, y, cx + sw * 0.85, y + h * 0.01, 0xFFFF8070, 1, 0.22); }
                break;
            case 2: // vascular
                Line(cx - sw * 0.1, chest - h * 0.03, cx - sw * 0.1, hip + h * 0.03, 0xFFFF4060, 2, 0.6); Line(cx + sw * 0.1, chest - h * 0.03, cx + sw * 0.1, hip + h * 0.03, 0xFF4080FF, 2, 0.6);
                for (int i = 0; i < 8; i++) { double y = chest + i * h * 0.03; Line(cx - sw * 0.1, y, cx - sw * (0.6 + 0.05 * i % 3), y + h * 0.03, 0xFFFF4060, 1, 0.4); Line(cx + sw * 0.1, y, cx + sw * (0.6 + 0.05 * i % 3), y + h * 0.03, 0xFF4080FF, 1, 0.4); }
                break;
            case 3: // nervous
                Line(cx, top + headR, cx, hip, 0xFFFFE070, 2, 0.8);
                for (int i = 0; i < 10; i++) { double y = shoulder + i * h * 0.04; Line(cx, y, cx - sw * 0.9, y + h * 0.03, 0xFFFFE070, 1, 0.35); Line(cx, y, cx + sw * 0.9, y + h * 0.03, 0xFFFFE070, 1, 0.35); }
                break;
            default: // organs
                double pulse = 1 + 0.12 * beat;
                Dc.DrawEllipse(Br(0xFFFF3050, 0.35), Pn(0xFFFF3050, 1.4, 0.9), new Point(cx - sw * 0.2, chest - h * 0.05), sw * 0.2 * pulse, sw * 0.24 * pulse);
                Dc.DrawEllipse(Br(0xFF60C0FF, 0.2), Pn(0xFF60C0FF, 1.2, 0.8), new Point(cx - sw * 0.55, chest - h * 0.07), sw * 0.26, sw * 0.42);
                Dc.DrawEllipse(Br(0xFF60C0FF, 0.2), Pn(0xFF60C0FF, 1.2, 0.8), new Point(cx + sw * 0.55, chest - h * 0.07), sw * 0.26, sw * 0.42);
                Dc.DrawEllipse(Br(0xFFC08040, 0.25), Pn(0xFFC08040, 1.2, 0.8), new Point(cx + sw * 0.35, waist - h * 0.04), sw * 0.3, sw * 0.17);
                Dc.DrawEllipse(Br(0xFFC0A060, 0.2), Pn(0xFFC0A060, 1.2, 0.8), new Point(cx - sw * 0.2, waist + h * 0.01), sw * 0.4, sw * 0.14);
                break;
        }
        // region selection brackets
        (double y0, double y1)[] reg = { (top, neck + h * 0.01), (shoulder, waist), (waist, hip), (hip, foot), (top, foot) };
        var rg = reg[region % 5]; var sel = new Rect(cx - w * 0.75, rg.y0 - h * 0.01, w * 1.5, rg.y1 - rg.y0 + h * 0.02);
        Brackets(sel, P.Accent, 9, 1.6);
        // scanning plane
        if (scan > 0 && scan < 1)
        {
            double y = rg.y0 + (rg.y1 - rg.y0) * scan;
            Fill(new Rect(cx - w * 0.9, y - h * 0.012, w * 1.8, h * 0.024), P.Accent, 0.16); GlowLine(cx - w * 0.9, y, cx + w * 0.9, y, P.Accent, 1.6);
            for (int i = 0; i < 6; i++) Line(cx - w * 0.9, y + i * U(2), cx + w * 0.9, y + i * U(2), P.Accent, 1, 0.1 - i * 0.015);
        }
        // clickable region bands
        for (int i = 0; i < 5; i++) { var (a, b) = reg[i]; if (i == 4) continue; AddHit("body:" + i, new Rect(cx - w * 0.8, a, w * 1.6, b - a)); }
        AddHit("body:4", new Rect(r.X, r.Y, U(34), U(18)));
        Text("ALL", r.X + U(3), r.Y + U(2), 8, region % 5 == 4 ? P.Accent : P.Text, Align.Left, true, 0.9);
    }
}
