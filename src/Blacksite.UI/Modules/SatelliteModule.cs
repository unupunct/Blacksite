using Avalonia;
using Blacksite.Core.Simulation;
using Blacksite.UI.Rendering;

namespace Blacksite.UI.Modules;

/// <summary>PRESET 06 – geospatial intelligence centre. Orbits and tracks are simulated, not real tracking data.</summary>
public sealed class SatelliteModule : ModuleBase
{
    bool _regional, _grid = true, _coverage = true, _tracks = true;
    double _rotLon = 20, _tilt = 22, _zoom = 1, _cLon = 10, _cLat = 30, _mapZoom = 2.5;
    int _sel;
    double _scan = 1;
    bool _scanning;

    readonly (string id, double lat, double lon)[] _markers =
    {
        ("TGT-ALPHA", 48.2, 11.5), ("TGT-BRAVO", -23.5, -46.6), ("TGT-CHARLIE", 35.6, 139.7), ("TGT-DELTA", -33.8, 151.2), ("TGT-ECHO", 30.0, 31.2), ("TGT-FOXTROT", 55.7, 37.6),
    };

    public override string[] Actions => new[] { "SCAN", "GRID", "COVERAGE", "TRACKS", "REGION" };
    public override bool IsActive(string a) => a switch { "SCAN" => _scanning, "GRID" => _grid, "COVERAGE" => _coverage, "TRACKS" => _tracks, "REGION" => _regional, _ => false };
    public override string PrimaryAction => "SCAN";

    public override string? OnAction(string id)
    {
        switch (id)
        {
            case "SCAN": _scanning = true; _scan = 0; C.Audio.Play("ping"); return "SCANNING SWEEP INITIATED";
            case "GRID": _grid = !_grid; return "GRID " + (_grid ? "ON" : "OFF");
            case "COVERAGE": _coverage = !_coverage; return "COVERAGE " + (_coverage ? "ON" : "OFF");
            case "TRACKS": _tracks = !_tracks; return "ORBITAL TRACKS " + (_tracks ? "ON" : "OFF");
            case "REGION":
                _regional = !_regional;
                if (_regional) { var s = C.Sim.Satellites[_sel]; _cLon = s.Lon; _cLat = Math.Clamp(s.Lat, -60, 60); }
                return _regional ? "REGIONAL VIEW" : "GLOBAL VIEW";
        }
        return null;
    }

    public override void Update(double dt)
    {
        if (_scanning) { _scan += dt / 5; if (_scan >= 1) { _scanning = false; C.Toast("SCAN COMPLETE — 6 CONTACTS CATALOGUED (SIMULATED)"); C.Audio.Confirm(); } }
        if (!_regional) _rotLon += dt * 2.2 * (_dragging ? 0 : 1);
        _dragging = false;
    }
    bool _dragging;

    public override void Draw(Gfx g, Rect area)
    {
        var lay = Layout.Adaptive(g, area, 4, 0.6);
        DrawMain(g, lay.Main);
        for (int i = 0; i < lay.Panels.Count; i++) DrawPanel(g, i, lay.Panels[i]);
    }

    Point Proj(Rect c, double cx, double cy, double rad, double lat, double lon, out bool visible)
    {
        if (_regional) { var p = World.Project(c, lon, lat, _cLon, _cLat, _mapZoom); visible = c.Contains(p); return p; }
        var (pt, z) = World.Ortho(cx, cy, rad, lon, lat, _rotLon, _tilt); visible = z > 0.02; return pt;
    }

