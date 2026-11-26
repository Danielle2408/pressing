namespace Pressing.Models;

public sealed class InventoryItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal QuantityOnHand { get; set; }
    public decimal AlertThreshold { get; set; }
    public string? Supplier { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<StockMovement> Movements { get; set; } = new List<StockMovement>();
}
