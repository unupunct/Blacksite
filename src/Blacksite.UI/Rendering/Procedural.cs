using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Blacksite.Core.Themes;

namespace Blacksite.UI.Rendering;

/// <summary>Seamless 3D value noise (sampled on sphere points so textures wrap without seams).</summary>
public static class Noise
{
    static float H(int x, int y, int z)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + z * 1274126177;
            h = (h ^ (h >> 13)) * 1274126177; h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777215f;
        }
    }

    static float S(float t) => t * t * (3 - 2 * t);
    static float L(float a, float b, float t) => a + (b - a) * t;

    public static float N3(float x, float y, float z)
    {
        int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y), zi = (int)MathF.Floor(z);
        float xf = S(x - xi), yf = S(y - yi), zf = S(z - zi);
        float c00 = L(H(xi, yi, zi), H(xi + 1, yi, zi), xf), c10 = L(H(xi, yi + 1, zi), H(xi + 1, yi + 1, zi), xf);
        float c01 = L(H(xi, yi, zi + 1), H(xi + 1, yi, zi + 1), xf), c11 = L(H(xi, yi + 1, zi + 1), H(xi + 1, yi + 1, zi + 1), xf);
        return L(L(c00, c10, yf), L(c01, c11, yf), zf);
    }

    public static float Fbm(float x, float y, float z, int oct = 5)
    {
        float sum = 0, amp = 0.5f, norm = 0;
        for (int i = 0; i < oct; i++) { sum += amp * N3(x, y, z); norm += amp; x *= 2.03f; y *= 2.03f; z *= 2.03f; amp *= 0.5f; }
        return sum / norm;
    }
}

public enum PlanetKind { Earth, Rocky, Gas, Ice }

/// <summary>Baked equirectangular surface (albedo, clouds, city lights) plus a software sphere rasteriser.</summary>
public sealed class PlanetSurface
{
    public readonly int TW, TH;
    public volatile bool Ready;
    public readonly PlanetKind Kind;
    uint[] _albedo = null!;           // 0xAARRGGBB, A = ocean specular mask for Earth
    byte[] _clouds = null!;
    byte[] _lights = null!;
    readonly uint _baseCol, _bandCol;
    readonly int _seed;
    public uint[] Albedo => _albedo;

    WriteableBitmap? _bmp; int _size;
    int[] _px = Array.Empty<int>();
    double _lastRot = double.NaN, _lastTilt = double.NaN, _lastTime = -9; int _lastSize;

    public PlanetSurface(PlanetKind kind, uint baseCol, uint bandCol, int seed, bool hi = false)
    {
        TW = hi ? 1024 : 512; TH = TW / 2;
        Kind = kind; _baseCol = baseCol; _bandCol = bandCol; _seed = seed;
        Task.Run(Bake);
    }

    (float x, float y, float z) P(int tx, int ty)
    {
        double lon = (tx + 0.5) / TW * Math.PI * 2 - Math.PI, lat = Math.PI / 2 - (ty + 0.5) / TH * Math.PI;
        return ((float)(Math.Cos(lat) * Math.Cos(lon)), (float)Math.Sin(lat), (float)(Math.Cos(lat) * Math.Sin(lon)));
    }

    static float Bl(uint a, uint b, uint c, uint d, int sh, float wx, float wy)
    {
        float p = ((a >> sh) & 0xFF) * (1 - wx) + ((b >> sh) & 0xFF) * wx, q = ((c >> sh) & 0xFF) * (1 - wx) + ((d >> sh) & 0xFF) * wx;
        return p * (1 - wy) + q * wy;
    }

    static uint Mix(uint a, uint b, float t)
    {
        t = Math.Clamp(t, 0, 1);
        uint C(int sh) => (uint)Math.Clamp((int)(((a >> sh) & 0xFF) * (1 - t) + ((b >> sh) & 0xFF) * t), 0, 255);
        return 0xFF000000 | (C(16) << 16) | (C(8) << 8) | C(0);
    }

    static uint Rgb(int r, int g, int b) => 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | (uint)b;

