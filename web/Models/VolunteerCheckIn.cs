namespace web.Models;

/// <summary>
/// Repræsenterer én check-in session for en frivillig.
/// En frivillig kan have flere check-ins samme dag.
/// </summary>
public class VolunteerCheckIn
{
    public int Id { get; set; }
    public int SeasonId { get; set; }
    public int VolunteerId { get; set; }

    /// <summary>Dato for check-in (dato-del af CheckedInAt, gemt separat for hurtig søgning).</summary>
    public DateOnly CheckInDate { get; set; }

    public DateTime CheckedInAt { get; set; } = DateTime.UtcNow;

    /// <summary>Null = stadig indchecket. Sat ved checkout.</summary>
    public DateTime? CheckedOutAt { get; set; }

    /// <summary>Nuværende lokation, fx "Pit" eller "Bar8".</summary>
    public string CurrentLocation { get; set; } = "Pit";

    public Volunteer Volunteer { get; set; } = null!;
    public ICollection<VolunteerLocationLog> LocationLogs { get; set; } = [];
}