    void DrawMain(Gfx g, Rect r)
    {
        var c = g.Panel(r, _regional ? "REGIONAL VIEW // SIMULATED DATA" : "GLOBAL VIEW // SIMULATED DATA");
        double cx = c.Center.X, cy = c.Center.Y, rad = Math.Min(c.Width, c.Height) * 0.46 * _zoom;
        g.AddHit("map", c);
        using var clip = g.Clip(c);
        if (!_regional) g.HoloBase(c, cx, c.Bottom - g.U(16), rad * 1.5);
        if (_regional) g.FlatMap(c, _cLon, _cLat, _mapZoom, g.P.Secondary, _grid);
        else
        {
            g.Globe(cx, cy, rad, _rotLon, _tilt, g.P.Panel, g.P.Secondary, _grid);
        }
        var sats = C.Sim.Satellites;
        // tracks & coverage
        for (int i = 0; i < sats.Count; i++)
        {
            var s = sats[i]; bool sel = i == _sel;
            if (_tracks && (sel || g.Quality == Core.Configuration.GraphicsProfile.High || i % 3 == 0))
            {
                var pts = new List<Point>(); int steps = sel ? 60 : 30;
                for (int k = 0; k <= steps; k++)
                {
                    var (la, lo) = C.Sim.Predict(s, k * s.Period / 40 - s.Period * 0.1);
                    var p = Proj(c, cx, cy, rad, la, lo, out bool vis);
                    if (vis) pts.Add(p); else { if (pts.Count > 1) g.Poly(pts, sel ? g.P.Accent : g.P.Primary, sel ? 1.4 : 1, sel ? 0.9 : 0.3); pts.Clear(); }
                }
                if (pts.Count > 1) g.Poly(pts, sel ? g.P.Accent : g.P.Primary, sel ? 1.4 : 1, sel ? 0.9 : 0.3);
            }
        }
        for (int i = 0; i < sats.Count; i++)
        {
            var s = sats[i]; bool sel = i == _sel;
            var p = Proj(c, cx, cy, rad, s.Lat, s.Lon, out bool vis);
            if (!vis) continue;
            if (_coverage) { double cr = (_regional ? c.Width * 0.06 * _mapZoom / 2.5 : rad * 0.2) * (0.6 + s.AltitudeKm / 2400.0); g.Circle(p.X, p.Y, cr, g.P.Primary, 1, sel ? 0.6 : 0.25, g.U(3)); if (sel) g.Disc(p.X, p.Y, cr, g.P.Primary, 0.06); }
            g.Disc(p.X, p.Y, g.U(sel ? 4 : 3), sel ? g.P.Accent : g.P.Primary);
            if (sel) { g.GlowCircle(p.X, p.Y, g.U(9), g.P.Accent, 1.3); g.Text(s.Name, p.X + g.U(12), p.Y - g.U(6), 9, g.P.Accent, Align.Left, true); }
            g.AddHit("sat:" + i, new Rect(p.X - g.U(12), p.Y - g.U(12), g.U(24), g.U(24)));
        }
        // markers
        foreach (var m in _markers)
        {
            var p = Proj(c, cx, cy, rad, m.lat, m.lon, out bool vis); if (!vis) continue;
            double near = _scanning ? Math.Abs(((p.X - c.X) / c.Width) - _scan) : 1;
            bool lit = !_scanning && _scan >= 1 || near < 0.12 || _scan > 0 && ((p.X - c.X) / c.Width) < _scan;
            g.Fill(new Rect(p.X - g.U(3), p.Y - g.U(3), g.U(6), g.U(6)), lit ? g.P.Warning : g.P.Text, lit ? 0.9 : 0.35);
            if (lit && (g.Quality != Core.Configuration.GraphicsProfile.Low)) g.Text(m.id, p.X + g.U(6), p.Y - g.U(5), 7.5, g.P.Warning, Align.Left, false, 0.8);
        }
        // scanning sweep
        if (_scanning)
        {
            double x = c.X + c.Width * _scan;
            g.Fill(new Rect(x - g.U(30), c.Y, g.U(30), c.Height), g.P.Accent, 0.10);
            g.GlowLine(x, c.Y, x, c.Bottom, g.P.Accent, 1.6);
        }
        else if (!_regional) g.Arc(cx, cy, rad + g.U(10), g.Time * 40, 50, g.P.Accent, 1.4, 0.6);
        if (!_regional) g.HoloShimmer(new Rect(cx - rad * 1.02, cy - rad * 1.02, rad * 2.04, rad * 2.04), null, true);
        g.Text("SIMULATED ORBITS — NOT REAL-WORLD TRACKING", c.X + g.U(4), c.Bottom - g.U(12), 8, g.P.Warning, Align.Left, false, 0.7);
        g.Text(_regional ? $"CENTRE {_cLat:0.0}N {_cLon:0.0}E  ZOOM {_mapZoom:0.0}X" : $"ROT {_rotLon % 360:0}°  TILT {_tilt:0}°", c.Right - g.U(4), c.Bottom - g.U(12), 8, g.P.Text, Align.Right, false, 0.7);
    }

