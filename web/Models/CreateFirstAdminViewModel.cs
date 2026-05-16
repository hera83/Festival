using System.ComponentModel.DataAnnotations;

namespace web.Models;

public class CreateFirstAdminViewModel
{
    [Required]
    [Display(Name = "Visningsnavn")]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [MinLength(6)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passwords matcher ikke.")]
    [Display(Name = "Gentag password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
