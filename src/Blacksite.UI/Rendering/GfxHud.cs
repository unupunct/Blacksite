using Avalonia;
using Avalonia.Media;
using Blacksite.Core.Themes;

namespace Blacksite.UI.Rendering;

public sealed partial class Gfx
{
    /// <summary>Corner-bracket frame used on every panel.</summary>
    public void Brackets(Rect r, uint c, double len = 10, double w = 1.4, double a = 1)
    {
        len = U(len);
        double x1 = r.X, y1 = r.Y, x2 = r.Right, y2 = r.Bottom;
        Line(x1, y1, x1 + len, y1, c, w, a); Line(x1, y1, x1, y1 + len, c, w, a);
        Line(x2, y1, x2 - len, y1, c, w, a); Line(x2, y1, x2, y1 + len, c, w, a);
        Line(x1, y2, x1 + len, y2, c, w, a); Line(x1, y2, x1, y2 - len, c, w, a);
        Line(x2, y2, x2 - len, y2, c, w, a); Line(x2, y2, x2, y2 - len, c, w, a);
    }

    /// <summary>Titled panel. Returns the inner content rectangle.</summary>
    public Rect Panel(Rect r, string title, uint? accent = null, bool fill = true)
    {
        uint c = accent ?? P.Primary;
        if (fill) Fill(r, P.Panel, 0.72);
        Frame(r, c, 1, 0.35);
        Brackets(r, c);
        double th = U(18);
        if (title.Length > 0)
        {
            Fill(new Rect(r.X + 1, r.Y + 1, r.Width - 2, th), c, 0.10);
            Text(title, r.X + U(8), r.Y + U(3), 10, c, Align.Left, true);
            double blink = Pulse(0.7, r.X);
            Disc(r.Right - U(10), r.Y + th / 2, U(2.5), c, 0.35 + 0.65 * blink);
            return new Rect(r.X + U(6), r.Y + th + U(4), Math.Max(1, r.Width - U(12)), Math.Max(1, r.Height - th - U(10)));
        }
        return r.Deflate(U(6));
    }

    public bool IsHover(string id) => HoverId == id;
    public bool IsPressed(string id) => PressedId == id && PointerIsDown;

    /// <summary>Tactile button: hover glow, press flash, registered for input.</summary>
    public void Button(string id, Rect r, string label, bool active = false, bool enabled = true, uint? color = null, double fs = 11)
    {
        uint c = color ?? P.Primary;
        if (!enabled) c = Palette.Lerp(c, P.Background, 0.7);
        double flash = PressAnim(id);
        bool hover = enabled && IsHover(id), down = enabled && IsPressed(id);
        double bgA = active ? 0.30 : hover ? 0.20 : 0.08;
        bgA += flash * 0.5 + (down ? 0.25 : 0);
        Fill(r, c, bgA);
        Frame(r, c, active || hover ? 1.6 : 1, enabled ? 0.9 : 0.4);
        if (Glow && (active || hover || flash > 0)) Frame(r.Inflate(U(1.5)), c, 2.5, 0.12 + flash * 0.3);
        double notch = U(5);
        Line(r.X, r.Y + notch, r.X + notch, r.Y, P.Background, 2, 1);
        TextIn(r, label, fs, active ? P.Text : c, Align.Center, true, enabled ? 1 : 0.5);
        if (active) Fill(new Rect(r.X, r.Bottom - U(2), r.Width, U(2)), c, 0.9);
        AddHit(id, r, HitKind.Button, enabled);
    }

    public void Toggle(string id, Rect r, string label, bool on, uint? color = null)
    {
        uint c = color ?? P.Primary;
        double sw = U(34), sh = U(16);
        var track = new Rect(r.Right - sw, r.Center.Y - sh / 2, sw, sh);
        TextIn(new Rect(r.X, r.Y, r.Width - sw - U(8), r.Height), label, 11, P.Text, Align.Left);
        Fill(track, on ? c : P.Panel, on ? 0.35 : 0.9);
        Frame(track, c, 1, 0.8);
        double kx = on ? track.Right - sh + U(2) : track.X + U(2);
        Fill(new Rect(kx, track.Y + U(2), sh - U(4), sh - U(4)), on ? c : P.Text, on ? 1 : 0.4);
        if (PressAnim(id) > 0) Frame(track.Inflate(U(2)), c, 2, PressAnim(id));
        AddHit(id, r, HitKind.Button);
    }

