using System.ComponentModel.DataAnnotations;

namespace Pressing.ViewModels;

public sealed class RegisterViewModel
{
    [Required(ErrorMessage = "Le nom est obligatoire.")]
    [StringLength(120, MinimumLength = 2, ErrorMessage = "Le nom doit contenir entre 2 et 120 caractères.")]
    [Display(Name = "Nom complet")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "L’adresse e-mail est obligatoire.")]
    [EmailAddress(ErrorMessage = "Saisis une adresse e-mail valide.")]
    [Display(Name = "Adresse e-mail")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le téléphone est obligatoire.")]
    [Phone(ErrorMessage = "Saisis un numéro de téléphone valide.")]
    [Display(Name = "Téléphone")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le mot de passe est obligatoire.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mot de passe")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirme le mot de passe.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Les mots de passe ne correspondent pas.")]
    [Display(Name = "Confirmer le mot de passe")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