    void Bake()
    {
        _albedo = new uint[TW * TH]; _clouds = new byte[TW * TH]; _lights = new byte[TW * TH];
        float sd = _seed * 7.31f;
        float[]? mask = Kind == PlanetKind.Earth ? EarthMask() : null;
        Parallel.For(0, TH, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) }, ty =>
        {
            double lat = 90 - (ty + 0.5) / TH * 180;
            for (int tx = 0; tx < TW; tx++)
            {
                var (x, y, z) = P(tx, ty);
                int i = ty * TW + tx;
                float h = Noise.Fbm(x * 3 + sd, y * 3, z * 3, 6);
                float cl = Noise.Fbm(x * 2.2f + 40 + sd, y * 4.5f, z * 2.2f, 5);
                _clouds[i] = (byte)(Math.Clamp((cl - 0.55f) * 5f, 0, 1) * 255);
                switch (Kind)
                {
                    case PlanetKind.Earth:
                        {
                            float m = mask![i] + (h - 0.5f) * 0.45f;
                            float moist = Noise.Fbm(x * 2.5f + 90, y * 2.5f, z * 2.5f, 4);
                            if (m < 0.5f)
                            {
                                float depth = Math.Clamp((0.5f - m) * 3.2f, 0, 1);
                                _albedo[i] = (Mix(Rgb(30, 100, 150), Rgb(6, 26, 66), depth) & 0x00FFFFFF) | 0xFF000000u; _albedo[i] |= 0;
                                _albedo[i] = (_albedo[i] & 0x00FFFFFF) | 0xFE000000u; // alpha 0xFE marks ocean (specular)
                            }
                            else
                            {
                                uint c;
                                float elev = Math.Clamp((h - 0.5f) * 3.2f * Math.Min(1f, (m - 0.5f) * 6f), 0, 1);
                                double al = Math.Abs(lat);
                                if (al > 66 + (h - 0.5f) * 14) c = Mix(Rgb(220, 230, 240), Rgb(245, 248, 252), h);
                                else if (moist < 0.42f && al < 38) c = Mix(Rgb(196, 168, 112), Rgb(160, 126, 80), h);
                                else c = Mix(Rgb(34, 84, 40), Rgb(96, 124, 62), moist);
                                if (elev > 0.35f) c = Mix(c, Rgb(120, 112, 100), (elev - 0.35f) * 2.2f);
                                if (elev > 0.8f) c = Mix(c, Rgb(240, 244, 248), (elev - 0.8f) * 5);
                                _albedo[i] = c;
                                float ci = Noise.Fbm(x * 9 + 7, y * 9, z * 9, 3);
                                if (al < 62 && ci > 0.62f && moist > 0.38f) _lights[i] = (byte)(Math.Clamp((ci - 0.62f) * 9, 0, 1) * 255);
                            }
                            break;
                        }
                    case PlanetKind.Gas:
                        {
                            float turb = Noise.Fbm(x * 4 + sd, y * 14, z * 4, 5);
                            float band = 0.5f + 0.5f * MathF.Sin((float)(lat * 0.28 / 1.0) + (turb - 0.5f) * 5.5f + MathF.Sin(y * 22 + sd) * 0.6f);
                            float fine = Noise.Fbm(x * 18, y * 60, z * 18, 3);
                            var c = Mix(_baseCol, _bandCol, band * 0.85f + (fine - 0.5f) * 0.35f);
                            if (Math.Abs(lat + 22) < 5 && Math.Abs(Math.Atan2(z, x) - 1.0) < 0.35) c = Mix(c, Rgb(190, 90, 60), 0.55f);   // storm
                            _albedo[i] = c; break;
                        }
                    case PlanetKind.Ice:
                        {
                            float crack = MathF.Abs(Noise.Fbm(x * 6 + sd, y * 6, z * 6, 4) - 0.5f);
                            var c = Mix(_baseCol, Rgb(240, 248, 255), h * 0.9f);
                            c = Mix(c, _bandCol, crack < 0.03f ? 0.7f : 0f);
                            _albedo[i] = c; break;
                        }
                    default:
                        {
                            float ridge = 1 - MathF.Abs(Noise.Fbm(x * 5 + sd, y * 5, z * 5, 5) * 2 - 1);
                            var c = Mix(Mix(_bandCol, _baseCol, h), Rgb(250, 235, 210), ridge * ridge * 0.35f);
                            float crater = Noise.N3(x * 14 + sd, y * 14, z * 14);
                            if (crater > 0.84f) c = Mix(c, Rgb(40, 30, 28), 0.35f);
                            _albedo[i] = c; break;
                        }
                }
            }
        });
        Ready = true;
    }

    /// <summary>Rasterises the bundled continent outlines into a soft land mask.</summary>
    float[] EarthMask()
    {
        var m = new byte[TW * TH];
        foreach (var poly in World.Continents)
        {
            int n = poly.Length / 2;
            for (int ty = 0; ty < TH; ty++)
            {
                double lat = 90 - (ty + 0.5) / TH * 180;
                var xs = new List<double>();
                for (int a = 0; a < n; a++)
                {
                    int b = (a + 1) % n;
                    double y1 = poly[a * 2 + 1], y2 = poly[b * 2 + 1];
                    if ((y1 <= lat && y2 > lat) || (y2 <= lat && y1 > lat))
                        xs.Add(poly[a * 2] + (lat - y1) / (y2 - y1) * (poly[b * 2] - poly[a * 2]));
                }
                xs.Sort();
                for (int k = 0; k + 1 < xs.Count; k += 2)
                {
                    int x0 = (int)((xs[k] + 180) / 360 * TW), x1 = (int)((xs[k + 1] + 180) / 360 * TW);
                    for (int x = Math.Max(0, x0); x <= Math.Min(TW - 1, x1); x++) m[ty * TW + x] ^= 1;
                }
            }
        }
        var f = new float[TW * TH]; var t = new float[TW * TH];
        for (int i = 0; i < f.Length; i++) f[i] = m[i];
        for (int pass = 0; pass < 3; pass++)
        {
            for (int y = 0; y < TH; y++)
                for (int x = 0; x < TW; x++)
                {
                    float s = 0; int c = 0;
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                        { int yy = y + dy; if (yy < 0 || yy >= TH) continue; s += f[yy * TW + (x + dx + TW) % TW]; c++; }
                    t[y * TW + x] = s / c;
                }
            (f, t) = (t, f);
        }
        return f;
    }

    // ---- per (size, tilt) geometry cache: all trigonometry happens once, frames only do texture lookups
    int _gSize; double _gTilt = double.NaN;
    float[] _lonB = Array.Empty<float>(), _fv = Array.Empty<float>(), _nl = Array.Empty<float>(), _lit = Array.Empty<float>(), _spec = Array.Empty<float>(), _atmoK = Array.Empty<float>(), _edge = Array.Empty<float>();

    void BuildGeometry(int size, double tiltDeg)
    {
        _gSize = size; _gTilt = tiltDeg;
        int n = size * size;
        _lonB = new float[n]; _fv = new float[n]; _nl = new float[n]; _lit = new float[n]; _spec = new float[n]; _atmoK = new float[n]; _edge = new float[n];
        double tilt = tiltDeg * Math.PI / 180, ct = Math.Cos(tilt), st = Math.Sin(tilt), R = size / 2.0;
        double lx = -0.62, ly = 0.42, lz = 0.66, ln = Math.Sqrt(lx * lx + ly * ly + lz * lz); lx /= ln; ly /= ln; lz /= ln;
        bool earth = Kind == PlanetKind.Earth;
        for (int py = 0; py < size; py++)
        {
            double y = (R - (py + 0.5)) / R;
            for (int px = 0; px < size; px++)
            {
                int i = py * size + px;
                double x = ((px + 0.5) - R) / R, d2 = x * x + y * y;
                if (d2 >= 1.0) { _edge[i] = 0; continue; }
                double z = Math.Sqrt(1 - d2);
                double lat = Math.Asin(Math.Clamp(y * ct + z * st, -1, 1));
                _lonB[i] = (float)Math.Atan2(x, z * ct - y * st);
                _fv[i] = (float)((Math.PI / 2 - lat) / Math.PI * TH - 0.5);
                double nl = x * lx + y * ly + z * lz;
                _nl[i] = (float)Math.Max(0, nl);
                double lit = Math.Clamp((nl + 0.08) / 0.34, 0, 1); lit = lit * lit * (3 - 2 * lit);
                _lit[i] = (float)lit;
                double rz = 2 * nl * z - lz;
                _spec[i] = (float)(Math.Pow(Math.Max(0, rz), 38) * 0.75 * lit);
                _atmoK[i] = (float)(Math.Pow(1 - z, 3.2) * (0.25 + 0.95 * Math.Max(0.0, nl + 0.25)) * (earth ? 1.0 : 0.5));
                _edge[i] = (float)Math.Clamp((1 - Math.Sqrt(d2)) * R, 0, 1);
            }
        }
    }

    /// <summary>Renders the lit, rotated sphere into a cached bitmap (re-rasterised at most ~10 Hz).</summary>
    public WriteableBitmap? Render(double time, double rotLonDeg, double tiltDeg, int size, double cloudShift, bool quality)
    {
        if (!Ready) return null;
        size = Math.Clamp((size + 15) / 16 * 16, 64, quality ? 640 : 320);
        bool dirty = _bmp == null || size != _lastSize || Math.Abs(tiltDeg - _lastTilt) > 0.4 || time - _lastTime > (quality ? 1 / 30.0 : 1 / 14.0);
        if (!dirty) return _bmp;
        _lastRot = rotLonDeg; _lastTilt = tiltDeg; _lastTime = time; _lastSize = size;
        if (_bmp == null || _size != size)
        {
            _bmp = new WriteableBitmap(new PixelSize(size, size), new Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, AlphaFormat.Premul);
            _px = new int[size * size]; _size = size;
        }
        if (_gSize != size || Math.Abs(_gTilt - tiltDeg) > 0.4) BuildGeometry(size, tiltDeg);
        float rot = (float)(rotLonDeg * Math.PI / 180);
        bool earth = Kind == PlanetKind.Earth;
        int cloudOff = (int)(cloudShift * TW / (Math.PI * 2));
        uint atmo = earth ? Rgb(110, 170, 255) : Kind == PlanetKind.Gas ? Mix(_baseCol, 0xFFFFFFFF, 0.4f) : Rgb(200, 190, 180);
        float ar = ((atmo >> 16) & 0xFF), ag = ((atmo >> 8) & 0xFF), ab = (atmo & 0xFF);
        const float InvTau = 1f / (MathF.PI * 2);
        int n = size * size;
        System.Threading.Tasks.Parallel.For(0, size, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) }, py =>
        {
            for (int pxl = 0; pxl < size; pxl++)
            {
                int i = py * size + pxl;
                float edge = _edge[i];
                if (edge <= 0) { _px[i] = 0; continue; }
                float u = (_lonB[i] + rot + MathF.PI) * InvTau; u -= MathF.Floor(u);
                float fu = u * TW - 0.5f, fv = _fv[i];
                int x0 = (int)MathF.Floor(fu), y0 = Math.Clamp((int)MathF.Floor(fv), 0, TH - 2);
                float wx = fu - x0, wy = Math.Clamp(fv - y0, 0f, 1f);
                int xa = x0 & (TW - 1), xb = (x0 + 1) & (TW - 1);
                uint a00 = _albedo[y0 * TW + xa], a10 = _albedo[y0 * TW + xb], a01 = _albedo[(y0 + 1) * TW + xa], a11 = _albedo[(y0 + 1) * TW + xb];
                float cr = Bl(a00, a10, a01, a11, 16, wx, wy), cg = Bl(a00, a10, a01, a11, 8, wx, wy), cb = Bl(a00, a10, a01, a11, 0, wx, wy);
                float lit = _lit[i];
                float light = 0.045f + 0.95f * (earth ? lit : _nl[i]);
                float r = cr * light, g = cg * light, b = cb * light;
                if (earth)
                {
                    int ty = Math.Clamp((int)(fv + 0.5f), 0, TH - 1), tx = (int)(u * TW) & (TW - 1);
                    if ((a00 >> 24) == 0xFE) { float sp = _spec[i]; r += sp * 255; g += sp * 255; b += sp * 235; }
                    float cl = _clouds[ty * TW + ((tx + cloudOff) & (TW - 1))] / 255f;
                    float cl_l = 0.06f + 0.96f * lit;
                    r = r * (1 - cl * 0.88f) + 250 * cl_l * cl * 0.88f; g = g * (1 - cl * 0.88f) + 252 * cl_l * cl * 0.88f; b = b * (1 - cl * 0.88f) + 255 * cl_l * cl * 0.88f;
                    float li = _lights[ty * TW + tx] / 255f * (1 - lit) * (1 - cl * 0.8f);
                    r += li * 255; g += li * 190; b += li * 90;
                }
                float ak = _atmoK[i];
                r += ar * ak; g += ag * ak; b += ab * ak;
                int ir = (int)Math.Clamp(r * edge, 0, 255), ig = (int)Math.Clamp(g * edge, 0, 255), ib = (int)Math.Clamp(b * edge, 0, 255);
                _px[i] = ((int)(255 * edge) << 24) | (ir << 16) | (ig << 8) | ib;
            }
        });
        using (var fb = _bmp.Lock()) Marshal.Copy(_px, 0, fb.Address, n);
        return _bmp;
    }
}
/// <summary>Cached procedural backdrops (nebula) and plasma sprites.</summary>
public static class Sprites
{
    static readonly Dictionary<(string, uint, uint, int), WriteableBitmap> Cache = new();

