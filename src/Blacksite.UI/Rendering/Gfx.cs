using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Blacksite.Core.Configuration;
using Blacksite.Core.Themes;

namespace Blacksite.UI.Rendering;

public enum HitKind { Button, Slider, Region }

public readonly record struct Hit(string Id, Rect R, HitKind Kind, bool Enabled = true);

public enum Align { Left, Center, Right }

/// <summary>
/// Immediate-mode drawing surface shared by every preset. Wraps the Avalonia DrawingContext with
/// cached brushes/pens/text, HUD widgets and a per-frame hit-region registry for touch/mouse input.
/// </summary>
public sealed partial class Gfx
{
    public DrawingContext Dc = null!;
    public Palette P = null!;
    public AppSettings S = null!;
    public double Time, Dt;
    public double Scale = 1;
    public double Brightness = 1, HudOpacity = 1;
    public GraphicsProfile Quality = GraphicsProfile.Balanced;
    public bool Reduced;
    public bool Touch;
    public Rect Bounds;

    public readonly List<Hit> Hits = new(128);
    public Point Pointer = new(-1000, -1000);
    public bool PointerIsDown;
    public string? PressedId;
    public string? HoverId;

    static readonly FontFamily Mono = new("Cascadia Mono, Consolas, Menlo, DejaVu Sans Mono, monospace");
    static readonly Typeface Reg = new(Mono, FontStyle.Normal, FontWeight.Normal);
    static readonly Typeface Bold = new(Mono, FontStyle.Normal, FontWeight.Bold);

    readonly Dictionary<uint, ImmutableSolidColorBrush> _brushes = new();
    readonly Dictionary<(uint, int, int), ImmutablePen> _pens = new();
    readonly Dictionary<(string, int, uint, bool), FormattedText> _text = new();
    readonly Dictionary<string, double> _press = new();

    public double PixelScale = 1;   // device pixels per DIP: sphere/texture bitmaps are rendered at native resolution
    public bool Glow => Quality != GraphicsProfile.Low && !Reduced && S.Glow;
    public bool Rich => Quality == GraphicsProfile.High;

    public void Begin(DrawingContext dc, Rect bounds)
    {
        Dc = dc; Bounds = bounds; Hits.Clear();
        if (_text.Count > 900) _text.Clear();
        if (_brushes.Count > 2000) _brushes.Clear();
        if (_pens.Count > 1500) _pens.Clear();
    }

    // ---------- colour helpers
    public Color Col(uint argb, double alpha = 1)
    {
        double a = ((argb >> 24) & 0xFF) / 255.0 * alpha * HudOpacity;
        double b = Brightness;
        return Color.FromArgb((byte)Math.Clamp(a * 255, 0, 255),
            (byte)Math.Clamp(((argb >> 16) & 0xFF) * b, 0, 255),
            (byte)Math.Clamp(((argb >> 8) & 0xFF) * b, 0, 255),
            (byte)Math.Clamp((argb & 0xFF) * b, 0, 255));
    }

    public IBrush Br(uint argb, double alpha = 1)
    {
        uint key = Palette.WithAlpha(argb, (((argb >> 24) & 0xFF) / 255.0) * alpha);
        if (!_brushes.TryGetValue(key, out var b)) _brushes[key] = b = new ImmutableSolidColorBrush(Col(key));
        return b;
    }

    public IPen Pn(uint argb, double w = 1, double alpha = 1, double dash = 0)
    {
        uint key = Palette.WithAlpha(argb, (((argb >> 24) & 0xFF) / 255.0) * alpha);
        var k = (key, (int)(w * 10 * 100 / 100), (int)(dash * 10));
        if (!_pens.TryGetValue(k, out var p))
        {
            ImmutableDashStyle? ds = dash > 0 ? new ImmutableDashStyle(new[] { dash, dash * 0.8 }, 0) : null;
            _pens[k] = p = new ImmutablePen(new ImmutableSolidColorBrush(Col(key)), w, ds, PenLineCap.Round, PenLineJoin.Round);
        }
        return p;
    }

    public double U(double v) => v * Scale;

    // ---------- primitives
    public void Line(double x1, double y1, double x2, double y2, uint c, double w = 1, double a = 1) =>
        Dc.DrawLine(Pn(c, w, a), new Point(x1, y1), new Point(x2, y2));

    public void Fill(Rect r, uint c, double a = 1) => Dc.DrawRectangle(Br(c, a), null, r);

    public void Frame(Rect r, uint c, double w = 1, double a = 1, double dash = 0) =>
        Dc.DrawRectangle(null, Pn(c, w, a, dash), r);

    public void Circle(double cx, double cy, double rad, uint c, double w = 1, double a = 1, double dash = 0) =>
        Dc.DrawEllipse(null, Pn(c, w, a, dash), new Point(cx, cy), rad, rad);

    public void Disc(double cx, double cy, double rad, uint c, double a = 1) =>
        Dc.DrawEllipse(Br(c, a), null, new Point(cx, cy), rad, rad);

