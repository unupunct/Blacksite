using Avalonia;
using Blacksite.Core.Configuration;
using Blacksite.Core.Themes;
using Blacksite.UI.Rendering;

namespace Blacksite.UI.Modules;

/// <summary>
/// Base for the dense multi-panel presets (11–15). Provides a zoned panel layout (wide) / scrolling stack (narrow),
/// per-panel expand, show/hide/reorder, per-preset accent, simulation/alert/animation speed and persisted layouts.
/// </summary>
public abstract class DeckModule : ModuleBase
{
    public enum Zone { Main, Left, Right, Bottom }

    protected sealed class PanelDef
    {
        public string Id = "", Title = ""; public Zone Zone; public double Weight = 1; public Action<Gfx, Rect> Draw = null!; public Rect? Frac;   // Frac = free placement (fractions of the content area)
    }

    protected readonly List<PanelDef> Defs = new();
    bool _config, _built;
    string? _expanded;
    double _scroll, _contentH, _viewH;
    bool _narrow;
    protected double AnimT;
    static readonly string?[] Accents = { null, "#00DCFF", "#2878FF", "#28FF5A", "#FFAA20", "#FF40D0", "#AA6EFF", "#FF3030", "#E8F4FF" };
    static readonly string[] AccentNames = { "DEFAULT", "CYAN", "BLUE", "GREEN", "AMBER", "MAGENTA", "VIOLET", "RED", "WHITE" };

    protected PanelLayout Cfg
    {
        get
        {
            if (!C.Settings.PanelLayouts.TryGetValue(Info.Id, out var l)) C.Settings.PanelLayouts[Info.Id] = l = new PanelLayout();
            return l;
        }
    }

    protected void Def(string id, string title, Zone z, double weight, Action<Gfx, Rect> draw) => Defs.Add(new PanelDef { Id = id, Title = title, Zone = z, Weight = weight, Draw = draw });
    /// <summary>Panel with an explicit placement (x, y, w, h as fractions of the content area) for wide screens; narrow screens still stack by zone.</summary>
    protected void DefAt(string id, string title, Zone z, double x, double y, double w, double h, Action<Gfx, Rect> draw) => Defs.Add(new PanelDef { Id = id, Title = title, Zone = z, Weight = 1, Draw = draw, Frac = new Rect(x, y, w, h) });
    protected void Rebuild() { Defs.Clear(); _expanded = null; BuildPanels(); _scroll = 0; }
    protected abstract void BuildPanels();
    public abstract string[] ModuleActions { get; }
    protected abstract string? DoAction(string id);
    protected virtual void Tick(double simDt, double animDt) { }
    protected virtual bool PanelDrag(Point p, Vector d, string? id) => false;
    protected virtual bool PanelWheel(Point p, double delta) => false;
    protected virtual void PanelTap(Point p, string? id) { }
    protected virtual void PanelDoubleTap(Point p, string? id) { }
    protected virtual string? PanelButton(string id) => null;
    protected virtual void PanelSlider(string id, double v) { }
    protected virtual void BeforeDraw(Gfx g) { }
    protected virtual void AfterDraw(Gfx g, Rect area) { }

    public override bool CustomSounds => true;
    public sealed override string[] Actions => ModuleActions.Append("PANELS").ToArray();
    public override bool IsActive(string a) => a == "PANELS" && _config || IsActionActive(a);
    protected virtual bool IsActionActive(string a) => false;

    public sealed override string? OnAction(string id)
    {
        if (id == "PANELS") { _config = !_config; return _config ? "PANEL & DISPLAY OPTIONS" : "OPTIONS CLOSED"; }
        return DoAction(id);
    }

    protected override void OnInit() { }

    void Ensure() { if (_built) return; _built = true; BuildPanels(); }

    public sealed override void Update(double dt)
    {
        Ensure();
        var c = Cfg; AnimT += dt * c.AnimSpeed;
        Tick(C.SimDt * c.SimSpeed, dt * c.AnimSpeed);
    }

    // ------------------------------------------------------------------ layout
    List<PanelDef> Visible()
    {
        var c = Cfg;
        IEnumerable<PanelDef> q = Defs.Where(d => !c.Hidden.Contains(d.Id));
        if (c.Order.Count > 0) q = q.OrderBy(d => { int i = c.Order.IndexOf(d.Id); return i < 0 ? 1000 + Defs.IndexOf(d) : i; });
        return q.ToList();
    }

