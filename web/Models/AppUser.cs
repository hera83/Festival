using Microsoft.AspNetCore.Identity;

namespace web.Models;

public class AppUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;

    // Profilbillede — metadata i DB, fil i App_files/avatars/
    public string? AvatarFileName { get; set; }
    public string? AvatarContentType { get; set; }
    public long? AvatarFileSize { get; set; }
    public DateTime? AvatarUploadedAt { get; set; }

    // "system" (default), "dark" eller "light"
    public string ColorMode { get; set; } = "system";
}