    public void Glow2(Action<double, double> draw)
    {
        if (!Glow) { draw(1, 1); return; }
        draw(3.2, 0.12); draw(1.8, 0.28); draw(1, 1);
    }

    public void GlowLine(double x1, double y1, double x2, double y2, uint c, double w = 1.2, double a = 1)
    {
        if (Glow) Line(x1, y1, x2, y2, c, w * 3.2, a * 0.15);
        Line(x1, y1, x2, y2, c, w, a);
    }

    public void GlowCircle(double cx, double cy, double r, uint c, double w = 1.2, double a = 1, double dash = 0)
    {
        if (Glow) Circle(cx, cy, r, c, w * 3.4, a * 0.13, dash);
        Circle(cx, cy, r, c, w, a, dash);
    }

    public void Arc(double cx, double cy, double r, double startDeg, double sweepDeg, uint c, double w = 2, double a = 1)
    {
        if (Math.Abs(sweepDeg) < 0.1) return;
        sweepDeg = Math.Clamp(sweepDeg, -359.9, 359.9);
        double s = startDeg * Math.PI / 180, e = (startDeg + sweepDeg) * Math.PI / 180;
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(new Point(cx + Math.Cos(s) * r, cy + Math.Sin(s) * r), false);
            ctx.ArcTo(new Point(cx + Math.Cos(e) * r, cy + Math.Sin(e) * r), new Size(r, r), 0,
                Math.Abs(sweepDeg) > 180, sweepDeg > 0 ? SweepDirection.Clockwise : SweepDirection.CounterClockwise);
            ctx.EndFigure(false);
        }
        if (Glow) Dc.DrawGeometry(null, Pn(c, w * 3, a * 0.14), g);
        Dc.DrawGeometry(null, Pn(c, w, a), g);
    }

    public void Poly(IReadOnlyList<Point> pts, uint c, double w = 1, double a = 1, bool close = false)
    {
        if (pts.Count < 2) return;
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(pts[0], false);
            for (int i = 1; i < pts.Count; i++) ctx.LineTo(pts[i]);
            ctx.EndFigure(close);
        }
        Dc.DrawGeometry(null, Pn(c, w, a), g);
    }

    public void PolyFill(IReadOnlyList<Point> pts, uint fill, double fa, uint? stroke = null, double w = 1, double sa = 1)
    {
        if (pts.Count < 3) return;
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(pts[0], true);
            for (int i = 1; i < pts.Count; i++) ctx.LineTo(pts[i]);
            ctx.EndFigure(true);
        }
        Dc.DrawGeometry(Br(fill, fa), stroke.HasValue ? Pn(stroke.Value, w, sa) : null, g);
    }

    public void Rotated(double cx, double cy, double deg, Action draw)
    {
        var m = Matrix.CreateTranslation(-cx, -cy) * Matrix.CreateRotation(deg * Math.PI / 180) * Matrix.CreateTranslation(cx, cy);
        using (Dc.PushTransform(m)) draw();
    }

    public IDisposable Clip(Rect r) => Dc.PushClip(r);

    // ---------- text
    FormattedText Ft(string t, double size, uint col, bool bold)
    {
        var key = (t, (int)(size * 4), col, bold);
        if (!_text.TryGetValue(key, out var ft))
        {
            ft = new FormattedText(t, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, bold ? Bold : Reg, size, Br(col));
            _text[key] = ft;
        }
        return ft;
    }

    public double TextWidth(string t, double size, bool bold = false) => Ft(t, size, 0xFFFFFFFF, bold).Width;

    public void Text(string t, double x, double y, double size, uint col, Align al = Align.Left, bool bold = false, double alpha = 1)
    {
        if (string.IsNullOrEmpty(t)) return;
        size *= S.FontScale;
        uint c = Palette.WithAlpha(col, (((col >> 24) & 0xFF) / 255.0) * alpha);
        var ft = Ft(t, size, c, bold);
        double px = al == Align.Left ? x : al == Align.Center ? x - ft.Width / 2 : x - ft.Width;
        Dc.DrawText(ft, new Point(px, y));
    }

    public void TextIn(Rect r, string t, double size, uint col, Align al = Align.Center, bool bold = false, double alpha = 1)
    {
        double sz = size * S.FontScale;
        double h = Ft(t, sz, col, bold).Height;
        double x = al == Align.Left ? r.X : al == Align.Center ? r.Center.X : r.Right;
        Text(t, x, r.Y + (r.Height - h) / 2, size, col, al, bold, alpha);
    }

    // ---------- hit registry
    public void AddHit(string id, Rect r, HitKind k = HitKind.Region, bool enabled = true) => Hits.Add(new Hit(id, r, k, enabled));

    public void FlashPress(string id) => _press[id] = Time;

    double PressAnim(string id) =>
        _press.TryGetValue(id, out var t) ? Math.Clamp(1 - (Time - t) / 0.35, 0, 1) : 0;

    public double Pulse(double speed = 1, double phase = 0) => 0.5 + 0.5 * Math.Sin(Time * speed * 2 * Math.PI + phase);
}
