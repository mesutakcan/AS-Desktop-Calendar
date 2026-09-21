namespace DesktopCalendar;

/// <summary>Adds/removes the app from HKCU\...\CurrentVersion\Run, equivalent to the original's Form_Load logic.</summary>
internal static class StartupManager
{
    private const string RegStartupRunPath = "HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Run\\";

    public static void Apply(string productName, bool enable)
    {
        string regKey = RegStartupRunPath + productName;
        string appFullPath = Environment.ProcessPath ?? "";
        string currentValue = RegistryHelper.ReadKey(regKey);

        if (enable)
        {
            string quotedPath = $"\"{appFullPath}\"";
            if (!string.Equals(currentValue, quotedPath, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(currentValue, appFullPath, StringComparison.OrdinalIgnoreCase) &&
                appFullPath != "")
            {
                RegistryHelper.WriteKey(regKey, quotedPath);
            }
        }
        else if (currentValue != "")
        {
            RegistryHelper.DeleteKey(regKey);
        }
    }

    public static bool IsEnabled(string productName)
        => RegistryHelper.ReadKey(RegStartupRunPath + productName) != "";
}
