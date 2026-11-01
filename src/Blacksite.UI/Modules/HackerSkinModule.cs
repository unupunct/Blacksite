using Avalonia;

namespace Blacksite.UI.Modules;

/// <summary>PRESET 02 – HACKER TERMINAL on the supplied artwork (green / blue / red consoles). Coordinates are in the picture's own pixels.</summary>
public sealed class HackerSkinModule : SkinModule
{
    protected override string Resource => "hacker.png";

    readonly Band[] _bands =
    {
        new Band { Name = "ORBITAL SATELLITE", Src = new Rect(0, 0, 1536, 350), Fg = 0xFF4CFF7A, Accent = 0xFFD4FFDC, Dim = 0xFF2FBF58, Prompt = "root@blacksite:~#",
            Term = new Rect(524, 66, 490, 196), FontPx = 11.2, LinePx = 13.45, Compute = new Rect(621, 283, 180, 46), PicText = true, GrantedAt = new Rect(828, 291, 136, 20),
            Fx = { Fx.Orbit(183, 107, 92, 24, 0xFF9CFFB8, -0.35, 3), Fx.Scan(new Rect(98, 62, 176, 92), 0xFF50FF80), Fx.Node(345, 118, 0xFF9CFFB8), Fx.Node(402, 92, 0xFF9CFFB8), Fx.Node(445, 128, 0xFF9CFFB8), Fx.Node(382, 150, 0xFF9CFFB8), Fx.Node(1414, 263, 0xFFFF5040, 5),
                Fx.Eq(new Rect(1184, 139, 86, 17), 0xFF3CFF6A, 4), Fx.Radar(1100, 122, 52, 0xFF50FF80, 1, 7, 0.8) },
            Waves = { new Wave { R = new Rect(103, 235, 166, 66), Col = 0xFF3CFF6A, Seed = 1 }, new Wave { R = new Rect(1057, 230, 212, 68), Col = 0xFF3CFF6A, Seed = 2, Amp = 0.9 } } },
        new Band { Name = "GLOBAL DATABASE", Src = new Rect(0, 352, 1536, 328), Fg = 0xFFC4E6FF, Accent = 0xFF9AE4FF, Dim = 0xFF5AA8E8, Prompt = "root@blacksite:~#",
            Term = new Rect(532, 416, 484, 172), FontPx = 11, LinePx = 13.65, Compute = new Rect(668, 598, 200, 50), GrantedAt = new Rect(884, 608, 128, 26),
            Subs = new[] { new Rect(668, 644, 58, 14), new Rect(742, 644, 58, 14), new Rect(818, 644, 58, 14) }, SubNames = new[] { "DECRYPT", "EXTRACT", "ACCESS" }, Clock = new Rect(1362, 364, 54, 14),
            Fx = { Fx.Node(50, 430, 0xFFFF5040, 5), Fx.Node(118, 468, 0xFFFF5040, 5), Fx.Node(160, 440, 0xFFFF5040, 5), Fx.Node(70, 486, 0xFFFF5040, 5), Fx.Node(30, 452, 0xFFFF5040, 4),
                Fx.Scan(new Rect(286, 400, 230, 100), 0xFF50C8FF, false), Fx.Radar(1318, 592, 50, 0xFF50C8FF, 1, 5), Fx.Scan(new Rect(1286, 414, 190, 104), 0xFF50C8FF, true, 0.7) } },
        new Band { Name = "SAT-7", Src = new Rect(0, 684, 1536, 340), Fg = 0xFFEAD0CC, Accent = 0xFFFF9A8A, Dim = 0xFFB07068, Prompt = "root@sat-7:~#", Timestamped = true,
            Term = new Rect(530, 742, 480, 150), FontPx = 11, LinePx = 14.2, Compute = new Rect(652, 914, 236, 54), GrantedAt = new Rect(896, 928, 116, 24),
            Fx = { Fx.Radar(86, 792, 54, 0xFFFF5A4A, 1, 7), Fx.Orbit(86, 792, 62, 18, 0xFFFFA090, 0.5, 2), Fx.Node(320, 918, 0xFFFF4040, 8), Fx.Node(50, 900, 0xFFFF5A4A, 5), Fx.Node(110, 925, 0xFFFF5A4A, 5), Fx.Node(150, 890, 0xFFFF5A4A, 5), Fx.Node(215, 950, 0xFFFF5A4A, 5), Fx.Node(190, 905, 0xFFFF5A4A, 5),
                Fx.Eq(new Rect(1052, 745, 206, 50), 0xFFFF4040, 8), Fx.Radar(1355, 905, 48, 0xFFFF5A4A, 1, 6, 0.7) },
            Waves = { new Wave { R = new Rect(283, 752, 214, 56), Col = 0xFFFF4444, Seed = 3 } } },
    };