    public void Slider(string id, Rect r, double v, string label, string valueText, uint? color = null)
    {
        uint c = color ?? P.Primary;
        double lh = U(14);
        Text(label, r.X, r.Y, 10, P.Text, Align.Left, false, 0.85);
        Text(valueText, r.Right, r.Y, 10, c, Align.Right, true);
        var tr = new Rect(r.X, r.Y + lh + U(5), r.Width, U(6));
        Fill(tr, c, 0.12); Frame(tr, c, 1, 0.5);
        Fill(new Rect(tr.X, tr.Y, tr.Width * Math.Clamp(v, 0, 1), tr.Height), c, 0.7);
        double kx = tr.X + tr.Width * Math.Clamp(v, 0, 1);
        var knob = new Rect(kx - U(5), tr.Center.Y - U(9), U(10), U(18));
        Fill(knob, P.Text, 0.95); Frame(knob, c, 1.2);
        AddHit(id, new Rect(r.X - U(6), r.Y + lh - U(4), r.Width + U(12), r.Height - lh + U(8)), HitKind.Slider);
    }

    public void Bar(Rect r, double v, uint c, string? label = null, string? valueText = null, double warnAt = 2)
    {
        v = Math.Clamp(v, 0, 1);
        uint col = v > warnAt ? P.Warning : c;
        double lh = label != null ? U(13) : 0;
        if (label != null) { Text(label, r.X, r.Y - 1, 9, P.Text, Align.Left, false, 0.8); }
        if (valueText != null) Text(valueText, r.Right, r.Y - 1, 9, col, Align.Right, true);
        var tr = new Rect(r.X, r.Y + lh, r.Width, Math.Max(U(5), r.Height - lh));
        Fill(tr, c, 0.10); Frame(tr, c, 1, 0.4);
        int segs = Math.Max(6, (int)(tr.Width / U(7)));
        double sw = tr.Width / segs;
        int lit = (int)(v * segs);
        for (int i = 0; i < lit; i++) Fill(new Rect(tr.X + i * sw + 1, tr.Y + 1.5, sw - 2, tr.Height - 3), col, 0.55 + 0.4 * i / segs);
    }

    public void Badge(string text, double x, double y, uint c, bool filled = false, double fs = 9)
    {
        double w = TextWidth(text, fs * S.FontScale, true) + U(10), h = U(15);
        var r = new Rect(x, y, w, h);
        Fill(r, c, filled ? 0.85 : 0.12); Frame(r, c, 1, 0.8);
        TextIn(r, text, fs, filled ? P.Background : c, Align.Center, true);
    }

    public void StatusDot(double x, double y, bool ok, double r = 3.5)
    {
        uint c = ok ? 0xFF30FF70 : P.Warning;
        Disc(x, y, U(r), c, 0.4 + 0.6 * Pulse(ok ? 0.5 : 2, x));
    }

    public void KeyVal(double x, double y, double w, string k, string v, uint? vc = null, double fs = 10)
    {
        Text(k, x, y, fs, P.Text, Align.Left, false, 0.6);
        Text(v, x + w, y, fs, vc ?? P.Primary, Align.Right, true);
    }

    public void Grid(Rect r, double step, uint c, double a = 0.12)
    {
        step = U(step);
        for (double x = r.X; x <= r.Right; x += step) Line(x, r.Y, x, r.Bottom, c, 1, a);
        for (double y = r.Y; y <= r.Bottom; y += step) Line(r.X, y, r.Right, y, c, 1, a);
    }

    /// <summary>Top strip common to every preset.</summary>
    public void TitleStrip(Rect r, string title, string right)
    {
        Fill(r, P.Primary, 0.07);
        Line(r.X, r.Bottom, r.Right, r.Bottom, P.Primary, 1, 0.5);
        Text(title, r.X + U(10), r.Y + (r.Height - U(16)) / 2, 15, P.Primary, Align.Left, true);
        Text(right, r.Right - U(10), r.Y + (r.Height - U(11)) / 2, 10, P.Text, Align.Right, false, 0.7);
    }
}
