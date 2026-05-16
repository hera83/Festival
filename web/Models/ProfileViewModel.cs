using System.ComponentModel.DataAnnotations;

namespace web.Models;

public class ProfileViewModel
{
    [Required(ErrorMessage = "Navn er påkrævet.")]
    [Display(Name = "Navn")]
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Brugernavn er påkrævet.")]
    [Display(Name = "Brugernavn")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email er påkrævet.")]
    [EmailAddress(ErrorMessage = "Ugyldig emailadresse.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Ugyldigt telefonnummer.")]
    [Display(Name = "Telefonnummer")]
    public string? PhoneNumber { get; set; }

    [Display(Name = "Ny adgangskode")]
    [DataType(DataType.Password)]
    [MinLength(6, ErrorMessage = "Adgangskoden skal være mindst 6 tegn.")]
    public string? NewPassword { get; set; }

    [Display(Name = "Bekræft ny adgangskode")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Adgangskoderne er ikke ens.")]
    public string? ConfirmPassword { get; set; }

    // Beskåret billede sendes som base64-streng fra cropperens canvas
    public string? CroppedImageBase64 { get; set; }

    // Til visning i view
    public string? AvatarFileName { get; set; }

    // "system" eller "dark" eller "light"
    public string ColorMode { get; set; } = "system";
}
