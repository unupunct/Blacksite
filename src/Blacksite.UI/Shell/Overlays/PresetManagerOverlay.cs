using Avalonia;
using Blacksite.Core.Configuration;
using Blacksite.Core.Presets;
using Blacksite.Core.Themes;
using Blacksite.UI.Rendering;

namespace Blacksite.UI.Shell.Overlays;

public sealed class PresetManagerOverlay : Overlay
{
    string? _edit;          // custom preset id being edited
    bool _renaming; string _nameBuf = "";
    bool _favOnly;
    string _status = "";

    static readonly string[] ColorNames = { "DEFAULT", "CYAN", "BLUE", "GREEN", "EMERALD", "AMBER", "ORANGE", "RED", "MAGENTA", "VIOLET", "WHITE", "OLIVE" };
    static readonly string?[] ColorHex = { null, "#00DCFF", "#2878FF", "#28FF5A", "#00EB96", "#FFAA20", "#FF8C14", "#FF3030", "#FF40D0", "#AA6EFF", "#E8F4FF", "#96AF46" };

    public PresetManagerOverlay(BlacksiteView v) : base(v) { }

    AppSettings S => C.Settings;
    public override bool WantsText => _renaming;

    public override void Draw(Gfx g, Rect b)
    {
        var inner = Frame(g, b, "PRESET MANAGER", 1280, 900);
        double bh = g.U(g.Touch ? 36 : 28), gap = g.U(5);
        // toolbar
        string[] ids = { "pm:rot", "pm:new", "pm:imp", "pm:exp", "pm:favs" };
        string[] lab = { S.AutoRotate ? "ROTATE: ON" : "ROTATE: OFF", "+ CUSTOM", "IMPORT", "EXPORT", _favOnly ? "ALL" : "FAVORITES" };
        double bw = (inner.Width - gap * 4) / 5;
        for (int i = 0; i < 5; i++)
            g.Button(ids[i], new Rect(inner.X + i * (bw + gap), inner.Y, bw, bh), lab[i], i == 0 && S.AutoRotate || i == 4 && _favOnly, true, null, bw < g.U(90) ? 8.5 : 10);

        double editH = _edit != null ? g.U(g.Touch ? 86 : 70) : 0;
        var list = new Rect(inner.X, inner.Y + bh + gap, inner.Width - g.U(6), inner.Height - bh - gap - editH - (_status.Length > 0 ? g.U(16) : 0));
        var all = C.AllPresets().Where(x => !_favOnly || S.Favorites.Contains(x.id)).ToList();
        int cols = list.Width > g.U(1000) ? 5 : list.Width > g.U(620) ? 3 : 2;
        double cw = (list.Width - gap * (cols - 1)) / cols, ch = cw * (cols >= 5 ? 0.88 : 0.82) + g.U(10);
        int rows = (all.Count + cols - 1) / cols;
        ContentHeight = rows * (ch + gap);
        BeginList(g, list);
        using (g.Clip(list))
        {
            for (int i = 0; i < all.Count; i++)
            {
                var r = new Rect(list.X + (i % cols) * (cw + gap), list.Y + (i / cols) * (ch + gap) - Scroll, cw, ch);
                if (r.Bottom < list.Y || r.Y > list.Bottom) continue;
                DrawCard(g, r, all[i].id, all[i].name, all[i].info, all[i].custom);
            }
        }
        DrawScrollbar(g);
        if (_edit != null) DrawEditor(g, new Rect(inner.X, inner.Bottom - editH - (_status.Length > 0 ? g.U(16) : 0), inner.Width, editH));
        if (_status.Length > 0) g.Text(_status, inner.X, inner.Bottom - g.U(13), 9, g.P.Primary, Align.Left, true);
    }

