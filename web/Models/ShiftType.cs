namespace web.Models;

using web.Utils;

public class ShiftType
{
    public int Id { get; set; }
    public int SeasonId { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public int RequiredCount { get; set; } = 0;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public DateTime CreatedAt { get; set; } = AppTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = AppTime.UtcNow;

    public ICollection<Shift> Shifts { get; set; } = [];
}
