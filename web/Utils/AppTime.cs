namespace web.Utils;

public static class AppTime
{
    public static DateTime Now => DateTime.Now;
    public static DateTime CopenhagenNow => DateTime.Now;
    public static DateOnly CopenhagenToday => DateOnly.FromDateTime(CopenhagenNow);
    public static int CurrentSeason => CopenhagenNow.Year;

    public static DateTime ToCopenhagen(DateTime value)
    {
        return value;
    }

    public static DateTime CopenhagenLocal(DateTime localValue)
    {
        return localValue;
    }
}