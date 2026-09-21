using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;

namespace DesktopCalendar;

/// <summary>Renders the current + next month calendar over a background image.</summary>
internal sealed class CalendarRenderer
{
    private readonly AppSettings _s;

    public CalendarRenderer(AppSettings settings) => _s = settings;

    /// <summary>Draws the calendar onto a wallpaper bitmap covering the desktop/target monitor.</summary>
    public Bitmap Render(string wallpaperSourceFile, IReadOnlyList<DateEntry> holidays, IReadOnlyList<DateEntry> reminders)
    {
        var culture = CultureInfo.CurrentCulture;
        var months = new string[13];
        for (int n = 1; n <= 12; n++)
            months[n] = culture.DateTimeFormat.GetMonthName(n);

        bool isSundayFirst = string.Equals(_s.FirstDayOfWeek, "Sunday", StringComparison.OrdinalIgnoreCase);

        var weekDays = new string[8]; // 1..7
        DayOfWeek[] dowOrder = isSundayFirst
            ? new[] { DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday }
            : new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };

        for (int n = 1; n <= 7; n++)
        {
            string abbr = culture.DateTimeFormat.GetAbbreviatedDayName(dowOrder[n - 1]);
            weekDays[n] = abbr.Length >= 2 ? abbr[..2] : abbr;
        }

        // Determine target screen and total virtual screen bounds
        Screen[] allScreens = Screen.AllScreens;
        int targetIdx = Math.Clamp(_s.TargetMonitorIndex, 0, Math.Max(0, allScreens.Length - 1));
        Screen targetScreen = allScreens.Length > 0 ? allScreens[targetIdx] : Screen.PrimaryScreen!;

        // If single screen or invalid virtual screen, fallback to targetScreen bounds
        Rectangle virtualBounds = allScreens.Length > 1 && SystemInformation.VirtualScreen.Width > 0
            ? SystemInformation.VirtualScreen
            : targetScreen.Bounds;

        int totalWidth = virtualBounds.Width;
        int totalHeight = virtualBounds.Height;

        DateTime now = DateTime.Now;
        int currentMonth = now.Month;
        int currentYear = now.Year;
        int currentDay = now.Day;
        int monthsAhead = Math.Max(0, _s.NextMonthsCount);

        // Precompute which (month, year) each future block is, and how many week-rows it needs.
        var futureMonths = new List<(int month, int year, int rows)>();
        int fm = currentMonth, fy = currentYear;
        for (int i = 0; i < monthsAhead; i++)
        {
            fm++; if (fm > 12) { fm = 1; fy++; }
            futureMonths.Add((fm, fy, RowsNeeded(fy, fm, isSundayFirst)));
        }

        var bmp = new Bitmap(totalWidth, totalHeight);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            ApplyWallpaperBackground(g, wallpaperSourceFile, virtualBounds, allScreens);

            int targetScreenHeight = targetScreen.Bounds.Height;
            var style = (_s.FontBold ? FontStyle.Bold : FontStyle.Regular) | (_s.FontItalic ? FontStyle.Italic : FontStyle.Regular);
            using var font1 = new Font(_s.FontName, (float)(targetScreenHeight / _s.FontRatio1), style);
            using var font2 = new Font(_s.FontName, (float)(targetScreenHeight / _s.FontRatio2), style);

            using var fmt = new StringFormat(StringFormat.GenericTypographic);
            fmt.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;

            SizeF charSize = g.MeasureString("8", font1, int.MaxValue, fmt);
            float charWidth = charSize.Width, charHeight = charSize.Height;
            SizeF char2Size = g.MeasureString("8", font2, int.MaxValue, fmt);
            float char2Width = char2Size.Width, char2Height = char2Size.Height;

            // Measure the whole block up front so it can be anchored anywhere on screen.
            // 7 columns with 3-char pitch: col 0 is at 0, col 6 is at 18*charWidth, plus 2 chars for day text = 20*charWidth.
            int rowsCurrent = RowsNeeded(currentYear, currentMonth, isSundayFirst);
            float currentMonthWidth = charWidth * 20f;
            float currentMonthHeight = charHeight + (1 + rowsCurrent) * charHeight;

            int colCount = Math.Max(1, _s.NextMonthsColumns);
            float futureColWidth = char2Width * 20f;
            float futureColGap = char2Width * 2f; // spacing between columns

            int futureTotal = futureMonths.Count;
            int rowCount = (int)Math.Ceiling((double)futureTotal / colCount);

