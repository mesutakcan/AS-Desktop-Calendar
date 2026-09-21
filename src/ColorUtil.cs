using System.Drawing;
using System.Globalization;

namespace DesktopCalendar;

internal static class ColorUtil
{
    public static Color ParseVbColor(string s, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(s)) return fallback;
        s = s.Trim();
        if (s.StartsWith("&H", StringComparison.OrdinalIgnoreCase)) s = s[2..];
        if (s.EndsWith('&')) s = s[..^1];
        if (!uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint raw)) return fallback;
        return FromColorRef(raw);
    }

    public static Color FromColorRef(uint colorRef)
    {
        byte b = (byte)((colorRef >> 16) & 0xFF);
        byte g = (byte)((colorRef >> 8) & 0xFF);
        byte r = (byte)(colorRef & 0xFF);
        return Color.FromArgb(r, g, b);
    }

    public static string ToVbColorString(Color c)
    {
        uint colorRef = ((uint)c.B << 16) | ((uint)c.G << 8) | c.R;
        return "&H" + colorRef.ToString("X6", CultureInfo.InvariantCulture);
    }
}
