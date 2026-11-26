namespace Pressing.Models;

public enum StockMovementDirection
{
    Incoming,
    Outgoing
}

public sealed class StockMovement
{
    public int Id { get; set; }
    public int InventoryItemId { get; set; }
    public InventoryItem InventoryItem { get; set; } = null!;
    public StockMovementDirection Direction { get; set; }
    public decimal Quantity { get; set; }
    public string? Note { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
