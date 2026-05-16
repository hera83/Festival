namespace web.Models;

public class ShiftType
{
    public int Id { get; set; }
    public int SeasonId { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public int RequiredCount { get; set; } = 0;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Shift> Shifts { get; set; } = [];
}
