namespace Pressing.Models;

public sealed class LaundryOrder
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public string CreatedByUserId { get; set; } = string.Empty;
    public ApplicationUser CreatedByUser { get; set; } = null!;
    public OrderStatus Status { get; set; } = OrderStatus.Received;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpectedReadyAtUtc { get; set; }
    public bool Express { get; set; }
    public bool HomeCollection { get; set; }
    public bool HomeDelivery { get; set; }
    public decimal ExpressSurcharge { get; set; }
    public decimal CollectionFee { get; set; }
    public decimal DeliveryFee { get; set; }
    public string? Notes { get; set; }
    public string? CancellationReason { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public ICollection<OrderLine> Lines { get; set; } = new List<OrderLine>();
    public ICollection<OrderStatusHistory> StatusHistory { get; set; } = new List<OrderStatusHistory>();
    public ICollection<OrderPayment> Payments { get; set; } = new List<OrderPayment>();
}
