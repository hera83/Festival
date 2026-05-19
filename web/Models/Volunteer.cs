namespace web.Models;

using web.Utils;

public class Volunteer
{
    public int Id { get; set; }
    public int SeasonId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string QrToken { get; set; } = Guid.NewGuid().ToString("N");
    public bool QrCodeSent { get; set; }
    public DateTime? QrCodeSentAt { get; set; }
    public string? QrCodeSentBy { get; set; }
    public DateTime CreatedAt { get; set; } = AppTime.Now;
    public DateTime UpdatedAt { get; set; } = AppTime.Now;

    public ICollection<Shift> Shifts { get; set; } = [];
}
