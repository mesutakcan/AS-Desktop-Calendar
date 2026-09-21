using System.Drawing.Imaging;

namespace DesktopCalendar;

/// <summary>Equivalent of the wallpaper-file bookkeeping and SystemParametersInfo call from Form_Load.</summary>
internal static class WallpaperService
{
    public const string WallpaperFileName = "wallpaper.bmp";
    public const string RegCpanelDesktopPath = "HKEY_CURRENT_USER\\Control Panel\\Desktop\\";

    /// <summary>
    /// Figures out which image file the calendar should be drawn onto: the
    /// user's real wallpaper, remembered across our own generated one.
    /// </summary>
    public static string DetermineSourceImage(string iniFile, string wallpaperFile)
    {
        string activeWallpaper = RegistryHelper.ReadKey(RegCpanelDesktopPath + "Wallpaper");
        string prevWallpaper = IniFile.Read(iniFile, "WALLPAPER", "prevWallpaper", "");

        if (string.Equals(activeWallpaper, wallpaperFile, StringComparison.OrdinalIgnoreCase))
            activeWallpaper = File.Exists(prevWallpaper) ? prevWallpaper : "";

        if (File.Exists(activeWallpaper))
        {
            if (prevWallpaper != activeWallpaper)
                IniFile.Write(iniFile, "WALLPAPER", "prevWallpaper", activeWallpaper);
            return activeWallpaper;
        }

        IniFile.DeleteKey(iniFile, "WALLPAPER", "prevWallpaper");
        return "";
    }

    public static bool IsTiled() => RegistryHelper.ReadKey(RegCpanelDesktopPath + "TileWallpaper") == "1";

    public static int GetWallpaperStyle()
        => int.TryParse(RegistryHelper.ReadKey(RegCpanelDesktopPath + "WallpaperStyle"), out int v) ? v : 0;

    public static void SaveAndApply(Bitmap bmp, string wallpaperFile)
    {
        bmp.Save(wallpaperFile, ImageFormat.Bmp);

        if (File.Exists(wallpaperFile))
        {
            // If multiple monitors exist, Windows must treat the virtual desktop image as "Span" (Style 22, Tile 0),
            // otherwise Windows stretches the whole multi-screen canvas separately onto each screen.
            if (Screen.AllScreens.Length > 1)
            {
                RegistryHelper.WriteKey(RegCpanelDesktopPath + "WallpaperStyle", "22");
                RegistryHelper.WriteKey(RegCpanelDesktopPath + "TileWallpaper", "0");
            }

            NativeMethods.SystemParametersInfo(NativeMethods.SPI_SETDESKWALLPAPER, 0, wallpaperFile,
                NativeMethods.SPIF_UPDATEINIFILE | NativeMethods.SPIF_SENDWININICHANGE);
        }
    }
}
