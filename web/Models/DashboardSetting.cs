namespace web.Models;

using web.Utils;

public class DashboardSetting
{
    public int Id { get; set; }
    public int SeasonId { get; set; }
    public string Key { get; set; } = "";
    public string? Value { get; set; }
    public DateTime UpdatedAt { get; set; } = AppTime.UtcNow;
}