            // Compute height per grid row of future months
            float futureTotalHeight = 0f;
            var rowHeights = new float[rowCount];
            if (futureTotal > 0)
            {
                bool isColumnFirst = string.Equals(_s.NextMonthsLayout, "ColumnFirst", StringComparison.OrdinalIgnoreCase);
                for (int i = 0; i < futureTotal; i++)
                {
                    int r = isColumnFirst ? (i % rowCount) : (i / colCount);
                    float mHeight = char2Height + (1 + futureMonths[i].rows) * char2Height; // title + grid
                    if (mHeight > rowHeights[r]) rowHeights[r] = mHeight;
                }

                for (int r = 0; r < rowCount; r++)
                {
                    futureTotalHeight += rowHeights[r];
                    if (r < rowCount - 1) futureTotalHeight += char2Height; // row gap
                }
            }

            int actualColumnsUsed = Math.Min(colCount, Math.Max(1, futureTotal));
            float futureBlockWidth = futureTotal > 0
                ? actualColumnsUsed * futureColWidth + (actualColumnsUsed - 1) * futureColGap
                : 0f;

            float blockWidth = Math.Max(currentMonthWidth, futureBlockWidth);
            float blockHeight = currentMonthHeight;
            if (futureTotal > 0)
                blockHeight += (char2Height / 2f) + futureTotalHeight;

            // Position relative to target monitor's bounds mapped onto the virtual screen
            float monitorLeft = targetScreen.Bounds.Left - virtualBounds.Left;
            float monitorTop = targetScreen.Bounds.Top - virtualBounds.Top;
            float monitorWidth = targetScreen.Bounds.Width;
            float monitorHeight = targetScreen.Bounds.Height;

            float blockOriginX = monitorLeft + ComputeOriginX(_s.CalendarAnchor, monitorWidth, blockWidth) + _s.StartOffsetX;
            float offsetY = monitorTop + ComputeOriginY(_s.CalendarAnchor, monitorHeight, blockHeight) + _s.StartOffsetY;

            // Align active month and future block precisely based on CalendarAnchor:
            // Right-aligned: both active month and future block touch the right edge of blockWidth.
            // Left-aligned: both touch the left edge (blockOriginX).
            // Center-aligned: both are centered within blockWidth.
            float currentMonthX, futureStartX;
            if (_s.CalendarAnchor.EndsWith("Right", StringComparison.Ordinal))
            {
                currentMonthX = blockOriginX + (blockWidth - currentMonthWidth);
                futureStartX = blockOriginX + (blockWidth - futureBlockWidth);
            }
            else if (_s.CalendarAnchor.EndsWith("Left", StringComparison.Ordinal))
            {
                currentMonthX = blockOriginX;
                futureStartX = blockOriginX;
            }
            else // *Center
            {
                currentMonthX = blockOriginX + (blockWidth - currentMonthWidth) / 2f;
                futureStartX = blockOriginX + (blockWidth - futureBlockWidth) / 2f;
            }

            // Draw active month
            PicPrint(g, font1, fmt, currentMonthX, offsetY, $"{months[currentMonth]} {currentYear}", _s.FontColor);
            float cursorY = offsetY + charHeight;
            DrawCalendarGrid(g, font1, fmt, currentMonthX, cursorY, currentMonth, currentYear, weekDays,
                charWidth, charHeight, holidays, reminders, isCurrentMonth: true, currentDay: currentDay, isSundayFirst: isSundayFirst);

