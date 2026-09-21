using System.Drawing;
using System.Globalization;

namespace DesktopCalendar;

internal sealed class AppSettings
{
    // [APP]
    public bool RunAtStartup { get; set; } = true;

    // [FONT]
    public string FontName { get; set; } = "Tahoma";
    public bool FontBold { get; set; } = true;
    public bool FontItalic { get; set; } = false;
    public Color FontColor { get; set; } = Color.White;
    public Color WeekdayColor { get; set; } = Color.FromArgb(79, 174, 21);
    public Color HolidayColor { get; set; } = Color.Red;
    public Color ReminderColor { get; set; } = Color.Cyan;
    public double FontRatio1 { get; set; } = 45;
    public double FontRatio2 { get; set; } = 65;

    // [SHADOW] (independent toggle - can combine with any other effect)
    public bool ShadowEnabled { get; set; } = true;
    public Color ShadowColor { get; set; } = Color.Black;
    public int ShadowOffsetX { get; set; } = 2;
    public int ShadowOffsetY { get; set; } = 2;
    /// <summary>Blur radius in pixels. 0 = crisp offset shadow, no blur.</summary>
    public int ShadowBlur { get; set; } = 3;

    // [OUTLINE] (independent toggle - can combine with any other effect)
    public bool OutlineEnabled { get; set; } = true;
    public Color OutlineColor { get; set; } = Color.Black;
    /// <summary>Outline stroke width in pixels.</summary>
    public int OutlineThicknessPixels { get; set; } = 3;

    // [GLOW] (independent toggle - can combine with any other effect)
    public bool GlowEnabled { get; set; } = false;
    public Color GlowColor { get; set; } = Color.FromArgb(255, 255, 255);
    public int GlowRadius { get; set; } = 6;

    // [CALENDAR]
    /// <summary>How many months after the current one to draw, stacked below it. 0 = current month only.</summary>
    public int NextMonthsCount { get; set; } = 1;
    /// <summary>Number of columns for next months (>= 1).</summary>
    public int NextMonthsColumns { get; set; } = 1;
    /// <summary>Layout order for multi-column next months: "RowFirst" or "ColumnFirst".</summary>
    public string NextMonthsLayout { get; set; } = "RowFirst";

    // [SHAPE]
    /// <summary>"Circle" | "Ellipse" | "Rectangle" | "RoundedRectangle" | "Capsule"</summary>
    public string CurrentDayShape { get; set; } = "Capsule";
    public Color ShapeFillColor { get; set; } = Color.FromArgb(243, 180, 48);

    // [CALENDAR POSITION]
    /// <summary>"TopLeft"|"TopCenter"|"TopRight"|"MiddleLeft"|"MiddleCenter"|"MiddleRight"|"BottomLeft"|"BottomCenter"|"BottomRight"</summary>
    public string CalendarAnchor { get; set; } = "TopRight";
    public int StartOffsetX { get; set; } = -15;
    public int StartOffsetY { get; set; } = 10;

    // [MONITOR & REGIONAL]
    /// <summary>0-based index of monitor to render the calendar on. 0 = primary screen.</summary>
    public int TargetMonitorIndex { get; set; } = 0;

    /// <summary>"Monday" or "Sunday"</summary>
    public string FirstDayOfWeek { get; set; } = "Monday";