    public override void Draw(Gfx g, Rect area)
    {
        Ensure();
        var saved = g.P; var cfg = Cfg;
        if (cfg.Accent != null)
        {
            uint a = Palette.Parse(cfg.Accent, saved.Primary);
            g.P = saved with { Primary = a, Accent = Palette.Lerp(a, 0xFFFFFFFF, 0.55) };
        }
        BeforeDraw(g);
        double gap = g.U(g.S.UiDensity == 0 ? 4 : g.S.UiDensity == 2 ? 9 : 6);
        area = area.Deflate(gap);
        var vis = Visible();
        _narrow = area.Width < g.U(900) || area.Width / area.Height < 1.2;
        if (_expanded != null && vis.Any(v => v.Id == _expanded)) { var d = vis.First(v => v.Id == _expanded); DrawPanel(g, d, area, true); }
        else
        {
            _expanded = null;
            if (_narrow) DrawNarrow(g, area, vis, gap); else DrawWide(g, area, vis, gap);
        }
        AfterDraw(g, area);
        if (_config) DrawConfig(g, area);
        g.P = saved;
    }

    void DrawPanel(Gfx g, PanelDef d, Rect r, bool expanded)
    {
        var inner = g.Panel(r, d.Title);
        double th = g.U(18);
        g.AddHit("dk:exp:" + d.Id, new Rect(r.X, r.Y, r.Width - (expanded ? g.U(60) : 0), th));
        if (expanded) g.Button("dk:close", new Rect(r.Right - g.U(56), r.Y + 1, g.U(54), th - 2), "CLOSE", false, true, null, 8);
        using (g.Clip(inner))
        {
            bool holo = d.Zone == Zone.Main && d.Id != "map";
            if (holo) g.HoloBase(inner, inner.Center.X, inner.Bottom - g.U(10), Math.Min(inner.Width * 0.55, inner.Height * 0.9));
            d.Draw(g, inner);
            if (holo) g.HoloShimmer(inner);
        }
    }

    void DrawWide(Gfx g, Rect area, List<PanelDef> vis, double gap)
    {
        if (vis.Any(v => v.Frac.HasValue))
        {
            foreach (var v in vis) { var f = v.Frac ?? new Rect(0, 0, 1, 1); DrawPanel(g, v, new Rect(area.X + f.X * area.Width + gap / 2, area.Y + f.Y * area.Height + gap / 2, Math.Max(10, f.Width * area.Width - gap), Math.Max(10, f.Height * area.Height - gap)), false); }
            return;
        }
        var main = vis.Where(v => v.Zone == Zone.Main).ToList(); var L = vis.Where(v => v.Zone == Zone.Left).ToList();
        var R = vis.Where(v => v.Zone == Zone.Right).ToList(); var B = vis.Where(v => v.Zone == Zone.Bottom).ToList();
        double sideShare = 0.28;
        double lw = L.Count > 0 ? area.Width * sideShare : 0, rw = R.Count > 0 ? area.Width * sideShare : 0;
        if (main.Count == 0 && B.Count == 0) { lw = L.Count > 0 && R.Count > 0 ? area.Width / 2 - gap / 2 : area.Width; rw = R.Count > 0 ? (L.Count > 0 ? area.Width / 2 - gap / 2 : area.Width) : 0; }
        var left = new Rect(area.X, area.Y, lw, area.Height);
        var right = new Rect(area.Right - rw, area.Y, rw, area.Height);
        double cx = area.X + lw + (lw > 0 ? gap : 0), cwid = area.Right - rw - (rw > 0 ? gap : 0) - cx;
        var center = new Rect(cx, area.Y, Math.Max(0, cwid), area.Height);
        Column(g, left, L, gap);
        Column(g, right, R, gap);
        if (center.Width > 10)
        {
            if (main.Count > 0 && B.Count > 0)
            {
                double bh = center.Height * 0.32;
                Column(g, new Rect(center.X, center.Y, center.Width, center.Height - bh - gap), main, gap);
                Row(g, new Rect(center.X, center.Bottom - bh, center.Width, bh), B, gap);
            }
            else if (main.Count > 0) Column(g, center, main, gap);
            else if (B.Count > 0) Row(g, center, B, gap);
        }
    }

    void Column(Gfx g, Rect r, List<PanelDef> l, double gap)
    {
        if (l.Count == 0 || r.Width < 10) return;
        double total = l.Sum(x => x.Weight), avail = r.Height - gap * (l.Count - 1), y = r.Y;
        foreach (var d in l) { double h = avail * d.Weight / total; DrawPanel(g, d, new Rect(r.X, y, r.Width, h), false); y += h + gap; }
    }

    void Row(Gfx g, Rect r, List<PanelDef> l, double gap)
    {
        if (l.Count == 0) return;
        double total = l.Sum(x => x.Weight), avail = r.Width - gap * (l.Count - 1), x0 = r.X;
        foreach (var d in l) { double w = avail * d.Weight / total; DrawPanel(g, d, new Rect(x0, r.Y, w, r.Height), false); x0 += w + gap; }
    }