            if (futureTotal > 0)
            {
                float futureStartY = offsetY + currentMonthHeight + (char2Height / 2f);

                // Precompute top Y position for each grid row
                var rowTops = new float[rowCount];
                float currRowY = futureStartY;
                for (int r = 0; r < rowCount; r++)
                {
                    rowTops[r] = currRowY;
                    currRowY += rowHeights[r] + char2Height;
                }

                bool isColumnFirst = string.Equals(_s.NextMonthsLayout, "ColumnFirst", StringComparison.OrdinalIgnoreCase);

                for (int i = 0; i < futureTotal; i++)
                {
                    int c, r;
                    if (isColumnFirst)
                    {
                        r = i % rowCount;
                        c = i / rowCount;
                    }
                    else
                    {
                        r = i / colCount;
                        c = i % colCount;
                    }

                    float cellX = futureStartX + c * (futureColWidth + futureColGap);
                    float cellY = rowTops[r];

                    var f = futureMonths[i];
                    PicPrint(g, font2, fmt, cellX, cellY, $"{months[f.month]} {f.year}", _s.FontColor);
                    float afterHeaderY = cellY + char2Height;

                    DrawCalendarGrid(g, font2, fmt, cellX, afterHeaderY, f.month, f.year, weekDays,
                        char2Width, char2Height, holidays, reminders, isCurrentMonth: false, currentDay: currentDay, isSundayFirst: isSundayFirst);
                }
            }
        }

        return bmp;
    }

    private static int RowsNeeded(int year, int month, bool isSundayFirst)
    {
        int daysInMonth = DateTime.DaysInMonth(year, month);
        DayOfWeek dow = new DateTime(year, month, 1).DayOfWeek;
        int firstDow = isSundayFirst ? (int)dow : ((int)dow + 6) % 7;
        return (int)Math.Ceiling((firstDow + daysInMonth) / 7.0);
    }

    private static float ComputeOriginX(string anchor, float screenWidth, float blockWidth)
    {
        if (anchor.EndsWith("Left", StringComparison.Ordinal)) return 0f;
        if (anchor.EndsWith("Right", StringComparison.Ordinal)) return screenWidth - blockWidth;
        return (screenWidth - blockWidth) / 2f; // *Center
    }

    private static float ComputeOriginY(string anchor, float screenHeight, float blockHeight)
    {
        if (anchor.StartsWith("Top", StringComparison.Ordinal)) return 0f;
        if (anchor.StartsWith("Bottom", StringComparison.Ordinal)) return screenHeight - blockHeight;
        return (screenHeight - blockHeight) / 2f; // Middle*
    }

    private float DrawCalendarGrid(Graphics g, Font font, StringFormat fmt, float x, float y,
        int month, int year, string[] weekDays, float charWidth, float charHeight,
        IReadOnlyList<DateEntry> holidays, IReadOnlyList<DateEntry> reminders, bool isCurrentMonth, int currentDay, bool isSundayFirst)
    {
        for (int n = 1; n <= 7; n++)
            PicPrint(g, font, fmt, (n - 1) * charWidth * 3 + x, y, weekDays[n], _s.WeekdayColor);

        int daysInMonth = DateTime.DaysInMonth(year, month);
        DayOfWeek dow = new DateTime(year, month, 1).DayOfWeek;
        int firstDow = isSundayFirst ? (int)dow : ((int)dow + 6) % 7;
        int col = firstDow;
        int row = 1;
        float rowY = y;

        for (int day = 1; day <= daysInMonth; day++)
        {
            if (col == 7) { col = 0; row++; }

            float posX = x + col * charWidth * 3;
            float posY = y + row * charHeight;
            col++;

            if (isCurrentMonth && day == currentDay)
                DrawCurrentDayShape(g, posX, posY, charWidth, charHeight);

            if (DateEntryStore.ContainsMatch(reminders, day, month, year))
                DrawReminderMark(g, posX, posY, charWidth, charHeight);

            float dayX = day < 10 ? posX + charWidth / 2 : posX;
            var (dayTopColor, dayBottomColor) = GetDayColors(day, month, year, holidays);
            PicPrint(g, font, fmt, dayX, posY, day.ToString(), dayTopColor, dayBottomColor);

            rowY = posY;
        }

        return rowY + charHeight;
    }

    private void DrawCurrentDayShape(Graphics g, float posX, float posY, float charWidth, float charHeight)
    {
        using var fillBrush = new SolidBrush(_s.ShapeFillColor);
        using var borderPen = new Pen(_s.FontColor, 1.5f);

        switch (_s.CurrentDayShape)
        {
            case "Circle":
                {
                    float cx = posX + charWidth - 1, cy = posY + charHeight * 0.5f, r = charHeight * 0.65f;
                    var rect = new RectangleF(cx - r, cy - r, r * 2, r * 2);
                    g.FillEllipse(fillBrush, rect);
                    g.DrawEllipse(borderPen, rect);
                    break;
                }
            case "Ellipse":
                {
                    float cx = posX + charWidth - 1, cy = posY - 1 + charHeight * 0.5f;
                    float rx = charHeight * 0.5f + charHeight / 3f, ry = rx * 0.75f;
                    var rect = new RectangleF(cx - rx, cy - ry, rx * 2, ry * 2);
                    g.FillEllipse(fillBrush, rect);
                    g.DrawEllipse(borderPen, rect);
                    break;
                }
            case "Rectangle":
                {
                    var rect = new RectangleF(posX - charWidth / 2, posY, charWidth * 2.75f, charHeight);
                    g.FillRectangle(fillBrush, rect);
                    g.DrawRectangle(borderPen, rect.X, rect.Y, rect.Width, rect.Height);
                    break;
                }
            case "RoundedRectangle":
                {
                    // Modest corner radius (~20% of height) - a proper "rounded rectangle",
                    // as opposed to Capsule's fully-round left/right ends.
                    var rect = new RectangleF(posX - charWidth / 2, posY, charWidth * 2.75f, charHeight);
                    using var path = RoundedRect(rect, charHeight * 0.20f);
                    g.FillPath(fillBrush, path);
                    g.DrawPath(borderPen, path);
                    break;
                }
            case "Capsule":
            default:
                {
                    var rect = new RectangleF(posX - charWidth * 0.5f, posY, charWidth * 3f, charHeight);
                    using var path = RoundedRect(rect, charHeight * 0.6f);
                    g.FillPath(fillBrush, path);
                    g.DrawPath(borderPen, path);
                    break;
                }
        }
    }

    private void DrawReminderMark(Graphics g, float posX, float posY, float charWidth, float charHeight)
    {
        var rect = new RectangleF(posX - charWidth * 0.5f, posY + charHeight * 0.5f, charWidth * 3f, charHeight * 0.5f);
        using var path = RoundedRect(rect, charHeight * 0.5f);
        using var fillBrush = new SolidBrush(_s.ReminderColor);
        using var borderPen = new Pen(Color.Black, 1f);
        g.FillPath(fillBrush, path);
        g.DrawPath(borderPen, path);
    }

    private static GraphicsPath RoundedRect(RectangleF rect, float radius)
    {
        float d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
        var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>
    /// Returns (topColor, bottomColor). bottomColor is null for a normal fill (weekday,
    /// weekend, or full-day holiday). For a holiday whose Portion is "Afternoon", the top
    /// half is drawn in the normal font color and the bottom half in the holiday color,
    /// visually splitting the day number to show it's only a half-day holiday.
    /// </summary>
    private (Color top, Color? bottom) GetDayColors(int day, int month, int year, IReadOnlyList<DateEntry> holidays)
    {
        var dow = new DateTime(year, month, day).DayOfWeek;
        bool isWeekend = dow == DayOfWeek.Saturday || dow == DayOfWeek.Sunday;
        DateEntry? match = DateEntryStore.FindMatch(holidays, day, month, year);

        if (isWeekend || (match != null && match.Portion != "Afternoon"))
            return (_s.HolidayColor, null); // weekend, or a full-day holiday

        if (match != null && match.Portion == "Afternoon")
            return (_s.FontColor, _s.HolidayColor); // half-day holiday: split

        return (_s.FontColor, null);
    }

    // ---- Text effects ----

    /// <summary>
    /// Each effect is an independent on/off layer, always composited in the same
    /// order so any combination looks predictable: glow, then shadow, then the
    /// outline stroke, then the final fill on top (which also handles the
    /// half-day-holiday split).
    /// </summary>
    private void PicPrint(Graphics g, Font font, StringFormat fmt, float x, float y, string text, Color textColor, Color? splitBottomColor = null)
    {
        if (text.Length == 0) return;

        if (_s.GlowEnabled)
            DrawBlurredText(g, font, fmt, x, y, text, _s.GlowColor, _s.GlowRadius);

        if (_s.ShadowEnabled)
            DrawBlurredText(g, font, fmt, x + _s.ShadowOffsetX, y + _s.ShadowOffsetY, text, _s.ShadowColor, _s.ShadowBlur);

        // Build the glyph outline path ONCE and use it for both the outline stroke and the
        // final fill below, so they are always pixel-perfectly aligned. DrawString and
        // GraphicsPath.AddString apply different hinting/grid-fitting and can otherwise
        // drift apart by a pixel or two, which read as a misaligned "shadow" next to the
        // stroke rather than a clean outline.
        float emSizePx = font.Size * g.DpiY / 72f;
        using var path = new GraphicsPath();
        path.AddString(text, font.FontFamily, (int)font.Style, emSizePx, new PointF(x, y), fmt);

        if (_s.OutlineEnabled)
        {
            float outlineWidth = Math.Max(0.5f, _s.OutlineThicknessPixels);
            using var outlinePen = new Pen(_s.OutlineColor, outlineWidth) { LineJoin = LineJoin.Round };
            g.DrawPath(outlinePen, path);
        }

        FillPathSplit(g, path, textColor, splitBottomColor);
    }

    /// <summary>Fills a path solid, or clipped into a top half / bottom half with two different colors.</summary>
    private static void FillPathSplit(Graphics g, GraphicsPath path, Color topColor, Color? bottomColor)
    {
        if (bottomColor is null)
        {
            using var brush = new SolidBrush(topColor);
            g.FillPath(brush, path);
            return;
        }

        RectangleF bounds = path.GetBounds();
        var topRect = new RectangleF(bounds.X - 2, bounds.Y - 2, bounds.Width + 4, bounds.Height / 2f + 2);
        var bottomRect = new RectangleF(bounds.X - 2, bounds.Y + bounds.Height / 2f, bounds.Width + 4, bounds.Height / 2f + 2);

        Region savedClip = g.Clip;
        try
        {
            g.SetClip(topRect, CombineMode.Intersect);
            using (var topBrush = new SolidBrush(topColor)) g.FillPath(topBrush, path);
            g.Clip = savedClip;

            g.SetClip(bottomRect, CombineMode.Intersect);
            using (var bottomBrush = new SolidBrush(bottomColor.Value)) g.FillPath(bottomBrush, path);
        }
        finally
        {
            g.Clip = savedClip;
        }
    }

    /// <summary>
    /// Renders text into an offscreen bitmap, box-blurs it (3 passes ~= gaussian), and
    /// composites it at (x, y). Used for both the "shadow" effect (offset + blur) and
    /// the "glow" effect (no offset, larger blur, bright color).
    /// </summary>
    private static void DrawBlurredText(Graphics g, Font font, StringFormat fmt, float x, float y, string text, Color color, int blurRadius)
    {
        if (text.Length == 0) return;
        blurRadius = Math.Clamp(blurRadius, 0, 24);

        SizeF textSize = g.MeasureString(text, font, int.MaxValue, fmt);
        int margin = blurRadius * 2 + 4;
        int w = Math.Max(1, (int)Math.Ceiling(textSize.Width) + margin * 2);
        int h = Math.Max(1, (int)Math.Ceiling(textSize.Height) + margin * 2);

        using var temp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var tg = Graphics.FromImage(temp))
        {
            tg.SmoothingMode = SmoothingMode.AntiAlias;
            float emSizePx = font.Size * tg.DpiY / 72f;
            using var path = new GraphicsPath();
            path.AddString(text, font.FontFamily, (int)font.Style, emSizePx, new PointF(margin, margin), fmt);
            using var brush = new SolidBrush(color);
            tg.FillPath(brush, path);
        }

        if (blurRadius > 0)
            BoxBlur(temp, blurRadius);

        g.DrawImage(temp, x - margin, y - margin);
    }

    /// <summary>3 passes of horizontal+vertical box blur approximates a gaussian blur cheaply.</summary>
    private static void BoxBlur(Bitmap bmp, int radius)
    {
        var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            int stride = data.Stride;
            int byteCount = stride * bmp.Height;
            byte[] pixels = new byte[byteCount];
            Marshal.Copy(data.Scan0, pixels, 0, byteCount);

            for (int pass = 0; pass < 3; pass++)
            {
                pixels = BoxBlurPass(pixels, bmp.Width, bmp.Height, stride, radius, horizontal: true);
                pixels = BoxBlurPass(pixels, bmp.Width, bmp.Height, stride, radius, horizontal: false);
            }

            Marshal.Copy(pixels, 0, data.Scan0, byteCount);
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }

    private static byte[] BoxBlurPass(byte[] src, int w, int h, int stride, int radius, bool horizontal)
    {
        var dst = new byte[src.Length];

        if (horizontal)
        {
            for (int y = 0; y < h; y++)
            {
                int rowStart = y * stride;
                for (int x = 0; x < w; x++)
                {
                    long a = 0, r = 0, gsum = 0, b = 0;
                    int count = 0;
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int sx = x + dx;
                        if (sx < 0 || sx >= w) continue;
                        int idx = rowStart + sx * 4;
                        b += src[idx]; gsum += src[idx + 1]; r += src[idx + 2]; a += src[idx + 3];
                        count++;
                    }
                    int dIdx = rowStart + x * 4;
                    dst[dIdx] = (byte)(b / count);
                    dst[dIdx + 1] = (byte)(gsum / count);
                    dst[dIdx + 2] = (byte)(r / count);
                    dst[dIdx + 3] = (byte)(a / count);
                }
            }
        }
        else
        {
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    long a = 0, r = 0, gsum = 0, b = 0;
                    int count = 0;
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        int sy = y + dy;
                        if (sy < 0 || sy >= h) continue;
                        int idx = sy * stride + x * 4;
                        b += src[idx]; gsum += src[idx + 1]; r += src[idx + 2]; a += src[idx + 3];
                        count++;
                    }
                    int dIdx = y * stride + x * 4;
                    dst[dIdx] = (byte)(b / count);
                    dst[dIdx + 1] = (byte)(gsum / count);
                    dst[dIdx + 2] = (byte)(r / count);
                    dst[dIdx + 3] = (byte)(a / count);
                }
            }
        }

        return dst;
    }

    // ---- Background wallpaper compositing ----

    private static void ApplyWallpaperBackground(Graphics g, string wallpaperFile, Rectangle virtualBounds, Screen[] allScreens)
    {
        Color deskColor = ColorUtil.FromColorRef(NativeMethods.GetSysColor(NativeMethods.COLOR_DESKTOP));
        using (var bg = new SolidBrush(deskColor))
            g.FillRectangle(bg, 0, 0, virtualBounds.Width, virtualBounds.Height);

        if (string.IsNullOrEmpty(wallpaperFile) || !File.Exists(wallpaperFile)) return;

        using Image img = Image.FromFile(wallpaperFile);
        int style = WallpaperService.GetWallpaperStyle();
        bool isTiled = WallpaperService.IsTiled();

        if (allScreens.Length > 1)
        {
            // Draw background onto each monitor's viewport within the virtual screen
            foreach (var scr in allScreens)
            {
                int mx = scr.Bounds.Left - virtualBounds.Left;
                int my = scr.Bounds.Top - virtualBounds.Top;
                int mw = scr.Bounds.Width;
                int mh = scr.Bounds.Height;

                var state = g.Save();
                g.SetClip(new Rectangle(mx, my, mw, mh));
                DrawImageIntoRect(g, img, mx, my, mw, mh, style, isTiled);
                g.Restore(state);
            }
        }
        else
        {
            DrawImageIntoRect(g, img, 0, 0, virtualBounds.Width, virtualBounds.Height, style, isTiled);
        }
    }

    private static void DrawImageIntoRect(Graphics g, Image img, float rx, float ry, float rw, float rh, int style, bool isTiled)
    {
        int imgW = img.Width, imgH = img.Height;

        if (isTiled)
        {
            for (float y = ry; y < ry + rh; y += imgH)
                for (float x = rx; x < rx + rw; x += imgW)
                    g.DrawImage(img, x, y, imgW, imgH);
            return;
        }

        double imgRatio = (double)imgW / imgH;
        double boxRatio = (double)rw / rh;

        switch (style)
        {
            case 0: // Centered
                g.DrawImage(img, rx + (rw - imgW) / 2f, ry + (rh - imgH) / 2f, imgW, imgH);
                break;
            case 2: // Stretched
                g.DrawImage(img, rx, ry, rw, rh);
                break;
            case 3:
            case 6: // Fit
                DrawContainOrCover(g, img, rx, ry, rw, rh, imgRatio, boxRatio, cover: false);
                break;
            case 10:
            case 22: // Fill or Span
                DrawContainOrCover(g, img, rx, ry, rw, rh, imgRatio, boxRatio, cover: true);
                break;
            default:
                g.DrawImage(img, rx, ry, rw, rh);
                break;
        }
    }

    private static void DrawContainOrCover(Graphics g, Image img, float rx, float ry, float rw, float rh, double imgRatio, double boxRatio, bool cover)
    {
        bool widthDriven = cover ? imgRatio < boxRatio : imgRatio > boxRatio;
        if (widthDriven)
        {
            double newHeight = rw / imgRatio;
            g.DrawImage(img, rx, (float)(ry + (rh - newHeight) / 2), rw, (float)newHeight);
        }
        else
        {
            double newWidth = rh * imgRatio;
            g.DrawImage(img, (float)(rx + (rw - newWidth) / 2), ry, (float)newWidth, rh);
        }
    }
}
