namespace web.Models;

/// <summary>
/// Repræsenterer en planlagt fremtidig flytning af en frivillig.
/// En frivillig kan kun have én aktiv planlagt flytning ad gangen.
/// </summary>
public class ScheduledMove
{
    public int Id { get; set; }
    public int SeasonId { get; set; }
    public int VolunteerId { get; set; }

    /// <summary>Destination, fx "Pit" eller postnavnet.</summary>
    public string TargetLocation { get; set; } = string.Empty;

    /// <summary>Tidspunkt flytningen skal udføres.</summary>
    public DateTime ScheduledAt { get; set; }

    /// <summary>Oprettet af (brugernavn/display).</summary>
    public string CreatedByUser { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    /// <summary>Null = afventer. Sat når baggrundstjenesten udfører den.</summary>
    public DateTime? ExecutedAt { get; set; }

    /// <summary>Annulleret af brugeren.</summary>
    public bool IsCancelled { get; set; }

    public Volunteer Volunteer { get; set; } = null!;
}
