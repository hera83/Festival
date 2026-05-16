namespace web.Models;

public class Shift
{
    public int Id { get; set; }
    public int SeasonId { get; set; }
    public int VolunteerId { get; set; }
    public int ShiftTypeId { get; set; }

    public Volunteer Volunteer { get; set; } = null!;
    public ShiftType ShiftType { get; set; } = null!;
}
