using Pressing.Models;

namespace Pressing.ViewModels;

public sealed class CustomerPortalViewModel
{
    public Customer Customer { get; init; } = null!;
    public IReadOnlyList<LaundryOrder> Orders { get; init; } = [];
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
