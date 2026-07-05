namespace web.Models;

/// <summary>
/// Midlertidig undertrykkelse af en frivillig fra udeblivelseslisten.
/// Frivillige med et aktivt snooze (SnoozedUntil i fremtiden) vises ikke
/// som udeblevet, selvom vagten stadig er passeret og de ikke er checket ind.
/// </summary>
public class NoShowSnooze
{
    public int Id { get; set; }
    public int SeasonId { get; set; }
    public int VolunteerId { get; set; }

    public DateTime SnoozedUntil { get; set; }

    /// <summary>Oprettet/forlænget af (brugernavn/display).</summary>
    public string CreatedByUser { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public Volunteer Volunteer { get; set; } = null!;
}
