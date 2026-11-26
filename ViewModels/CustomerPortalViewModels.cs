using Pressing.Models;

namespace Pressing.ViewModels;

public sealed class CustomerPortalViewModel
{
    public Customer Customer { get; init; } = null!;
    public IReadOnlyList<LaundryOrder> Orders { get; init; } = [];
    public IReadOnlyList<LoyaltyLedgerEntry> LoyaltyHistory { get; init; } = [];
    public decimal CurrencyPerPoint { get; init; }
}

public sealed class CustomerOrderDetailsViewModel
{
    public LaundryOrder Order { get; init; } = null!;
    public IReadOnlyList<CustomerClaim> Claims { get; init; } = [];
}

public sealed class CustomerClaimCreateViewModel
{
    public int OrderId { get; set; }
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(2000, MinimumLength = 10)]
    public string Description { get; set; } = string.Empty;
}

public sealed class CustomerClaimsListViewModel
{
    public IReadOnlyList<CustomerClaim> Claims { get; init; } = [];
}

public sealed class CustomerProfileViewModel
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(120, MinimumLength = 2)]
    [System.ComponentModel.DataAnnotations.Display(Name = "Nom complet")]
    public string FullName { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.StringLength(400)]
    [System.ComponentModel.DataAnnotations.Display(Name = "Adresse principale")]
    public string? Address { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
}

public sealed class ChangeCustomerPasswordViewModel
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.DataType(System.ComponentModel.DataAnnotations.DataType.Password)]
    [System.ComponentModel.DataAnnotations.Display(Name = "Mot de passe actuel")]
    public string CurrentPassword { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.DataType(System.ComponentModel.DataAnnotations.DataType.Password)]
    [System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 10)]
    [System.ComponentModel.DataAnnotations.Display(Name = "Nouveau mot de passe")]
    public string NewPassword { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.DataType(System.ComponentModel.DataAnnotations.DataType.Password)]
    [System.ComponentModel.DataAnnotations.Compare(nameof(NewPassword), ErrorMessage = "Les mots de passe ne correspondent pas.")]
    [System.ComponentModel.DataAnnotations.Display(Name = "Confirmer le nouveau mot de passe")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class CustomerOrderRequestViewModel
{
    public List<OrderLineInputViewModel> Lines { get; set; } = [new()];
    public bool HomeCollection { get; set; }
    public bool HomeDelivery { get; set; }
    public DateTime? CollectionAtLocal { get; set; }
    public string? CollectionAddress { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? Notes { get; set; }
}

public sealed class CustomerOrderRequestPageViewModel
{
    public CustomerOrderRequestViewModel Form { get; init; } = new();
    public IReadOnlyList<GarmentCategory> Categories { get; init; } = [];
    public IReadOnlyList<LaundryService> Services { get; init; } = [];
    public IReadOnlyList<OrderPriceOptionViewModel> Prices { get; init; } = [];
    public OrderSettings Settings { get; init; } = new();
}