    void DrawNarrow(Gfx g, Rect area, List<PanelDef> vis, double gap)
    {
        g.AddHit("dk:scroll", area);
        _viewH = area.Height;
        var main = vis.Where(v => v.Zone == Zone.Main).ToList();
        var rest = vis.Where(v => v.Zone != Zone.Main).ToList();
        double y = area.Y - _scroll, cols = area.Width > g.U(360) ? 2 : 1;
        using (g.Clip(area))
        {
            foreach (var m in main) { double h = Math.Min(area.Width * 0.95, Math.Max(g.U(240), area.Height * 0.62)); DrawIf(g, m, new Rect(area.X, y, area.Width, h), area); y += h + gap; }
            double cw = (area.Width - gap * (cols - 1)) / cols;
            for (int i = 0; i < rest.Count; i += (int)cols)
            {
                double rh = 0;
                for (int k = 0; k < cols && i + k < rest.Count; k++) rh = Math.Max(rh, g.U(150) * Math.Max(0.9, rest[i + k].Weight));
                for (int k = 0; k < cols && i + k < rest.Count; k++) DrawIf(g, rest[i + k], new Rect(area.X + k * (cw + gap), y, cw, rh), area);
                y += rh + gap;
            }
        }
        _contentH = y + _scroll - area.Y;
        _scroll = Math.Clamp(_scroll, 0, Math.Max(0, _contentH - _viewH));
        if (_contentH > _viewH) { double f = _viewH / _contentH; g.Fill(new Rect(area.Right - g.U(2), area.Y + (area.Height - area.Height * f) * (_scroll / (_contentH - _viewH)), g.U(2), area.Height * f), g.P.Primary, 0.6); }
    }

    void DrawIf(Gfx g, PanelDef d, Rect r, Rect view) { if (r.Bottom < view.Y - 2 || r.Y > view.Bottom + 2) return; DrawPanel(g, d, r, false); }

    // ------------------------------------------------------------------ options popup
    void DrawConfig(Gfx g, Rect area)
    {
        g.Fill(area, 0xFF000000, 0.7); g.AddHit("dk:blocker", area);
        double w = Math.Min(area.Width - g.U(8), g.U(760)), h = Math.Min(area.Height - g.U(8), g.U(520));
        var r = new Rect(area.Center.X - w / 2, area.Center.Y - h / 2, w, h);
        var inner = g.Panel(r, "PANELS & DISPLAY OPTIONS");
        g.AddHit("dk:frame", r);
        bool two = inner.Width > g.U(560);
        double colW = two ? inner.Width / 2 - g.U(4) : inner.Width, rowH = Math.Min(g.U(g.Touch ? 28 : 22), (inner.Height - g.U(170)) / Math.Max(1, two ? (Defs.Count + 1) / 2 : Defs.Count));
        var c = Cfg; var ordered = Defs.OrderBy(d => { int i = c.Order.IndexOf(d.Id); return i < 0 ? 1000 + Defs.IndexOf(d) : i; }).ToList();
        for (int i = 0; i < ordered.Count; i++)
        {
            var d = ordered[i]; int col = two ? i / ((ordered.Count + 1) / 2) : 0, row = two ? i % ((ordered.Count + 1) / 2) : i;
            double x = inner.X + col * (colW + g.U(8)), y = inner.Y + row * rowH;
            bool on = !c.Hidden.Contains(d.Id);
            g.Button("dk:vis:" + d.Id, new Rect(x, y, g.U(48), rowH - 2), on ? "SHOW" : "HIDE", on, true, null, 7.5);
            g.Text(d.Title, x + g.U(54), y + (rowH - g.U(10)) / 2, 8.5, on ? g.P.Text : g.P.Text, Align.Left, on, on ? 1 : 0.4);
            g.Button("dk:up:" + d.Id, new Rect(x + colW - g.U(46), y, g.U(22), rowH - 2), "<", false, true, null, 8);
            g.Button("dk:dn:" + d.Id, new Rect(x + colW - g.U(22), y, g.U(22), rowH - 2), ">", false, true, null, 8);
        }
        double sy = inner.Bottom - g.U(160);
        g.Slider("dk:sim", new Rect(inner.X, sy, inner.Width / 3 - g.U(6), g.U(36)), (c.SimSpeed - 0.25) / 3.75, "DATA UPDATE SPEED", $"{c.SimSpeed:0.00}X");
        g.Slider("dk:anim", new Rect(inner.X + inner.Width / 3, sy, inner.Width / 3 - g.U(6), g.U(36)), (c.AnimSpeed - 0.25) / 1.75, "ANIMATION SPEED", $"{c.AnimSpeed:0.00}X");
        g.Slider("dk:alert", new Rect(inner.X + inner.Width * 2 / 3, sy, inner.Width / 3, g.U(36)), c.AlertFrequency / 3, "ALERT FREQUENCY", c.AlertFrequency < 0.05 ? "OFF" : $"{c.AlertFrequency:0.0}X");
        double by = sy + g.U(50), bw = (inner.Width - g.U(12)) / 4, bh = g.U(g.Touch ? 36 : 28);
        int ai = Math.Max(0, Array.FindIndex(Accents, a => a == c.Accent));
        g.Button("dk:accent", new Rect(inner.X, by, bw, bh), "ACCENT: " + AccentNames[ai], false, true, null, 8.5);
        g.Button("dk:reset", new Rect(inner.X + bw + g.U(4), by, bw, bh), "RESTORE LAYOUT", false, true, g.P.Warning, 8.5);
        g.Button("dk:save", new Rect(inner.X + 2 * (bw + g.U(4)), by, bw, bh), "SAVE LAYOUT", true, true, null, 8.5);
        g.Button("dk:done", new Rect(inner.X + 3 * (bw + g.U(4)), by, bw, bh), "DONE", false, true, null, 8.5);
        g.Text("LAYOUTS ARE STORED PER PRESET AND SURVIVE PRESET SWITCHING", inner.X, inner.Bottom - g.U(12), 7.5, g.P.Text, Align.Left, false, 0.5);
    }