    public static AppSettings Load(string iniPath)
    {
        return new AppSettings
        {
            RunAtStartup = ParseBool(IniFile.Read(iniPath, "APP", "runAtStartup", "True"), true),

            FontName = IniFile.Read(iniPath, "FONT", "fontName", "Tahoma"),
            FontBold = ParseBool(IniFile.Read(iniPath, "FONT", "fontBold", "True"), true),
            FontItalic = ParseBool(IniFile.Read(iniPath, "FONT", "fontItalic", "False"), false),
            FontColor = ColorUtil.ParseVbColor(IniFile.Read(iniPath, "FONT", "fontColor", "&HFFFFFF"), Color.White),
            WeekdayColor = ColorUtil.ParseVbColor(IniFile.Read(iniPath, "FONT", "weekdayColor", "&H15AE4F"), Color.ForestGreen),
            HolidayColor = ColorUtil.ParseVbColor(IniFile.Read(iniPath, "FONT", "holidayColor", "&H0000FF"), Color.Red),
            ReminderColor = ColorUtil.ParseVbColor(IniFile.Read(iniPath, "FONT", "reminderColor", "&HFFFF00"), Color.Cyan),
            FontRatio1 = Math.Clamp(ParseDouble(IniFile.Read(iniPath, "FONT", "fontRatio_1", "45"), 45), 5.0, 300.0),
            FontRatio2 = Math.Clamp(ParseDouble(IniFile.Read(iniPath, "FONT", "fontRatio_2", "65"), 65), 5.0, 300.0),

            ShadowEnabled = ParseBool(IniFile.Read(iniPath, "SHADOW", "enabled", "True"), true),
            ShadowColor = ColorUtil.ParseVbColor(IniFile.Read(iniPath, "SHADOW", "color", "&H000000"), Color.Black),
            ShadowOffsetX = Math.Clamp(ParseInt(IniFile.Read(iniPath, "SHADOW", "offsetX", "2"), 2), -200, 200),
            ShadowOffsetY = Math.Clamp(ParseInt(IniFile.Read(iniPath, "SHADOW", "offsetY", "2"), 2), -200, 200),
            ShadowBlur = Math.Clamp(ParseInt(IniFile.Read(iniPath, "SHADOW", "blur", "3"), 3), 0, 50),

            OutlineEnabled = ParseBool(IniFile.Read(iniPath, "OUTLINE", "enabled", "True"), true),
            OutlineColor = ColorUtil.ParseVbColor(IniFile.Read(iniPath, "OUTLINE", "color", "&H000000"), Color.Black),
            OutlineThicknessPixels = Math.Clamp(ParseInt(IniFile.Read(iniPath, "OUTLINE", "thicknessPixels", "3"), 3), 1, 50),

            GlowEnabled = ParseBool(IniFile.Read(iniPath, "GLOW", "enabled", "False"), false),
            GlowColor = ColorUtil.ParseVbColor(IniFile.Read(iniPath, "GLOW", "color", "&HFFFFFF"), Color.FromArgb(255, 255, 255)),
            GlowRadius = Math.Clamp(ParseInt(IniFile.Read(iniPath, "GLOW", "radius", "6"), 6), 1, 50),

            NextMonthsCount = Math.Clamp(ParseInt(IniFile.Read(iniPath, "CALENDAR", "nextMonthsCount", "1"), 1), 0, 36),
            NextMonthsColumns = Math.Clamp(ParseInt(IniFile.Read(iniPath, "CALENDAR", "nextMonthsColumns", "1"), 1), 1, 12),
            NextMonthsLayout = IniFile.Read(iniPath, "CALENDAR", "nextMonthsLayout", "RowFirst"),
            FirstDayOfWeek = IniFile.Read(iniPath, "CALENDAR", "firstDayOfWeek", "Monday"),

            CurrentDayShape = IniFile.Read(iniPath, "SHAPE", "currentDayShape", "Capsule"),
            ShapeFillColor = ColorUtil.ParseVbColor(IniFile.Read(iniPath, "SHAPE", "shapeFillColor", "&H30B4F3"), Color.Orange),

            StartOffsetX = Math.Clamp(ParseInt(IniFile.Read(iniPath, "CALENDAR POSITION", "startOffsetX", "-15"), -15), -5000, 5000),
            StartOffsetY = Math.Clamp(ParseInt(IniFile.Read(iniPath, "CALENDAR POSITION", "startOffsetY", "10"), 10), -5000, 5000),
            CalendarAnchor = IniFile.Read(iniPath, "CALENDAR POSITION", "anchor", "TopRight"),
            TargetMonitorIndex = Math.Max(0, ParseInt(IniFile.Read(iniPath, "CALENDAR POSITION", "targetMonitorIndex", "0"), 0)),
        };
    }

