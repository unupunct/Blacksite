using Avalonia;
using Avalonia.Media;
using Blacksite.Core.Configuration;
using Blacksite.Core.Simulation;
using Blacksite.Core.Themes;

namespace Blacksite.UI.Rendering;

public sealed partial class Gfx
{
    readonly List<Point> _tmp = new(300);

    /// <summary>Scrolling line graph from a history buffer.</summary>
    public void Graph(Rect r, RingBuffer buf, uint c, double? min = null, double? max = null, bool fill = true, string? label = null, string? unit = null)
    {
        Fill(r, P.Background, 0.5);
        Frame(r, c, 1, 0.3);
        for (int i = 1; i < 4; i++) Line(r.X, r.Y + r.Height * i / 4, r.Right, r.Y + r.Height * i / 4, c, 1, 0.08);
        for (int i = 1; i < 8; i++) Line(r.X + r.Width * i / 8, r.Y, r.X + r.Width * i / 8, r.Bottom, c, 1, 0.06);
        if (buf.Count < 2) return;
        var (mn, mx) = buf.Range();
        double lo = min ?? mn, hi = max ?? mx;
        if (hi - lo < 1e-6) hi = lo + 1;
        int n = buf.Count, step = Quality == GraphicsProfile.Low ? 3 : 1;
        _tmp.Clear();
        for (int i = 0; i < n; i += step)
        {
            double x = r.X + r.Width * i / (n - 1);
            double y = r.Bottom - 2 - (r.Height - 4) * Math.Clamp((buf[i] - lo) / (hi - lo), 0, 1);
            _tmp.Add(new Point(x, y));
        }
        if (fill && Quality != GraphicsProfile.Low)
        {
            var area = new List<Point>(_tmp.Count + 2) { new Point(r.X, r.Bottom) };
            area.AddRange(_tmp); area.Add(new Point(r.Right, r.Bottom));
            PolyFill(area, c, 0.12);
        }
        if (Glow) Poly(_tmp, c, 3.5, 0.12);
        Poly(_tmp, c, 1.3, 0.95);
        if (label != null) Text(label, r.X + U(4), r.Y + U(2), 9, c, Align.Left, true, 0.9);
        string val = buf.Last.ToString(buf.Last > 99 ? "0" : "0.0") + (unit ?? "");
        Text(val, r.Right - U(4), r.Y + U(2), 9, P.Text, Align.Right, false, 0.9);
    }

    public void Bars(Rect r, IReadOnlyList<double> values, uint c, double highlightAbove = 2)
    {
        if (values.Count == 0) return;
        double bw = r.Width / values.Count;
        for (int i = 0; i < values.Count; i++)
        {
            double v = Math.Clamp(values[i], 0, 1), h = (r.Height - 2) * v;
            Fill(new Rect(r.X + i * bw + 1, r.Bottom - h, Math.Max(1, bw - 2), h), v > highlightAbove ? P.Warning : c, 0.7);
        }
        Line(r.X, r.Bottom, r.Right, r.Bottom, c, 1, 0.6);
    }

    /// <summary>Rotating dial-style gauge with tick marks and needle.</summary>
    public void Gauge(double cx, double cy, double rad, double v, double max, string label, string unit, uint? color = null, double warnFrac = 0.85)
    {
        uint c = color ?? P.Primary;
        double frac = Math.Clamp(v / max, 0, 1);
        uint col = frac > warnFrac ? P.Warning : c;
        double start = 135, sweep = 270;
        Circle(cx, cy, rad, c, 1, 0.3);
        Arc(cx, cy, rad - U(4), start, sweep, c, 1.2, 0.25);
        Arc(cx, cy, rad - U(4), start, sweep * frac, col, 3.2, 1);
        int ticks = 28;
        for (int i = 0; i <= ticks; i++)
        {
            double ang = (start + sweep * i / ticks) * Math.PI / 180;
            double r1 = rad - U(9), r2 = rad - U(i % 4 == 0 ? 17 : 13);
            Line(cx + Math.Cos(ang) * r1, cy + Math.Sin(ang) * r1, cx + Math.Cos(ang) * r2, cy + Math.Sin(ang) * r2, c, 1, i % 4 == 0 ? 0.7 : 0.3);
        }
        double na = (start + sweep * frac) * Math.PI / 180;
        GlowLine(cx, cy, cx + Math.Cos(na) * (rad - U(14)), cy + Math.Sin(na) * (rad - U(14)), col, 1.8);
        Disc(cx, cy, U(4), col);
        Text(v.ToString(max >= 100 ? "0" : "0.0"), cx, cy + rad * 0.25, Math.Max(10, rad * 0.30 / Scale), P.Text, Align.Center, true);
        Text(label, cx, cy + rad * 0.62, Math.Max(8, rad * 0.16 / Scale), c, Align.Center, true, 0.9);
        if (unit.Length > 0) Text(unit, cx, cy + rad * 0.80, Math.Max(7, rad * 0.13 / Scale), P.Text, Align.Center, false, 0.55);
    }

