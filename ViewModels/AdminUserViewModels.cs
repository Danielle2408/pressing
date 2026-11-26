using System.ComponentModel.DataAnnotations;
using Pressing.Models;
using Pressing.Security;

namespace Pressing.ViewModels;

public sealed class AdminUserListViewModel
{
    public string? Role { get; init; }
    public IReadOnlyList<AdminUserRowViewModel> Users { get; init; } = [];
}

public sealed class AdminUserRowViewModel
{
    public ApplicationUser User { get; init; } = null!;
    public string Role { get; init; } = string.Empty;
}

public sealed class CreateStaffUserViewModel
{
    [Required, StringLength(120, MinimumLength = 2)]
    [Display(Name = "Nom complet")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    [Display(Name = "Adresse e-mail")]
    public string Email { get; set; } = string.Empty;

    [Required, Phone]
    [Display(Name = "Téléphone")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Rôle")]
    public string Role { get; set; } = Roles.Operator;

    [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 10)]
    [Display(Name = "Mot de passe temporaire")]
    public string TemporaryPassword { get; set; } = string.Empty;
}

public sealed class ResetStaffPasswordViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 10)]
    [Display(Name = "Nouveau mot de passe temporaire")]
    public string TemporaryPassword { get; set; } = string.Empty;
}

public sealed class EditAdminUserViewModel
{
    public string UserId { get; set; } = string.Empty;
    [Required, StringLength(120, MinimumLength = 2)]
    [Display(Name = "Nom complet")]
    public string FullName { get; set; } = string.Empty;
    [Required, EmailAddress]
    [Display(Name = "Adresse e-mail")]
    public string Email { get; set; } = string.Empty;
    [Required, Phone]
    [Display(Name = "Téléphone")]
    public string PhoneNumber { get; set; } = string.Empty;
}
