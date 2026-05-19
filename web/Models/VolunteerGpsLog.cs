namespace web.Models;

using web.Utils;

/// <summary>
/// Logger GPS-lokation for en frivillig hver gang de åbner appen eller trykker på en menu-knap.
/// </summary>
public class VolunteerGpsLog
{
    public int Id { get; set; }
    public int VolunteerId { get; set; }
    public int SeasonId { get; set; }

    public string VolunteerKey { get; set; } = string.Empty;
    public string VolunteerName { get; set; } = string.Empty;

    /// <summary>Breddegrad (latitude)</summary>
    public double Latitude { get; set; }

    /// <summary>Længdegrad (longitude)</summary>
    public double Longitude { get; set; }

    /// <summary>GPS-nøjagtighed i meter (kan være null hvis ukendt)</summary>
    public double? Accuracy { get; set; }

    /// <summary>Hvad udløste logningen: "AppOpen", "NavOverblik", "NavVagter", osv.</summary>
    public string Trigger { get; set; } = string.Empty;

    public DateTime LoggedAt { get; set; } = AppTime.Now;

    public Volunteer Volunteer { get; set; } = null!;
}
