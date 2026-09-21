using System.Globalization;
using System.Linq;
using System.Text;

namespace DesktopCalendar;

/// <summary>
/// Reads/writes holidays.csv / reminders.csv (with a header row, UTF-8 with an explicit
/// BOM) and provides fast date matching for rendering.
///
/// The delimiter is NOT hardcoded, because no single choice is correct in every locale:
/// Excel's double-click-to-open CSV behavior uses the current Windows user's "List
/// separator" setting (Control Panel > Region), which is comma where the decimal
/// separator is a dot and semicolon where it's a comma. Tab avoids that ambiguity for
/// Excel but then Excel doesn't auto-split columns on double-click at all.
///
/// So instead: when WRITING, we ask .NET for the current user's own list separator
/// (CultureInfo.CurrentCulture.TextInfo.ListSeparator - the exact same OS setting Excel
/// itself reads), so a file saved on this machine opens correctly by double-click in
/// Excel on this machine, which is the overwhelmingly common case. When READING, we
/// never assume that same delimiter is still correct (the file may have been moved to
/// another machine, or the region setting may have changed since) - we detect it from
/// the file's own first line instead. This means the app always reads its own files
/// correctly regardless of what wrote them or on what machine.
///
/// A malformed line is simply skipped - it never corrupts the rest of the file, unlike
/// a single syntax error in JSON.
/// </summary>
internal static class DateEntryStore
{
    private static readonly char[] DelimiterCandidates = { ',', ';', '\t' };
    private static readonly string[] Header = { "Day", "Month", "Year", "Label", "Portion" };
    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);

    public static List<DateEntry> Load(string filePath)
    {
        var result = new List<DateEntry>();
        if (!File.Exists(filePath)) return result;

        string[] lines = File.ReadAllLines(filePath, Utf8WithBom);
        if (lines.Length == 0) return result;

        char delimiter = DetectDelimiter(lines);

        foreach (var line in lines)
        {
            if (line.Length == 0) continue;

            var fields = ParseCsvLine(line, delimiter);
            if (fields.Count < 2) continue;

            // The header row's "Day" column fails to parse as an int, so it's skipped
            // here naturally - no separate header-detection step needed.
            if (!int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int day)) continue;
            if (!int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int month)) continue;

            int? year = null;
            if (fields.Count > 2 && fields[2].Length > 0 && int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y))
                year = y;

            string label = fields.Count > 3 ? fields[3] : "";
            string portion = fields.Count > 4 && fields[4].Length > 0 ? fields[4] : "Full";

            result.Add(new DateEntry { Day = day, Month = month, Year = year, Label = label, Portion = portion });
        }

        return result;
    }

    public static void Save(string filePath, IEnumerable<DateEntry> entries)
    {
        char delimiter = GetPreferredDelimiter();

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(delimiter, Header));

        foreach (var e in entries)
        {
            string[] fields =
            {
                e.Day.ToString(CultureInfo.InvariantCulture),
                e.Month.ToString(CultureInfo.InvariantCulture),
                e.Year?.ToString(CultureInfo.InvariantCulture) ?? "",
                e.Label,
                e.Portion,
            };
            sb.AppendLine(string.Join(delimiter, fields.Select(f => EscapeCsvField(f, delimiter))));
        }

        string dir = Path.GetDirectoryName(filePath) ?? "";
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        string tempPath = Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(filePath)}_{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(tempPath, sb.ToString(), Utf8WithBom);
            File.Move(tempPath, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
        }
    }

    public static bool ContainsMatch(IReadOnlyList<DateEntry> entries, int day, int month, int year)
        => FindMatch(entries, day, month, year) != null;

    public static DateEntry? FindMatch(IReadOnlyList<DateEntry> entries, int day, int month, int year)
    {
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].Matches(day, month, year)) return entries[i];
        return null;
    }

    /// <summary>The current Windows user's own list separator - exactly what Excel reads for double-click CSV opening.</summary>
    private static char GetPreferredDelimiter()
    {
        string sep = CultureInfo.CurrentCulture.TextInfo.ListSeparator;
        return sep.Length == 1 ? sep[0] : ',';
    }

    /// <summary>Picks whichever candidate delimiter appears most often in the file's first non-empty line.</summary>
    private static char DetectDelimiter(string[] lines)
    {
        string firstLine = lines.FirstOrDefault(l => l.Length > 0) ?? "";

        char best = ',';
        int bestCount = -1;
        foreach (char candidate in DelimiterCandidates)
        {
            int count = firstLine.Count(c => c == candidate);
            if (count > bestCount)
            {
                bestCount = count;
                best = candidate;
            }
        }
        return best;
    }

    private static string EscapeCsvField(string field, char delimiter)
    {
        if (field.IndexOfAny(new[] { delimiter, '"', '\n', '\r' }) < 0)
            return field;
        return "\"" + field.Replace("\"", "\"\"") + "\"";
    }

    /// <summary>Minimal CSV line parser: handles quoted fields, embedded delimiters, and doubled-up quotes ("").</summary>
    private static List<string> ParseCsvLine(string line, char delimiter)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == delimiter)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }
}
