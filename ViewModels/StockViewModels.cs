using System.ComponentModel.DataAnnotations;
using Pressing.Models;

namespace Pressing.ViewModels;

public sealed class StockViewModel
{
    public IReadOnlyList<InventoryItem> Items { get; init; } = [];
    public IReadOnlyList<StockMovement> Movements { get; init; } = [];
    public IReadOnlyList<Supplier> Suppliers { get; init; } = [];
    public CreateInventoryItemViewModel NewItem { get; init; } = new();
    public RecordStockMovementViewModel NewMovement { get; init; } = new();
    public CreateSupplierViewModel NewSupplier { get; init; } = new();
}

public sealed class CreateInventoryItemViewModel
{
    [Required, StringLength(120, MinimumLength = 2)]
    [Display(Name = "Produit")]
    public string Name { get; set; } = string.Empty;
    [Required, StringLength(24, MinimumLength = 1)]
    [Display(Name = "Unité")]
    public string Unit { get; set; } = string.Empty;
    [Range(typeof(decimal), "0", "100000000")]
    [Display(Name = "Stock initial")]
    public decimal InitialQuantity { get; set; }
    [Range(typeof(decimal), "0", "100000000")]
    [Display(Name = "Seuil d’alerte")]
    public decimal AlertThreshold { get; set; }
    [Display(Name = "Fournisseur (facultatif)")]
    public int? SupplierId { get; set; }
}

public sealed class CreateSupplierViewModel
{
    [Required, StringLength(120, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;
    [StringLength(120)]
    public string? ContactName { get; set; }
    [StringLength(32)]
    public string? PhoneNumber { get; set; }
    [EmailAddress, StringLength(254)]
    public string? Email { get; set; }
    [StringLength(400)]
    public string? Address { get; set; }
}

public sealed class RecordStockMovementViewModel
{
    [Range(1, int.MaxValue)]
    [Display(Name = "Produit")]
    public int InventoryItemId { get; set; }
    [EnumDataType(typeof(StockMovementDirection))]
    [Display(Name = "Mouvement")]
    public StockMovementDirection Direction { get; set; }
    [Range(typeof(decimal), "0.001", "100000000")]
    [Display(Name = "Quantité")]
    public decimal Quantity { get; set; }
    [StringLength(500)]
    public string? Note { get; set; }
}
