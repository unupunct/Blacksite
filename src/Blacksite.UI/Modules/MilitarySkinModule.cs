using Avalonia;

namespace Blacksite.UI.Modules;

/// <summary>PRESET 10 – TACTICAL on the supplied artwork (Phantom / Shadow / Titan commands). Picture-pixel coordinates.</summary>
public sealed class MilitarySkinModule : SkinModule
{
    protected override string Resource => "military.png";

    readonly Band[] _bands =
    {
        new Band { Name = "PHANTOM COMMAND", Src = new Rect(0, 0, 1672, 324), Fg = 0xFF6AFFB0, Accent = 0xFF9AFFD0, Dim = 0xFF3CBF80, FontPx = 9,
            Clock = new Rect(1552, 15, 104, 13), ClockFmt = "yyyy-MM-dd HH:mm:ss", ClockPx = 8.2,
            Fx = { Fx.Orbit(640, 170, 190, 70, 0xFF60E0FF, -0.4, 3), Fx.Orbit(640, 170, 150, 125, 0xFFFFFFFF, 0.9, 2, 0.8), Fx.Node(515, 150, 0xFFFF5040, 6), Fx.Node(790, 155, 0xFF60FF90, 6), Fx.Node(850, 185, 0xFF60E0FF, 6), Fx.Node(465, 105, 0xFF60E0FF, 6),
                Fx.Radar(832, 248, 32, 0xFFFF5040, 1, 6), Fx.Node(1130, 112, 0xFF60FF90, 5), Fx.Node(1172, 80, 0xFF60FF90, 5), Fx.Node(1200, 140, 0xFF60FF90, 5), Fx.Node(1115, 150, 0xFF60FF90, 5), Fx.Scan(new Rect(1092, 62, 120, 120), 0xFF60FF90, true, 0.8),
                Fx.Radar(1225, 245, 30, 0xFF60FF90, 1, 4, 0.6) },
            Waves = { new Wave { R = new Rect(1394, 186, 118, 38), Col = 0xFF3CFF6A, Seed = 11 } } },
        new Band { Name = "SHADOW OPERATIONS", Src = new Rect(0, 328, 1672, 318), Fg = 0xFFB8F0D8, Accent = 0xFF54FFB0, Dim = 0xFF58B890, FontPx = 9.4, LinePx = 11.9,
            Term = new Rect(1444, 380, 198, 190), Compute = new Rect(1487, 585, 156, 28), PicText = true, GrantedAt = new Rect(1487, 585, 156, 28),
            Clock = new Rect(1597, 342, 54, 13), ClockPx = 8.2,
            Fx = { Fx.Node(788, 485, 0xFFFF4030, 12), Fx.Node(742, 398, 0xFFFF4030, 7), Fx.Node(690, 468, 0xFFFF4030, 7), Fx.Node(940, 440, 0xFFFF4030, 7), Fx.Node(912, 510, 0xFFFF4030, 7), Fx.Radar(788, 485, 130, 0xFFFF6040, 1, 8, 0.6),
                Fx.Scan(new Rect(980, 495, 214, 70), 0xFFFFFFFF, false, 0.8), Fx.Scan(new Rect(976, 392, 212, 76), 0xFFB0FFE0, true), Fx.Eq(new Rect(246, 498, 202, 42), 0xFF40FF70, 3), Fx.Radar(1013, 530, 22, 0xFFFFFFFF, 1, 3, 1.4), Fx.Radar(300, 600, 28, 0xFF40FF70, 1, 4) } },
        new Band { Name = "TITAN COMMAND", Src = new Rect(0, 650, 1672, 291), Fg = 0xFF9AD8FF, Accent = 0xFFFFB050, Dim = 0xFF5A98C8, FontPx = 9,
            Clock = new Rect(1597, 662, 54, 13), ClockPx = 8.2, Compute = new Rect(1498, 709, 150, 22), GrantedAt = new Rect(1498, 709, 150, 22), PicText = true,
            Subs = new[] { new Rect(1498, 734, 150, 22), new Rect(1498, 759, 150, 22), new Rect(1498, 784, 150, 22) }, SubNames = new[] { "UAV", "RADAR", "SUPPORT" },
            Fx = { Fx.Radar(343, 752, 92, 0xFF50C8FF, 0.6, 8), Fx.Orbit(660, 805, 108, 38, 0xFF60E0FF, -0.3, 3), Fx.Node(905, 745, 0xFF60E0FF, 5), Fx.Node(960, 772, 0xFF60E0FF, 5), Fx.Node(1030, 742, 0xFF60E0FF, 5), Fx.Node(1072, 790, 0xFF60E0FF, 5), Fx.Node(955, 712, 0xFF60E0FF, 5),
                Fx.Scan(new Rect(1134, 706, 244, 70), 0xFFB0E8FF, true), Fx.Radar(1433, 880, 26, 0xFFFFB050, 1, 4), Fx.Scan(new Rect(596, 735, 140, 130), 0xFF60E0FF, true, 0.6) },
            Waves = { new Wave { R = new Rect(240, 818, 196, 28), Col = 0xFF30D0FF, Seed = 12 } } },
    };

    protected override Band[] Bands => _bands;

    protected override string[] Live(Band b, int idx, double p, double t, bool granted)
    {
        if (idx != 1) return Array.Empty<string>();
        int pct = (int)Math.Round(p * 100);
        return new[] { "scan network 10.0.0.0/16", "  Scanning...", "  Host 10.0.12.34 - open", "  Host 10.0.12.67 - open", "  Host 10.0.15.89 - filtered", "  Host 10.0.18.21 - open", "  Scan complete. 3/254 hosts.", "",
            "> analyze image --sat", "  Processing satellite image...", "  Target detected: 2 objects", "  Confidence: 87%", "", "> decrypt --key 7f3d9c",
            granted ? "  Decryption complete. ACCESS GRANTED" : "  Attempting decryption...", $"  Decryption in progress: {pct}%" };
    }
}
