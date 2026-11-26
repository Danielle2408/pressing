namespace Pressing.Models;

public sealed class OrderStatusHistory
{
    public int Id { get; set; }
    public int LaundryOrderId { get; set; }
    public LaundryOrder LaundryOrder { get; set; } = null!;
    public OrderStatus? PreviousStatus { get; set; }
    public OrderStatus NewStatus { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
    public string? Reason { get; set; }
}
