using System.ComponentModel.DataAnnotations;

namespace web.Models;

public class LoginViewModel
{
    [Required]
    [Display(Name = "Brugernavn")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Husk mig")]
    public bool RememberMe { get; set; }

    public bool CanCreateFirstAdmin { get; set; }
}