    protected override Band[] Bands => _bands;

    static string Hex(double t, int salt)
    {
        var sb = new System.Text.StringBuilder(); uint h = (uint)(t * 2.2) * 2654435761u + (uint)salt * 40503u;
        for (int i = 0; i < 8; i++) { h = h * 1664525u + 1013904223u; sb.Append(((h >> 24) & 0xFF).ToString("X2")).Append(i == 3 ? "  " : " "); }
        return sb.ToString().TrimEnd();
    }

    protected override string[] Live(Band b, int idx, double p, double t, bool granted)
    {
        int pct = (int)Math.Round(p * 100);
        switch (idx)
        {
            case 0:
                {
                    int rem = (int)((1 - p) * 180);
                    var l = new List<string> { "root@blacksite:~# ./decrypt.sh --target=203.0.113.47", "[*] Initializing attack vector...", "[*] Target identified: ORBITAL SATELLITE", "[*] Establishing connection...", "[*] Connection established.", "[*] Loading encryption modules...", "[*] Starting brute force attack...",
                        $"[*] Progress: {pct}%  |  Speed: {12.0 + 0.8 * Math.Sin(t * 1.3):0.0}M keys/sec", granted ? "[+] ACCESS GRANTED — session key recovered" : $"[*] Remaining time:  00:0{rem / 60}:{rem % 60:00}", "", Hex(t, 1), Hex(t + 3, 2), "", "root@blacksite:~# " };
                    return l.ToArray();
                }
            case 1:
                {
                    var l = new List<string> { "[*] Scanning target systems...", "[*] Found 1 open port (22/tcp)", "[*] Connecting to 198.51.100.23...", "[*] Authentication required...", "[*] Attempting credential brute force...", "[*] User: admin  |  Password: *********", "[*] Access level: 1", "[*] Enumerating database structure...", "[*] Found 42 tables, 1287 records",
                        granted ? "[+] Decrypting data blocks... COMPLETE" : "[*] Decrypting data blocks...", $"[*] Progress: {pct}%", "[#BAR]" };
                    return l.ToArray();
                }
            default:
                {
                    var now = DateTime.Now; string S(int k) => $"[{now.AddSeconds(k - 8):HH:mm:ss}]";
                    var l = new List<string> { $"{S(0)} Establishing connection to SAT-7...", $"{S(3)} Connection established.", $"{S(4)} Starting authentication bypass...", $"{S(9)} Bypassing encryption (AES-256)...", $"{S(12)} Extracting data blocks...",
                        $"{S(18)} Decryption progress: {Math.Min(pct, 42)}%", $"{S(22)} Decryption progress: {Math.Min(pct, 67)}%", $"{S(26)} Decryption progress: {Math.Min(pct, 91)}%", granted ? $"{S(30)} Access granted. Preparing data transfer..." : $"{S(30)} Awaiting COMPUTE...", "", "root@sat-7:~#" };
                    return l.ToArray();
                }
        }
    }
}
