namespace DesktopCalendar;

/// <summary>A single holiday or reminder date. Year == null means it repeats every year.</summary>
internal sealed class DateEntry
{
    public int Day { get; set; }
    public int Month { get; set; }
    public int? Year { get; set; }
    public string Label { get; set; } = "";

    /// <summary>"Full" (whole day) or "Afternoon" (half day, starting in the afternoon). Only meaningful for holidays.</summary>
    public string Portion { get; set; } = "Full";

    public bool Matches(int day, int month, int year)
        => Day == day && Month == month && (Year is null || Year == year);
}
