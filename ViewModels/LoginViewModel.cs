using System.ComponentModel.DataAnnotations;

namespace Pressing.ViewModels;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Saisis ton adresse e-mail ou ton téléphone.")]
    [Display(Name = "E-mail ou téléphone")]
    public string Identifier { get; set; } = string.Empty;

    [Required(ErrorMessage = "Saisis ton mot de passe.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mot de passe")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Se souvenir de moi")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
