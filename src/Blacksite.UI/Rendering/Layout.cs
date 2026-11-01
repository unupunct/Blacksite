using Avalonia;

namespace Blacksite.UI.Rendering;

/// <summary>Adaptive panel layout: wide screens get side columns, narrow/portrait screens stack.</summary>
public static class Layout
{
    public sealed class Result
    {
        public Rect Main;
        public List<Rect> Panels = new();
        public bool Narrow;
    }

    public static Result Adaptive(Gfx g, Rect area, int panelCount, double mainWeight = 0.5)
    {
        double gap = g.U(g.S.UiDensity == 0 ? 4 : g.S.UiDensity == 2 ? 10 : 7);
        var res = new Result();
        area = area.Deflate(gap);
        bool narrow = area.Width < g.U(820) || area.Width / area.Height < 1.15;
        res.Narrow = narrow;
        if (!narrow)
        {
            int left = (panelCount + 1) / 2, right = panelCount - left;
            double sideW = (area.Width * (1 - mainWeight) - gap * 2) / 2;
            if (right == 0) sideW = area.Width * (1 - mainWeight) - gap;
            var lcol = new Rect(area.X, area.Y, sideW, area.Height);
            var mid = new Rect(area.X + sideW + gap, area.Y, area.Width - sideW * (right > 0 ? 2 : 1) - gap * (right > 0 ? 2 : 1), area.Height);
            var rcol = new Rect(area.Right - sideW, area.Y, sideW, area.Height);
            res.Main = mid;
            // alternate fill order so panels read left→right top→bottom
            var l = Column(lcol, left, gap); var r = right > 0 ? Column(rcol, right, gap) : new List<Rect>();
            for (int i = 0; i < panelCount; i++) res.Panels.Add(i % 2 == 0 ? l[i / 2] : r[i / 2]);
        }
        else
        {
            double mainH = area.Height * (area.Width > area.Height ? 0.55 : 0.46);
            res.Main = new Rect(area.X, area.Y, area.Width, mainH);
            var rest = new Rect(area.X, area.Y + mainH + gap, area.Width, area.Height - mainH - gap);
            double minH = g.U(g.Touch ? 110 : 96);
            int cols = rest.Width > g.U(540) ? 3 : 2;
            int rows = Math.Max(1, (int)((rest.Height + gap) / (minH + gap)));
            rows = Math.Min(rows, (panelCount + cols - 1) / cols);
            int fit = Math.Min(panelCount, rows * cols);
            double cw = (rest.Width - gap * (cols - 1)) / cols, ch = (rest.Height - gap * (rows - 1)) / rows;
            for (int i = 0; i < fit; i++)
                res.Panels.Add(new Rect(rest.X + (i % cols) * (cw + gap), rest.Y + (i / cols) * (ch + gap), cw, ch));
        }
        return res;
    }

    public static List<Rect> Column(Rect r, int n, double gap)
    {
        var l = new List<Rect>(n);
        if (n <= 0) return l;
        double h = (r.Height - gap * (n - 1)) / n;
        for (int i = 0; i < n; i++) l.Add(new Rect(r.X, r.Y + i * (h + gap), r.Width, h));
        return l;
    }

    public static Rect[] Cols(Rect r, double gap, params double[] w)
    {
        double total = w.Sum(), avail = r.Width - gap * (w.Length - 1), x = r.X;
        var o = new Rect[w.Length];
        for (int i = 0; i < w.Length; i++) { double cw = avail * w[i] / total; o[i] = new Rect(x, r.Y, cw, r.Height); x += cw + gap; }
        return o;
    }

    public static Rect[] Rows(Rect r, double gap, params double[] w)
    {
        double total = w.Sum(), avail = r.Height - gap * (w.Length - 1), y = r.Y;
        var o = new Rect[w.Length];
        for (int i = 0; i < w.Length; i++) { double ch = avail * w[i] / total; o[i] = new Rect(r.X, y, r.Width, ch); y += ch + gap; }
        return o;
    }
}