    void DrawCard(Gfx g, Rect r, string id, string name, PresetInfo info, CustomPreset? custom)
    {
        var pal = C.PaletteFor(info, custom);
        bool current = V.PresetId == id;
        var saved = g.P; g.P = pal;
        g.Fill(r, pal.Panel, 0.9);
        var th = new Rect(r.X + g.U(4), r.Y + g.U(4), r.Width - g.U(8), (r.Height - g.U(8)) * 0.52);
        using (g.Clip(th)) Thumbnail(g, th, info.Id, pal);
        g.Frame(th, pal.Primary, 1, 0.4);
        double ty = th.Bottom + g.U(4);
        g.Text(name, r.X + g.U(8), ty, r.Width < g.U(180) ? 9 : 11, pal.Primary, Align.Left, true);
        HeatWarningOverlay.Wrap(g, info.Description, r.X + g.U(8), ty + g.U(15), r.Width - g.U(16), 8);
        double by = r.Bottom - g.U(18);
        // colour theme swatches
        uint[] sw = { pal.Primary, pal.Secondary, pal.Accent, pal.Warning };
        for (int i = 0; i < sw.Length; i++) g.Fill(new Rect(r.X + g.U(8) + i * g.U(11), by, g.U(9), g.U(9)), sw[i]);
        var sp = custom?.SoundProfile ?? info.Sound.ToString();
        g.Text(sp.ToUpper(), r.X + g.U(8) + 4 * g.U(11) + g.U(4), by - g.U(1), 8, pal.Text, Align.Left, false, 0.7);
        g.Frame(r, pal.Primary, current ? 2 : 1, current ? 1 : 0.4);
        if (current) g.Brackets(r, pal.Accent, 12, 2.5);
        g.P = saved;
        g.AddHit("pm:go:" + id, r, HitKind.Button);
        // favourite star (drawn after hit registration so it wins)
        bool fav = S.Favorites.Contains(id);
        double bs = g.U(g.Touch ? 28 : 22);
        var star = new Rect(th.Right - bs - g.U(2), th.Y + g.U(2), bs, bs);
        g.Fill(star, g.P.Background, 0.7);
        g.Button("pm:fav:" + id, star, fav ? "*" : "+", fav, true, 0xFFFFC040, 12);
        // reorder arrows
        var l = new Rect(r.Right - bs * 2 - g.U(6), r.Bottom - bs - g.U(4), bs, bs);
        g.Button("pm:left:" + id, l, "<", false, true, null, 10);
        g.Button("pm:right:" + id, new Rect(l.Right + g.U(2), l.Y, bs, bs), ">", false, true, null, 10);
        if (custom != null)
            g.Button("pm:edit:" + id, new Rect(r.Right - bs * 2 - g.U(6) - g.U(46), l.Y, g.U(42), bs), "EDIT", _edit == custom.Id, true, null, 8.5);
    }

    void DrawEditor(Gfx g, Rect r)
    {
        var cp = S.CustomPresets.FirstOrDefault(x => "c:" + x.Id == _edit || x.Id == _edit);
        if (cp == null) { _edit = null; return; }
        var inner = g.Panel(r, "EDIT CUSTOM PRESET");
        double bh = Math.Min(g.U(g.Touch ? 36 : 28), inner.Height), gap = g.U(4);
        string name = _renaming ? _nameBuf + (g.Pulse(2) > 0.5 ? "_" : " ") : cp.Name;
        string[] ids = { "pm:ren", "pm:base", "pm:pri", "pm:sec", "pm:snd", "pm:del" };
        string[] lab =
        {
            name, "BASE: " + PresetCatalog.Get(cp.BasePresetId).Name.Split(' ')[0],
            "PRIMARY: " + ColorNames[Math.Max(0, Array.FindIndex(ColorHex, x => x == cp.Primary))],
            "SECOND: " + ColorNames[Math.Max(0, Array.FindIndex(ColorHex, x => x == cp.Secondary))],
            "SOUND: " + (cp.SoundProfile ?? PresetCatalog.Get(cp.BasePresetId).Sound.ToString()).ToUpper(), "DELETE",
        };
        int perRow = inner.Width < g.U(560) ? 3 : 6;
        double bw = (inner.Width - gap * (perRow - 1)) / perRow;
        for (int i = 0; i < ids.Length; i++)
            g.Button(ids[i], new Rect(inner.X + (i % perRow) * (bw + gap), inner.Y + (i / perRow) * (bh + gap), bw, bh), lab[i], i == 0 && _renaming, true, i == 5 ? g.P.Warning : null, 8.5);
    }

