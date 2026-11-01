namespace Blacksite.Core.Themes;

/// <summary>Colour system for one preset. Colours are 0xAARRGGBB.</summary>
public sealed record Palette(
    string Name,
    uint Background,
    uint Panel,
    uint Primary,
    uint Secondary,
    uint Accent,
    uint Text,
    uint Warning)
{
    public static uint Rgb(byte r, byte g, byte b) => 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | b;

    public static uint WithAlpha(uint c, double a)
    {
        var al = (uint)Math.Clamp(a * 255.0, 0, 255);
        return (al << 24) | (c & 0x00FFFFFFu);
    }

    public static uint Lerp(uint a, uint b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        uint L(int sh) => (uint)Math.Round(((a >> sh) & 0xFF) * (1 - t) + ((b >> sh) & 0xFF) * t);
        return (L(24) << 24) | (L(16) << 16) | (L(8) << 8) | L(0);
    }

    public static uint Parse(string hex, uint fallback)
    {
        hex = hex.Trim().TrimStart('#');
        if (hex.Length == 6) hex = "FF" + hex;
        return uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var v) ? v : fallback;
    }

    public static string ToHex(uint c) => "#" + (c & 0xFFFFFF).ToString("X6");
}
