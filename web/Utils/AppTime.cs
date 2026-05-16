namespace web.Utils;

public static class AppTime
{
    private static readonly TimeZoneInfo CopenhagenTimeZone = ResolveCopenhagenTimeZone();

    public static DateTime UtcNow => DateTime.UtcNow;
    public static DateTime CopenhagenNow => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, CopenhagenTimeZone);
    public static DateOnly CopenhagenToday => DateOnly.FromDateTime(CopenhagenNow);
    public static int CurrentSeason => CopenhagenNow.Year;

    public static DateTime ToCopenhagen(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        return TimeZoneInfo.ConvertTimeFromUtc(utc, CopenhagenTimeZone);
    }

    public static DateTime CopenhagenLocalToUtc(DateTime localValue)
    {
        var unspecified = localValue.Kind == DateTimeKind.Unspecified
            ? localValue
            : DateTime.SpecifyKind(localValue, DateTimeKind.Unspecified);

        return TimeZoneInfo.ConvertTimeToUtc(unspecified, CopenhagenTimeZone);
    }

    private static TimeZoneInfo ResolveCopenhagenTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");
        }
    }
}