using Avalonia;

namespace Blacksite.UI.Rendering;

/// <summary>Hand-simplified continent outlines (lon,lat degrees) bundled as an offline map. Not survey-accurate.</summary>
public static class World
{
    public static readonly double[][] Continents =
    {
        // North America
        new double[]{-168,66,-156,71,-140,70,-125,70,-110,73,-95,72,-82,69,-70,63,-62,58,-56,52,-66,45,-70,42,-76,35,-81,30,-80,25,-84,29,-90,29,-97,26,-97,21,-105,20,-110,23,-115,30,-110,24,-106,19,-96,16,-92,15,-86,12,-82,8,-78,8,-85,12,-88,16,-95,19,-100,20,-105,25,-112,30,-118,34,-124,40,-124,48,-130,54,-140,59,-150,60,-160,58,-165,62},
        // Greenland
        new double[]{-73,78,-60,82,-35,83,-20,80,-20,70,-30,66,-43,60,-50,64,-56,70,-68,76},
        // South America
        new double[]{-78,8,-72,12,-62,10,-52,5,-50,0,-35,-5,-38,-14,-41,-22,-48,-26,-53,-34,-58,-38,-65,-42,-68,-52,-72,-54,-75,-48,-74,-38,-71,-30,-70,-18,-76,-14,-81,-6,-80,0,-77,4},
        // Europe
        new double[]{-10,36,-9,43,-2,44,-4,48,2,51,8,54,10,57,6,58,5,62,12,66,20,70,28,71,32,67,30,60,28,56,30,50,38,47,36,44,28,41,24,38,14,41,10,43,3,37,-5,36},
        // Africa
        new double[]{-17,21,-13,28,-9,32,-6,36,10,37,11,33,20,32,32,31,34,28,38,20,43,12,51,11,48,3,40,-4,39,-12,35,-24,32,-29,26,-34,20,-35,17,-30,13,-17,12,-13,13,-4,9,-1,9,4,5,6,-4,5,-8,4,-13,8,-17,14},
        // Asia
        new double[]{30,50,40,56,60,68,70,73,90,77,110,77,140,73,170,70,180,66,170,60,160,55,142,52,135,45,130,42,125,38,122,31,121,24,110,20,108,12,105,9,100,13,98,8,100,2,104,1,100,8,94,16,90,22,87,21,80,15,78,8,73,16,72,22,67,24,57,26,52,28,48,30,44,36,36,36,36,31,34,29,30,36,28,41,34,45,38,47},
        // Australia
        new double[]{114,-22,122,-18,130,-12,137,-12,142,-11,146,-19,153,-26,150,-37,141,-38,135,-35,130,-32,124,-34,115,-34},
        // Antarctica band
        new double[]{-180,-72,-120,-76,-60,-72,0,-70,60,-67,120,-66,180,-72,180,-85,-180,-85},
        // Madagascar / islands
        new double[]{44,-13,50,-15,48,-25,43,-22},
        new double[]{130,31,140,38,145,43,141,45,136,36},
        new double[]{-6,50,2,52,0,58,-6,58},
    };

    /// <summary>Equirectangular projection into rect with pan/zoom (centre lon/lat, zoom 1 = whole world).</summary>
    public static Point Project(Rect r, double lon, double lat, double cLon, double cLat, double zoom)
    {
        double sx = r.Width / 360.0 * zoom, sy = r.Height / 180.0 * zoom;
        return new Point(r.Center.X + (lon - cLon) * sx, r.Center.Y - (lat - cLat) * sy);
    }

    /// <summary>Orthographic projection on a sphere. Returns visibility via z.</summary>
    public static (Point p, double z) Ortho(double cx, double cy, double rad, double lon, double lat, double rotLon, double tiltLat)
    {
        double lo = (lon - rotLon) * Math.PI / 180, la = lat * Math.PI / 180, t = tiltLat * Math.PI / 180;
        double x = Math.Cos(la) * Math.Sin(lo);
        double y = Math.Sin(la) * Math.Cos(t) - Math.Cos(la) * Math.Cos(lo) * Math.Sin(t);
        double z = Math.Sin(la) * Math.Sin(t) + Math.Cos(la) * Math.Cos(lo) * Math.Cos(t);
        return (new Point(cx + x * rad, cy - y * rad), z);
    }
}