    /// <summary>Vertical thermometer-style meter.</summary>
    public void Meter(Rect r, double v, double max, string label, uint? color = null, double warnFrac = 0.8)
    {
        uint c = color ?? P.Primary;
        double frac = Math.Clamp(v / max, 0, 1);
        uint col = frac > warnFrac ? P.Warning : c;
        var tube = new Rect(r.X + r.Width * 0.35, r.Y, r.Width * 0.3, r.Height - U(26));
        Fill(tube, c, 0.08); Frame(tube, c, 1, 0.6);
        double h = tube.Height * frac;
        Fill(new Rect(tube.X + 1, tube.Bottom - h, tube.Width - 2, h), col, 0.75);
        for (int i = 0; i <= 10; i++)
        {
            double y = tube.Bottom - tube.Height * i / 10;
            Line(tube.Right + 2, y, tube.Right + U(i % 5 == 0 ? 9 : 5), y, c, 1, 0.6);
        }
        Text(v.ToString("0"), r.Center.X, r.Bottom - U(24), 10, col, Align.Center, true);
        Text(label, r.Center.X, r.Bottom - U(12), 8, P.Text, Align.Center, false, 0.7);
    }

    public void Radar(double cx, double cy, double rad, double sweepDeg, IEnumerable<(double ang, double dist, uint col, string? tag)> blips, uint? color = null)
    {
        uint c = color ?? P.Primary;
        Disc(cx, cy, rad, P.Background, 0.55);
        for (int i = 1; i <= 4; i++) Circle(cx, cy, rad * i / 4, c, 1, 0.22);
        Line(cx - rad, cy, cx + rad, cy, c, 1, 0.2); Line(cx, cy - rad, cx, cy + rad, c, 1, 0.2);
        for (int d = 0; d < 360; d += 30)
        {
            double a = d * Math.PI / 180;
            Line(cx + Math.Cos(a) * rad * 0.93, cy + Math.Sin(a) * rad * 0.93, cx + Math.Cos(a) * rad, cy + Math.Sin(a) * rad, c, 1, 0.6);
        }
        int trail = Quality == GraphicsProfile.Low ? 6 : 22;
        for (int i = 0; i < trail; i++)
        {
            double a = (sweepDeg - i * 2.2) * Math.PI / 180;
            Line(cx, cy, cx + Math.Cos(a) * rad, cy + Math.Sin(a) * rad, c, 1.5, 0.30 * (1 - (double)i / trail));
        }
        GlowLine(cx, cy, cx + Math.Cos(sweepDeg * Math.PI / 180) * rad, cy + Math.Sin(sweepDeg * Math.PI / 180) * rad, c, 1.6);
        foreach (var b in blips)
        {
            double a = b.ang * Math.PI / 180, d = b.dist * rad;
            double diff = ((sweepDeg - b.ang) % 360 + 360) % 360;
            double fade = diff < 200 ? 1 - diff / 220.0 : 0.15;
            double bx = cx + Math.Cos(a) * d, by = cy + Math.Sin(a) * d;
            Disc(bx, by, U(3), b.col, 0.25 + 0.75 * fade);
            if (diff < 40) Circle(bx, by, U(3 + diff / 5), b.col, 1, 0.5 * (1 - diff / 40));
            if (b.tag != null) Text(b.tag, bx + U(6), by - U(5), 8, b.col, Align.Left, false, 0.35 + 0.65 * fade);
        }
        Circle(cx, cy, rad, c, 1.4, 0.8);
    }

    /// <summary>Concentric rotating ring set (holographic).</summary>
    public void Rings(double cx, double cy, double rad, int count, uint c, double speed = 1)
    {
        for (int i = 0; i < count; i++)
        {
            double r = rad * (0.35 + 0.65 * (i + 1) / count);
            double dir = i % 2 == 0 ? 1 : -1;
            double rot = Time * speed * (12 + i * 9) * dir;
            double a = 0.35 + 0.25 * Math.Sin(Time * 0.8 + i);
            int segs = 3 + i % 3;
            for (int s = 0; s < segs; s++)
            {
                double start = rot + s * 360.0 / segs;
                Arc(cx, cy, r, start, 360.0 / segs * 0.6, c, 1.4 + (i % 2), a);
            }
            if (i % 2 == 1 && Quality != GraphicsProfile.Low)
                Circle(cx, cy, r + U(5), c, 1, 0.15, U(3));
        }
    }

    public void HexStream(Rect r, Random rng, uint c, int rows, double fs = 9)
    {
        double lh = U(fs + 3);
        int n = Math.Min(rows, (int)(r.Height / lh));
        long seed = (long)(Time * 8);
        for (int i = 0; i < n; i++)
        {
            var rr = new Random((int)(seed + i * 977 + (int)r.X));
            var sb = new System.Text.StringBuilder();
            sb.Append((0x7F000000 + (seed + i) * 16).ToString("X8")).Append("  ");
            int bytes = Math.Max(4, (int)(r.Width / U(fs * 1.7)) - 5);
            for (int b = 0; b < bytes; b++) sb.Append(rr.Next(256).ToString("X2")).Append(' ');
            Text(sb.ToString(), r.X, r.Y + i * lh, fs, c, Align.Left, false, 0.35 + 0.5 * ((i + seed) % 5) / 5.0);
        }
    }
}