    CustomPreset? Edited => S.CustomPresets.FirstOrDefault(x => x.Id == _edit);

    public override void OnButton(string id)
    {
        base.OnButton(id);
        if (!id.StartsWith("pm:")) return;
        var p = id.Split(':', 3);
        _status = "";
        switch (p[1])
        {
            case "go": V.SwitchTo(p[2], true); break;
            case "fav":
                if (!S.Favorites.Remove(p[2])) S.Favorites.Add(p[2]);
                C.Save(); break;
            case "favs": _favOnly = !_favOnly; _status = _favOnly ? "SHOWING FAVORITES" : "SHOWING ALL"; break;
            case "left": case "right": Move(p[2], p[1] == "left" ? -1 : 1); break;
            case "rot": S.AutoRotate = !S.AutoRotate; V.ApplySettings(); _status = S.AutoRotate ? $"AUTO ROTATION EVERY {S.AutoRotateSeconds}S" : "AUTO ROTATION OFF"; break;
            case "new":
                {
                    var bp = PresetCatalog.Get(V.Module.Info.Id);
                    var cp = new CustomPreset { Name = $"CUSTOM {S.CustomPresets.Count + 1}", BasePresetId = bp.Id };
                    S.CustomPresets.Add(cp); _edit = cp.Id; C.Save(); _status = "CUSTOM PRESET CREATED"; break;
                }
            case "edit": _edit = p[2].StartsWith("c:") ? p[2][2..] : p[2]; _renaming = false; break;
            case "ren": if (Edited is { } e) { _renaming = !_renaming; _nameBuf = e.Name; if (!_renaming) { } } break;
            case "base":
                if (Edited is { } e1) { int i = PresetCatalog.All.ToList().FindIndex(x => x.Id == e1.BasePresetId); e1.BasePresetId = PresetCatalog.All[(i + 1) % PresetCatalog.All.Count].Id; Apply(e1); }
                break;
            case "pri": if (Edited is { } e2) { e2.Primary = NextColor(e2.Primary); Apply(e2); } break;
            case "sec": if (Edited is { } e3) { e3.Secondary = NextColor(e3.Secondary); Apply(e3); } break;
            case "snd":
                if (Edited is { } e4)
                {
                    var names = Enum.GetNames<SoundProfile>(); string cur = e4.SoundProfile ?? PresetCatalog.Get(e4.BasePresetId).Sound.ToString();
                    e4.SoundProfile = names[(Array.IndexOf(names, cur) + 1) % names.Length]; Apply(e4);
                }
                break;
            case "del":
                if (Edited is { } e5) { S.CustomPresets.Remove(e5); S.Favorites.Remove("c:" + e5.Id); S.PresetOrder.Remove("c:" + e5.Id); if (V.PresetId == "c:" + e5.Id) V.SwitchTo("space", true); _edit = null; C.Save(); _status = "PRESET DELETED"; }
                break;
            case "exp":
                _ = Task.Run(async () => { try { var ok = await C.Platform.SaveTextFileAsync("BLACKSITE_PRESETS.json", ConfigStore.ExportPresets(S)); _status = ok ? "PRESETS EXPORTED" : "EXPORT CANCELLED"; } catch (Exception ex) { _status = "ERROR: " + ex.Message; } });
                break;
            case "imp":
                _ = ImportAsync();
                break;
        }
    }

