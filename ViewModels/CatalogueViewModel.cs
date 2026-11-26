using Pressing.Models;

namespace Pressing.ViewModels;

public sealed class CatalogueViewModel
{
    public IReadOnlyList<CatalogueRateViewModel> Rates { get; init; } = [];
}

public sealed class CatalogueRateViewModel
{
    public string Category { get; init; } = string.Empty;
    public string Service { get; init; } = string.Empty;
    public decimal Price { get; init; }
}

public sealed class AdminCatalogueViewModel
{
    public IReadOnlyList<GarmentCategory> Categories { get; init; } = [];
    public IReadOnlyList<LaundryService> Services { get; init; } = [];
    public IReadOnlyList<ServiceTariff> Tariffs { get; init; } = [];
}
