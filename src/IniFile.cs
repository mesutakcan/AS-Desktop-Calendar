using System.Text;

namespace DesktopCalendar;

/// <summary>
/// Thin wrapper over GetPrivateProfileString / WritePrivateProfileString.
/// </summary>
internal static class IniFile
{
    public static string Read(string filePath, string section, string key, string defaultValue = "")
    {
        var buffer = new StringBuilder(32767);
        NativeMethods.GetPrivateProfileString(section, key, defaultValue, buffer, buffer.Capacity, filePath);
        return buffer.ToString();
    }

    public static void Write(string filePath, string section, string key, string value)
        => NativeMethods.WritePrivateProfileString(section, key, value, filePath);

    public static void DeleteKey(string filePath, string section, string key)
        => NativeMethods.WritePrivateProfileString(section, key, null, filePath);
}