    async Task ImportAsync()
    {
        try
        {
            var t = await C.Platform.PickTextFileAsync("Import BLACKSITE_PRESETS.json");
            if (t == null) { _status = "IMPORT CANCELLED"; return; }
            int n = ConfigStore.ImportPresets(S, t); C.Save(); _status = $"IMPORTED {n} ITEM(S)";
        }
        catch (Exception ex) { _status = "IMPORT FAILED: " + ex.Message; }
    }

    static string? NextColor(string? cur)
    {
        int i = Array.FindIndex(ColorHex, x => x == cur); if (i < 0) i = 0;
        return ColorHex[(i + 1) % ColorHex.Length];
    }

    void Apply(CustomPreset cp) { C.Save(); if (V.PresetId == "c:" + cp.Id) V.SwitchTo("c:" + cp.Id, false); }

    void Move(string id, int dir)
    {
        var order = C.AllPresets().Select(x => x.id).ToList();
        int i = order.IndexOf(id), j = i + dir;
        if (i < 0 || j < 0 || j >= order.Count) return;
        (order[i], order[j]) = (order[j], order[i]);
        S.PresetOrder = order; C.Save();
    }

    public override bool Key(string combo, string? text)
    {
        if (!_renaming) return false;
        var e = Edited; if (e == null) { _renaming = false; return false; }
        if (text != null) { foreach (var ch in text) if (!char.IsControl(ch) && _nameBuf.Length < 22) _nameBuf += char.ToUpperInvariant(ch); return true; }
        if (combo == "Back") { if (_nameBuf.Length > 0) _nameBuf = _nameBuf[..^1]; return true; }
        if (combo == "Enter") { if (_nameBuf.Trim().Length > 0) e.Name = _nameBuf.Trim(); _renaming = false; C.Save(); return true; }
        if (combo == "Escape") { _renaming = false; return true; }
        return false;
    }