    public void Save(string iniPath)
    {
        IniFile.Write(iniPath, "APP", "runAtStartup", RunAtStartup.ToString());

        IniFile.Write(iniPath, "FONT", "fontName", FontName);
        IniFile.Write(iniPath, "FONT", "fontBold", FontBold.ToString());
        IniFile.Write(iniPath, "FONT", "fontItalic", FontItalic.ToString());
        IniFile.Write(iniPath, "FONT", "fontColor", ColorUtil.ToVbColorString(FontColor));
        IniFile.Write(iniPath, "FONT", "weekdayColor", ColorUtil.ToVbColorString(WeekdayColor));
        IniFile.Write(iniPath, "FONT", "holidayColor", ColorUtil.ToVbColorString(HolidayColor));
        IniFile.Write(iniPath, "FONT", "reminderColor", ColorUtil.ToVbColorString(ReminderColor));
        IniFile.Write(iniPath, "FONT", "fontRatio_1", FontRatio1.ToString(CultureInfo.InvariantCulture));
        IniFile.Write(iniPath, "FONT", "fontRatio_2", FontRatio2.ToString(CultureInfo.InvariantCulture));

        IniFile.Write(iniPath, "SHADOW", "enabled", ShadowEnabled.ToString());
        IniFile.Write(iniPath, "SHADOW", "color", ColorUtil.ToVbColorString(ShadowColor));
        IniFile.Write(iniPath, "SHADOW", "offsetX", ShadowOffsetX.ToString(CultureInfo.InvariantCulture));
        IniFile.Write(iniPath, "SHADOW", "offsetY", ShadowOffsetY.ToString(CultureInfo.InvariantCulture));
        IniFile.Write(iniPath, "SHADOW", "blur", ShadowBlur.ToString(CultureInfo.InvariantCulture));

        IniFile.Write(iniPath, "OUTLINE", "enabled", OutlineEnabled.ToString());
        IniFile.Write(iniPath, "OUTLINE", "color", ColorUtil.ToVbColorString(OutlineColor));
        IniFile.Write(iniPath, "OUTLINE", "thicknessPixels", OutlineThicknessPixels.ToString(CultureInfo.InvariantCulture));

        IniFile.Write(iniPath, "GLOW", "enabled", GlowEnabled.ToString());
        IniFile.Write(iniPath, "GLOW", "color", ColorUtil.ToVbColorString(GlowColor));
        IniFile.Write(iniPath, "GLOW", "radius", GlowRadius.ToString(CultureInfo.InvariantCulture));

        IniFile.Write(iniPath, "CALENDAR", "nextMonthsCount", NextMonthsCount.ToString(CultureInfo.InvariantCulture));
        IniFile.Write(iniPath, "CALENDAR", "nextMonthsColumns", NextMonthsColumns.ToString(CultureInfo.InvariantCulture));
        IniFile.Write(iniPath, "CALENDAR", "nextMonthsLayout", NextMonthsLayout);
        IniFile.Write(iniPath, "CALENDAR", "firstDayOfWeek", FirstDayOfWeek);

        IniFile.Write(iniPath, "SHAPE", "currentDayShape", CurrentDayShape);
        IniFile.Write(iniPath, "SHAPE", "shapeFillColor", ColorUtil.ToVbColorString(ShapeFillColor));

        IniFile.Write(iniPath, "CALENDAR POSITION", "startOffsetX", StartOffsetX.ToString(CultureInfo.InvariantCulture));
        IniFile.Write(iniPath, "CALENDAR POSITION", "startOffsetY", StartOffsetY.ToString(CultureInfo.InvariantCulture));
        IniFile.Write(iniPath, "CALENDAR POSITION", "anchor", CalendarAnchor);
        IniFile.Write(iniPath, "CALENDAR POSITION", "targetMonitorIndex", TargetMonitorIndex.ToString(CultureInfo.InvariantCulture));
    }

    private static bool ParseBool(string s, bool fallback) => bool.TryParse(s, out bool v) ? v : fallback;

    private static double ParseDouble(string s, double fallback)
        => double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : fallback;

    private static int ParseInt(string s, int fallback)
        => int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out int v) ? v : fallback;
}
