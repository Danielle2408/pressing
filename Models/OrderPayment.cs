namespace Pressing.Models;

public sealed class OrderPayment
{
    public int Id { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public int LaundryOrderId { get; set; }
    public LaundryOrder LaundryOrder { get; set; } = null!;
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public OrderPaymentStatus Status { get; set; } = OrderPaymentStatus.Confirmed;
    public string Currency { get; set; } = "XAF";
    public string? TransactionReference { get; set; }
    public string? ProviderReference { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public ApplicationUser CreatedByUser { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