    // ------------------------------------------------------------------ procedural thumbnails
    public static void Thumbnail(Gfx g, Rect r, string id, Palette pal)
    {
        g.Fill(r, pal.Background);
        double cx = r.Center.X, cy = r.Center.Y, m = Math.Min(r.Width, r.Height);
        double t = g.Time;
        switch (id)
        {
            case "space":
                for (int i = 0; i < 24; i++) g.Disc(r.X + (i * 53 % 97) / 97.0 * r.Width, r.Y + (i * 37 % 89) / 89.0 * r.Height, g.U(0.8), 0xFFFFFFFF, 0.5);
                g.Planet(cx - m * 0.1, cy, m * 0.28, 0xFFD8B070, 0xFF8A6030, t * 0.4, true);
                g.Circle(cx - m * 0.1, cy, m * 0.5, pal.Primary, 1, 0.5);
                break;
            case "hacker":
                for (int i = 0; i < 9; i++) g.Text(new string((char)('0' + (i * 7) % 10), 1) + (0x3A00 + i * 311).ToString("X4") + " " + (0xBEEF * (i + 3)).ToString("X6"), r.X + g.U(6), r.Y + g.U(4) + i * g.U(11), 8, pal.Primary, Align.Left, false, 0.3 + 0.7 * ((i + (int)(t * 3)) % 4) / 4.0);
                break;
            case "surveillance":
                for (int i = 0; i < 4; i++)
                {
                    var c = new Rect(r.X + 4 + (i % 2) * (r.Width / 2 - 2), r.Y + 4 + (i / 2) * (r.Height / 2 - 2), r.Width / 2 - 8, r.Height / 2 - 8);
                    g.Fill(c, pal.Secondary, 0.25); g.Frame(c, pal.Primary, 1, 0.6);
                    g.Disc(c.X + 6, c.Y + 6, g.U(2.5), 0xFFFF3030, g.Pulse(1, i));
                }
                break;
            case "jarvis":
                g.Rings(cx, cy, m * 0.46, 5, pal.Primary, 1); g.Disc(cx, cy, m * 0.07, pal.Accent); g.GlowCircle(cx, cy, m * 0.12, pal.Primary);
                break;
            case "lockdown":
                for (double x = r.X - r.Height; x < r.Right; x += g.U(18)) g.Line(x, r.Bottom, x + r.Height, r.Y, pal.Primary, g.U(5), 0.10);
                g.PolyFill(new[] { new Point(cx, cy - m * 0.3), new Point(cx + m * 0.3, cy + m * 0.22), new Point(cx - m * 0.3, cy + m * 0.22) }, pal.Primary, 0.2 + 0.2 * g.Pulse(1.5), pal.Primary, 2);
                g.Text("!", cx, cy - m * 0.12, 26, pal.Primary, Align.Center, true);
                break;
            case "satellite":
                g.Globe(cx, cy, m * 0.42, t * 20, 20, pal.Panel, pal.Primary, true);
                break;
            case "reactor":
                g.GlowCircle(cx, cy, m * 0.12 + m * 0.02 * g.Pulse(1), pal.Primary, 3);
                for (int i = 0; i < 3; i++) g.Arc(cx, cy, m * (0.2 + 0.1 * i), t * 60 * (i % 2 == 0 ? 1 : -1), 250, i == 1 ? pal.Secondary : pal.Primary, 2.2);
                g.Arc(cx, cy, m * 0.48, 135, 200, pal.Accent, 2);
                break;
            case "cyber":
                var rnd = new Random(5);
                var pts = Enumerable.Range(0, 9).Select(_ => new Point(r.X + 10 + rnd.NextDouble() * (r.Width - 20), r.Y + 10 + rnd.NextDouble() * (r.Height - 20))).ToArray();
                for (int i = 1; i < pts.Length; i++) g.Line(pts[i].X, pts[i].Y, pts[i - 1].X, pts[i - 1].Y, pal.Primary, 1, 0.4);
                foreach (var p in pts) g.Disc(p.X, p.Y, g.U(3), pal.Primary);
                g.Disc(pts[3].X, pts[3].Y, g.U(5), pal.Warning, 0.5 + 0.5 * g.Pulse(2));
                break;
            case "deepspace":
                for (int i = 0; i < 40; i++)
                {
                    double a = i * 2.399, d = ((i * 17 + t * 25) % 100) / 100.0;
                    g.Line(cx + Math.Cos(a) * d * m * 0.15, cy + Math.Sin(a) * d * m * 0.15, cx + Math.Cos(a) * d * m * 0.8, cy + Math.Sin(a) * d * m * 0.8, i % 3 == 0 ? pal.Secondary : pal.Primary, 1, d * 0.8);
                }
                break;
            case "orbital":
                g.Globe(cx - m * 0.12, cy, m * 0.3, t * 14, 22, pal.Panel, pal.Secondary, true);
                g.Dc.DrawEllipse(null, g.Pn(pal.Accent, 1.2, 0.8), new Point(cx - m * 0.12, cy), m * 0.5, m * 0.18);
                for (int i = 0; i < 5; i++) g.Disc(cx - m * 0.12 + Math.Cos(t * 0.6 + i * 1.3) * m * 0.5, cy + Math.Sin(t * 0.6 + i * 1.3) * m * 0.18, g.U(2.2), i == 0 ? pal.Warning : pal.Primary);
                for (int i = 0; i < 3; i++) g.Text("GS-0" + (i + 1), r.Right - g.U(36), r.Y + g.U(6) + i * g.U(10), 6.5, pal.Primary, Align.Left, false, 0.8);
                break;
            case "quantum":
                g.Rings(cx, cy, m * 0.46, 4, pal.Primary, 0.8);
                for (int i = 0; i < 12; i++) { double a = i * 0.52 + t, rr = m * (0.18 + 0.2 * Math.Abs(Math.Sin(a * 0.7 + i))); g.Disc(cx + Math.Cos(a) * rr, cy + Math.Sin(a) * rr * 0.6, g.U(1.8), i % 3 == 0 ? pal.Accent : pal.Secondary); }
                g.Disc(cx, cy, m * 0.05, pal.Accent); g.GlowCircle(cx, cy, m * 0.09, pal.Accent, 1.4);
                break;
            case "commarray":
                { var sp = new List<Point>(); for (int i = 0; i <= 40; i++) { double v = 0.1 + 0.05 * Math.Sin(i * 3.1 + t) + 0.8 * Math.Exp(-Math.Pow((i - 14) / 1.2, 2)) + 0.5 * Math.Exp(-Math.Pow((i - 29) / 1.2, 2)); sp.Add(new Point(r.X + r.Width * i / 40, r.Y + r.Height * 0.52 - v * r.Height * 0.4)); }
                  g.Poly(sp, pal.Primary, 1.4, 0.95);
                  for (int y = 0; y < 8; y++) for (int x = 0; x < 28; x++) { double v = 0.15 + 0.7 * Math.Exp(-Math.Pow((x - 10 - y * 0.2) / 1.1, 2)) + 0.3 * Math.Sin(x * 2.3 + y + t) * 0.15; g.Fill(new Rect(r.X + 4 + x * (r.Width - 8) / 28, r.Y + r.Height * 0.58 + y * r.Height * 0.045, (r.Width - 8) / 28 - 1, r.Height * 0.04), v > 0.6 ? pal.Accent : pal.Primary, Math.Clamp(v * 0.8, 0.05, 0.9)); } }
                break;
            case "medical":
                { var ep = new List<Point>(); for (int i = 0; i <= 80; i++) { double ph = ((i / 80.0) * 2 + t * 0.4) % 1, v = 0.12 * Math.Exp(-Math.Pow((ph - 0.18) / 0.05, 2)) + Math.Exp(-Math.Pow((ph - 0.5) / 0.012, 2)) - 0.2 * Math.Exp(-Math.Pow((ph - 0.46) / 0.015, 2)) + 0.25 * Math.Exp(-Math.Pow((ph - 0.75) / 0.06, 2)); ep.Add(new Point(r.X + r.Width * i / 80, cy - v * r.Height * 0.3)); }
                  g.Poly(ep, pal.Secondary, 1.6, 0.95); g.Text("72", r.Right - g.U(8), r.Y + g.U(4), 18, pal.Secondary, Align.Right, true); g.Text("SpO2 98", r.X + g.U(6), r.Bottom - g.U(14), 8, pal.Primary, Align.Left, true); }
                break;
            case "facility":
                for (int lv = 0; lv < 6; lv++) for (int c = 0; c < 4; c++) { var rc = new Rect(r.X + r.Width * 0.12 + c * r.Width * 0.2, r.Y + 5 + lv * (r.Height - 10) / 6, r.Width * 0.17, (r.Height - 10) / 6 - 3); uint col = (lv * 4 + c) % 11 == 3 ? pal.Warning : pal.Secondary; g.Fill(rc, col, 0.18); g.Frame(rc, col, 1, 0.7); }
                g.Fill(new Rect(r.X + r.Width * 0.04, r.Y + 5, g.U(3), r.Height - 10), pal.Accent, 0.6);
                break;
            default:
                g.Grid(r, 14, pal.Primary, 0.18);
                g.Radar(cx + m * 0.1, cy, m * 0.36, t * 80 % 360, new (double, double, uint, string?)[] { (40, 0.5, pal.Secondary, null), (200, 0.7, pal.Warning, null) }, pal.Primary);
                g.Fill(new Rect(r.X + 8, r.Y + 8, 12, 8), pal.Secondary, 0.8);
                break;
        }
    }
}
