namespace web.Utils;

public static class AppTime
{
    public static DateTime Now => DateTime.Now;
    public static DateTime CopenhagenNow => DateTime.Now;
    public static DateOnly CopenhagenToday => DateOnly.FromDateTime(DateTime.Now);
    public static int CurrentSeason => DateTime.Now.Year;

    public static DateTime ToCopenhagen(DateTime value) => value;
    public static DateTime CopenhagenLocal(DateTime localValue) => localValue;
}
