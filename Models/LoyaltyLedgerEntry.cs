namespace Pressing.Models;

public sealed class LoyaltyLedgerEntry
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public int LaundryOrderId { get; set; }
    public LaundryOrder LaundryOrder { get; set; } = null!;
    public int Points { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
