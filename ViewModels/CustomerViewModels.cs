using System.ComponentModel.DataAnnotations;
using Pressing.Models;

namespace Pressing.ViewModels;

public sealed class CustomerSearchViewModel
{
    public string? Query { get; init; }
    public IReadOnlyList<Customer> Customers { get; init; } = [];
}

public sealed class CreateCustomerViewModel
{
    [Required(ErrorMessage = "Le nom est obligatoire.")]
    [StringLength(120, MinimumLength = 2, ErrorMessage = "Le nom doit contenir entre 2 et 120 caractères.")]
    [Display(Name = "Nom complet")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le téléphone est obligatoire.")]
    [Phone(ErrorMessage = "Saisis un numéro de téléphone valide.")]
    [StringLength(32)]
    [Display(Name = "Téléphone")]
    public string PhoneNumber { get; set; } = string.Empty;

    [StringLength(400)]
    [Display(Name = "Adresse (facultatif)")]
    public string? Address { get; set; }
}

public sealed class CustomerDetailsViewModel
{
    public Customer Customer { get; init; } = null!;
}
