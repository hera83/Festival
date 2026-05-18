namespace web.Models;

using web.Utils;

public class VolunteerMeta
{
    public int Id { get; set; }
    public int VolunteerId { get; set; }
    public string? AppConfirmCode { get; set; }
    public DateTime? AppConfirmCodeExpiry { get; set; }
    public DateTime CreatedAt { get; set; } = AppTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = AppTime.UtcNow;

    public Volunteer Volunteer { get; set; } = null!;
}
