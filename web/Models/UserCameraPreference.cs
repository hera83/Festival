namespace web.Models;

using web.Utils;

public class UserCameraPreference
{
    public int Id { get; set; }

    /// <summary>Identity user id for the logged-in user.</summary>
    public string UserId { get; set; } = "";

    /// <summary>The chosen camera deviceId.</summary>
    public string DeviceId { get; set; } = "";

    /// <summary>
    /// A sorted, comma-separated list of ALL video deviceIds available on the
    /// device at the time the preference was saved.  Used to detect if the user
    /// is now on a different device (different camera hardware).
    /// </summary>
    public string DeviceFingerprint { get; set; } = "";

    public DateTime UpdatedAt { get; set; } = AppTime.Now;
}