    // ------------------------------------------------------------------ input
    public sealed override string? OnModuleButton(string id)
    {
        if (id.StartsWith("dk:"))
        {
            var p = id.Split(':'); var c = Cfg;
            switch (p[1])
            {
                case "close": _expanded = null; return null;
                case "done": _config = false; C.Save(); return "OPTIONS CLOSED";
                case "save": C.Save(); return "LAYOUT SAVED";
                case "vis": if (!c.Hidden.Remove(p[2])) c.Hidden.Add(p[2]); C.Save(); return null;
                case "up": case "dn": Move(p[2], p[1] == "up" ? -1 : 1); return null;
                case "accent": { int i = Array.FindIndex(Accents, a => a == c.Accent); c.Accent = Accents[(Math.Max(0, i) + 1) % Accents.Length]; C.Save(); return null; }
                case "reset": c.Hidden.Clear(); c.Order.Clear(); c.Accent = null; c.SimSpeed = c.AnimSpeed = c.AlertFrequency = 1; C.Save(); return "DEFAULT LAYOUT RESTORED";
            }
            return null;
        }
        return PanelButton(id);
    }

    void Move(string id, int dir)
    {
        var c = Cfg; if (c.Order.Count == 0) c.Order = Defs.Select(d => d.Id).ToList();
        foreach (var d in Defs) if (!c.Order.Contains(d.Id)) c.Order.Add(d.Id);
        int i = c.Order.IndexOf(id), j = i + dir; if (i < 0 || j < 0 || j >= c.Order.Count) return;
        (c.Order[i], c.Order[j]) = (c.Order[j], c.Order[i]); C.Save();
    }

    public sealed override void OnModuleSlider(string id, double v)
    {
        var c = Cfg;
        switch (id)
        {
            case "dk:sim": c.SimSpeed = Math.Round((0.25 + v * 3.75) * 20) / 20; return;
            case "dk:anim": c.AnimSpeed = Math.Round((0.25 + v * 1.75) * 20) / 20; return;
            case "dk:alert": c.AlertFrequency = Math.Round(v * 3 * 10) / 10; return;
        }
        PanelSlider(id, v);
    }

    public sealed override void Tap(Point p, string? id)
    {
        if (id != null && id.StartsWith("dk:exp:")) { _expanded = _expanded == null ? id[7..] : null; return; }
        PanelTap(p, id);
    }

    public sealed override void DoubleTap(Point p, string? id) { if (id != null && id.StartsWith("dk:exp:")) return; PanelDoubleTap(p, id); }

    public sealed override bool Drag(Point p, Vector d, string? id)
    {
        if (_config) return false;
        if (PanelDrag(p, d, id)) return true;
        if (_narrow && _expanded == null) { _scroll = Math.Clamp(_scroll - d.Y, 0, Math.Max(0, _contentH - _viewH)); return true; }
        return false;
    }

    public sealed override void Wheel(Point p, double delta)
    {
        if (_config) return;
        if (PanelWheel(p, delta)) return;
        if (_narrow && _expanded == null) _scroll = Math.Clamp(_scroll - delta * 60, 0, Math.Max(0, _contentH - _viewH));
    }
}
