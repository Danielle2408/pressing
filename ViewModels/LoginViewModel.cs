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

public sealed class ForgotPasswordViewModel
{
    [Required, EmailAddress]
    [Display(Name = "Adresse e-mail")]
    public string Email { get; set; } = string.Empty;
}

public sealed class ResetPasswordViewModel
{
    [Required]
    public string UserId { get; set; } = string.Empty;
    [Required]
    public string Token { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 10)]
    [Display(Name = "Nouveau mot de passe")]
    public string Password { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Les mots de passe ne correspondent pas.")]
    [Display(Name = "Confirmer le mot de passe")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
