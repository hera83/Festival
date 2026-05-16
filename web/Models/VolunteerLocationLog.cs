namespace web.Models;

/// <summary>
/// Logger alle bevægelser for en frivillig i løbet af en check-in session:
/// CheckIn (→ Pit), Move (→ Bar8, → Pit, osv.), CheckOut.
/// </summary>
public class VolunteerLocationLog
{
    public int Id { get; set; }
    public int CheckInId { get; set; }
    public int VolunteerId { get; set; }
    public int SeasonId { get; set; }

    /// <summary>CheckIn, Move, CheckOut</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Lokation efter hændelsen, fx "Pit", "Bar8". Null ved CheckOut.</summary>
    public string? Location { get; set; }

    public DateTime OccurredAt { get; set; } = DateTime.Now;

    public VolunteerCheckIn CheckIn { get; set; } = null!;
    public Volunteer Volunteer { get; set; } = null!;
}
