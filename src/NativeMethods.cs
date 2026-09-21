using System.Runtime.InteropServices;
using System.Text;

namespace DesktopCalendar;

internal static class NativeMethods
{
    public const int SPI_SETDESKWALLPAPER = 20;
    public const int SPIF_UPDATEINIFILE = 0x01;
    public const int SPIF_SENDWININICHANGE = 0x02;

    public const int COLOR_DESKTOP = 1;

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

    [DllImport("user32.dll")]
    public static extern uint GetSysColor(int nIndex);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern int GetPrivateProfileString(string section, string? key, string defaultValue,
        StringBuilder returnedString, int size, string filePath);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern int WritePrivateProfileString(string section, string? key, string? value, string filePath);
}
