using Avalonia;

namespace Blacksite.UI.Modules;

/// <summary>PRESET 01 – SPACE COMMAND on the supplied artwork (planetary map / holographic solar system / deep space scan).</summary>
public sealed class SpaceSkinModule : SkinModule
{
    protected override string Resource => "space.png";

    readonly Band[] _bands =
    {
        new Band { Name = "PLANETARY NAVIGATION", Src = new Rect(0, 0, 1536, 358), Fg = 0xFFB8E8FF, Accent = 0xFF5ACBFF, Dim = 0xFF4A90C8, FontPx = 9.5,
            Clock = new Rect(1446, 14, 66, 14), ClockPx = 8.6,
            Fx = { Fx.Orbit(776, 176, 185, 62, 0xFFFFE0A0, 0, 2, 1.1), Fx.Orbit(776, 176, 270, 92, 0xFF9CD8FF, -0.08, 2, 0.8), Fx.Orbit(776, 176, 340, 120, 0xFFFF9C60, 0.06, 2, 0.6), Fx.Radar(492, 316, 30, 0xFF5ACBFF, 1, 4),
                Fx.Orbit(1363, 300, 44, 30, 0xFFFF5A4A, 0.4, 2, 1.4), Fx.Node(676, 108, 0xFFFFFFFF, 5), Fx.Node(995, 142, 0xFFFFFFFF, 5), Fx.Node(1150, 118, 0xFF5ACBFF, 5), Fx.Radar(1480, 215, 28, 0xFF5ACBFF, 1, 3, 0.7) },
            Compute = new Rect(306, 187, 138, 20), GrantedAt = new Rect(306, 187, 138, 20), PicText = true,
            Subs = new[] { new Rect(1110, 198, 178, 30) }, SubNames = new[] { "SET TARGET" },
            Waves = { new Wave { R = new Rect(1322, 214, 100, 30), Col = 0xFF30D0FF, Seed = 21 }, new Wave { R = new Rect(224, 150, 150, 28), Col = 0xFF30D0FF, Seed = 22, Amp = 0.8 } } },
        new Band { Name = "HOLOGRAPHIC SOLAR SYSTEM", Src = new Rect(0, 362, 1536, 356), Fg = 0xFFB8E8FF, Accent = 0xFF5ACBFF, Dim = 0xFF4A90C8,
            Fx = { Fx.Orbit(672, 527, 150, 62, 0xFFFFE0A0, 0, 2, 1.2), Fx.Orbit(672, 527, 240, 100, 0xFF9CD8FF, -0.05, 3, 0.9), Fx.Orbit(672, 527, 330, 138, 0xFFFF9C60, 0.05, 3, 0.65), Fx.Orbit(672, 527, 400, 170, 0xFFB090FF, -0.08, 2, 0.45),
                Fx.Orbit(1470, 470, 22, 14, 0xFFFF5A4A, 0.5, 1, 2), Fx.Scan(new Rect(350, 410, 700, 290), 0xFF5ACBFF, false, 0.5), Fx.Radar(1235, 546, 36, 0xFF5ACBFF, 0.7, 5) },
            Subs = new[] { new Rect(1314, 544, 90, 24), new Rect(1414, 544, 94, 24), new Rect(1314, 574, 90, 24), new Rect(1414, 574, 94, 24) }, SubNames = new[] { "CENTER", "TRACK", "TARGET", "TRAJECTORY" } },
        new Band { Name = "DEEP SPACE SCAN", Src = new Rect(0, 722, 1536, 302), Fg = 0xFFB8E8FF, Accent = 0xFF5ACBFF, Dim = 0xFF4A90C8, FontPx = 9.5,
            Clock = new Rect(1388, 995, 52, 12), ClockPx = 8.2,
            Fx = { Fx.Radar(738, 838, 150, 0xFF5ACBFF, 0.8, 8, 0.5), Fx.Radar(378, 917, 34, 0xFF5ACBFF, 1, 5), Fx.Orbit(110, 833, 60, 22, 0xFFFFFFFF, -0.3, 2), Fx.Scan(new Rect(474, 768, 484, 176), 0xFF5ACBFF, true, 0.5), Fx.Radar(1488, 874, 24, 0xFF5ACBFF, 1, 3) },
            Compute = new Rect(205, 883, 54, 12), GrantedAt = new Rect(205, 883, 54, 12), PicText = true,
            Subs = new[] { new Rect(334, 783, 116, 16), new Rect(334, 806, 116, 16), new Rect(334, 828, 116, 16), new Rect(334, 851, 116, 16) }, SubNames = new[] { "WIDE", "NARROW", "DEEP", "TARGET" },
            Waves = { new Wave { R = new Rect(24, 926, 168, 38), Col = 0xFF30D0FF, Seed = 23 }, new Wave { R = new Rect(1394, 795, 112, 40), Col = 0xFF30D0FF, Seed = 24 }, new Wave { R = new Rect(1255, 862, 112, 30), Col = 0xFF30D0FF, Seed = 25 } } },
    };

    protected override Band[] Bands => _bands;
    protected override string[] Live(Band b, int idx, double p, double t, bool granted) => Array.Empty<string>();
}
