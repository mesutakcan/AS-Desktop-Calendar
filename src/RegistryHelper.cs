using Microsoft.Win32;

namespace DesktopCalendar;

/// <summary>
/// Reads/writes/deletes a single HKCU value given a full path
/// ("HKEY_CURRENT_USER\Software\...\ValueName"), the same way the
/// original Registry.cls (backed by WScript.Shell) was used.
/// </summary>
internal static class RegistryHelper
{
    public static string ReadKey(string fullPath)
    {
        try
        {
            var (keyPath, valueName) = Split(fullPath);
            using var key = Registry.CurrentUser.OpenSubKey(keyPath);
            return key?.GetValue(valueName)?.ToString() ?? "";
        }
        catch
        {
            return "";
        }
    }

    public static bool WriteKey(string fullPath, string value)
    {
        try
        {
            var (keyPath, valueName) = Split(fullPath);
            using var key = Registry.CurrentUser.CreateSubKey(keyPath);
            key?.SetValue(valueName, value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool DeleteKey(string fullPath)
    {
        try
        {
            var (keyPath, valueName) = Split(fullPath);
            using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
            key?.DeleteValue(valueName, throwOnMissingValue: false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static (string keyPath, string valueName) Split(string fullPath)
    {
        string trimmed = fullPath;
        const string hkcuPrefix = "HKEY_CURRENT_USER\\";
        if (trimmed.StartsWith(hkcuPrefix, StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[hkcuPrefix.Length..];

        int idx = trimmed.LastIndexOf('\\');
        if (idx < 0)
            return ("", trimmed);

        return (trimmed[..idx], trimmed[(idx + 1)..]);
    }
}