    static WriteableBitmap Make(int w, int h, Func<int, int, uint> px)
    {
        var bmp = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, AlphaFormat.Premul);
        var data = new int[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) data[y * w + x] = (int)px(x, y);
        using var fb = bmp.Lock(); Marshal.Copy(data, 0, fb.Address, data.Length);
        return bmp;
    }

    static uint Pre(float r, float g, float b, float a)
    {
        a = Math.Clamp(a, 0, 1);
        return ((uint)(a * 255) << 24) | ((uint)Math.Clamp(r * a, 0, 255) << 16) | ((uint)Math.Clamp(g * a, 0, 255) << 8) | (uint)Math.Clamp(b * a, 0, 255);
    }

    public static WriteableBitmap Nebula(uint colA, uint colB, int seed)
    {
        var key = ("neb", colA, colB, seed);
        if (Cache.TryGetValue(key, out var b)) return b;
        return Cache[key] = Make(320, 180, (x, y) =>
        {
            float u = x / 320f * 4 + seed, v = y / 180f * 2.25f;
            float wx = Noise.Fbm(u, v, 1.3f, 4), wy = Noise.Fbm(u + 5.2f, v + 1.3f, 2.1f, 4);
            float n = Noise.Fbm(u + wx * 1.8f, v + wy * 1.8f, 3.7f, 6);
            float dens = Math.Clamp((n - 0.34f) * 2.8f, 0, 1);
            float mixv = Noise.Fbm(u * 0.7f + 11, v * 0.7f, 9, 3);
            float r = ((colA >> 16) & 0xFF) * (1 - mixv) + ((colB >> 16) & 0xFF) * mixv;
            float g = ((colA >> 8) & 0xFF) * (1 - mixv) + ((colB >> 8) & 0xFF) * mixv;
            float bl = (colA & 0xFF) * (1 - mixv) + (colB & 0xFF) * mixv;
            float core = Math.Clamp((n - 0.62f) * 3, 0, 1);
            r += core * 120; g += core * 120; bl += core * 120;
            return Pre(r, g, bl, dens * 0.95f);
        });
    }

    static uint Px(int r, int g, int b, float a = 1) => ((uint)(Math.Clamp(a, 0, 1) * 255) << 24) | ((uint)Math.Clamp((int)(r * a), 0, 255) << 16) | ((uint)Math.Clamp((int)(g * a), 0, 255) << 8) | (uint)Math.Clamp((int)(b * a), 0, 255);

    /// <summary>Terrain relief map (mode 0: coloured hillshade), satellite imagery (mode 1: grayscale with fields/roads) or topographic night view (mode 2).</summary>
    public static WriteableBitmap Terrain(int seed, int mode)
    {
        var key = ("ter", (uint)mode, 0u, seed);
        if (Cache.TryGetValue(key, out var b)) return b;
        const int W = 320, H = 200;
        float Hf(float x, float y) => Noise.Fbm(x * 0.012f + seed * 3.1f, y * 0.012f, seed * 1.7f, 6);
        return Cache[key] = Make(W, H, (x, y) =>
        {
            float h = Hf(x, y), dx = Hf(x + 2, y) - Hf(x - 2, y), dy = Hf(x, y + 2) - Hf(x, y - 2);
            float shade = Math.Clamp(0.62f + (-dx * 0.7f - dy * 0.7f) * 9f, 0.15f, 1.3f);
            if (mode == 1)
            {   // satellite imagery: fields, roads, buildings
                int cx = x / 14, cy = y / 14; uint hh = (uint)(cx * 73856093 ^ cy * 19349663 ^ seed);
                float tone = 0.28f + 0.5f * h + ((hh % 100) / 100f - 0.5f) * 0.22f;
                bool road = Math.Abs((x % 58) - 29) < 1 || Math.Abs((y % 46) - 23) < 1 || Math.Abs(((x + y) % 97) - 48) < 1;
                if (road) tone = 0.72f; if (((hh >> 8) % 11 == 0) && !road) tone += 0.25f;
                float v = Math.Clamp(tone * shade, 0, 1) * 215; return Px((int)v, (int)(v * 1.02), (int)(v * 0.96));
            }
            if (mode == 2)
            {   // topographic: contour lines on dark ground
                float c = (h * 22) % 1; bool line = c < 0.07f; float v = line ? 0.85f : 0.10f + h * 0.18f;
                return Px((int)(v * 60), (int)(v * 230), (int)(v * 190));
            }
            if (h < 0.46f) { float d = (0.46f - h) * 4; return Px((int)(10 + 8 * (1 - d)), (int)(38 + 30 * (1 - d)), (int)(70 + 50 * (1 - d)), 1); }
            float e = (h - 0.46f) * 3.2f; int r, gg, bl;
            if (e < 0.25f) { r = 52; gg = 94; bl = 44; } else if (e < 0.55f) { r = 92; gg = 112; bl = 58; } else if (e < 0.8f) { r = 124; gg = 98; bl = 70; } else { r = 214; gg = 214; bl = 210; }
            return Px((int)(r * shade), (int)(gg * shade), (int)(bl * shade));
        });
    }

    /// <summary>Weather radar reflectivity over transparent ground (green / yellow / orange / red cells).</summary>
    public static WriteableBitmap Weather(int seed)
    {
        var key = ("wx", 0u, 0u, seed);
        if (Cache.TryGetValue(key, out var b)) return b;
        return Cache[key] = Make(256, 160, (x, y) =>
        {
            float n = Noise.Fbm(x * 0.022f + seed * 5, y * 0.03f, seed, 5), m = Noise.Fbm(x * 0.07f, y * 0.08f + 9, seed * 2, 3);
            float v = n * 0.85f + m * 0.25f - 0.02f;
            if (v < 0.5f) return 0;
            if (v < 0.58f) return Px(40, 190, 70, 0.85f); if (v < 0.66f) return Px(250, 230, 40, 0.9f); if (v < 0.74f) return Px(255, 150, 20, 0.92f); return Px(240, 30, 30, 0.95f);
        });
    }

    public static WriteableBitmap Plasma(uint col, int seed)
    {
        var key = ("pla", col, 0u, seed);
        if (Cache.TryGetValue(key, out var b)) return b;
        const int S = 256;
        return Cache[key] = Make(S, S, (x, y) =>
        {
            float dx = (x - S / 2f) / (S / 2f), dy = (y - S / 2f) / (S / 2f);
            float r = MathF.Sqrt(dx * dx + dy * dy);
            if (r >= 1) return 0;
            float n = Noise.Fbm(dx * 3.2f + seed, dy * 3.2f, seed * 0.5f, 6);
            float swirl = Noise.Fbm(dx * 6 + 8, dy * 6, 4, 4);
            float fall = MathF.Pow(1 - r, 1.15f);
            float v = Math.Clamp(((n * 1.1f + swirl * 0.5f) * fall - 0.12f) * 2.2f, 0, 1);
            float rr = ((col >> 16) & 0xFF), gg = ((col >> 8) & 0xFF), bb = (col & 0xFF);
            float w = v * v * v;
            return Pre(rr + (255 - rr) * w, gg + (255 - gg) * w, bb + (255 - bb) * w, Math.Clamp(v * 1.4f * fall + 0.05f * (1 - r), 0, 1));
        });
    }
}