    void DrawPanel(Gfx g, int i, Rect r)
    {
        var sats = C.Sim.Satellites;
        switch (i)
        {
            case 0:
                {
                    var c = g.Panel(r, "SATELLITE CATALOGUE");
                    double lh = Math.Min(g.U(g.Touch ? 24 : 17), c.Height / sats.Count);
                    for (int k = 0; k < sats.Count; k++)
                    {
                        var rc = new Rect(c.X, c.Y + k * lh, c.Width, lh);
                        if (k == _sel) g.Fill(rc, g.P.Primary, 0.16);
                        g.StatusDot(rc.X + g.U(5), rc.Center.Y, sats[k].Signal > 0.35, 2.5);
                        g.Text(sats[k].Name, rc.X + g.U(14), rc.Y + (lh - g.U(9)) / 2, 8.5, g.P.Text, Align.Left, k == _sel);
                        g.Text($"{sats[k].Lat:+00;-00}/{sats[k].Lon:+000;-000}", rc.Right - g.U(2), rc.Y + (lh - g.U(9)) / 2, 8, g.P.Primary, Align.Right);
                        g.AddHit("sat:" + k, rc);
                    }
                    break;
                }
            case 1:
                {
                    var s = sats[_sel]; var c = g.Panel(r, "ORBITAL DATA // " + s.Name);
                    double lh = g.U(14);
                    string[] k = { "ALTITUDE", "INCLINATION", "PERIOD", "LATITUDE", "LONGITUDE", "VELOCITY", "SIGNAL" };
                    string[] v = { $"{s.AltitudeKm:0} KM", $"{s.Inclination * 57.2958:0.0}°", $"{s.Period * 1.0:0} MIN (SIM)", $"{s.Lat:0.00}°", $"{s.Lon:0.00}°", $"{7.9 - s.AltitudeKm / 2200.0:0.00} KM/S", $"{s.Signal * 100:0}%" };
                    for (int n = 0; n < Math.Min(7, (int)(c.Height / lh)); n++) g.KeyVal(c.X, c.Y + n * lh, c.Width, k[n], v[n], null, 9);
                    break;
                }
            case 2:
                {
                    var c = g.Panel(r, "TELEMETRY");
                    var rows = Layout.Rows(c, g.U(4), 1, 1);
                    g.Graph(rows[0], C.Sim.Signal, g.P.Primary, 0, 1, true, "DOWNLINK");
                    g.Graph(rows[1], C.Sim.NetIn, g.P.Accent, null, null, true, "DATA RATE", " MB/S");
                    break;
                }
            case 3:
                {
                    var c = g.Panel(r, "TRACKING MARKERS");
                    double lh = Math.Min(g.U(14), c.Height / _markers.Length);
                    for (int n = 0; n < _markers.Length; n++)
                    {
                        var m = _markers[n]; var rc = new Rect(c.X, c.Y + n * lh, c.Width, lh);
                        g.Text(m.id, rc.X, rc.Y + g.U(1), 8.5, g.P.Warning, Align.Left, true);
                        g.Text($"{m.lat:0.0}, {m.lon:0.0}", rc.Right, rc.Y + g.U(1), 8.5, g.P.Text, Align.Right);
                        g.AddHit("mk:" + n, rc);
                    }
                    break;
                }
        }
    }

    public override void Tap(Point p, string? id)
    {
        if (id == null) return;
        if (id.StartsWith("sat:")) { _sel = int.Parse(id[4..]); var s = C.Sim.Satellites[_sel]; if (_regional) { _cLon = s.Lon; _cLat = Math.Clamp(s.Lat, -60, 60); } C.Toast($"{s.Name} SELECTED"); C.Audio.Play("confirm"); }
        else if (id.StartsWith("mk:")) { var m = _markers[int.Parse(id[3..])]; _cLon = m.lon; _cLat = m.lat; if (!_regional) _rotLon = m.lon; C.Toast($"{m.id} @ {m.lat:0.0}, {m.lon:0.0}"); }
    }

    public override bool Drag(Point p, Vector d, string? id)
    {
        if (id != "map" && (id == null || !id.StartsWith("sat:"))) return false;
        _dragging = true;
        if (_regional) { _cLon -= d.X / (_mapZoom * 2.5); _cLat = Clamp(_cLat + d.Y / (_mapZoom * 2.5), -80, 80); }
        else { _rotLon -= d.X * 0.4; _tilt = Clamp(_tilt + d.Y * 0.3, -60, 60); }
        return true;
    }

    public override void Wheel(Point p, double delta)
    {
        if (_regional) _mapZoom = Clamp(_mapZoom * (1 + delta * 0.12), 1, 10);
        else _zoom = Clamp(_zoom * (1 + delta * 0.12), 0.6, 2.2);
    }
}
