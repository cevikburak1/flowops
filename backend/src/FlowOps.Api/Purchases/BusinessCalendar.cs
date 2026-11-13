namespace FlowOps.Api;

internal sealed record BusinessCalendar(string TimeZone, DayOfWeek[] WorkingDays, TimeOnly Opens, TimeOnly Closes, DateOnly[] Holidays)
{
    public bool Valid() => Zone() is not null && WorkingDays is { Length: >= 1 and <= 7 } &&
        WorkingDays.All(day => Enum.IsDefined(day)) && WorkingDays.Distinct().Count() == WorkingDays.Length &&
        Closes > Opens && Closes - Opens >= TimeSpan.FromHours(1) &&
        Holidays is { Length: <= 366 } && Holidays.Distinct().Count() == Holidays.Length;

    private TimeZoneInfo? Zone()
    {
        if (string.IsNullOrWhiteSpace(TimeZone) || TimeZone.Length > 100) return null;
        try { return TimeZoneInfo.FindSystemTimeZoneById(TimeZone); }
        catch (Exception error) when (error is TimeZoneNotFoundException or InvalidTimeZoneException) { return null; }
    }

    public DateTimeOffset Deadline(DateTimeOffset start, int minutes)
    {
        if (minutes is < 1 or > 14400 || !Valid()) throw new ArgumentOutOfRangeException(nameof(minutes));
        var zone = Zone()!;
        var remaining = TimeSpan.FromMinutes(minutes);
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(start, zone).DateTime);
        for (var day = 0; day < 12000; day++, date = date.AddDays(1))
        {
            if (!WorkingDays.Contains(date.DayOfWeek) || Holidays.Contains(date)) continue;
            var open = Boundary(date.ToDateTime(Opens), zone, opening: true);
            var close = Boundary(date.ToDateTime(Closes), zone, opening: false);
            var from = start > open ? start : open;
            if (from >= close) continue;
            var available = close - from;
            if (remaining <= available) return (from + remaining).ToUniversalTime();
            remaining -= available;
        }
        throw new InvalidOperationException("The business calendar did not provide enough working time.");
    }

    private static DateTimeOffset Boundary(DateTime local, TimeZoneInfo zone, bool opening)
    {
        while (zone.IsInvalidTime(local)) local = local.AddMinutes(1);
        var offset = zone.IsAmbiguousTime(local)
            ? opening ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetAmbiguousTimeOffsets(local).Min()
            : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset);
    }
}
