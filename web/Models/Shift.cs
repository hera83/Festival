namespace web.Models;

public class Shift
{
    public int Id { get; set; }
    public int SeasonId { get; set; }
    public int VolunteerId { get; set; }
    public int ShiftTypeId { get; set; }

    // Admin-only override af denne tilmeldings faktiske tid, uden at ændre selve vagten (ShiftType)
    public DateTime? CustomStartTime { get; set; }
    public DateTime? CustomEndTime { get; set; }

    public Volunteer Volunteer { get; set; } = null!;
    public ShiftType ShiftType { get; set; } = null!;
}
