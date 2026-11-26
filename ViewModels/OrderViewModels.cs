using System.ComponentModel.DataAnnotations;
using Pressing.Models;

namespace Pressing.ViewModels;

public sealed class OrderLineInputViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Choisis un article.")]
    public int CategoryId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Choisis un service.")]
    public int ServiceId { get; set; }

    [Range(1, 100, ErrorMessage = "La quantité doit être comprise entre 1 et 100.")]
    public int Quantity { get; set; } = 1;

    [StringLength(60)]
    [Display(Name = "Couleur")]
    public string? Color { get; set; }

    [StringLength(80)]
    [Display(Name = "Marque")]
    public string? Brand { get; set; }

    [StringLength(500)]
    [Display(Name = "État à la réception")]
    public string? ReceptionCondition { get; set; }

    [StringLength(500)]
    [Display(Name = "Note sur l’article")]
    public string? Notes { get; set; }
}

public sealed class CreateOrderViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Choisis un client.")]
    [Display(Name = "Client")]
    public int CustomerId { get; set; }

    [Required]
    [Display(Name = "Date et heure prévues")]
    public DateTime ExpectedReadyAtLocal { get; set; }

    [Display(Name = "Service express")]
    public bool Express { get; set; }

    [Display(Name = "Collecte à domicile")]
    public bool HomeCollection { get; set; }

    [Display(Name = "Livraison à domicile")]
    public bool HomeDelivery { get; set; }

    [StringLength(1000)]
    [Display(Name = "Remarques de la commande")]
    public string? Notes { get; set; }

    public List<OrderLineInputViewModel> Lines { get; set; } = [new()];
}

public sealed class OrderCreatePageViewModel
{
    public CreateOrderViewModel Form { get; init; } = new();
    public IReadOnlyList<Customer> Customers { get; init; } = [];
    public IReadOnlyList<GarmentCategory> Categories { get; init; } = [];
    public IReadOnlyList<LaundryService> Services { get; init; } = [];
    public IReadOnlyList<OrderPriceOptionViewModel> Prices { get; init; } = [];
    public OrderSettings Settings { get; init; } = new();
}

public sealed class OrderPriceOptionViewModel
{
    public int CategoryId { get; init; }
    public int ServiceId { get; init; }
    public string ServiceName { get; init; } = string.Empty;
    public decimal Price { get; init; }
}

public sealed class OrderListViewModel
{
    public OrderStatus? Status { get; init; }
    public string? Query { get; init; }
    public IReadOnlyList<LaundryOrder> Orders { get; init; } = [];
}

public sealed class OrderDetailsViewModel
{
    public LaundryOrder Order { get; init; } = null!;
    public IReadOnlyList<OrderStatus> AllowedStatuses { get; init; } = [];
}
